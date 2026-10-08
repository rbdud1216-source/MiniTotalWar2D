using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Jobs;

/// <summary>
/// 유닛 시뮬레이션에 필요한 순수 값 타입(Blittable) 데이터 구조체입니다.
/// </summary>
public struct UnitJobData
{
    public int isPlayer;              // 1 = Player, 0 = Enemy
    public int isAlive;               // 1 = Alive, 0 = Dead
    public int currentState;          // 0 = Idle, 1 = Move, 2 = AttackMove, 3 = MeleeEngaged
    public int targetIndex;           // 타겟 유닛 인덱스 (-1 if none)
    public Vector3 targetPosition;    // 목표 이동 좌표
    public Quaternion targetRotation;  // 목표 회전
    public float currentHp;
    public float maxHp;
    public float damage;
    public int armor;                 // 기본 방어력 (0 ~ 10000, 10000 = 100.00% 완전 방어)
    public float attackCooldown;
    public float lastAttackTime;
    public float moveSpeed;
    public float currentSpeed;
    public float acceleration;
    public float stoppingDistance;
    public float detectRange;
    public float attackRange;
    public float minAttackRange;
    public float optimalRangeMin;
    public float closeRangeDamageRatio;
    public float knockbackPower;
    public int useSidearm;
    public float sidearmSwitchDistance;
    public float sidearmAttackRange;
    public float sidearmDamage;
    public float sidearmAttackCooldown;
    public float sidearmKnockbackPower;
    public float combatStoppingDistance; // 교전(백병전) 시 발을 멈추는 정지 거리 (m)
    public float engagementOffset;       // 적을 향해 접근할 때 적 중심으로부터의 목표 교전 간격/위치 (m)
    public float personalRadius;
    public float engagementStartTime; // 교전 개시 시점
    public float freeCombatDuration;   // 자유교전 전환 타이머 (20초 +- 10초)
    public int autoAttackEnabled;      // 1 = 자동 선제 돌격 요격(기본), 0 = 근접 접촉 방어 모드(V)
    public int isFreeUnit;             // 1 = 부대 없는 자유 유닛 (반경 1.15m 비정규 난전), 0 = 부대 유닛 (반경 0.70m 밀집 대형)
    public int squadId;                // 소속 부대 인스턴스 ID (-1 = 자유 유닛)
    public int targetSquadId;          // 지휘관이 지정한 목표 적 부대 ID (-1 = 미지정)
    public float mass;                 // 유닛 질량/무게 (kg)
    public float chargeSpeed;          // 돌격 속도
    public float chargeBonus;          // 돌격 보너스 계수
    public float maxChargeDamage;      // 첫 충돌 시 최대 데미지 한계치
    public int chargeImpactReady;      // 1 = 돌격 충격 장전 완료, 0 = 충격 소진
    public int canReflectCharge;       // 1 = 돌격 반사 가능(장창병), 0 = 불가능(검병)
    public float knockdownThreshold;  // 넘어짐 판정 넉백 속도 임계값 (m/s)
    public float knockdownDuration;   // 무력화 유지 시간 (초, 기본 3.0초)
    public float knockdownTimer;      // 현재 남은 무력화 시간 (0보다 크면 이동/공격 불가)
    public int isImmuneToKnockdown;   // 1 = 넘어짐/무력화 면역(불굴 특수능력), 0 = 넘어짐 가능
    public float baseMeleeKnockback;   // 평타 넉백 기본 세기 (기본: 0.45m/s)
    public float maxMeleeKnockbackCap; // 다대일 평타 넉백 상한 속도 (기본: 1.2m/s)
    public float sidearmBaseKnockback; // 보조무기 평타 넉백 기본 세기 (기본: 0.2m/s)
    public float sidearmMaxKnockbackCap; // 보조무기 다대일 넉백 상한 속도 (기본: 0.8m/s)
    public Vector3 knockbackVelocity;  // 넉백/충격 물리 속도

    // 🏹 원거리(사격) 및 투사체 물리 속성
    public int isRangedUnit;           // 1 = 원거리 궁병, 0 = 근접 보병
    public int currentAmmo;            // 실시간 잔여 탄약
    public int squadHasAmmo;           // 1 = 부대 전우 탄약 융통 가능(화살 잔여), 0 = 부대 화살 완전 고갈
    public int maxAmmo;                // 최대 소지 탄약
    public float rangedAttackRange;    // 최대 사거리 (150m)
    public float optimalRange;         // 최적 사거리 (50m)
    public float rangedMinRange;       // 최소 사거리 (5m)
    public float rangedBaseDamage;     // 원거리 기본 대미지 (15)
    public float minDamageRatioAtMax;  // 최대 사거리 최소 대미지 비율 (0.55)
    public float rangedAttackCooldown; // 사격 쿨다운 (2.2s)
    public float minSpreadRadius;      // 근거리 오차 반경 (0.3m)
    public float maxSpreadRadius;      // 최대 사거리 오차 반경 (3.5m)
    public int trajectoryMode;         // 0 = HighArc, 1 = Flat
    public float projectileSpeed;      // 화살 속도 (30m/s)
    public float projectileDrag;       // 화살 공기 저항 감속 계수 (0.25)
    public Vector3 launchOffset;       // 무기 발사 위치 로컬 오프셋 (X, Y, Z)
    public float gravityScale;         // 중력 계수 (1.0)
    public float rangedKnockbackPower; // 원거리 공격 피격 넉백 세기 (m/s)
    public int ignoreArmor;            // 1 = 방어력 완전 무시, 0 = 방어력 적용
    public float armorPiercingRatio;   // 관통 비율 (0.25)
    public int armorShredAmount;       // 방어력 삭감치
    public float meleeSwitchDistance;  // 백병전 전환 거리 (5.0m)
    public int canFireWhileMoving;     // 1 = 이동 중 사격 허용, 0 = 정지 시에만 사격
    public int row;                    // 대형 내 행 위치 (0, 1, 2...)
    public int col;                    // 대형 내 열 위치 (0, 1, 2...)
    public int fireAtWill;             // 1 = 자유 사격 켜짐, 0 = 꺼짐 (지정 명령 시에만 사격)

    // 📐 원거리 3축 공간 및 사선 클리어런스 (Job System)
    public float minRearSpacing;               // 후방 최소 필요 거리 (m)
    public float minLateralSpacing;            // 측면 최소 필요 거리 (m)
    public float headClearanceMargin;          // 머리 위 안전 여유 고도 (m)
    public int allowStaggeredRank2DirectFire;  // 1 = 체커보드 2열 틈새 직사 허용, 0 = 불허
    public int enableSequentialFire;           // 1 = 순차 사격 On, 0 = Off
    public float sequentialRowDelay;           // 순차 사격 행간 지연 시간 (초)
    public float squadSpacingX;                // 소속 부대 가로 실효 간격 (m)
    public float squadSpacingZ;                // 소속 부대 세로 실효 간격 (m)
    public int totalRows;                      // 소속 부대 총 행(Row) 수
    public int isStaggeredFormation;           // 1 = 체커보드 대형, 0 = 완전 직렬

    // ⛰️ 지형 및 경사면 설정
    public int alignToSlope;                   // 1 = 옵션 B (경사면 기울기 반영), 0 = 옵션 A (수직 유지)
    public float groundYOffset;                // 지면 밀착 높이 오프셋
}

/// <summary>
/// 멀티스레드 Job에서 화살 발사를 요청하기 위한 Blittable 커맨드 구조체입니다.
/// </summary>
public struct ArrowLaunchCommand
{
    public int isPlayer;
    public Vector3 shooterPos;
    public Vector3 targetPos;
    public float projectileSpeed;
    public float projectileDrag;
    public float damage;
    public float armorPiercingRatio;
    public int armorShredAmount;
    public int ignoreArmor;
    public int trajectoryMode;
    public float gravityScale;
    public float spreadRadius;
    public int hasAllyObstruction;     // 1 = 앞에 아군 차폐가 있어 48~75도 고각 곡사, 0 = 0~45도 직사/표준곡사
    public int shooterRow;             // 🏹 사수의 방진 내 행(Row) 인덱스 (미세 분산용)
    public float knockbackPower;       // 원거리 피격 넉백 세기 (m/s)
}

/// <summary>
/// 2,000 ~ 20,000기의 대규모 전장 유닛을 CPU 멀티코어로 병렬 시뮬레이션하는 매니저입니다.
/// </summary>
public class UnitJobSimulationManager : MonoBehaviour
{
    private static UnitJobSimulationManager _instance;
    private static bool isApplicationQuitting = false;

    public static bool IsQuitting => isApplicationQuitting;
    public static bool HasInstance => _instance != null && !isApplicationQuitting;

    public static UnitJobSimulationManager Instance
    {
        get
        {
            if (isApplicationQuitting) return null;

            if (_instance == null)
            {
                _instance = FindAnyObjectByType<UnitJobSimulationManager>();
                if (_instance == null && !isApplicationQuitting)
                {
                    GameObject go = new GameObject("UnitJobSimulationManager");
                    _instance = go.AddComponent<UnitJobSimulationManager>();
                }
            }
            return _instance;
        }
    }

    public List<Unit> registeredUnits = new List<Unit>();
    private TransformAccessArray transformAccessArray;

    private NativeArray<UnitJobData> unitDataArray;
    private NativeArray<Vector3> positionArray;
    private NativeArray<Vector3> separationForceArray;
    private NativeArray<int> targetIndexArray;
    private NativeParallelMultiHashMap<int, int> squadMap;
    private NativeQueue<ArrowLaunchCommand> arrowLaunchQueue;

    private bool isNativeArraysAllocated = false;
    private JobHandle simulationJobHandle;

