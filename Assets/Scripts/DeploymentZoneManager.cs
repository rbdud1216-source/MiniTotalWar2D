using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토탈워식 부대 배치 구역(Deployment Zone)의 기하학적 경계를 정의하고,
/// 지면 테두리 시각화 렌더링 및 부대 이동 좌표 클램핑을 총괄하는 매니저 클래스입니다.
/// </summary>
[DisallowMultipleComponent]
public class DeploymentZoneManager : MonoBehaviour
{
    public static DeploymentZoneManager Instance { get; private set; }

    [Header("1. 플레이어 배치 구역 (World X-Z)")]
    [KoreanLabel("아군 배치 구역 중심", "플레이어 군단이 배치될 수 있는 사각 구역의 중심점입니다.")]
    public Vector3 playerZoneCenter = new Vector3(0f, 0f, -60f);

    [KoreanLabel("아군 배치 구역 크기 (X, Z)", "플레이어 군단 배치 구역의 가로 폭(X)과 세로 깊이(Z)입니다.")]
    public Vector2 playerZoneSize = new Vector2(160f, 40f);

    [Header("2. 적군 배치 구역 (World X-Z)")]
    [KoreanLabel("적군 배치 구역 중심", "적군 군단이 배치되는 사각 구역의 중심점입니다.")]
    public Vector3 enemyZoneCenter = new Vector3(0f, 0f, 60f);

    [KoreanLabel("적군 배치 구역 크기 (X, Z)", "적군 군단 배치 구역의 가로 폭(X)과 세로 깊이(Z)입니다.")]
    public Vector2 enemyZoneSize = new Vector2(160f, 40f);

    [Header("3. 지면 테두리 시각화 설정")]
    [KoreanLabel("아군 테두리 색상", "배치 단계 동안 지면에 표시될 아군 구역 테두리 색상입니다.")]
    public Color playerBorderColor = new Color(0.2f, 0.8f, 1.0f, 0.85f);

    [KoreanLabel("적군 테두리 색상", "배치 단계 동안 지면에 표시될 적군 구역 테두리 색상입니다.")]
    public Color enemyBorderColor = new Color(1.0f, 0.25f, 0.25f, 0.65f);

    [KoreanLabel("테두리 라인 두께", "지면에 그려지는 배치 경계선의 두께입니다.")]
    public float lineWidth = 0.6f;

    [KoreanLabel("지형 밀착 샘플링 간격", "지형 굴곡을 따라 경계선을 그릴 때 샘플링할 거리 간격(단위: m, 기본 1.0m)입니다.")]
    public float segmentStep = 1.0f;

    [KoreanLabel("지면 높이 오프셋", "경계선이 지형에 묻히지 않도록 지면 위로 띄울 높이(단위: m, 기본 0.35m)입니다.")]
    public float heightOffset = 0.35f;

    // 라인 렌더러
    private LineRenderer playerZoneLine;
    private LineRenderer enemyZoneLine;
    private TerrainHeightManager terrainManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // BattleManager의 인스펙터 스폰 좌표와 자동 동기화하여 구석 쏠림 방지
            if (BattleManager.Instance != null)
            {
                if (playerZoneCenter == new Vector3(0f, 0f, -60f) && BattleManager.Instance.PlayerSpawnCenter != Vector3.zero)
                {
                    playerZoneCenter = BattleManager.Instance.PlayerSpawnCenter;
                    enemyZoneCenter = BattleManager.Instance.EnemySpawnCenter;
                }
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        terrainManager = TerrainHeightManager.Instance;

        CreateZoneLineRenderers();
        UpdateZoneVisuals();

        // 지형 로딩 완료 보장용 2차 갱신 (지형 비동기 안착 시 재샘플링)
        StartCoroutine(DelayedVisualsRefresh());

        // 전투 상태 머신 이벤트 구독
        if (BattlePhaseManager.Instance != null)
        {
            BattlePhaseManager.Instance.OnPhaseChanged += OnBattlePhaseChanged;
        }
    }

