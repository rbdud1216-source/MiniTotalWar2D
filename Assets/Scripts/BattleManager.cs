using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using MiniTotalWar.ECS;

/// <summary>
/// 개별 부대의 스폰 속성(인원 수, 가로 열 수, 진형 형태, 스폰 위치 등)을 정의하는 직렬화 데이터 클래스입니다.
/// </summary>
[System.Serializable]
public class SquadSpawnConfig
{
    [Tooltip("부대 식별 명칭 (UI 및 로그용)")]
    public string squadName = "보병대";

    [Tooltip("부대 총 인원 수")]
    public int unitCount = 60;

    [Tooltip("가로 열(Columns) 수 (행의 길이)")]
    public int columns = 15;

    [Tooltip("부대 초기 진형 형태")]
    public SquadFormationType formationType = SquadFormationType.Normal;

    [Tooltip("스폰 위치 수동 지정 여부 (체크 시 customPosition 사용, 미체크 시 진영 스폰 중심 기준 자동 횡대 정렬)")]
    public bool useCustomPosition = false;

    [Tooltip("수동 스폰 좌표 (World X-Z 평면)")]
    public Vector3 customPosition = Vector3.zero;

    [Tooltip("수동 회전 각도 (Y축, 0~360도)")]
    public float customRotationY = 0f;

    [Tooltip("개별 유닛 프리팹 (지정하지 않으면 진영 기본 유닛 프리팹 사용)")]
    public GameObject customUnitPrefab = null;
}