    public int TotalRegisteredUnits => registeredUnits != null ? registeredUnits.Count : 0;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        transformAccessArray = new TransformAccessArray(100);
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        simulationJobHandle.Complete();
        DisposeNativeArrays();
        if (transformAccessArray.isCreated)
        {
            transformAccessArray.Dispose();
        }
    }

    private void DisposeNativeArrays()
    {
        if (isNativeArraysAllocated)
        {
            if (unitDataArray.IsCreated) unitDataArray.Dispose();
            if (positionArray.IsCreated) positionArray.Dispose();
            if (separationForceArray.IsCreated) separationForceArray.Dispose();
            if (targetIndexArray.IsCreated) targetIndexArray.Dispose();
            if (squadMap.IsCreated) squadMap.Dispose();
            if (arrowLaunchQueue.IsCreated) arrowLaunchQueue.Dispose();
            isNativeArraysAllocated = false;
        }
    }

    private void AllocateNativeArrays(int count)
    {
        DisposeNativeArrays();
        if (count <= 0) return;

        unitDataArray = new NativeArray<UnitJobData>(count, Allocator.Persistent);
        positionArray = new NativeArray<Vector3>(count, Allocator.Persistent);
        separationForceArray = new NativeArray<Vector3>(count, Allocator.Persistent);
        targetIndexArray = new NativeArray<int>(count, Allocator.Persistent);
        squadMap = new NativeParallelMultiHashMap<int, int>(count, Allocator.Persistent);
        arrowLaunchQueue = new NativeQueue<ArrowLaunchCommand>(Allocator.Persistent);

        isNativeArraysAllocated = true;
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null || registeredUnits.Contains(unit)) return;

        simulationJobHandle.Complete();

        // 1. 기존 유닛 데이터 백업 (자신의 simulationIndex 기반 1:1 정확 매핑)
        Dictionary<Unit, UnitJobData> backupData = BackupCurrentJobData();

        registeredUnits.Add(unit);

        RebuildSimulationBuffers(backupData);
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null || !registeredUnits.Contains(unit)) return;

        simulationJobHandle.Complete();

        // 1. 제거 대상 유닛을 제외하고 나머지 유닛들의 데이터만 안전하게 백업
        Dictionary<Unit, UnitJobData> backupData = BackupCurrentJobData(excludeUnit: unit);

        unit.simulationIndex = -1;
        registeredUnits.Remove(unit);

        RebuildSimulationBuffers(backupData);
    }

    private Dictionary<Unit, UnitJobData> BackupCurrentJobData(Unit excludeUnit = null)
    {
        Dictionary<Unit, UnitJobData> backup = new Dictionary<Unit, UnitJobData>();
        if (isNativeArraysAllocated && unitDataArray.IsCreated)
        {
            for (int i = 0; i < registeredUnits.Count; i++)
            {
                Unit u = registeredUnits[i];
                if (u == null || u == excludeUnit) continue;

                int oldIdx = u.simulationIndex;
                if (oldIdx >= 0 && oldIdx < unitDataArray.Length)
                {
                    backup[u] = unitDataArray[oldIdx];
                }
            }
        }
        return backup;
    }

    private void RebuildSimulationBuffers(Dictionary<Unit, UnitJobData> backupData)
    {
        int count = registeredUnits.Count;

        // 1. TransformAccessArray 전면 재생성 (registeredUnits와 100% 동일한 순서 보장)
        if (transformAccessArray.isCreated)
        {
            transformAccessArray.Dispose();
        }

        transformAccessArray = new TransformAccessArray(Mathf.Max(1, count));
        for (int i = 0; i < count; i++)
        {
            Unit u = registeredUnits[i];
            if (u != null)
            {
                transformAccessArray.Add(u.transform);
            }
        }

        // 2. NativeArray 버퍼 재할당
        AllocateNativeArrays(count);

        // 3. 데이터 1:1 정확 매핑 및 초기화
        for (int i = 0; i < count; i++)
        {
            Unit u = registeredUnits[i];
            if (u == null) continue;

            u.simulationIndex = i;

            if (backupData != null && backupData.TryGetValue(u, out UnitJobData oldData))
            {
                oldData.isAlive = (u.currentHp > 0) ? 1 : 0;
                oldData.currentHp = u.currentHp;
                oldData.armor = u.armor;
                oldData.isFreeUnit = (u.mySquad == null) ? 1 : 0;
                oldData.attackRange = (u.attackRange > 0.1f) ? u.attackRange : 1.45f;
                oldData.minAttackRange = u.minAttackRange;
                oldData.optimalRangeMin = u.optimalRangeMin;
                oldData.closeRangeDamageRatio = (u.closeRangeDamageRatio > 0.05f) ? u.closeRangeDamageRatio : 1.0f;
                oldData.knockbackPower = (u.knockbackPower > 0f) ? u.knockbackPower : 1.0f;
                oldData.useSidearm = u.useSidearm ? 1 : 0;
                oldData.sidearmSwitchDistance = (u.sidearmSwitchDistance > 0.1f) ? u.sidearmSwitchDistance : 1.2f;
                oldData.sidearmAttackRange = (u.sidearmAttackRange > 0.1f) ? u.sidearmAttackRange : 1.0f;
                oldData.sidearmDamage = (u.sidearmDamage > 0f) ? u.sidearmDamage : 4.0f;
                oldData.sidearmAttackCooldown = (u.sidearmAttackCooldown > 0.05f) ? u.sidearmAttackCooldown : 0.8f;
                oldData.sidearmKnockbackPower = u.sidearmKnockbackPower;
                oldData.sidearmBaseKnockback = (u.sidearmBaseKnockback > 0f) ? u.sidearmBaseKnockback : 0.2f;
                oldData.sidearmMaxKnockbackCap = (u.sidearmMaxKnockbackCap > 0f) ? u.sidearmMaxKnockbackCap : 0.8f;
                oldData.combatStoppingDistance = (u.combatStoppingDistance > 0.1f) ? u.combatStoppingDistance : 1.05f;
                oldData.engagementOffset = (u.engagementOffset > 0.05f) ? u.engagementOffset : 0.40f;

                // 🏹 원거리(사격) 속성 동기화
                oldData.isRangedUnit = u.isRangedUnit ? 1 : 0;
                oldData.maxAmmo = u.maxAmmo;
                oldData.currentAmmo = u.currentAmmo;
                oldData.rangedAttackRange = (u.rangedAttackRange > 1.0f) ? u.rangedAttackRange : 150.0f;
                oldData.optimalRange = (u.optimalRange > 1.0f) ? u.optimalRange : 50.0f;
                oldData.rangedMinRange = u.rangedMinRange;
                oldData.rangedBaseDamage = (u.rangedBaseDamage > 0f) ? u.rangedBaseDamage : 15.0f;
                oldData.minDamageRatioAtMax = (u.minDamageRatioAtMax > 0.05f) ? u.minDamageRatioAtMax : 0.55f;
                oldData.rangedAttackCooldown = (u.rangedAttackCooldown > 0.05f) ? u.rangedAttackCooldown : 2.2f;
                oldData.minSpreadRadius = u.minSpreadRadius;
                oldData.maxSpreadRadius = (u.maxSpreadRadius > 0.1f) ? u.maxSpreadRadius : 3.5f;
                oldData.trajectoryMode = (int)u.trajectoryMode;
                oldData.projectileSpeed = (u.projectileSpeed > 1.0f) ? u.projectileSpeed : 30.0f;
                oldData.projectileDrag = (u.projectileDrag >= 0f) ? u.projectileDrag : 0.25f;
                oldData.launchOffset = (u.launchOffset.y > 0.1f) ? u.launchOffset : new Vector3(0.15f, 1.35f, 0.45f);
                oldData.gravityScale = (u.gravityScale > 0.05f) ? u.gravityScale : 1.0f;
                oldData.rangedKnockbackPower = (u.rangedKnockbackPower >= 0f) ? u.rangedKnockbackPower : 0.35f;
                oldData.ignoreArmor = u.ignoreArmor ? 1 : 0;
                oldData.armorPiercingRatio = u.armorPiercingRatio;
                oldData.armorShredAmount = u.armorShredAmount;
                oldData.meleeSwitchDistance = (u.meleeSwitchDistance > 0.1f) ? u.meleeSwitchDistance : 5.0f;
                oldData.canFireWhileMoving = u.canFireWhileMoving ? 1 : 0;
                oldData.row = u.Row;
                oldData.col = u.Col;
                oldData.fireAtWill = ((u.mySquad != null) ? u.mySquad.fireAtWill : u.fireAtWill) ? 1 : 0;

                // 📐 원거리 3축 공간 및 사선 클리어런스 데이터 동기화
                oldData.minRearSpacing = u.minRearSpacing;
                oldData.minLateralSpacing = u.minLateralSpacing;
                oldData.headClearanceMargin = (u.headClearanceMargin > 0.05f) ? u.headClearanceMargin : 0.45f;
                oldData.allowStaggeredRank2DirectFire = u.allowStaggeredRank2DirectFire ? 1 : 0;
                oldData.enableSequentialFire = (u.mySquad != null && u.mySquad.enableSequentialFire) ? 1 : 0;
                oldData.sequentialRowDelay = (u.mySquad != null) ? u.mySquad.sequentialRowDelay : 0.4f;
                oldData.squadSpacingX = (u.mySquad != null) ? (u.mySquad.spacingX * u.mySquad.formationWidthMultiplier) : 1.0f;
                oldData.squadSpacingZ = (u.mySquad != null) ? (u.mySquad.spacingZ * u.mySquad.formationLengthMultiplier) : 1.0f;
                oldData.totalRows = (u.mySquad != null) ? ((u.mySquad.totalGridRows > 0) ? u.mySquad.totalGridRows : Mathf.CeilToInt((float)u.mySquad.MemberCount / Mathf.Max(1, u.mySquad.currentColumns))) : 1;
                oldData.isStaggeredFormation = (u.mySquad != null && u.mySquad.useStaggeredFormation) ? 1 : 0;

                if (u.FixedTargetPos != Vector3.zero)
                {
                    oldData.targetPosition = u.FixedTargetPos;
                    oldData.targetRotation = u.TargetRotation;
                }
                unitDataArray[i] = oldData;
            }
            else
            {
                Vector3 tgtPos = (u.FixedTargetPos != Vector3.zero) ? u.FixedTargetPos : u.transform.position;
                Quaternion tgtRot = (u.TargetRotation != Quaternion.identity) ? u.TargetRotation : u.transform.rotation;

                bool isRunning = (u.mySquad != null) ? u.mySquad.isRunning : u.isRunning;
                float defaultSpeed = (u.mySquad != null) ? u.mySquad.targetSpeed : (u.isRunning ? u.runSpeed : u.walkSpeed);
                if (defaultSpeed <= 0f) defaultSpeed = (u.mySquad == null) ? 1.2f : 1.0f;
                float defaultAccel = isRunning ? 8.0f : 5.0f;

                UnitJobData data = new UnitJobData
                {
                    isPlayer = u.isPlayer ? 1 : 0,
                    isAlive = (u.currentHp > 0) ? 1 : 0,
                    currentState = (int)u.currentState,
                    targetIndex = -1,
                    targetPosition = tgtPos,
                    targetRotation = tgtRot,
                    currentHp = u.currentHp,
                    maxHp = u.maxHp,
                    damage = u.damage,
                    armor = u.armor,
                    attackCooldown = u.attackCooldown,
                    lastAttackTime = Time.time - Random.Range(0f, u.attackCooldown),
                    moveSpeed = defaultSpeed,
                    currentSpeed = 0f,
                    acceleration = defaultAccel,
                    stoppingDistance = 0.2f,
                    detectRange = u.detectRange,
                    attackRange = (u.attackRange > 0.1f) ? u.attackRange : 1.45f,
                    minAttackRange = u.minAttackRange,
                    optimalRangeMin = u.optimalRangeMin,
                    closeRangeDamageRatio = (u.closeRangeDamageRatio > 0.05f) ? u.closeRangeDamageRatio : 1.0f,
                    knockbackPower = (u.knockbackPower > 0f) ? u.knockbackPower : 1.0f,
                    useSidearm = u.useSidearm ? 1 : 0,
                    sidearmSwitchDistance = (u.sidearmSwitchDistance > 0.1f) ? u.sidearmSwitchDistance : 1.2f,
                    sidearmAttackRange = (u.sidearmAttackRange > 0.1f) ? u.sidearmAttackRange : 1.0f,
                    sidearmDamage = (u.sidearmDamage > 0f) ? u.sidearmDamage : 4.0f,
                    sidearmAttackCooldown = (u.sidearmAttackCooldown > 0.05f) ? u.sidearmAttackCooldown : 0.8f,
                    sidearmKnockbackPower = u.sidearmKnockbackPower,
                    combatStoppingDistance = (u.combatStoppingDistance > 0.1f) ? u.combatStoppingDistance : 1.05f,
                    engagementOffset = (u.engagementOffset > 0.05f) ? u.engagementOffset : 0.40f,
                    personalRadius = (u.mySquad == null) ? 1.15f : 1.00f,
                    engagementStartTime = 0f,
                    freeCombatDuration = Random.Range(2f, 8f),
                    autoAttackEnabled = u.autoAttackEnabled ? 1 : 0,
                    isFreeUnit = (u.mySquad == null) ? 1 : 0,
                    squadId = (u.mySquad != null) ? u.mySquad.GetInstanceID() : -1,
                    targetSquadId = (u.mySquad != null && u.mySquad.currentTargetSquad != null) ? u.mySquad.currentTargetSquad.GetInstanceID() : -1,
                    mass = (u.mass > 0f) ? u.mass : 100f,
                    chargeSpeed = (u.chargeSpeed > 0f) ? u.chargeSpeed : 4.8f,
                    chargeBonus = (u.chargeBonus > 0f) ? u.chargeBonus : 15f,
                    maxChargeDamage = (u.maxChargeDamage > 0f) ? u.maxChargeDamage : 35f,
                    chargeImpactReady = 1,
                    canReflectCharge = u.canReflectCharge ? 1 : 0,
                    knockdownThreshold = (u.knockdownSpeedThreshold > 0f) ? u.knockdownSpeedThreshold : 2.0f,
                    knockdownDuration = (u.knockdownDuration > 0f) ? u.knockdownDuration : 3.0f,
                    knockdownTimer = 0f,
                    isImmuneToKnockdown = u.isImmuneToKnockdown ? 1 : 0,
                    baseMeleeKnockback = (u.baseMeleeKnockback > 0f) ? u.baseMeleeKnockback : 0.45f,
                    maxMeleeKnockbackCap = (u.maxMeleeKnockbackCap > 0f) ? u.maxMeleeKnockbackCap : 1.2f,
                    sidearmBaseKnockback = (u.sidearmBaseKnockback > 0f) ? u.sidearmBaseKnockback : 0.2f,
                    sidearmMaxKnockbackCap = (u.sidearmMaxKnockbackCap > 0f) ? u.sidearmMaxKnockbackCap : 0.8f,
                    knockbackVelocity = Vector3.zero,

                    // 🏹 원거리(사격) 속성 초기화
                    isRangedUnit = u.isRangedUnit ? 1 : 0,
                    maxAmmo = u.maxAmmo,
                    currentAmmo = u.currentAmmo,
                    squadHasAmmo = (u.mySquad != null) ? (u.mySquad.HasAmmo ? 1 : 0) : (u.currentAmmo > 0 ? 1 : 0),
                    rangedAttackRange = (u.rangedAttackRange > 1.0f) ? u.rangedAttackRange : 150.0f,
                    optimalRange = (u.optimalRange > 1.0f) ? u.optimalRange : 50.0f,
                    rangedMinRange = u.rangedMinRange,
                    rangedBaseDamage = (u.rangedBaseDamage > 0f) ? u.rangedBaseDamage : 15.0f,
                    minDamageRatioAtMax = (u.minDamageRatioAtMax > 0.05f) ? u.minDamageRatioAtMax : 0.55f,
                    rangedAttackCooldown = (u.rangedAttackCooldown > 0.05f) ? u.rangedAttackCooldown : 2.2f,
                    minSpreadRadius = u.minSpreadRadius,
                    maxSpreadRadius = (u.maxSpreadRadius > 0.1f) ? u.maxSpreadRadius : 3.5f,
                    trajectoryMode = (int)u.trajectoryMode,
                    projectileSpeed = (u.projectileSpeed > 1.0f) ? u.projectileSpeed : 30.0f,
                    projectileDrag = (u.projectileDrag >= 0f) ? u.projectileDrag : 0.25f,
                    launchOffset = Vector3.Scale(
                        ((u.launchPoint != null) ? u.launchPoint.localPosition : ((u.launchOffset.sqrMagnitude > 0.001f) ? u.launchOffset : new Vector3(0.25f, 0.15f, 0.45f))),
                        u.transform.localScale),
                    gravityScale = (u.gravityScale > 0.05f) ? u.gravityScale : 1.0f,
                    rangedKnockbackPower = (u.rangedKnockbackPower >= 0f) ? u.rangedKnockbackPower : 0.35f,
                    ignoreArmor = u.ignoreArmor ? 1 : 0,
                    armorPiercingRatio = u.armorPiercingRatio,
                    armorShredAmount = u.armorShredAmount,
                    meleeSwitchDistance = (u.meleeSwitchDistance > 0.1f) ? u.meleeSwitchDistance : 5.0f,
                    canFireWhileMoving = u.canFireWhileMoving ? 1 : 0,
                    row = u.Row,
                    col = u.Col,
                    fireAtWill = ((u.mySquad != null) ? u.mySquad.fireAtWill : u.fireAtWill) ? 1 : 0,

                    // 📐 원거리 3축 공간 및 사선 클리어런스 초기화
                    minRearSpacing = u.minRearSpacing,
                    minLateralSpacing = u.minLateralSpacing,
                    headClearanceMargin = (u.headClearanceMargin > 0.05f) ? u.headClearanceMargin : 0.45f,
                    allowStaggeredRank2DirectFire = u.allowStaggeredRank2DirectFire ? 1 : 0,
                    enableSequentialFire = (u.mySquad != null && u.mySquad.enableSequentialFire) ? 1 : 0,
                    sequentialRowDelay = (u.mySquad != null) ? u.mySquad.sequentialRowDelay : 0.4f,
                    squadSpacingX = (u.mySquad != null) ? (u.mySquad.spacingX * u.mySquad.formationWidthMultiplier) : 1.0f,
                    squadSpacingZ = (u.mySquad != null) ? (u.mySquad.spacingZ * u.mySquad.formationLengthMultiplier) : 1.0f,
                    totalRows = (u.mySquad != null) ? ((u.mySquad.totalGridRows > 0) ? u.mySquad.totalGridRows : Mathf.CeilToInt((float)u.mySquad.MemberCount / Mathf.Max(1, u.mySquad.currentColumns))) : 1,
                    isStaggeredFormation = (u.mySquad != null && u.mySquad.useStaggeredFormation) ? 1 : 0
                };

                unitDataArray[i] = data;
            }

            positionArray[i] = u.transform.position;
        }
    }

    public void UpdateUnitTargetPosition(Unit unit, Vector3 destination, Quaternion rotation, UnitCommandState state)
    {
        if (unit == null) return;

        simulationJobHandle.Complete();

        int idx = unit.simulationIndex;
        if (idx >= 0 && idx < registeredUnits.Count && isNativeArraysAllocated && unitDataArray.IsCreated)
        {
            UnitJobData data = unitDataArray[idx];
            data.targetPosition = destination;
            data.targetRotation = rotation;
            data.currentState = (int)state;
            data.isFreeUnit = (unit.mySquad == null) ? 1 : 0;

            // 🚨 핵심: 이동/후퇴(Move = 1) 명령을 내렸을 때는 교전 락을 즉시 완전히 해제하여 탈출/도망 100% 보장!
            if (state == UnitCommandState.Move)
            {
                data.engagementStartTime = 0f;
                data.targetIndex = -1;
                data.chargeImpactReady = 0;
                data.knockbackVelocity = Vector3.zero;
            }

            unitDataArray[idx] = data;
        }
    }

    public void UpdateUnitSpeedAndAccel(Unit unit, float speed, float accel)
    {
        if (unit == null) return;

        simulationJobHandle.Complete();

        int idx = unit.simulationIndex;
        if (idx >= 0 && idx < registeredUnits.Count && isNativeArraysAllocated && unitDataArray.IsCreated)
        {
            UnitJobData data = unitDataArray[idx];
            data.moveSpeed = speed;
            data.acceleration = accel;
        }
    }

    /// <summary>
    /// 부대 지휘관이 지정한 목표 적 부대 ID(TargetSquadId)를 소속 부대원 전원에게 일괄 주입
    /// </summary>
    public void UpdateSquadTargetSquadId(Squad squad, int targetSquadId)
    {
        if (squad == null || !isNativeArraysAllocated || !unitDataArray.IsCreated) return;

        simulationJobHandle.Complete();

        int mySquadId = squad.GetInstanceID();
        int count = registeredUnits.Count;

        for (int i = 0; i < count; i++)
        {
            Unit u = registeredUnits[i];
            if (u != null && u.mySquad == squad)
            {
                UnitJobData data = unitDataArray[i];
                data.squadId = mySquadId;
                data.targetSquadId = targetSquadId;
                unitDataArray[i] = data;
            }
        }
    }

    /// <summary>
    /// 진형/태세/밀집도에 따라 계산된 부대 소속 유닛들의 방어력, 무게(질량), 공격 쿨다운 및 원거리 클리어런스 파라미터를 일괄 갱신합니다.
    /// </summary>
    public void UpdateSquadCombatModifiers(
        Squad squad, int effectiveArmor, float effectiveMass, float effectiveCooldown,
        float effSpacingX, float effSpacingZ, int totalRows, bool isStaggered,
        bool enableSequentialFire, float sequentialRowDelay)
    {
        if (squad == null || !isNativeArraysAllocated || !unitDataArray.IsCreated) return;

        simulationJobHandle.Complete();

        int count = registeredUnits.Count;
        for (int i = 0; i < count; i++)
        {
            Unit u = registeredUnits[i];
            if (u != null && u.mySquad == squad)
            {
                UnitJobData data = unitDataArray[i];
                data.armor = effectiveArmor;
                data.mass = effectiveMass;
                data.attackCooldown = effectiveCooldown;
                data.squadSpacingX = effSpacingX;
                data.squadSpacingZ = effSpacingZ;
                data.totalRows = totalRows;
                data.isStaggeredFormation = isStaggered ? 1 : 0;
                data.enableSequentialFire = enableSequentialFire ? 1 : 0;
                data.sequentialRowDelay = sequentialRowDelay;
                unitDataArray[i] = data;

                // 유닛 자체 필드도 함께 동기화
                u.armor = effectiveArmor;
                u.mass = effectiveMass;
                u.attackCooldown = effectiveCooldown;
            }
        }
    }

    private void Update()
    {
        int unitCount = registeredUnits.Count;
        if (unitCount == 0 || !isNativeArraysAllocated) return;

        // 1. 이전 프레임의 멀티코어 Job 완료 대기
        simulationJobHandle.Complete();

        // 2. 메인 스레드에서 유닛 상태 동기화 및 사망 처리
        float currentTime = Time.time;
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < unitCount; i++)
        {
            Unit u = registeredUnits[i];
            if (u == null) continue;

            UnitJobData data = unitDataArray[i];
            data.autoAttackEnabled = u.autoAttackEnabled ? 1 : 0;
            data.fireAtWill = ((u.mySquad != null) ? u.mySquad.fireAtWill : u.fireAtWill) ? 1 : 0;
            data.mass = (u.mass > 0f) ? u.mass : 100f;
            data.chargeSpeed = (u.chargeSpeed > 0f) ? u.chargeSpeed : 4.8f;
            data.chargeBonus = (u.chargeBonus > 0f) ? u.chargeBonus : 15f;
            data.maxChargeDamage = (u.maxChargeDamage > 0f) ? u.maxChargeDamage : 35f;
            data.canReflectCharge = u.canReflectCharge ? 1 : 0;
            data.knockdownThreshold = (u.knockdownSpeedThreshold > 0f) ? u.knockdownSpeedThreshold : 2.0f;
            data.knockdownDuration = (u.knockdownDuration > 0f) ? u.knockdownDuration : 3.0f;
            data.isImmuneToKnockdown = u.isImmuneToKnockdown ? 1 : 0;
            data.baseMeleeKnockback = (u.baseMeleeKnockback > 0f) ? u.baseMeleeKnockback : 0.45f;
            data.maxMeleeKnockbackCap = (u.maxMeleeKnockbackCap > 0f) ? u.maxMeleeKnockbackCap : 1.2f;
            data.sidearmBaseKnockback = (u.sidearmBaseKnockback > 0f) ? u.sidearmBaseKnockback : 0.2f;
            data.sidearmMaxKnockbackCap = (u.sidearmMaxKnockbackCap > 0f) ? u.sidearmMaxKnockbackCap : 0.8f;
            data.damage = u.damage;
            data.isFreeUnit = (u.mySquad == null) ? 1 : 0;
            data.squadId = (u.mySquad != null) ? u.mySquad.GetInstanceID() : -1;
            data.targetSquadId = (u.mySquad != null && u.mySquad.currentTargetSquad != null && u.mySquad.currentTargetSquad.MemberCount > 0) 
                ? u.mySquad.currentTargetSquad.GetInstanceID() 
                : -1;

            // ⚡ [R] 걷기/달리기 속도 및 가속도 실시간 동기화 (시스템 엔진 정상화)
            bool isRunning = (u.mySquad != null) ? u.mySquad.isRunning : u.isRunning;
            float targetSpd = (u.mySquad != null) ? u.mySquad.targetSpeed : (u.isRunning ? u.runSpeed : u.walkSpeed);
            data.moveSpeed = (targetSpd > 0f) ? targetSpd : (isRunning ? 2.8f : 1.2f);
            data.acceleration = isRunning ? 8.0f : 5.0f;
            data.alignToSlope = u.alignToSlope ? 1 : 0;
            data.groundYOffset = (u.groundYOffset > 0.01f) ? u.groundYOffset : 0.39f;
            unitDataArray[i] = data;
            positionArray[i] = u.transform.position;

            // 체력, 상태, 잔여 탄약 및 전우 탄약 융통 동기화
            u.currentHp = data.currentHp;
            u.currentState = (UnitCommandState)data.currentState;

            if (data.currentAmmo < u.currentAmmo)
            {
                int consumedCount = u.currentAmmo - data.currentAmmo;
                for (int c = 0; c < consumedCount; c++)
                {
                    u.ConsumeAmmo();
                }
            }
            data.currentAmmo = u.currentAmmo;
            data.squadHasAmmo = (u.mySquad != null) ? (u.mySquad.HasAmmo ? 1 : 0) : (u.currentAmmo > 0 ? 1 : 0);
            data.isRangedUnit = u.isRangedUnit ? 1 : 0;
            unitDataArray[i] = data;

            if (data.currentHp <= 0 && data.isAlive == 1)
            {
                data.isAlive = 0;
                unitDataArray[i] = data;
                u.TakeDamage(9999f); // 사망 콜백 트리거
            }
        }

        // 3. [Job 1] 유닛 간 소프트 척력(Separation) 병렬 계산
        CalculateSeparationJob separationJob = new CalculateSeparationJob
        {
            positions = positionArray,
            unitData = unitDataArray,
            separationForces = separationForceArray,
            totalCount = unitCount
        };
        JobHandle separationHandle = separationJob.Schedule(unitCount, 64);

        // 3.5 [Job 1.5] 부대 해시맵(SquadMap) 구축 병렬 계산
        squadMap.Clear();
        BuildSquadMapJob buildMapJob = new BuildSquadMapJob
        {
            unitData = unitDataArray,
            squadMap = squadMap.AsParallelWriter()
        };
        JobHandle buildMapHandle = buildMapJob.Schedule(unitCount, 64);

        JobHandle searchDeps = JobHandle.CombineDependencies(separationHandle, buildMapHandle);

        // 4. [Job 2] 최근접 적 탐색(Enemy Targeting) 병렬 계산
        EnemySearchJob searchJob = new EnemySearchJob
        {
            positions = positionArray,
            unitData = unitDataArray,
            squadMap = squadMap,
            targetIndices = targetIndexArray,
            terrainData = (TerrainHeightManager.Instance != null && TerrainHeightManager.Instance.IsDataReady)
                ? TerrainHeightManager.Instance.HeightData
                : default,
            totalCount = unitCount
        };
        JobHandle searchHandle = searchJob.Schedule(unitCount, 64, searchDeps);

        // 5. [Job 3] 이동, 회전, 전투 판정 및 Transform 갱신 병렬 실행
        UnitMovementAndCombatJob moveAndCombatJob = new UnitMovementAndCombatJob
        {
            unitData = unitDataArray,
            separationForces = separationForceArray,
            targetIndices = targetIndexArray,
            positions = positionArray,
            arrowLaunchQueue = arrowLaunchQueue.AsParallelWriter(),
            terrainData = (TerrainHeightManager.Instance != null && TerrainHeightManager.Instance.IsDataReady) 
                ? TerrainHeightManager.Instance.HeightData 
                : default,
            deltaTime = deltaTime,
            currentTime = currentTime,
            totalCount = unitCount
        };
        simulationJobHandle = moveAndCombatJob.Schedule(transformAccessArray, searchHandle);
    }

    private void LateUpdate()
    {
        // LateUpdate 종료 전 이번 프레임의 연산 완료 보장
        simulationJobHandle.Complete();

        // 🏹 멀티스레드 Job에서 인큐된 화살 발사 명령들을 일괄 발사 처리
        if (arrowLaunchQueue.IsCreated)
        {
            while (arrowLaunchQueue.TryDequeue(out ArrowLaunchCommand cmd))
            {
                if (ArrowSimulationManager.Instance != null)
                {
                    ArrowSimulationManager.Instance.LaunchArrow(
                        cmd.isPlayer,
                        cmd.shooterPos,
                        cmd.targetPos,
                        cmd.projectileSpeed,
                        cmd.projectileDrag,
                        cmd.damage,
                        cmd.armorPiercingRatio,
                        cmd.armorShredAmount,
                        cmd.ignoreArmor == 1,
                        (TrajectoryMode)cmd.trajectoryMode,
                        cmd.gravityScale,
                        cmd.spreadRadius,
                        cmd.hasAllyObstruction == 1,
                        cmd.shooterRow,
                        cmd.knockbackPower
                    );
                }
            }
        }

        // 🎯 [Job 완료 직후 안전한 수동 틱]: 화살 비행 및 착탄 처리를 메인 스레드에서 잡 충돌 없이 안전하게 일괄 연산!
        if (ArrowSimulationManager.Instance != null)
        {
            ArrowSimulationManager.Instance.ManualUpdate(Time.deltaTime);
        }
    }

    /// <summary>
    /// 화살이 목표 지면에 꽂혔을 때 착탄 반경 내 적 유닛에게 피해를 적용합니다.
    /// </summary>
    public void ApplyArrowDamageAtPosition(
        Vector3 impactPos,
        int shooterIsPlayer,
        float damage,
        float armorPiercingRatio,
        int armorShredAmount,
        bool ignoreArmor,
        float hitRadius = 1.0f,
        float knockbackPower = 0.35f)
    {
        if (!isNativeArraysAllocated || !unitDataArray.IsCreated) return;

        // 🛡️ [Job 동기화 철통 보호]: 잡이 아직 돌고 있다면 즉시 완료시켜 AtomicSafetyHandle 예외 원천 방지
        if (!simulationJobHandle.IsCompleted)
        {
            simulationJobHandle.Complete();
        }

        float hitRadiusSqr = hitRadius * hitRadius;
        int hitCount = 0;
        int safeLimit = Mathf.Min(registeredUnits.Count, unitDataArray.Length);

        for (int i = 0; i < safeLimit; i++)
        {
            UnitJobData data = unitDataArray[i];
            if (data.isAlive == 0 || data.isPlayer == shooterIsPlayer) continue;

            Vector3 uPos = positionArray[i];
            Vector3 diff = uPos - impactPos;
            diff.y = 0f;

            if (diff.sqrMagnitude <= hitRadiusSqr)
            {
                // 방어력 관통 계산
                float effectiveArmor = (ignoreArmor || data.armor < 0) ? 0f : Mathf.Max(0f, data.armor - armorShredAmount);
                float damageReduction = Mathf.Clamp(effectiveArmor, 0, 10000) / 10000f;

                float apDamage = damage * armorPiercingRatio;
                float normalDamage = damage * (1.0f - armorPiercingRatio);
                float finalDamage = apDamage + Mathf.Max(1.0f, normalDamage * (1.0f - damageReduction));

                data.currentHp -= finalDamage;

                // 유닛 설정에 따른 가변 피격 넉백 (0이면 넉백 없음)
                if (knockbackPower > 0f)
                {
                    Vector3 pushDir = diff.normalized;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector3.forward;
                    data.knockbackVelocity += pushDir * knockbackPower;
                }

                unitDataArray[i] = data;

                hitCount++;
                if (hitCount >= 1) break; // 화살 1발당 1명 타격
            }
        }
    }
}