    private System.Collections.IEnumerator DelayedVisualsRefresh()
    {
        yield return null;
        UpdateZoneVisuals();
        yield return new WaitForSeconds(0.2f);
        UpdateZoneVisuals();
    }

    private void OnDestroy()
    {
        if (BattlePhaseManager.Instance != null)
        {
            BattlePhaseManager.Instance.OnPhaseChanged -= OnBattlePhaseChanged;
        }
    }

    private void OnBattlePhaseChanged(BattlePhase newPhase)
    {
        // 배치 단계가 끝나면 지면 경계선 비활성화
        bool showLines = (newPhase == BattlePhase.Deployment);
        if (playerZoneLine != null) playerZoneLine.enabled = showLines;
        if (enemyZoneLine != null) enemyZoneLine.enabled = showLines;
    }

    private void CreateZoneLineRenderers()
    {
        // 아군 라인 렌더러
        GameObject playerLineObj = new GameObject("[PlayerDeploymentBorder]");
        playerLineObj.transform.SetParent(transform);
        playerZoneLine = playerLineObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(playerZoneLine, playerBorderColor);

        // 적군 라인 렌더러
        GameObject enemyLineObj = new GameObject("[EnemyDeploymentBorder]");
        enemyLineObj.transform.SetParent(transform);
        enemyZoneLine = enemyLineObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(enemyZoneLine, enemyBorderColor);
    }

    private void ConfigureLineRenderer(LineRenderer lr, Color col)
    {
        lr.useWorldSpace = true;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.loop = true;

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            lr.material = new Material(unlitShader);
        }
        lr.startColor = col;
        lr.endColor = col;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    /// <summary>
    /// 지형 굴곡을 고려하여 사각형 테두리 좌표를 촘촘히 샘플링하고 라인 렌더러를 갱신합니다.
    /// </summary>
    public void UpdateZoneVisuals()
    {
        UpdateSingleZoneVisual(playerZoneLine, playerZoneCenter, playerZoneSize);
        UpdateSingleZoneVisual(enemyZoneLine, enemyZoneCenter, enemyZoneSize);
    }

    private void UpdateSingleZoneVisual(LineRenderer lr, Vector3 center, Vector2 size)
    {
        if (lr == null) return;

        float halfW = size.x * 0.5f;
        float halfD = size.y * 0.5f;

        Vector3 c0 = new Vector3(center.x - halfW, 0f, center.z - halfD); // 좌하
        Vector3 c1 = new Vector3(center.x + halfW, 0f, center.z - halfD); // 우하
        Vector3 c2 = new Vector3(center.x + halfW, 0f, center.z + halfD); // 우상
        Vector3 c3 = new Vector3(center.x - halfW, 0f, center.z + halfD); // 좌상

        // 📈 [지형 밀착 세분화 샘플링 (Terrain Conforming Sub-Division)]:
        // 사각형의 4개 변을 segmentStep(기본 1.0m) 단위로 균등 분할하여 지형 굴곡을 100% 반영합니다.
        List<Vector3> points = new List<Vector3>(256);
        float step = Mathf.Max(0.5f, segmentStep);

        AddTerrainConformingEdge(points, c0, c1, step, heightOffset);
        AddTerrainConformingEdge(points, c1, c2, step, heightOffset);
        AddTerrainConformingEdge(points, c2, c3, step, heightOffset);
        AddTerrainConformingEdge(points, c3, c0, step, heightOffset);

        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }

    private void AddTerrainConformingEdge(List<Vector3> points, Vector3 start, Vector3 end, float step, float offset)
    {
        Vector3 dir = end - start;
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;

        int count = Mathf.Max(1, Mathf.CeilToInt(dist / step));
        Vector3 stepVec = dir / count;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = start + (stepVec * i);
            p.y = GetTerrainHeight(p.x, p.z) + offset;
            points.Add(p);
        }
    }

    private float GetTerrainHeight(float x, float z)
    {
        return TerrainHeightManager.SampleHeightFast(x, z);
    }

    #region Geometry Checks & Clamping

    /// <summary>
    /// 월드 좌표가 플레이어 배치 구역 내부인지 검사합니다.
    /// </summary>
    public bool IsInsidePlayerZone(Vector3 worldPos)
    {
        float halfW = playerZoneSize.x * 0.5f;
        float halfD = playerZoneSize.y * 0.5f;

        return (worldPos.x >= playerZoneCenter.x - halfW && worldPos.x <= playerZoneCenter.x + halfW &&
                worldPos.z >= playerZoneCenter.z - halfD && worldPos.z <= playerZoneCenter.z + halfD);
    }

    /// <summary>
    /// 월드 좌표를 플레이어 배치 구역 경계 내부로 클램핑합니다.
    /// </summary>
    public Vector3 ClampToPlayerZone(Vector3 worldPos)
    {
        float halfW = playerZoneSize.x * 0.5f;
        float halfD = playerZoneSize.y * 0.5f;

        float clampedX = Mathf.Clamp(worldPos.x, playerZoneCenter.x - halfW, playerZoneCenter.x + halfW);
        float clampedZ = Mathf.Clamp(worldPos.z, playerZoneCenter.z - halfD, playerZoneCenter.z + halfD);
        float y = GetTerrainHeight(clampedX, clampedZ);

        return new Vector3(clampedX, y, clampedZ);
    }

    /// <summary>
    /// 모든 아군 유닛이 플레이어 배치 구역 내부에 완전히 위치하는지 검사합니다.
    /// </summary>
    /// <param name="outsideCount">배치 구역 바깥에 위치한 아군 유닛 수</param>
    /// <returns>모든 유닛이 구역 내부에 있으면 true, 1명이라도 바깥에 있으면 false</returns>
    public bool AreAllPlayerUnitsInsideZone(out int outsideCount)
    {
        outsideCount = 0;

        // 1. Pure ECS 모드 검사
        if (BattleManager.Instance != null && BattleManager.Instance.usePureECS)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    typeof(MiniTotalWar.ECS.UnitEntityTag),
                    typeof(MiniTotalWar.ECS.UnitMovementData)
                );

                using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
                using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
                {
                    for (int i = 0; i < tags.Length; i++)
                    {
                        // Faction 1 = 플레이어(아군) 팀, 생존 유닛 검사
                        if (tags[i].Faction == 1 && tags[i].IsAlive == 1)
                        {
                            if (!IsInsidePlayerZone(movs[i].Position))
                            {
                                outsideCount++;
                            }
                        }
                    }
                }
                return outsideCount == 0;
            }
        }

        // 2. 일반 GameObject 유닛 모드 검사
        if (BattleManager.Instance != null)
        {
            var allSquads = BattleManager.Instance.GetAllSquads();
            for (int s = 0; s < allSquads.Count; s++)
            {
                Squad squad = allSquads[s];
                if (squad == null || !squad.isPlayer) continue;

                for (int u = 0; u < squad.members.Count; u++)
                {
                    Unit unit = squad.members[u];
                    if (unit != null && unit.IsAlive)
                    {
                        if (!IsInsidePlayerZone(unit.transform.position))
                        {
                            outsideCount++;
                        }
                    }
                }
            }
        }

        return outsideCount == 0;
    }

    /// <summary>
    /// 월드 좌표가 적군 배치 구역 내부인지 검사합니다.
    /// </summary>
    public bool IsInsideEnemyZone(Vector3 worldPos)
    {
        float halfW = enemyZoneSize.x * 0.5f;
        float halfD = enemyZoneSize.y * 0.5f;

        return (worldPos.x >= enemyZoneCenter.x - halfW && worldPos.x <= enemyZoneCenter.x + halfW &&
                worldPos.z >= enemyZoneCenter.z - halfD && worldPos.z <= enemyZoneCenter.z + halfD);
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // 씬 뷰 기즈모
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(playerZoneCenter.x, 0f, playerZoneCenter.z), new Vector3(playerZoneSize.x, 2f, playerZoneSize.y));

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(enemyZoneCenter.x, 0f, enemyZoneCenter.z), new Vector3(enemyZoneSize.x, 2f, enemyZoneSize.y));
    }
}