/// <summary>
/// 게임 내 유닛 및 부대 스폰, 배틀 시나리오 구성 및 전체 유닛 상태를 관리하는 매니저 클래스입니다.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("초고성능 순수 ECS 모드 (Pure ECS Zero-GameObject)")]
    [Tooltip("체크 시 GameObject를 0개로 만들고 순수 GPU Instanced ECS Entity로만 5만 기 시뮬레이션")]
    public bool usePureECS = false;

    [Header("기본 프리팹 설정")]
    [SerializeField] private GameObject playerUnitPrefab;
    [SerializeField] private GameObject enemyUnitPrefab;
    [SerializeField] private GameObject squadPrefab;

    [Header("진영 기본 스폰 위치 및 방향 (X-Z 평면)")]
    [Tooltip("플레이어 군단 기본 스폰 중심 좌표")]
    [SerializeField] private Vector3 playerSpawnCenter = new Vector3(0f, 0f, -15f);
    [Tooltip("플레이어 군단 기본 정면 회전 각도 (Y축, 기본: 0도 북쪽)")]
    [SerializeField] private float playerFacingAngle = 0f;

    [Tooltip("적군 군단 기본 스폰 중심 좌표")]
    [SerializeField] private Vector3 enemySpawnCenter = new Vector3(0f, 0f, 15f);
    [Tooltip("적군 군단 기본 정면 회전 각도 (Y축, 기본: 180도 남쪽)")]
    [SerializeField] private float enemyFacingAngle = 180f;

    [Header("군단 자동 횡대 배치 설정")]
    [Tooltip("자동 배치 시 부대와 부대 사이 가로 여유 간격 (미터)")]
    [SerializeField] private float squadSpacing = 4.0f;

    [Header("플레이어 군단 편성 (Player Army)")]
    [SerializeField] private List<SquadSpawnConfig> playerArmyConfigs = new List<SquadSpawnConfig>()
    {
        new SquadSpawnConfig { squadName = "제1 보병대", unitCount = 60, columns = 15 },
        new SquadSpawnConfig { squadName = "제2 보병대", unitCount = 60, columns = 15 }
    };

    [Header("적군 군단 편성 (Enemy Army)")]
    [SerializeField] private List<SquadSpawnConfig> enemyArmyConfigs = new List<SquadSpawnConfig>()
    {
        new SquadSpawnConfig { squadName = "적 선봉대", unitCount = 40, columns = 8 },
        new SquadSpawnConfig { squadName = "적 주력군", unitCount = 60, columns = 12 }
    };

    private readonly List<Squad> allSquads = new List<Squad>();
    private readonly List<Unit> unassignedUnits = new List<Unit>();

    public IReadOnlyList<Squad> GetAllSquads() => allSquads;

    public void UnregisterSquad(Squad squad)
    {
        if (squad != null && allSquads.Contains(squad))
        {
            allSquads.Remove(squad);
        }
    }

    public List<SquadSpawnConfig> PlayerArmyConfigs => playerArmyConfigs;
    public List<SquadSpawnConfig> EnemyArmyConfigs => enemyArmyConfigs;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SpawnBattleScenario();
    }

    #region 3D 다중 유닛 및 군단 스폰
    /// <summary>
    /// 인스펙터에 설정된 플레이어 및 적군 군단 목록을 바탕으로 전체 전투 시나리오를 시작합니다.
    /// </summary>
    public void SpawnBattleScenario()
    {
        allSquads.Clear();

        if (usePureECS && FindAnyObjectByType<MiniTotalWar.ECS.PureECSRenderer>() == null)
        {
            GameObject rendererObj = new GameObject("[PureECSRenderer]");
            rendererObj.AddComponent<MiniTotalWar.ECS.PureECSRenderer>();
        }

        // 1. 플레이어 군단 스폰
        SpawnArmy(true, playerArmyConfigs, playerSpawnCenter, playerFacingAngle);

        // 2. 적군 군단 스폰
        SpawnArmy(false, enemyArmyConfigs, enemySpawnCenter, enemyFacingAngle);

        Debug.Log($"[BattleManager] ⚔️ 전투 시나리오 스폰 완료! 총 {allSquads.Count}개 부대 배치됨. (순수 ECS 모드: {usePureECS})");

        // [추가] 적군 자동 AI 공격 (적 부대가 멈춰있지 않고 플레이어에게 돌격하도록 설정)
        OrderEnemiesToAttack();
    }

    private void OrderEnemiesToAttack()
    {
        List<Squad> players = new List<Squad>();
        List<Squad> enemies = new List<Squad>();

        foreach (var s in allSquads)
        {
            if (s.isPlayer) players.Add(s);
            else enemies.Add(s);
        }

        if (players.Count > 0 && enemies.Count > 0)
        {
            // 🎯 [정면 1:1 완벽 평행 매칭]
            // 전선에서 대각선으로 꺾여서 다른 부대를 치는 교차 버그를 100% 원천 차단하기 위해,
            // 아군과 적군 부대를 좌측에서 우측(X 좌표 오름차순)으로 정렬하여 1:1로 정면 상대를 지정합니다!
            players.Sort((a, b) => a.GetVisualCenter().x.CompareTo(b.GetVisualCenter().x));
            enemies.Sort((a, b) => a.GetVisualCenter().x.CompareTo(b.GetVisualCenter().x));

            for (int i = 0; i < enemies.Count; i++)
            {
                Squad e = enemies[i];
                if (e == null) continue;

                int targetIdx = Mathf.Clamp(i, 0, players.Count - 1);
                Squad targetPlayer = players[targetIdx];
                if (targetPlayer != null)
                {
                    e.CommandAttackSquad(targetPlayer);
                }
            }

            // 🛡️ [아군 부대 초기 1:1 정면 적군 락온 기본 부여]:
            // 아군 부대도 정면의 적 부대 ID를 시작부터 TargetSquadId로 보유하게 하여,
            // 12m 공간 해시 레이더나 백병전에서 옆 부대 적을 무차별 타겟팅하는 공백 시간을 100% 제거합니다!
            // (부대 상태는 대기/Idle 상태를 그대로 유지하므로 먼저 돌격하지 않고 단단히 방어진을 유지합니다)
            for (int i = 0; i < players.Count; i++)
            {
                Squad p = players[i];
                if (p == null) continue;

                int targetEnemyIdx = Mathf.Clamp(i, 0, enemies.Count - 1);
                Squad targetEnemy = enemies[targetEnemyIdx];
                if (targetEnemy != null)
                {
                    p.currentTargetSquad = targetEnemy;
                    p.SyncTargetSquadIdToSimulations(targetEnemy.GetInstanceID());
                }
            }
            Debug.Log("[BattleManager] 🤖 적군 및 아군 부대 전체에 X좌표 기준 1:1 정면 맞대결 매칭 완료.");
        }
    }

    /// <summary>
    /// 지정된 군단 설정 목록에 따라 부대들을 자동 횡대 또는 수동 좌표로 전장에 스폰합니다.
    /// </summary>
    private void SpawnArmy(bool isPlayer, List<SquadSpawnConfig> configs, Vector3 baseCenter, float facingAngle)
    {
        if (configs == null || configs.Count == 0) return;

        Quaternion baseRot = Quaternion.Euler(0f, facingAngle, 0f);

        // 자동 배치 대상 부대들의 인덱스와 크기 계산
        List<int> autoIndices = new List<int>();
        List<float> autoWidths = new List<float>();

        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            if (cfg == null) continue;

            if (!cfg.useCustomPosition)
            {
                autoIndices.Add(i);
                int cols = Mathf.Max(1, cfg.columns);
                float width = (cols - 1) * Squad.DEFAULT_SPACING_X;
                autoWidths.Add(width);
            }
        }

        // 자동 배치 부대들의 X 오프셋 계산 (진영 중심점 기준 대칭 정렬)
        Dictionary<int, Vector3> autoPositions = new Dictionary<int, Vector3>();
        if (autoIndices.Count > 0)
        {
            float totalWidth = 0f;
            for (int i = 0; i < autoWidths.Count; i++)
            {
                totalWidth += autoWidths[i];
                if (i < autoWidths.Count - 1) totalWidth += squadSpacing;
            }

            float currentLeft = -totalWidth * 0.5f;
            for (int i = 0; i < autoIndices.Count; i++)
            {
                int idx = autoIndices[i];
                float w = autoWidths[i];
                float centerX = currentLeft + (w * 0.5f);
                Vector3 localPos = new Vector3(centerX, 0f, 0f);
                Vector3 worldPos = baseCenter + (baseRot * localPos);
                autoPositions[idx] = worldPos;
                currentLeft += w + squadSpacing;
            }
        }

        // 개별 부대 스폰
        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            if (cfg == null) continue;

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (cfg.useCustomPosition)
            {
                spawnPos = cfg.customPosition;
                spawnRot = Quaternion.Euler(0f, cfg.customRotationY, 0f);
            }
            else
            {
                spawnPos = autoPositions.TryGetValue(i, out Vector3 pos) ? pos : baseCenter;
                spawnRot = baseRot;
            }

            SpawnSquad(isPlayer, cfg, spawnPos, spawnRot);
        }
    }

    /// <summary>
    /// 단일 부대 설정(SquadSpawnConfig)을 바탕으로 부대 및 유닛들을 생성합니다.
    /// </summary>
    public Squad SpawnSquad(bool isPlayer, SquadSpawnConfig config, Vector3 centerPosition, Quaternion spawnRotation)
    {
        if (squadPrefab == null)
        {
            Debug.LogError("[BattleManager] Squad Prefab이 Inspector에 할당되지 않았습니다.");
            return null;
        }

        GameObject squadObj = Instantiate(squadPrefab, centerPosition, spawnRotation);
        if (!string.IsNullOrEmpty(config.squadName))
        {
            squadObj.name = $"Squad_{config.squadName}_{(isPlayer ? "Player" : "Enemy")}";
        }

        Squad squad = squadObj.GetComponent<Squad>();
        if (squad == null)
        {
            Debug.LogError("[BattleManager] 생성된 SquadPrefab에서 Squad 컴포넌트를 찾을 수 없습니다.");
            Destroy(squadObj);
            return null;
        }

        int cols = Mathf.Max(1, config.columns);
        squad.squadName = !string.IsNullOrEmpty(config.squadName) ? config.squadName : (isPlayer ? "아군 부대" : "적군 부대");
        squad.isPlayer = isPlayer;
        squad.currentColumns = cols;
        squad.currentFormationType = config.formationType;

        GameObject prefabToSpawn = (config.customUnitPrefab != null)
            ? config.customUnitPrefab
            : (isPlayer ? playerUnitPrefab : enemyUnitPrefab);

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[BattleManager] {(isPlayer ? "Player" : "Enemy")} Unit Prefab이 할당되지 않았습니다.");
            return squad;
        }

        int unitCount = config.unitCount;
        List<SlotInfo> spawnSlots = squad.GenerateFormationSlots(unitCount, cols, out int totalRows, squad.currentFormationType);

        if (usePureECS)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                EntityManager em = world.EntityManager;
                EntityArchetype archetype = em.CreateArchetype(
                    typeof(UnitEntityTag),
                    typeof(UnitMovementData),
                    typeof(UnitCombatData),
                    typeof(UnitSeparationData),
                    typeof(SpatialGridCell)
                );

                for (int i = 0; i < unitCount; i++)
                {
                    var slot = (i < spawnSlots.Count) ? spawnSlots[i] : default;
                    Vector3 localOffset = (i < spawnSlots.Count) ? slot.localOffset : Vector3.zero;
                    Vector3 spawnPos = centerPosition + (spawnRotation * localOffset);

                    Entity entity = em.CreateEntity(archetype);

                    em.SetComponentData(entity, new UnitEntityTag
                    {
                        Faction = isPlayer ? 1 : 0,
                        SquadId = squad.GetInstanceID(),
                        IsFreeUnit = 0,
                        IsAlive = 1,
                        SlotIndex = i,
                        Row = slot.row,
                        Col = slot.col
                    });

                    em.SetComponentData(entity, new UnitMovementData
                    {
                        Position = spawnPos,
                        Rotation = spawnRotation,
                        Velocity = float3.zero,
                        TargetPosition = spawnPos,
                        TargetRotation = spawnRotation,
                        MoveSpeed = squad.targetSpeed > 0 ? squad.targetSpeed : 1.0f,
                        CurrentSpeed = 0f,
                        Acceleration = 5.5f,
                        StoppingDistance = 0.2f,
                        IsCharging = 0,
                        IsCombatRunning = 0
                    });

                    em.SetComponentData(entity, new UnitCombatData
                    {
                        CurrentHp = 100f,
                        MaxHp = 100f,
                        Damage = 10f,
                        AttackCooldown = 1.0f,
                        LastAttackTime = -100f,
                        DetectRange = 5.0f,
                        AttackRange = 0.8f,
                        CurrentState = 0,
                        TargetEntity = Entity.Null,
                        KnockbackVelocity = float3.zero,
                        Mass = 100f,
                        ChargeSpeed = 4.8f,
                        ChargeBonus = 15f,
                        MaxChargeDamage = 35f,
                        ChargeImpactReady = 1,
                        EngagementStartTime = 0f,
                        AutoAttackEnabled = 1,
                        TargetSquadId = -1
                    });

                    em.SetComponentData(entity, new UnitSeparationData
                    {
                        PersonalRadius = 1.00f,
                        SeparationForce = float3.zero
                    });
                }
            }

            squad.initialUnitCount = unitCount;
            allSquads.Add(squad);

            if (isPlayer && SquadCardUIManager.Instance != null)
            {
                SquadCardUIManager.Instance.RegisterSquad(squad);
            }
            if (SquadIconUIManager.Instance != null)
            {
                SquadIconUIManager.Instance.RegisterSquad(squad);
            }
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.RegisterSquad(squad);
            }

            return squad;
        }

        for (int i = 0; i < unitCount; i++)
        {
            var slot = (i < spawnSlots.Count) ? spawnSlots[i] : default;
            Vector3 localOffset = (i < spawnSlots.Count) ? slot.localOffset : Vector3.zero;
            Vector3 spawnPos = centerPosition + (spawnRotation * localOffset);

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject unitObj = Instantiate(prefabToSpawn, spawnPos, spawnRotation);
            Unit unit = unitObj.GetComponent<Unit>();

            if (unit != null)
            {
                unit.isPlayer = isPlayer;
                unit.SetGridPosition(slot.row, slot.col);
                squad.members.Add(unit);
            }
        }

        squad.initialUnitCount = squad.members.Count;
        squad.RebuildGridStructure(cols, forceSpatialSort: false);
        allSquads.Add(squad);

        return squad;
    }

    /// <summary>
    /// 기존 호환용 단일 부대 생성 함수입니다. (PlayerController 창설 및 단순 스폰용)
    /// </summary>
    public Squad SpawnSquad(bool isPlayer, int unitCount, Vector3 centerPosition, int columns = -1)
    {
        int cols = (columns > 0) ? columns : 5;
        SquadSpawnConfig config = new SquadSpawnConfig
        {
            squadName = isPlayer ? "Player Squad" : "Enemy Squad",
            unitCount = unitCount,
            columns = cols,
            formationType = SquadFormationType.Normal
        };
        Quaternion rot = isPlayer
            ? Quaternion.Euler(0f, playerFacingAngle, 0f)
            : Quaternion.Euler(0f, enemyFacingAngle, 0f);

        return SpawnSquad(isPlayer, config, centerPosition, rot);
    }
    #endregion

    #region 3D 물리 및 상태 리셋
    /// <summary>
    /// 씬 내 존재하는 모든 유닛의 물리 속도 및 NavMeshAgent 경로를 초기화합니다.
    /// </summary>
    public void ResetAllUnitsPhysics()
    {
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in units)
        {
            if (unit == null) continue;

            Rigidbody rb = unit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }
    #endregion

    #region 에디터 Gizmos 시각화
    private void OnDrawGizmosSelected()
    {
        // 1. 플레이어 진영 스폰 영역 및 부대 박스 그리기
        DrawArmyGizmos(true, playerArmyConfigs, playerSpawnCenter, playerFacingAngle, new Color(0f, 0.8f, 1f, 0.7f));

        // 2. 적군 진영 스폰 영역 및 부대 박스 그리기
        DrawArmyGizmos(false, enemyArmyConfigs, enemySpawnCenter, enemyFacingAngle, new Color(1f, 0.2f, 0.2f, 0.7f));
    }

    private void DrawArmyGizmos(bool isPlayer, List<SquadSpawnConfig> configs, Vector3 baseCenter, float facingAngle, Color baseColor)
    {
        // 진영 기본 중심점 구(Sphere)
        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(baseCenter, 1.0f);
        Gizmos.DrawLine(baseCenter, baseCenter + Quaternion.Euler(0f, facingAngle, 0f) * Vector3.forward * 3.0f);

        if (configs == null || configs.Count == 0) return;

        Quaternion baseRot = Quaternion.Euler(0f, facingAngle, 0f);

        // 자동 배치 오프셋 계산
        List<int> autoIndices = new List<int>();
        List<float> autoWidths = new List<float>();

        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            if (cfg != null && !cfg.useCustomPosition)
            {
                autoIndices.Add(i);
                int cols = Mathf.Max(1, cfg.columns);
                float width = (cols - 1) * Squad.DEFAULT_SPACING_X;
                autoWidths.Add(width);
            }
        }

        Dictionary<int, Vector3> autoPositions = new Dictionary<int, Vector3>();
        if (autoIndices.Count > 0)
        {
            float totalWidth = 0f;
            for (int i = 0; i < autoWidths.Count; i++)
            {
                totalWidth += autoWidths[i];
                if (i < autoWidths.Count - 1) totalWidth += squadSpacing;
            }

            float currentLeft = -totalWidth * 0.5f;
            for (int i = 0; i < autoIndices.Count; i++)
            {
                int idx = autoIndices[i];
                float w = autoWidths[i];
                float centerX = currentLeft + (w * 0.5f);
                autoPositions[idx] = baseCenter + (baseRot * new Vector3(centerX, 0f, 0f));
                currentLeft += w + squadSpacing;
            }
        }

        // 부대별 박스 그리기
        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            if (cfg == null) continue;

            Vector3 pos;
            Quaternion rot;

            if (cfg.useCustomPosition)
            {
                pos = cfg.customPosition;
                rot = Quaternion.Euler(0f, cfg.customRotationY, 0f);
            }
            else
            {
                pos = autoPositions.TryGetValue(i, out Vector3 p) ? p : baseCenter;
                rot = baseRot;
            }

            int cols = Mathf.Max(1, cfg.columns);
            int rows = Mathf.CeilToInt((float)Mathf.Max(1, cfg.unitCount) / cols);

            float width = (cols - 1) * Squad.DEFAULT_SPACING_X + 1.0f;
            float depth = (rows - 1) * Squad.DEFAULT_SPACING_Z + 1.0f;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);

            Gizmos.color = baseColor;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 0.5f, depth));

            // 전방 방향 화살표
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * (depth * 0.5f + 1.5f));

            Gizmos.matrix = oldMatrix;
        }
    }
    #endregion
}