/// <summary>
/// 유닛 간 겹침 방지 및 적군 방패벽(Solid Shield Wall) 척력을 병렬 계산하는 Job
/// </summary>
[BurstCompile]
public struct CalculateSeparationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<UnitJobData> unitData;
    [WriteOnly] public NativeArray<Vector3> separationForces;
    public int totalCount;

    public void Execute(int index)
    {
        if (unitData[index].isAlive == 0)
        {
            separationForces[index] = Vector3.zero;
            return;
        }

        Vector3 myPos = positions[index];
        int myPlayer = unitData[index].isPlayer;
        Vector3 totalSeparation = Vector3.zero;

        for (int i = 0; i < totalCount; i++)
        {
            if (i == index || unitData[i].isAlive == 0) continue;

            Vector3 diff = myPos - positions[i];
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            bool isEnemy = (unitData[i].isPlayer != myPlayer);

            // 🛡️ 부대 유닛(1.00m 고체 방패벽 볼륨감) vs 자유 유닛(1.15m 비정규 난전):
            float rMy = (unitData[index].isFreeUnit == 1) ? 1.15f : 1.00f;
            float rOther = (unitData[i].isFreeUnit == 1) ? 1.15f : 1.00f;

            // 🦅 [수정]: 그리드 락(교착) 방지를 위해 교전/돌격 중(상태 2, 3)인 아군끼리는 반경을 줄여 빈틈을 비집고 들어갈 수 있게 함
            if (!isEnemy && unitData[index].currentState >= 2 && unitData[i].currentState >= 2)
            {
                rMy = 0.7f;
                rOther = 0.7f;
            }

            float radius = (rMy + rOther) * 0.5f;

            if (distSqr < radius * radius)
            {
                float dist = Mathf.Sqrt(distSqr);
                if (dist < 0.001f)
                {
                    float angle = ((index * 37 + i * 17) % 360) * Mathf.Deg2Rad;
                    diff = new Vector3(Mathf.Cos(angle) * 0.05f, 0f, Mathf.Sin(angle) * 0.05f);
                    dist = 0.05f;
                }
                float weight = (radius - dist) / radius;
                float overlap = radius - dist;
                float penetrationBoost = 1.3f + (overlap * overlap * 25.0f);
                totalSeparation += (diff / dist) * (weight * penetrationBoost);
            }
        }

        // 척력 과도 누적으로 인한 반동 튕김 방지 (최대 5.0으로 클램프하여 밀어내는 힘 대폭 강화)
        if (totalSeparation.sqrMagnitude > 25.0f)
        {
            totalSeparation = totalSeparation.normalized * 5.0f;
        }

        separationForces[index] = totalSeparation;
    }
}

