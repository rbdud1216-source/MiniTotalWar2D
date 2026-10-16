using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using MiniTotalWar.ECS;

/// <summary>
/// 토탈워 스타일의 원거리 사격 호(Firing Arc), 3D 물리 탄도 궤적 및 지형 차폐 경고 시스템을
/// 부대(Squad) 및 자유 유닛(Free Unit)에 통합 관장하는 싱글톤 매니저입니다.
/// </summary>
public class RangeVisualizationManager : MonoBehaviour
{
    private static RangeVisualizationManager _instance;
    public static RangeVisualizationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RangeVisualizationManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RangeVisualizationManager");
                    _instance = go.AddComponent<RangeVisualizationManager>();
                }
            }
            return _instance;
        }
    }

    [Header("시각화 전술 토글 (단축키 [I])")]
    [Tooltip("사거리 시각화(사격 호 및 탄도 궤적) 전체 활성화 여부 ([I] 키로 토글 가능)")]
    public bool isVisualizationEnabled = true;

    [Header("인스펙터 전술 설정 (사용자 커스텀)")]
    [Tooltip("우클릭 공격 명령 중에도 타겟을 향한 탄도 궤적선을 계속 유지할지 여부 (기본: True)")]
    public bool keepTrajectoryDuringAttack = true;

    [Tooltip("사격 호 내부 반투명 채우기 면 활성화 여부 (기본: False - 토탈워 표준 외곽 사격선 중심 연출)")]
    public bool showFillArea = false;

    [Range(0.01f, 0.40f)]
    [Tooltip("사격 호 내부 면의 반투명 알파값 (기본: 0.05)")]
    public float fillAlpha = 0.05f;

    [Header("색상 톤앤매너 (전 병과 100% 표준 통일)")]
    public Color colorClear = new Color(0.18f, 0.83f, 0.45f, 0.90f);   // 에메랄드 그린 (#2ED573)
    public Color colorBlocked = new Color(1.00f, 0.13f, 0.13f, 0.95f); // 크림슨 레드 (#FF2222)

    // 객체 풀
    private readonly List<RangeArcRenderer> arcPool = new List<RangeArcRenderer>();
    private readonly List<TargetTrajectoryPredictor> trajectoryPool = new List<TargetTrajectoryPredictor>();

    // 현재 관리 중인 선택 상태
    private readonly List<Squad> currentSelectedSquads = new List<Squad>();
    private readonly List<Unit> currentSelectedUnits = new List<Unit>();
    private readonly List<Entity> currentSelectedECSEntities = new List<Entity>();
    private Squad currentHoveredEnemySquad = null;
    private Vector3? currentGroundTarget = null;

    // 우클릭 드래그 배치 선행 미리보기 슬롯
    private readonly List<(Squad squad, Vector3 dest, Quaternion rot)> dragPreviews = new List<(Squad, Vector3, Quaternion)>();

    private Transform poolContainer;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        GameObject container = new GameObject("RangeVisualization_Pool");
        container.transform.SetParent(transform, false);
        poolContainer = container.transform;
    }

    /// <summary>
    /// [I] 키 단축키로 사거리 시각화 표시 여부를 토글합니다.
    /// </summary>
    public void ToggleVisualization()
    {
        isVisualizationEnabled = !isVisualizationEnabled;
        if (!isVisualizationEnabled)
        {
            HideAllPoolObjects();
            Debug.Log("[RangeVisualization] 👁️ 사거리 시각화가 비활성화되었습니다. ([I] 키로 다시 켜기)");
        }
        else
        {
            RefreshVisualization();
            Debug.Log("[RangeVisualization] 👁️ 사거리 시각화가 활성화되었습니다. ([I] 키로 끄기)");
        }
    }

    private RangeArcRenderer GetOrCreateArcRenderer(int index)
    {
        while (arcPool.Count <= index)
        {
            GameObject arcObj = new GameObject($"RangeArc_Instance_{arcPool.Count}");
            arcObj.transform.SetParent(poolContainer, false);
            arcObj.AddComponent<MeshFilter>();
            arcObj.AddComponent<MeshRenderer>();
            RangeArcRenderer arc = arcObj.AddComponent<RangeArcRenderer>();
            arcPool.Add(arc);
        }
        return arcPool[index];
    }

    private TargetTrajectoryPredictor GetOrCreateTrajectoryPredictor(int index)
    {
        while (trajectoryPool.Count <= index)
        {
            GameObject trajObj = new GameObject($"TrajectoryPredictor_Instance_{trajectoryPool.Count}");
            trajObj.transform.SetParent(poolContainer, false);
            TargetTrajectoryPredictor traj = trajObj.AddComponent<TargetTrajectoryPredictor>();
            trajectoryPool.Add(traj);
        }
        return trajectoryPool[index];
    }

    /// <summary>
    /// PlayerController에서 선택된 부대 및 유닛 목록이 변경되었을 때 호출됩니다.
    /// (기존 2개 인자 호출과의 하위 호환성 유지)
    /// </summary>
    public void UpdateSelection(IReadOnlyList<Squad> selectedSquads, IReadOnlyList<Unit> selectedUnits)
    {
        UpdateSelection(selectedSquads, selectedUnits, null);
    }

    /// <summary>
    /// PlayerController에서 선택된 부대, GameObject 유닛 및 Pure ECS 엔티티 목록이 변경되었을 때 호출됩니다.
    /// </summary>
    public void UpdateSelection(IReadOnlyList<Squad> selectedSquads, IReadOnlyList<Unit> selectedUnits, IReadOnlyList<Entity> selectedECSEntities)
    {
        currentSelectedSquads.Clear();
        if (selectedSquads != null)
        {
            for (int i = 0; i < selectedSquads.Count; i++)
            {
                if (selectedSquads[i] != null && selectedSquads[i].IsRangedSquad && selectedSquads[i].isPlayer)
                {
                    currentSelectedSquads.Add(selectedSquads[i]);
                }
            }
        }

        currentSelectedUnits.Clear();
        if (selectedUnits != null)
        {
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i] != null && selectedUnits[i].isRangedUnit && selectedUnits[i].isPlayer)
                {
                    currentSelectedUnits.Add(selectedUnits[i]);
                }
            }
        }

        currentSelectedECSEntities.Clear();
        if (selectedECSEntities != null && selectedECSEntities.Count > 0)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    Entity ent = selectedECSEntities[i];
                    if (em.Exists(ent))
                    {
                        var tag = em.GetComponentData<UnitEntityTag>(ent);
                        var combat = em.GetComponentData<UnitCombatData>(ent);
                        // 플레이어 진영(1), 생존(1), 원거리 병과(1) 유닛 필터링
                        if (tag.Faction == 1 && tag.IsAlive == 1 && combat.IsRangedUnit == 1)
                        {
                            currentSelectedECSEntities.Add(ent);
                        }
                    }
                }
            }
        }

        RefreshVisualization();
    }

    /// <summary>
    /// 마우스 커서가 적 부대 위에 올려졌거나 벗어났을 때 호출됩니다.
    /// </summary>
    public void SetHoveredEnemy(Squad enemySquad)
    {
        if (currentHoveredEnemySquad == enemySquad) return;
        currentHoveredEnemySquad = enemySquad;
        RefreshVisualization();
    }

    /// <summary>
    /// 어택땅(Z키) 공격 지정 모드 등에서 빈 지면을 조준하고 있을 때 가상 목표 지점을 설정합니다.
    /// </summary>
    public void SetGroundTarget(Vector3? groundPos)
    {
        if (currentGroundTarget == groundPos) return;
        currentGroundTarget = groundPos;
        RefreshVisualization();
    }

    public void ClearGroundTarget()
    {
        if (currentGroundTarget == null) return;
        currentGroundTarget = null;
        RefreshVisualization();
    }

    private void Update()
    {
        // 시각화 갱신 및 선택 해제 시 풀 오브젝트 즉시 정리 (선택이 없으면 DeactivateRemainingPool 호출)
        RefreshVisualization();
    }

    private void RefreshVisualization()
    {
        if (!isVisualizationEnabled)
        {
            HideAllPoolObjects();
            return;
        }

        int activeArcCount = 0;
        int activeTrajCount = 0;

        Color mainColor = new Color(colorClear.r, colorClear.g, colorClear.b, showFillArea ? fillAlpha : 0.0f);
        Color rimColor = colorClear;

        // 🌟 [우클릭 드래그 배치 선행 미리보기 모드]
        if (dragPreviews.Count > 0)
        {
            Color dragMainColor = new Color(colorClear.r, colorClear.g, colorClear.b, showFillArea ? (fillAlpha * 0.85f) : 0.0f);
            Color dragRimColor = new Color(colorClear.r, colorClear.g, colorClear.b, 0.85f);

            for (int i = 0; i < dragPreviews.Count; i++)
            {
                var preview = dragPreviews[i];
                if (preview.squad == null || !preview.squad.IsRangedSquad || preview.squad.isMeleeMode || preview.squad.isForcedMeleeCharge) continue;

                float arcAngle = preview.squad.firingArcAngle > 0f ? preview.squad.firingArcAngle : ((preview.squad.trajectoryMode == TrajectoryMode.Flat) ? 60.0f : 120.0f);
                bool isFullCircle = arcAngle >= 359f;
                float maxRange = preview.squad.rangedAttackRange > 10f ? preview.squad.rangedAttackRange : 140.0f;

                RangeArcRenderer arc = GetOrCreateArcRenderer(activeArcCount++);
                arc.SetVisible(true);
                arc.SetColors(dragMainColor, dragRimColor);
                arc.UpdateArc(preview.dest, preview.rot, arcAngle, maxRange, 5.0f, isFullCircle);
            }

            // 드래그 중에는 궤적선은 끄고 풀 정리
            DeactivateRemainingPool(activeArcCount, activeTrajCount);
            return;
        }

        // 1. 선택된 원거리 부대들 각각 100% 모두 표출 (토탈워 오리지널)
        for (int i = 0; i < currentSelectedSquads.Count; i++)
        {
            Squad squad = currentSelectedSquads[i];
            if (squad == null || squad.MemberCount <= 0 || squad.isMeleeMode || squad.isForcedMeleeCharge) continue;

            Vector3 origin = squad.GetFrontLineCenter();
            origin.y = TerrainHeightManager.SampleHeightFast(origin.x, origin.z);
            Quaternion rot = squad.transform.rotation;

            // 각 병과의 firingArcAngle 설정값 동적 반영 (없으면 Flat: 60도, HighArc: 120도)
            float arcAngle = squad.firingArcAngle > 0f ? squad.firingArcAngle : ((squad.trajectoryMode == TrajectoryMode.Flat) ? 60.0f : 120.0f);
            bool isFullCircle = arcAngle >= 359f;
            float maxRange = squad.rangedAttackRange > 10f ? squad.rangedAttackRange : 140.0f;

            // 사격 호 렌더링
            RangeArcRenderer arc = GetOrCreateArcRenderer(activeArcCount++);
            arc.SetVisible(true);
            arc.SetColors(mainColor, rimColor);
            arc.UpdateArc(origin, rot, arcAngle, maxRange, 5.0f, isFullCircle);

            // 타겟 호버링 또는 공격 지정 중 탄도 궤적 렌더링
            Squad targetSquad = currentHoveredEnemySquad;
            if (targetSquad == null && keepTrajectoryDuringAttack && squad.currentTargetSquad != null && squad.currentTargetSquad.MemberCount > 0)
            {
                targetSquad = squad.currentTargetSquad;
            }

            if (targetSquad != null && targetSquad.MemberCount > 0 && targetSquad != squad && targetSquad.isPlayer != squad.isPlayer)
            {
                TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                traj.UpdatePrediction(origin, targetSquad, maxRange, squad.trajectoryMode, arcAngle, isFullCircle);
            }
            else if (currentGroundTarget.HasValue)
            {
                // 어택땅(Z키) 지면 조준 가상 탄도 궤적 렌더링
                TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                traj.UpdateGroundPrediction(origin, currentGroundTarget.Value, maxRange, squad.trajectoryMode, arcAngle, isFullCircle);
            }
        }

        // 2. 자유 유닛(Free Unit): 오직 1마리만 단독 선택되었을 때만 발밑에 표출
        if (currentSelectedSquads.Count == 0)
        {
            // 2-A. GameObject 자유 유닛 1마리 단독 선택
            if (currentSelectedUnits.Count == 1 && currentSelectedECSEntities.Count == 0)
            {
                Unit u = currentSelectedUnits[0];
                if (u != null && u.currentHp > 0 && !u.isForcedMelee)
                {
                    Vector3 origin = u.transform.position;
                    origin.y = TerrainHeightManager.SampleHeightFast(origin.x, origin.z);
                    Quaternion rot = u.transform.rotation;

                    float arcAngle = u.firingArcAngle > 0f ? u.firingArcAngle : ((u.trajectoryMode == TrajectoryMode.Flat) ? 60.0f : 120.0f);
                    bool isFullCircle = arcAngle >= 359f;
                    float maxRange = u.rangedAttackRange > 10f ? u.rangedAttackRange : 140.0f;

                    RangeArcRenderer arc = GetOrCreateArcRenderer(activeArcCount++);
                    arc.SetVisible(true);
                    arc.SetColors(mainColor, rimColor);
                    arc.UpdateArc(origin, rot, arcAngle, maxRange, 5.0f, isFullCircle);

                    if (currentHoveredEnemySquad != null && currentHoveredEnemySquad.MemberCount > 0 && !currentHoveredEnemySquad.isPlayer)
                    {
                        TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                        traj.UpdatePrediction(origin, currentHoveredEnemySquad, maxRange, u.trajectoryMode, arcAngle, isFullCircle);
                    }
                    else if (currentGroundTarget.HasValue)
                    {
                        TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                        traj.UpdateGroundPrediction(origin, currentGroundTarget.Value, maxRange, u.trajectoryMode, arcAngle, isFullCircle);
                    }
                }
            }
            // 2-B. Pure ECS 자유 유닛 1마리 단독 선택
            else if (currentSelectedUnits.Count == 0 && currentSelectedECSEntities.Count == 1)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    var em = world.EntityManager;
                    Entity ent = currentSelectedECSEntities[0];
                    if (em.Exists(ent))
                    {
                        var tag = em.GetComponentData<UnitEntityTag>(ent);
                        var mov = em.GetComponentData<UnitMovementData>(ent);
                        var combat = em.GetComponentData<UnitCombatData>(ent);

                        if (tag.Faction == 1 && tag.IsAlive == 1 && combat.IsRangedUnit == 1 && combat.IsForcedMelee == 0)
                        {
                            Vector3 origin = (Vector3)mov.Position;
                            origin.y = TerrainHeightManager.SampleHeightFast(origin.x, origin.z);
                            Quaternion rot = (Quaternion)mov.Rotation;
                            if (rot == Quaternion.identity && (mov.Velocity.x != 0f || mov.Velocity.z != 0f))
                            {
                                rot = Quaternion.LookRotation(new Vector3(mov.Velocity.x, 0f, mov.Velocity.z), Vector3.up);
                            }

                            TrajectoryMode trajMode = (combat.TrajectoryMode == 1) ? TrajectoryMode.Flat : TrajectoryMode.HighArc;
                            float arcAngle = (combat.TrajectoryMode == 1) ? 60.0f : 120.0f;
                            bool isFullCircle = arcAngle >= 359f;
                            float maxRange = combat.RangedAttackRange > 10f ? combat.RangedAttackRange : 140.0f;

                            RangeArcRenderer arc = GetOrCreateArcRenderer(activeArcCount++);
                            arc.SetVisible(true);
                            arc.SetColors(mainColor, rimColor);
                            arc.UpdateArc(origin, rot, arcAngle, maxRange, 5.0f, isFullCircle);

                            if (currentHoveredEnemySquad != null && currentHoveredEnemySquad.MemberCount > 0 && !currentHoveredEnemySquad.isPlayer)
                            {
                                TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                                traj.UpdatePrediction(origin, currentHoveredEnemySquad, maxRange, trajMode, arcAngle, isFullCircle);
                            }
                            else if (currentGroundTarget.HasValue)
                            {
                                TargetTrajectoryPredictor traj = GetOrCreateTrajectoryPredictor(activeTrajCount++);
                                traj.UpdateGroundPrediction(origin, currentGroundTarget.Value, maxRange, trajMode, arcAngle, isFullCircle);
                            }
                        }
                    }
                }
            }
        }

        DeactivateRemainingPool(activeArcCount, activeTrajCount);
    }

    private void DeactivateRemainingPool(int activeArcCount, int activeTrajCount)
    {
        for (int i = activeArcCount; i < arcPool.Count; i++)
        {
            arcPool[i].SetVisible(false);
        }
        for (int i = activeTrajCount; i < trajectoryPool.Count; i++)
        {
            trajectoryPool[i].Hide();
        }
    }

    /// <summary>
    /// 마우스 우클릭 드래그 배치 중일 때 단일 부대의 예상 도착 위치 및 회전각을 사격 호로 선행 투영합니다.
    /// </summary>
    public void ShowDragPlacementPreview(Squad squad, Vector3 destination, Quaternion rotation)
    {
        dragPreviews.Clear();
        if (squad != null && squad.IsRangedSquad)
        {
            dragPreviews.Add((squad, destination, rotation));
        }
        RefreshVisualization();
    }

    /// <summary>
    /// 다중 부대 우클릭 드래그 배치 중일 때 모든 원거리 부대의 예상 도착 위치 및 회전각을 일괄 선행 투영합니다.
    /// </summary>
    public void ShowDragPlacementPreviews(IReadOnlyList<(Squad squad, Vector3 dest, Quaternion rot)> previews)
    {
        dragPreviews.Clear();
        if (previews != null)
        {
            for (int i = 0; i < previews.Count; i++)
            {
                if (previews[i].squad != null && previews[i].squad.IsRangedSquad)
                {
                    dragPreviews.Add(previews[i]);
                }
            }
        }
        RefreshVisualization();
    }

    /// <summary>
    /// 우클릭 드래그 배치가 완료되었거나 취소되었을 때 드래그 미리보기를 해제합니다.
    /// </summary>
    public void ClearDragPlacementPreview()
    {
        if (dragPreviews.Count == 0) return;
        dragPreviews.Clear();
        RefreshVisualization();
    }

    private void HideAllPoolObjects()
    {
        for (int i = 0; i < arcPool.Count; i++) arcPool[i].SetVisible(false);
        for (int i = 0; i < trajectoryPool.Count; i++) trajectoryPool[i].Hide();
    }

    public void HideAll()
    {
        currentSelectedSquads.Clear();
        currentSelectedUnits.Clear();
        currentHoveredEnemySquad = null;
        currentGroundTarget = null;
        dragPreviews.Clear();

        HideAllPoolObjects();
    }
}
