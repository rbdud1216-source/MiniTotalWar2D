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
    public float attackCooldown;
    public float lastAttackTime;
    public float moveSpeed;
    public float currentSpeed;
    public float acceleration;
    public float stoppingDistance;
    public float detectRange;
    public float attackRange;
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
    public Vector3 knockbackVelocity;  // 넉백/충격 물리 속도
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

    private bool isNativeArraysAllocated = false;
    private JobHandle simulationJobHandle;

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
                oldData.isFreeUnit = (u.mySquad == null) ? 1 : 0;
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
                    attackCooldown = u.attackCooldown,
                    lastAttackTime = Time.time - Random.Range(0f, u.attackCooldown),
                    moveSpeed = defaultSpeed,
                    currentSpeed = 0f,
                    acceleration = defaultAccel,
                    stoppingDistance = 0.2f,
                    detectRange = u.detectRange,
                    attackRange = 0.8f,
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
                    knockbackVelocity = Vector3.zero
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
            data.mass = (u.mass > 0f) ? u.mass : 100f;
            data.chargeSpeed = (u.chargeSpeed > 0f) ? u.chargeSpeed : 4.8f;
            data.chargeBonus = (u.chargeBonus > 0f) ? u.chargeBonus : 15f;
            data.maxChargeDamage = (u.maxChargeDamage > 0f) ? u.maxChargeDamage : 35f;
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
            unitDataArray[i] = data;
            positionArray[i] = u.transform.position;

            // 체력 동기화
            u.currentHp = data.currentHp;
            u.currentState = (UnitCommandState)data.currentState;

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

        // 🎯 [1단계] 부대 지휘관이 지정한 목표 적 부대(targetSquadId) 소속 병사 100% 집중 탐색! (SquadMap O(K) 최적화)
        if (!isFreeUnit && me.targetSquadId != -1)
        {
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

                        // 지정된 목표 적 부대원 추격
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
            // SquadMap에서 못 찾음 (목표 부대원이 12m 밖으로 이동 또는 아직 안 들어옴)
            // -> 거리 무제한 전역 fallback: 목표 부대 소속인 가장 가까운 적 탐색 (슬롯 복귀 방지)
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
            targetIndices[index] = bestTargetIdx;
            return;
        }

        // 🎯 [2단계] 목표 부대 미지정 시: 12m 근접 레이더 탐색 (자유 유닛 및 방어 유닛 공통)
        if (me.targetSquadId == -1)
        {
            float localMinDistSqr = 144.0f; // 12.0m * 12.0m = 144.0f (300m 탐색 롤백으로 연산량 대폭 절감)
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
    public float deltaTime;
    public float currentTime;
    public int totalCount;

    public void Execute(int index, TransformAccess transform)
    {
        UnitJobData data = unitData[index];
        if (data.isAlive == 0) return;

        Vector3 currentPos = transform.position;
        Vector3 targetDest = data.targetPosition;
        int targetIdx = targetIndices[index];

        bool isCharging = false;
        bool isCombatRunning = false;
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

                    // ⚔️ 지나가면서 사거리(1.45m) 내 적은 즉시 반격!
                    if (distToEnemy <= 1.45f && currentTime >= data.lastAttackTime + data.attackCooldown)
                    {
                        data.lastAttackTime = currentTime;
                        float finalDamage = data.damage;
                        Vector3 pushDir = (enemyPos - currentPos).normalized;
                        if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.rotation * Vector3.forward;

                        float massRatio = data.mass / Mathf.Max(10f, enemyData.mass);
                        float microKnockbackSpeed = 1.3f * massRatio;
                        enemyData.knockbackVelocity += pushDir * microKnockbackSpeed;
                        enemyData.currentHp -= finalDamage;
                        unitData[targetIdx] = enemyData;
                    }
                }
                // 🛡️ [B. V 비활성화: 접촉 방어 모드]
                else if (data.autoAttackEnabled == 0)
                {
                    bool isInMeleeContact = (distToEnemy <= 1.45f);
                    if (isInMeleeContact)
                    {
                        data.currentState = 3; // MeleeEngaged
                        Vector3 toEnemy = (enemyPos - currentPos).normalized;
                        targetDest = enemyPos - (toEnemy * 0.40f);
                        isCharging = false;
                    }
                    else
                    {
                        if (data.currentState == 3) data.currentState = 1; // Hold
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
                        targetDest = enemyPos - (toEnemy * 0.70f);

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
                        if (isAttacking)
                        {
                            // 🦅 유저 요청 완벽 반영: 전투 시 억지로 대형(targetPosition)을 유지하려 들지 않고, 
                            // 완벽히 대형을 풀고 각자 가장 가까운 목표 부대원(enemyPos)에게 돌격!
                            Vector3 toEnemy = (enemyPos - currentPos).normalized;
                            targetDest = enemyPos - (toEnemy * 0.40f);

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
                                targetDest = enemyPos - (toEnemy * 0.40f);
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

                // [A] 직접 칼이 닿는 유효 타격 사거리 판정
                bool isInMelee = (distToEnemy <= 1.45f);
                if (isInMelee)
                {
                    if (data.currentState != 1) data.currentState = 3; // MeleeEngaged
                }
                else if (data.currentState == 3)
                {
                    data.currentState = (data.autoAttackEnabled == 0) ? 1 : 2; // Hold vs AttackMove
                }

                // ⚔️ [일반 공격 판정]: 사거리(1.45m) 내 적 공격! (currentState != 1일 때)
                if (data.currentState != 1 && distToEnemy <= 1.45f && currentTime >= data.lastAttackTime + data.attackCooldown)
                {
                    data.lastAttackTime = currentTime;

                    float finalDamage = data.damage;
                    Vector3 pushDir = (enemyPos - currentPos).normalized;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.rotation * Vector3.forward;

                    float massRatio = data.mass / Mathf.Max(10f, enemyData.mass);

                    if (data.chargeImpactReady == 1 && data.currentSpeed > 0.5f)
                    {
                        float speedRatio = data.currentSpeed / Mathf.Max(0.1f, data.chargeSpeed);
                        float impactBonus = data.chargeBonus * speedRatio * (data.mass / 100f);

                        finalDamage = Mathf.Min(data.damage + impactBonus, data.maxChargeDamage);
                        data.chargeImpactReady = 0;

                        float knockbackSpeed = 4.5f * speedRatio * massRatio;
                        enemyData.knockbackVelocity += pushDir * knockbackSpeed;
                    }
                    else
                    {
                        float microKnockbackSpeed = 1.3f * massRatio;
                        enemyData.knockbackVelocity += pushDir * microKnockbackSpeed;
                    }

                    enemyData.currentHp -= finalDamage;
                    unitData[targetIdx] = enemyData;
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
            // AttackMove 중에 타겟이 없으면 슬롯이 아닌 현재 진행 방향으로 계속 전진 (슬롯 복귀 차단)
            if (data.currentState == 2)
            {
                Vector3 currentForward = data.targetRotation * Vector3.forward;
                targetDest = currentPos + (currentForward.sqrMagnitude > 0.001f ? currentForward : Vector3.forward) * 2.0f;
                isCombatRunning = true;
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

            // 🗡️ 백병전 중(칼 사거리 1.30m 이내)에는 발을 딛고 칼싸움 (미끄러짐 및 관통 방지)
            if (distToEnemy <= 1.30f && data.currentState == 3)
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

        transform.position = newPos;

        // 4. 회전 갱신: 회전 불감대(Deadzone 5도)로 고개 떨림/움찔거림 100% 방지 및 자연스러운 진형 정렬 유지
        Vector3 lookDir = Vector3.zero;
        if (desiredDir.sqrMagnitude > 0.001f)
        {
            lookDir = desiredDir;
        }
        else if (targetIdx >= 0 && targetIdx < totalCount && unitData[targetIdx].isAlive == 1 && (data.currentState >= 2 || distToEnemy <= 1.8f))
        {
            // 교전/공격이동 중이거나, 방어진 상태에서 적이 1.8m 내로 접근했을 때 적을 정면으로 노려보고 방패 세움
            Vector3 toEnemy = positions[targetIdx] - currentPos;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.001f)
            {
                lookDir = toEnemy.normalized;
            }
        }

        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            float angleDiff = Quaternion.Angle(transform.rotation, targetRot);
            // 🛡️ [회전 불감대(Deadzone 5도)]: 5도 초과 시에만 회전하여 미세 떨림 방지
            if (angleDiff > 5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 180f * deltaTime);
            }
        }
        else if (data.targetRotation != Quaternion.identity)
        {
            // 단순 이동(1) 및 대기(0) 상태 시 부대 진형 목표 회전각을 5도 오차 범위 내로 자연스럽게 정렬
            float angleDiff = Quaternion.Angle(transform.rotation, data.targetRotation);
            if (angleDiff > 5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, data.targetRotation, 180f * deltaTime);
            }
        }

        unitData[index] = data;
    }
}