/// <summary>
/// 병렬 시뮬레이션용 SquadMap(부대 ID 기반 해시맵)을 초고속으로 구축하는 Job
/// </summary>
[BurstCompile]
public struct BuildSquadMapJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<UnitJobData> unitData;
    public NativeParallelMultiHashMap<int, int>.ParallelWriter squadMap;

    public void Execute(int index)
    {
        UnitJobData data = unitData[index];
        if (data.isAlive == 1 && data.squadId != -1)
        {
            squadMap.Add(data.squadId, index);
        }
    }
}

/// <summary>
/// 최근접 적을 탐색하는 병렬 Job (Phase 1, 2)
/// </summary>
[BurstCompile]
public struct EnemySearchJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<UnitJobData> unitData;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> squadMap;
    [WriteOnly] public NativeArray<int> targetIndices;
    public TerrainHeightData terrainData;
    public int totalCount;

    public void Execute(int index)
    {
        UnitJobData me = unitData[index];
        if (me.isAlive == 0)
        {
            targetIndices[index] = -1;
            return;
        }

        Vector3 myPos = positions[index];
        int bestTargetIdx = -1;
        bool isFreeUnit = (me.isFreeUnit == 1);
        float minSqrDist = 4000000f; // 최대 2000m

        // 🚨 [0단계: 5m 초근접 적 최우선 긴급 방어 (Emergency Melee Override)]:
        // 적이 내 코앞(meleeSwitchDistance = 5m) 안으로 쇄도해 들어왔다면,
        // 부대 지정 목표(targetSquadId)가 무엇이든 간에 내 목숨을 노리는 코앞의 적을 최우선으로 락온!
        // (이를 통해 난전 중 먼 적을 보며 활을 쏘는 치명적 결함을 100% 원천 차단)
        if (me.isRangedUnit == 1)
        {
            float emergencyDist = me.meleeSwitchDistance;
            float emergencySqr = emergencyDist * emergencyDist;
            int closestThreatIdx = -1;
            float closestThreatSqr = emergencySqr;

            for (int e = 0; e < totalCount; e++)
            {
                if (e == index) continue;
                UnitJobData threat = unitData[e];
                if (threat.isAlive == 1 && threat.isPlayer != me.isPlayer)
                {
                    Vector3 diff = positions[e] - myPos;
                    diff.y = 0f;
                    float sqr = diff.sqrMagnitude;
                    if (sqr < closestThreatSqr)
                    {
                        closestThreatSqr = sqr;
                        closestThreatIdx = e;
                    }
                }
            }

            if (closestThreatIdx != -1)
            {
                targetIndices[index] = closestThreatIdx;
                return;
            }
        }

        // 🎯 [1단계] 부대 지휘관이 지정한 목표 적 부대(targetSquadId) 소속 병사 집중 탐색! (SquadMap O(K) 최적화)
        if (!isFreeUnit && me.targetSquadId != -1)
        {
            // 🏹 [원거리 전용: 가시선(LoS) 확보 적 우선 타겟팅 및 대형 분산]
            if (me.isRangedUnit == 1)
            {
                int visibleCandidateCount = 0;
                int allCandidateCount = 0;
                int bestVisibleIdx = -1;
                int bestFallbackIdx = -1;
                int seed = (index * 7919 + me.col * 31 + me.row * 97) & 0x7FFFFFFF;

                if (squadMap.TryGetFirstValue(me.targetSquadId, out int i, out var it))
                {
                    do
                    {
                        if (i == index) continue;
                        UnitJobData other = unitData[i];
                        if (other.isAlive == 1 && other.isPlayer != me.isPlayer)
                        {
                            Vector3 otherPos = positions[i];
                            allCandidateCount++;
                            if (bestFallbackIdx == -1 || (seed % allCandidateCount == 0))
                            {
                                bestFallbackIdx = i;
                            }

                            // 지형 사선(LoS, 0.2m 눈높이) 검사
                            bool isClear = (terrainData.isValid == 1)
                                ? terrainData.CheckLineOfSight(myPos, otherPos, 0.2f, 0.2f, 4)
                                : true;

                            if (isClear)
                            {
                                visibleCandidateCount++;
                                if (bestVisibleIdx == -1 || (seed % visibleCandidateCount == 0))
                                {
                                    bestVisibleIdx = i;
                                }
                            }
                        }
                    } while (squadMap.TryGetNextValue(out i, ref it));
                }

                // 🔫 직사 유닛(Flat): 반드시 사선이 트인 보이는 적만 조준! 안 보이면 땅을 쏘지 않고 조준 대기
                if (me.trajectoryMode == 1)
                {
                    targetIndices[index] = bestVisibleIdx;
                    return;
                }
                else
                {
                    // 🏹 곡사 유닛(HighArc): 보이는 적 우선, 없으면 고각 곡사 타겟
                    targetIndices[index] = bestVisibleIdx != -1 ? bestVisibleIdx : bestFallbackIdx;
                    return;
                }
            }
            else
            {
                // ⚔️ [근접 보병]: 가장 가까운 적을 향해 대형 유지 돌격
                if (squadMap.TryGetFirstValue(me.targetSquadId, out int i, out var it))
                {
                    do
                    {
                        if (i == index) continue;
                        UnitJobData other = unitData[i];
                        if (other.isAlive == 1 && other.isPlayer != me.isPlayer)
                        {
                            Vector3 diff = positions[i] - myPos;
                            diff.y = 0f;
                            float sqrDist = diff.sqrMagnitude;

                            if (sqrDist < minSqrDist)
                            {
                                minSqrDist = sqrDist;
                                bestTargetIdx = i;
                            }
                        }
                    } while (squadMap.TryGetNextValue(out i, ref it));
                }

                if (bestTargetIdx != -1)
                {
                    targetIndices[index] = bestTargetIdx;
                    return;
                }
            }
            // SquadMap에서 못 찾음 (목표 부대원이 12m 밖으로 이동 또는 아직 안 들어옴)
            // -> 거리 무제한 전역 fallback: 목표 부대 소속인 적 분산 탐색
            if (me.isRangedUnit == 1)
            {
                int candidateCount = 0;
                int seed = (index * 7919 + me.col * 31 + me.row * 97) & 0x7FFFFFFF;
                for (int j = 0; j < totalCount; j++)
                {
                    if (j == index) continue;
                    UnitJobData other = unitData[j];
                    if (other.isAlive == 1 && other.isPlayer != me.isPlayer && other.squadId == me.targetSquadId)
                    {
                        candidateCount++;
                        if (bestTargetIdx == -1 || (seed % candidateCount == 0))
                        {
                            bestTargetIdx = j;
                        }
                    }
                }
            }
            else
            {
                for (int j = 0; j < totalCount; j++)
                {
                    if (j == index) continue;
                    UnitJobData other = unitData[j];
                    if (other.isAlive == 1 && other.isPlayer != me.isPlayer && other.squadId == me.targetSquadId)
                    {
                        Vector3 diff = positions[j] - myPos;
                        diff.y = 0f;
                        float sqrDist = diff.sqrMagnitude;
                        if (sqrDist < minSqrDist) { minSqrDist = sqrDist; bestTargetIdx = j; }
                    }
                }
            }
            targetIndices[index] = bestTargetIdx;
            return;
        }

        // 🎯 [2단계] 목표 부대 미지정 시: 일반 보병 12m 근접 레이더, 궁병은 원거리 사거리(150m) 레이더 탐색
        if (me.targetSquadId == -1)
        {
            float maxRadar = (me.isRangedUnit == 1) ? me.rangedAttackRange : 12.0f;
            float localMinDistSqr = maxRadar * maxRadar;

            // 🏹 [궁병 자유 사격 분산]: 사거리 내에 가장 가까운 적 부대(또는 적들)에게 저수지 샘플링으로 골고루 분산 사격!
            if (me.isRangedUnit == 1)
            {
                int primaryTargetSquadId = -1;
                float closestDistSqr = localMinDistSqr;

                // 1) 전방 시야각 내 가장 가까운 적 식별 및 해당 적의 부대 ID 파악
                for (int i = 0; i < totalCount; i++)
                {
                    if (i == index) continue;
                    UnitJobData other = unitData[i];
                    if (other.isAlive == 1 && other.isPlayer != me.isPlayer)
                    {
                        Vector3 diff = positions[i] - myPos;
                        diff.y = 0f;
                        float sqrDist = diff.sqrMagnitude;

                        if (me.isFreeUnit == 0 && sqrDist > 0.001f)
                        {
                            Vector3 myFwd = me.targetRotation * Vector3.forward;
                            float forwardDot = Vector3.Dot(myFwd, diff.normalized);
                            if (forwardDot < 0.15f) continue;
                        }

                        if (sqrDist < closestDistSqr)
                        {
                            closestDistSqr = sqrDist;
                            primaryTargetSquadId = other.squadId;
                            bestTargetIdx = i;
                        }
                    }
                }

                // 2) 만약 주 타겟 적 부대(squadId != -1)가 식별되었다면, 해당 부대 소속 병사들 중에서 고유 시드로 저수지 샘플링 분산!
                if (primaryTargetSquadId != -1)
                {
                    int candidateCount = 0;
                    int seed = (index * 7919 + me.col * 31 + me.row * 97) & 0x7FFFFFFF;
                    if (squadMap.TryGetFirstValue(primaryTargetSquadId, out int k, out var iter))
                    {
                        do
                        {
                            if (k == index) continue;
                            UnitJobData targetMember = unitData[k];
                            if (targetMember.isAlive == 1 && targetMember.isPlayer != me.isPlayer)
                            {
                                Vector3 diff = positions[k] - myPos;
                                diff.y = 0f;
                                if (diff.sqrMagnitude <= localMinDistSqr)
                                {
                                    candidateCount++;
                                    if (candidateCount == 1 || (seed % candidateCount == 0))
                                    {
                                        bestTargetIdx = k;
                                    }
                                }
                            }
                        } while (squadMap.TryGetNextValue(out k, ref iter));
                    }
                }
            }
            else
            {
                // ⚔️ [일반 근접 보병]: 12m 내 가장 가까운 적 1명에게 접근
                for (int i = 0; i < totalCount; i++)
                {
                    if (i == index) continue;
                    UnitJobData other = unitData[i];
                    if (other.isAlive == 1 && other.isPlayer != me.isPlayer)
                    {
                        Vector3 diff = positions[i] - myPos;
                        diff.y = 0f;
                        float sqrDist = diff.sqrMagnitude;

                        // 🛡️ [측면 옆 부대 오탐색 차단]: 시선 정면(forwardDot > 0.25f)의 적만 탐색
                        if (me.isFreeUnit == 0 && sqrDist > 0.001f)
                        {
                            Vector3 myFwd = me.targetRotation * Vector3.forward;
                            float forwardDot = Vector3.Dot(myFwd, diff.normalized);
                            if (forwardDot < 0.25f) continue;
                        }

                        if (sqrDist < localMinDistSqr)
                        {
                            localMinDistSqr = sqrDist;
                            bestTargetIdx = i;
                        }
                    }
                }
            }
        }

        targetIndices[index] = bestTargetIdx;
    }
}

/// <summary>
/// 유닛 이동, 회전, 전투 판정 및 Transform 위치를 멀티스레드로 일괄 갱신하는 Job
/// </summary>
[BurstCompile]
public struct UnitMovementAndCombatJob : IJobParallelForTransform
{
    [NativeDisableParallelForRestriction] public NativeArray<UnitJobData> unitData;
    [ReadOnly] public NativeArray<Vector3> separationForces;
    [ReadOnly] public NativeArray<int> targetIndices;
    [ReadOnly] public NativeArray<Vector3> positions;
    [WriteOnly] public NativeQueue<ArrowLaunchCommand>.ParallelWriter arrowLaunchQueue;
    [ReadOnly] public TerrainHeightData terrainData;
    public float deltaTime;
    public float currentTime;
    public int totalCount;

    public void Execute(int index, TransformAccess transform)
    {
        UnitJobData data = unitData[index];
        if (data.isAlive == 0) return;

        // 💫 넘어짐(무력화) 상태 타이머 업데이트 (타겟 유무와 무관하게 유닛 전체 적용)
        bool isKnockedDown = (data.knockdownTimer > 0f);
        if (isKnockedDown)
        {
            data.knockdownTimer = Mathf.Max(0f, data.knockdownTimer - deltaTime);
        }

        Vector3 currentPos = transform.position;
        Vector3 targetDest = data.targetPosition;
        int targetIdx = targetIndices[index];

        bool isCharging = false;
        bool isCombatRunning = false;
        bool isRangedShooting = false;
        float distToEnemy = 999f;

        // 1. 전투 및 타겟팅 판정 (외곽 접촉 기반 1:1 토탈워식 방진 전투 + 방어진 제자리 사수 반격)
        if (targetIdx >= 0 && targetIdx < totalCount)
        {
            UnitJobData enemyData = unitData[targetIdx];
            if (enemyData.isAlive == 1)
            {
                Vector3 enemyPos = positions[targetIdx];
                distToEnemy = Vector3.Distance(currentPos, enemyPos);

                // 🏃‍♂️ [A. 강제 단순 이동/후퇴 상태 (currentState == 1)]:
                // 유저가 도망/이동 명령을 내렸을 때는 적이 몇 m에 있든 무조건 100% targetPosition으로 질주!
                // 1.45m 내에 적이 있으면 지나가면서 반격만 하고, 목적지는 절대 덮어씌우지 않음!
                if (data.currentState == 1)
                {
                    targetDest = data.targetPosition;
                    isCharging = false;
                    isCombatRunning = false;

                    // ⚔️ 지나가면서 사거리 내 적은 즉시 반격!
                    if (distToEnemy <= data.attackRange && currentTime >= data.lastAttackTime + data.attackCooldown)
                    {
                        data.lastAttackTime = currentTime;
                        float finalDamage = data.damage;
                        Vector3 pushDir = (enemyPos - currentPos).normalized;
                        if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.rotation * Vector3.forward;

                        float massRatio = data.mass / Mathf.Max(10f, enemyData.mass);
                        float microKnockbackSpeed = 0.4f * massRatio;
                        Vector3 combinedKnockback = enemyData.knockbackVelocity + (pushDir * microKnockbackSpeed);
                        if (combinedKnockback.sqrMagnitude > 1.2f * 1.2f)
                        {
                            combinedKnockback = combinedKnockback.normalized * 1.2f;
                        }
                        enemyData.knockbackVelocity = combinedKnockback;

                        float damageReduction = Mathf.Clamp(enemyData.armor, 0, 10000) / 10000f;
                        float effectiveDamage = (enemyData.armor >= 10000) ? 0f : Mathf.Max(1.0f, finalDamage * (1.0f - damageReduction));
                        enemyData.currentHp -= effectiveDamage;
                        unitData[targetIdx] = enemyData;
                    }
                }
                // 🛡️ [B. V 비활성화: 접촉 방어 모드]
                else if (data.autoAttackEnabled == 0)
                {
                    bool isInMeleeContact = (distToEnemy <= data.attackRange);
                    if (isInMeleeContact)
                    {
                        data.currentState = 3; // MeleeEngaged
                        Vector3 toEnemy = (enemyPos - currentPos).normalized;
                        targetDest = enemyPos - (toEnemy * data.engagementOffset);
                        isCharging = false;
                    }
                    else
                    {
                        if (data.currentState == 3) data.currentState = 0; // Hold (제자리 대기)
                        targetDest = data.targetPosition;
                        isCharging = false;
                    }
                }
                // ⚔️ [C. 공격 이동 및 적군 AI (currentState == 2, 3 또는 isPlayer == 0)]:
                else
                {
                    bool isAttacking = (data.currentState == 2 || data.currentState == 3 || data.isPlayer == 0);

                    // 1. 자유 유닛: 적을 향해 직접 추격
                    if (data.isFreeUnit == 1)
                    {
                        Vector3 toEnemy = (enemyPos - currentPos).normalized;
                        targetDest = enemyPos - (toEnemy * Mathf.Max(0.70f, data.engagementOffset));

                        if (distToEnemy <= 12.0f)
                        {
                            isCharging = true;
                            isCombatRunning = false;
                        }
                        else if (distToEnemy <= 30.0f || isAttacking)
                        {
                            isCharging = false;
                            isCombatRunning = true;
                        }
                        else
                        {
                            isCharging = false;
                            isCombatRunning = false;
                        }
                    }
                    // 2. 부대 소속 유닛: 공격 상태 시 목표 적 부대원들을 기억하여 자유롭게 끝까지 추격 섬멸!
                    else
                    {
                        // 🏹 원거리 유닛(isRangedUnit == 1)은 적이 백병전 거리(5m) 밖이고 탄약이 남아있으면 무모하게 돌격하지 않고 대형 위치 유지!
                        if (data.isRangedUnit == 1 && distToEnemy > data.meleeSwitchDistance && (data.currentAmmo > 0 || data.squadHasAmmo == 1))
                        {
                            targetDest = data.targetPosition;
                            isCharging = false;
                            isCombatRunning = false;
                        }
                        else if (isAttacking)
                        {
                            // 🦅 유저 요청 완벽 반영: 전투 시 억지로 대형(targetPosition)을 유지하려 들지 않고, 
                            // 완벽히 대형을 풀고 각자 가장 가까운 목표 부대원(enemyPos)에게 돌격!
                            Vector3 toEnemy = (enemyPos - currentPos).normalized;
                            targetDest = enemyPos - (toEnemy * data.engagementOffset);

                            // 💥 공격 명령 시 전속력 돌격/추격
                            isCharging = true;
                            isCombatRunning = false;
                        }
                        else
                        {
                            // 가만히 대기 중이어도 적이 12m 내로 접근하면 자동 공격 활성화 시 맞돌격!
                            if (distToEnemy <= 12.0f && data.autoAttackEnabled == 1)
                            {
                                Vector3 toEnemy = (enemyPos - currentPos).normalized;
                                targetDest = enemyPos - (toEnemy * data.engagementOffset);
                                isCharging = true;
                                isCombatRunning = false;
                            }
                            else
                            {
                                targetDest = data.targetPosition;
                                isCharging = false;
                                isCombatRunning = false;
                            }
                        }
                    }
                }

                // 💥 [돌격 충격 장전 판정]: 달리기/돌격 가속으로 전진 중이면 충격량 장전
                if (data.currentSpeed >= data.moveSpeed * 1.1f || isCharging)
                {
                    data.chargeImpactReady = 1;
                }

                // 🏹 [1단계: 원거리 무기 - 활 사격 판정]
                isRangedShooting = false;
                if (data.isRangedUnit == 1)
                {
                    // 🚨 백병전 교전 중(currentState == 3)이거나 5m 이내에 적이 들어왔으면 사격 100% 절대 금지!
                    bool isEngagedInMelee = (data.currentState == 3);
                    bool isInsideMelee = (distToEnemy <= data.meleeSwitchDistance);
                    bool hasAmmo = (data.currentAmmo > 0 || data.squadHasAmmo == 1);

                    // 🏹 [자유 사격(Fire at Will) 검사]:
                    // fireAtWill이 꺼져(0) 있으면, 지휘관이 명시적으로 적 부대를 지정(targetSquadId != -1)하지 않은 한 자동 사격을 하지 않고 화살 절약!
                    bool canShootFireAtWill = (data.fireAtWill == 1) || (data.targetSquadId != -1);

                    // 🚀 [이동 중 사격 판정 (자유사격 이동 데드락 원천 방지)]:
                    // 1) 이동 중 사격 가능 유닛(canFireWhileMoving == 1)은 이동 중에도 사격 허용
                    // 2) 이동 중 사격 불가(canFireWhileMoving == 0) 유닛:
                    //    - 플레이어의 이동(currentState == 1) 명령 수행 중이고 아직 목적지에 도착하지 못했다면 사격 금지(이동 우선!)
                    //    - 목적지에 도착하여 멈춰 섰을 때(currentSpeed <= 0.25f)만 사격 허용!
                    Vector3 toDestVec = targetDest - currentPos;
                    toDestVec.y = 0f;
                    float distToDest = toDestVec.magnitude;
                    bool isMovingUnderOrder = (data.currentState == 1) && (distToDest > data.stoppingDistance);
                    bool canShootMovement = (data.canFireWhileMoving == 1) || (!isMovingUnderOrder && data.currentSpeed <= 0.25f);

                    // 📐 [3축 공간 및 사선 클리어런스 검사 (Clearance Check)]
                    // 1) 후방 공간 검사: 최후열이거나 최소 간격이 0(견착 무기: 쇠뇌/총)이거나 대형 세로 간격이 충분할 때 통과
                    bool isBackRow = (data.row >= data.totalRows - 1);
                    bool rearClear = isBackRow || (data.minRearSpacing <= 0.01f) || (data.squadSpacingZ >= data.minRearSpacing);

                    // 2) 측면 공간 검사: 대형 가로 간격 확보 여부
                    bool lateralClear = (data.minLateralSpacing <= 0.01f) || (data.squadSpacingX >= data.minLateralSpacing);

                    // 3) 전방 사선 및 언덕 경사면 클리어런스 (Front LoS & Hill Clearance)
                    bool frontClear = false;
                    if (data.row == 0)
                    {
                        // 1열은 앞에 아군이 없으므로 항상 통과
                        frontClear = true;
                    }
                    else if (data.row == 1 && data.isStaggeredFormation == 1 && data.allowStaggeredRank2DirectFire == 1)
                    {
                        // 체커보드(엇갈림) 2열은 전방 1열 사이 틈새로 직사 허용
                        frontClear = true;
                    }
                    else
                    {
                        // 3열 이상 또는 직렬 2열: 기하학적 높이차(언덕 경사면) + 탄도 발사각 앙각 판정
                        float distToFront = data.row * Mathf.Max(0.5f, data.squadSpacingZ);
                        float enemyDistSafe = Mathf.Max(1.0f, distToEnemy);

                        // 사수가 적보다 높은 언덕에 위치할 때의 지면 고도 우위
                        float slopeDy = (currentPos.y - enemyPos.y) * (distToFront / enemyDistSafe);

                        // 탄도 앙각에 따른 전열 머리 위 고도 상승치
                        float baseAngle;
                        if (data.trajectoryMode == 0) // HighArc (곡사: 활)
                        {
                            float angleRatio = Mathf.Clamp01(distToEnemy / Mathf.Max(1.0f, data.rangedAttackRange));
                            baseAngle = Mathf.Lerp(0.26f, 0.61f, angleRatio); // 15° ~ 35°
                        }
                        else // Flat (직사: 총/쇠뇌)
                        {
                            baseAngle = 0.035f; // 약 2° 조준각
                        }
                        float elevationDy = distToFront * Mathf.Tan(baseAngle);
                        float totalClearance = slopeDy + elevationDy;

                        frontClear = (totalClearance >= data.headClearanceMargin);
                    }

                    bool isClearanceSatisfied = rearClear && lateralClear && frontClear;

                    // 🏔️ [지형 사선(Line of Sight, LoS) 차폐 검사 및 직사/곡사 사격 분기 (유닛 체급 0.2m 눈높이 정밀화)]
                    bool isLosClear = (terrainData.isValid == 1)
                        ? terrainData.CheckLineOfSight(currentPos, enemyPos, 0.2f, 0.15f, 4)
                        : true;

                    bool canShootTerrainLoS;
                    int forceHighArc = 0;

                    if (data.trajectoryMode == 1) // Flat (직사: 총/쇠뇌)
                    {
                        // 🔫 [직사 무기 규칙]: 실제 시야에 적이 보일 때만 사격 허용! (강제 곡사 완전 금지)
                        canShootTerrainLoS = isLosClear;
                        forceHighArc = 0;
                    }
                    else // HighArc (곡사: 활/투석기)
                    {
                        // 🏹 [곡사 무기 규칙]: 시야가 트여 있거나, 지정 목표라면 언덕 너머 고각 곡사 허용
                        if (isLosClear)
                        {
                            canShootTerrainLoS = true;
                        }
                        else
                        {
                            canShootTerrainLoS = (data.targetSquadId != -1);
                            if (canShootTerrainLoS)
                            {
                                forceHighArc = 1;
                            }
                        }
                    }

                    if (!isEngagedInMelee && !isInsideMelee && hasAmmo && canShootFireAtWill && canShootMovement && isClearanceSatisfied && canShootTerrainLoS && distToEnemy <= data.rangedAttackRange && distToEnemy >= data.rangedMinRange)
                    {
                        isRangedShooting = true;

                        // ⏱️ [순차 사격(Rolling Volley) 행간 지연 시간 계산]
                        float rowTimingOffset = (data.enableSequentialFire == 1) ? (data.row * data.sequentialRowDelay) : 0f;

                        // 적을 향해 조준 및 쿨다운 경과 시 화살 발사
                        if (currentTime >= data.lastAttackTime + data.rangedAttackCooldown + rowTimingOffset)
                        {
                            // 🏹 [인체 역학적 무기 발사 위치 및 조준 앙각 사전 산출]
                            Vector3 toEnemy = enemyPos - currentPos;
                            float horizD = Mathf.Sqrt(toEnemy.x * toEnemy.x + toEnemy.z * toEnemy.z);
                            Vector3 fwdDir = horizD > 0.001f ? new Vector3(toEnemy.x / horizD, 0f, toEnemy.z / horizD) : Vector3.forward;
                            Vector3 rightDir = new Vector3(fwdDir.z, 0f, -fwdDir.x);

                            float targetH = enemyPos.y + Mathf.Max(0.15f, data.launchOffset.y);
                            float shoulderH = currentPos.y + data.launchOffset.y;
                            float aimPitch = Mathf.Atan2(targetH - shoulderH, Mathf.Max(0.1f, horizD));

                            float finalLaunchY = shoulderH + Mathf.Sin(aimPitch) * data.launchOffset.z;
                            float reachDist = Mathf.Cos(aimPitch) * data.launchOffset.z;
                            if (aimPitch < 0f) reachDist += (data.launchOffset.z * 0.25f) * (-Mathf.Sin(aimPitch));

                            // ⛰️ [발사 전 주변 지형 사전 검사 (Pre-Shot Terrain Clearance)]:
                            // 내 바로 앞 0.8~2.4m의 오르막 경사면이나 언덕 턱에 탄도가 걸리는지 검사하여 앞땅 낭비 방지!
                            bool isTerrainClearAhead = true;
                            if (terrainData.isValid == 1)
                            {
                                float upwardRatio = Mathf.Sin(aimPitch);
                                for (float checkD = 0.8f; checkD <= 2.4f; checkD += 0.8f)
                                {
                                    float checkX = currentPos.x + fwdDir.x * checkD;
                                    float checkZ = currentPos.z + fwdDir.z * checkD;
                                    float groundH = terrainData.SampleHeight(checkX, checkZ);
                                    float expectedH = finalLaunchY + (upwardRatio * checkD);
                                    if (expectedH <= groundH + 0.05f)
                                    {
                                        isTerrainClearAhead = false;
                                        break;
                                    }
                                }
                            }

                            // 앞땅에 부딪힐 각도가 아닐 때만 정상 발사 및 탄약 차감!
                            if (isTerrainClearAhead)
                            {
                                data.lastAttackTime = currentTime - rowTimingOffset;
                                data.currentAmmo--;

                                float distRatio = Mathf.Clamp01((distToEnemy - data.optimalRange) / Mathf.Max(1.0f, data.rangedAttackRange - data.optimalRange));
                                float finalDmg = data.rangedBaseDamage * (1.0f - (1.0f - data.minDamageRatioAtMax) * distRatio);
                                float spread = Mathf.Lerp(data.minSpreadRadius, data.maxSpreadRadius, distRatio);

                                // 직사 무기(Flat)는 절대로 강제 곡사를 타지 않음!
                                int hasAlly = (data.trajectoryMode == 0 && (enemyData.currentState == 3 || forceHighArc == 1)) ? 1 : 0;

                                Vector3 muzzlePos = new Vector3(currentPos.x, finalLaunchY, currentPos.z)
                                                  + (fwdDir * reachDist)
                                                  + (rightDir * data.launchOffset.x);

                                arrowLaunchQueue.Enqueue(new ArrowLaunchCommand
                                {
                                    isPlayer = data.isPlayer,
                                    shooterPos = muzzlePos,
                                    targetPos = new Vector3(enemyPos.x, targetH, enemyPos.z),
                                    projectileSpeed = data.projectileSpeed,
                                    projectileDrag = data.projectileDrag,
                                    damage = finalDmg,
                                    armorPiercingRatio = data.armorPiercingRatio,
                                    armorShredAmount = data.armorShredAmount,
                                    ignoreArmor = data.ignoreArmor,
                                    trajectoryMode = data.trajectoryMode,
                                    gravityScale = data.gravityScale,
                                    spreadRadius = spread,
                                    hasAllyObstruction = hasAlly,
                                    shooterRow = data.row,
                                    knockbackPower = data.rangedKnockbackPower
                                });
                            }
                        }
                    }
                }

                // [A] 직접 칼/창이 닿는 유효 타격 사거리 판정
                bool isSidearmActive = (data.useSidearm == 1 && distToEnemy <= data.sidearmSwitchDistance);
                float effectiveMeleeRange = isSidearmActive ? data.sidearmAttackRange : data.attackRange;

                bool isInMelee = (distToEnemy <= effectiveMeleeRange);
                if (isInMelee && !isRangedShooting)
                {
                    data.currentState = 3; // MeleeEngaged
                }
                else if (data.currentState == 3 && distToEnemy > data.meleeSwitchDistance)
                {
                    // 🛡️ 적이 백병전 거리(5m) 밖으로 완전히 달아났을 때만 백병전 해제
                    data.currentState = (data.autoAttackEnabled == 0) ? 0 : 2; // Hold(Idle) vs AttackMove
                }

                // ⚔️ [2단계 & 3단계: 백병전 타격 판정]: 원거리 사격 중이 아니고, 무력화 상태가 아니며 근접 교전 중일 때 칼/단검 공격!
                if (!isRangedShooting && !isKnockedDown && (isInMelee || data.currentState == 3))
                {
                    if (isSidearmActive)
                    {
                        // 🗡️ [보조무기 공격]: 품 안으로 파고든 적에게 보조무기 타격
                        if (distToEnemy <= data.sidearmAttackRange && currentTime >= data.lastAttackTime + data.sidearmAttackCooldown)
                        {
                            data.lastAttackTime = currentTime;

                            float finalDamage = data.sidearmDamage;
                            Vector3 pushDir = (enemyPos - currentPos).normalized;
                            if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.rotation * Vector3.forward;

                            float massRatio = data.mass / Mathf.Max(10f, enemyData.mass);
                            float baseSidearmKnock = (data.sidearmBaseKnockback > 0f) ? data.sidearmBaseKnockback : 0.2f;
                            float microKnockbackSpeed = baseSidearmKnock * massRatio * data.sidearmKnockbackPower;
                            Vector3 combinedKnockback = enemyData.knockbackVelocity + (pushDir * microKnockbackSpeed);
                            float maxCap = (data.sidearmMaxKnockbackCap > 0f) ? data.sidearmMaxKnockbackCap : 0.8f;
                            if (combinedKnockback.sqrMagnitude > maxCap * maxCap)
                            {
                                combinedKnockback = combinedKnockback.normalized * maxCap;
                            }
                            enemyData.knockbackVelocity = combinedKnockback;

                            float damageReduction = Mathf.Clamp(enemyData.armor, 0, 10000) / 10000f;
                            float effectiveDamage = (enemyData.armor >= 10000) ? 0f : Mathf.Max(1.0f, finalDamage * (1.0f - damageReduction));
                            enemyData.currentHp -= effectiveDamage;
                            unitData[targetIdx] = enemyData;
                        }
                    }
                    else
                    {
                        // ⚔️ [주무기 공격]: 최소 사거리 검사 및 스위트스팟 거리별 감쇠 계산
                        bool isInsideMinRange = (data.minAttackRange > 0.05f && distToEnemy < data.minAttackRange);
                        if (distToEnemy <= data.attackRange && currentTime >= data.lastAttackTime + data.attackCooldown)
                        {
                            data.lastAttackTime = currentTime;

                            float finalDamage = data.damage;

                            // 🎯 최소 사거리 미만(초밀착)이거나 최적 사거리 미만 시 데미지 감쇠 적용
                            if (isInsideMinRange || (data.optimalRangeMin > 0.05f && distToEnemy < data.optimalRangeMin))
                            {
                                finalDamage *= data.closeRangeDamageRatio;
                            }

                            Vector3 pushDir = (enemyPos - currentPos).normalized;
                            if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.rotation * Vector3.forward;

                            float massRatio = data.mass / Mathf.Max(10f, enemyData.mass);

                            // 🛡️ [창벽 저지 및 돌격 반사 피해 (Charge Reflection)]:
                            // 유닛이 돌격 반사 능력(canReflectCharge == 1)을 보유하고 있고,
                            // 제자리에 버티는 상태(접촉방어태세 autoAttackEnabled == 0 또는 정지 상태)에서 적이 돌격해올 경우 발동
                            bool isBracing = (data.autoAttackEnabled == 0 || data.currentSpeed < 1.0f);
                            bool isEnemyCharging = (enemyData.currentSpeed > 2.0f || enemyData.chargeImpactReady == 1);
                            if (data.canReflectCharge == 1 && isBracing && isEnemyCharging)
                            {
                                float enemySpeedScale = enemyData.chargeSpeed / 4.8f;
                                float reflectedChargeDamage = ((enemyData.chargeBonus > 0f) ? enemyData.chargeBonus : 15f) * enemySpeedScale;
                                finalDamage += reflectedChargeDamage;
                                enemyData.chargeImpactReady = 0; // 적의 돌격 충격 분쇄
                            }

                            // 💥 [돌격 쇄도 속도 비례 돌격 피해]:
                            // 돌격 속도가 빠를수록(예: 기병) 더 큰 충돌 피해를 입힘
                            // 공식: 돌격 추가 피해 = 돌격 보너스 × (돌격 쇄도 속도 ÷ 4.8m/s)
                            if (data.chargeImpactReady == 1 && data.currentSpeed > 2.0f)
                            {
                                float speedScale = data.chargeSpeed / 4.8f;
                                finalDamage += data.chargeBonus * speedScale;
                                data.chargeImpactReady = 0;
                            }

                            // 최대 돌격 충돌 피해 상한 적용
                            if (data.maxChargeDamage > 0f)
                            {
                                finalDamage = Mathf.Min(finalDamage, data.maxChargeDamage);
                            }

                            // 💨 [현실적인 물리 넉백 & 넘어짐(무력화) 판정]:
                            // 일반 평타(창 찌르기)는 적을 튕겨내지 않고 인스펙터 설정 미세 저지(baseMeleeKnockback, 기본 0.45m/s) 부여.
                            // 오직 전력 돌격 충돌(기병/보병 돌격 들이받기) 시에만 큰 넉백(1.8m/s) 부여.
                            bool isChargeHit = (data.chargeImpactReady == 1 && data.currentSpeed > 2.0f);
                            float baseKnockback = isChargeHit ? 1.8f : data.baseMeleeKnockback;
                            float knockbackSpeed = Mathf.Clamp(baseKnockback * massRatio, 0.2f, 2.0f) * data.knockbackPower;

                            // 🚨 [다대일 집중 공격 시 넉백 폭증 원천 차단]:
                            // 창병 여러 명이 동시에 한 명을 찔러도 넉백 속도가 무한 누적되지 않도록 인스펙터 상한선(maxMeleeKnockbackCap) 엄격 제한
                            Vector3 combinedKnockback = enemyData.knockbackVelocity + (pushDir * knockbackSpeed);
                            float maxAllowedKnockback = isChargeHit ? 2.2f : data.maxMeleeKnockbackCap;
                            if (combinedKnockback.sqrMagnitude > maxAllowedKnockback * maxAllowedKnockback)
                            {
                                combinedKnockback = combinedKnockback.normalized * maxAllowedKnockback;
                            }
                            enemyData.knockbackVelocity = combinedKnockback;

                            // 💫 넘어짐(Knockdown)은 일반 평타가 아닌 '돌격 충돌 피격'이면서 넘어짐 면역이 아닐 때만 발동!
                            if (isChargeHit && enemyData.isImmuneToKnockdown == 0 && knockbackSpeed >= enemyData.knockdownThreshold)
                            {
                                enemyData.knockdownTimer = enemyData.knockdownDuration; // 인스펙터 설정 시간 (기본 3.0초)
                            }

                            float damageReduction = Mathf.Clamp(enemyData.armor, 0, 10000) / 10000f;
                            float effectiveDamage = (enemyData.armor >= 10000) ? 0f : Mathf.Max(1.0f, finalDamage * (1.0f - damageReduction));
                            enemyData.currentHp -= effectiveDamage;
                            unitData[targetIdx] = enemyData;
                        }
                    }
                }
            }
            else
            {
                if (data.currentState == 3)
                {
                    data.currentState = 2; // AttackMove
                }
                Vector3 currentForward = data.targetRotation * Vector3.forward;
                targetDest = currentPos + (currentForward.sqrMagnitude > 0.001f ? currentForward : Vector3.forward) * 2.0f;
                isCharging = false;
                isCombatRunning = true;
            }
        }
        else
        {
            if (data.currentState == 3)
            {
                data.currentState = 2;
            }
            // AttackMove 중에 타겟이 없으면 보병은 계속 전진, 궁병은 맹목적 돌격 방지(대형 슬롯 유지)
            if (data.currentState == 2)
            {
                if (data.isRangedUnit == 1)
                {
                    targetDest = data.targetPosition;
                    isCombatRunning = false;
                }
                else
                {
                    Vector3 currentForward = data.targetRotation * Vector3.forward;
                    targetDest = currentPos + (currentForward.sqrMagnitude > 0.001f ? currentForward : Vector3.forward) * 2.0f;
                    isCombatRunning = true;
                }
            }
            else
            {
                targetDest = data.targetPosition;
                isCharging = false;
            }
        }
        // 2. 이동 벡터 및 척력 결합
        Vector3 moveDir = targetDest - currentPos;
        moveDir.y = 0f;
        float remainingDist = moveDir.magnitude;

        Vector3 desiredMove = Vector3.zero;
        Vector3 desiredDir = Vector3.zero;

        if (remainingDist > data.stoppingDistance)
        {
            desiredDir = moveDir.normalized;

            // 진행 방향과 현재 바라보는 방향 사이의 각도 계산
            Vector3 currentForward = transform.rotation * Vector3.forward;
            currentForward.y = 0f;
            float angleToTarget = (currentForward.sqrMagnitude > 0.001f && desiredDir.sqrMagnitude > 0.001f)
                ? Vector3.Angle(currentForward, desiredDir)
                : 0f;

            // 선회 감속 완화 (급선회 시에도 65% 이상 속도 유지하여 민첩한 방향 전환)
            float turnFactor = 1.0f;
            if (angleToTarget > 60f)
            {
                turnFactor = Mathf.Lerp(0.65f, 1.0f, Mathf.Clamp01((180f - angleToTarget) / 120f));
            }

            float combatRunSpd = (data.isFreeUnit == 1) ? 2.8f : 2.8f;
            float baseSpeed = isCharging ? data.chargeSpeed : (isCombatRunning ? Mathf.Max(combatRunSpd, data.moveSpeed) : data.moveSpeed);
            float maxDesiredSpeed = baseSpeed * turnFactor;

            // 🎯 [슬롯/목적지 도착 감속 (Arrival Deceleration)]:
            // 대형 복귀 또는 단순 이동 시 목적지 2.0m 이내로 접근하면 서서히 감속하여 부드럽게 슬롯에 안착 (튕김 및 급제동 방지)
            if (data.currentState == 1 || (data.currentState == 0 && data.squadId != -1))
            {
                if (remainingDist < 2.0f)
                {
                    float arrivalFactor = Mathf.Clamp01(remainingDist / 2.0f);
                    maxDesiredSpeed *= Mathf.Lerp(0.25f, 1.0f, arrivalFactor);
                }
            }

            // 🗡️ 백병전 중(정지 거리 combatStoppingDistance 이내)에는 발을 딛고 칼싸움 (미끄러짐 및 관통 방지)
            if (distToEnemy <= data.combatStoppingDistance && data.currentState == 3)
            {
                maxDesiredSpeed = 0f;
            }

            // 🏹 [궁병 제자리 사격 태세]: 이동 사격 불허 유닛은 활을 쏘는 동안(isRangedShooting) 발을 100% 멈춤 (돌격 방지)
            if (isRangedShooting && data.canFireWhileMoving == 0)
            {
                maxDesiredSpeed = 0f;
            }

            // 💫 넘어짐 무력화 중에는 이동 불가 (제자리에 쓰러짐)
            if (isKnockedDown)
            {
                maxDesiredSpeed = 0f;
            }

            // 전방 척력 감속 완화 (밀집 대형에서도 최소 65% 속도 유지하여 시원하게 전진)
            Vector3 curSepForce = separationForces[index];
            if (curSepForce.sqrMagnitude > 0.001f)
            {
                float backwardRepulsion = Vector3.Dot(desiredDir, -curSepForce);
                if (backwardRepulsion > 0.05f)
                {
                    float brakeFactor = Mathf.Clamp(1.0f - backwardRepulsion * 0.35f, 0.65f, 1.0f);
                    maxDesiredSpeed *= brakeFactor;
                }
            }

            // 가속도 상향 (기본 5.0m/s², 돌격 8.0m/s²로 민첩하고 시원한 이동감 보장)
            float baseAccel = isCharging ? 8.0f : ((data.acceleration > 0f) ? Mathf.Max(data.acceleration, 5.0f) : 5.0f);
            data.currentSpeed = Mathf.MoveTowards(data.currentSpeed, maxDesiredSpeed, baseAccel * deltaTime);

            desiredMove = desiredDir * (data.currentSpeed * deltaTime);
        }
        else
        {
            data.currentSpeed = Mathf.MoveTowards(data.currentSpeed, 0f, 10.0f * deltaTime);
            desiredMove = Vector3.zero;
        }

        // 💨 넉백(Knockback) 물리 속도 감쇠 및 위치 이동량 계산 (첫 충돌 거대 넉백 + 칼질 타격 미세 넉백)
        Vector3 knockbackMove = data.knockbackVelocity * deltaTime;
        data.knockbackVelocity = Vector3.MoveTowards(data.knockbackVelocity, Vector3.zero, 8.0f * deltaTime);

        // 🛡️ 소프트 척력(Separation) 완충 및 측면 슬라이딩 굴절 (유닛 겹침/포개짐 100% 차단 + 틈새 전진)
        Vector3 sepForce = separationForces[index];
        Vector3 sepMove = Vector3.zero;
        if (sepForce.sqrMagnitude > 0.001f)
        {
            if (desiredMove.sqrMagnitude > 0.001f)
            {
                Vector3 sepNorm = sepForce.normalized;
                float dot = Vector3.Dot(desiredMove, -sepNorm);
                if (dot > 0f)
                {
                    // 1. 전방 침범 성분을 제거하여 유닛 겹침(Stacking) 완벽 방지
                    desiredMove -= (-sepNorm * dot);
                }
            }

            sepMove = sepForce * (1.5f * deltaTime);
        }

        // 3. 최종 위치 갱신 (부드러운 이동 벡터 + 척력 결합으로 유닛 떨림 100% 원천 제거)
        Vector3 finalMove = desiredMove + sepMove + knockbackMove;
        Vector3 newPos = currentPos + finalMove;

        // ⛰️ 지형 고저차 실시간 밀착 반영 (터레인 굴곡 추적)
        if (terrainData.isValid == 1)
        {
            float groundY = terrainData.SampleHeight(newPos.x, newPos.z);
            newPos.y = groundY + data.groundYOffset;
        }

        // 🛑 [경사면 3D 합성 속도 강제 제한 (Slope Velocity Clamp)]:
        // 수평 이동 중 가파른 내리막이나 턱을 지날 때 Y축 낙차로 인해 3D 합성 속도가 폭증하여 "굴러 떨어지듯 가속"되는 현상 원천 차단!
        // (단, 수평 이동이 없는 정지/스폰 상태에서는 지면 밀착을 억제하지 않음)
        float horizMoveSq = finalMove.x * finalMove.x + finalMove.z * finalMove.z;
        if (horizMoveSq > 0.0001f)
        {
            Vector3 delta3D = newPos - currentPos;
            float actualDist3D = delta3D.magnitude;
            float maxAllowed3DSpeed = isCharging ? Mathf.Max(data.chargeSpeed * 1.25f, 6.0f) : Mathf.Max(data.moveSpeed * 1.5f, 5.5f);
            float maxAllowed3DDist = maxAllowed3DSpeed * deltaTime;

            if (actualDist3D > maxAllowed3DDist && actualDist3D > 0.0001f)
            {
                newPos = currentPos + (delta3D / actualDist3D) * maxAllowed3DDist;
            }
        }

        transform.position = newPos;

        // 4. 회전 갱신: 기본 수평 주시 방향(Flat Yaw) 결정
        Vector3 flatLookDir = Vector3.zero;
        if (desiredDir.sqrMagnitude > 0.001f)
        {
            flatLookDir = desiredDir;
        }
        else if (targetIdx >= 0 && targetIdx < totalCount && unitData[targetIdx].isAlive == 1 && (data.currentState >= 2 || distToEnemy <= 1.8f))
        {
            // 교전/공격이동 중이거나, 방어진 상태에서 적이 1.8m 내로 접근했을 때 적을 정면으로 노려보고 방패 세움
            Vector3 toEnemy = positions[targetIdx] - currentPos;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.001f)
            {
                flatLookDir = toEnemy.normalized;
            }
        }
        else if (data.targetRotation != Quaternion.identity)
        {
            // 단순 이동(1) 및 대기(0) 상태 시 부대 진형 목표 회전각 참조
            flatLookDir = data.targetRotation * Vector3.forward;
        }

        flatLookDir.y = 0f;
        if (flatLookDir.sqrMagnitude < 0.001f)
        {
            flatLookDir = transform.rotation * Vector3.forward;
            flatLookDir.y = 0f;
        }
        if (flatLookDir.sqrMagnitude < 0.001f)
        {
            flatLookDir = Vector3.forward;
        }
        flatLookDir.Normalize();

        // [옵션 A]: 중력 수직 유지 (Yaw 회전)
        Quaternion baseRot = Quaternion.LookRotation(flatLookDir, Vector3.up);
        Quaternion targetRot = baseRot;

        // [옵션 B]: 경사면 법선(Normal)에 맞춘 몸체 기울기 반영
        if (data.alignToSlope == 1 && terrainData.isValid == 1)
        {
            Unity.Mathematics.float3 norm3 = terrainData.SampleNormal(newPos.x, newPos.z);
            Vector3 surfaceNormal = new Vector3(norm3.x, norm3.y, norm3.z);
            targetRot = Quaternion.FromToRotation(Vector3.up, surfaceNormal) * baseRot;
        }

        float angleDiff = Quaternion.Angle(transform.rotation, targetRot);
        // 🛡️ [회전 불감대(Deadzone 3도)]: 3도 초과 시에만 회전하여 미세 떨림 방지
        if (angleDiff > 3f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 180f * deltaTime);
        }

        unitData[index] = data;
    }
}
