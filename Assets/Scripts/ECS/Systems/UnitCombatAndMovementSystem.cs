using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// 백병전 타격 시 발생하는 데미지 및 넉백 이벤트 구조체
    /// </summary>
    public struct DamageEvent
    {
        public Entity Target;
        public float Damage;
        public float3 PushDir;
        public float KnockbackSpeed;
    }

    /// <summary>
    /// 이동, 회전, 돌격(1.8m/s), 방패벽 제자리 반격, 백병전 데미지/넉백 및 사망 처리를 총괄하는 ECS 전투 시스템
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitSeparationSystem))]
    public partial struct UnitCombatAndMovementSystem : ISystem
    {
        private ComponentLookup<UnitCombatData> combatLookup;
        private ComponentLookup<UnitEntityTag> tagLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitEntityTag>();
            state.RequireForUpdate<UnitMovementData>();
            state.RequireForUpdate<UnitCombatData>();
            state.RequireForUpdate<UnitSeparationData>();

            combatLookup = state.GetComponentLookup<UnitCombatData>(false);
            tagLookup = state.GetComponentLookup<UnitEntityTag>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            ref var spatialSystem = ref state.WorldUnmanaged.GetExistingSystemState<SpatialHashGridSystem>();
            var spatialGrid = state.WorldUnmanaged.GetUnsafeSystemRef<SpatialHashGridSystem>(spatialSystem.SystemHandle);

            combatLookup.Update(ref state);
            tagLookup.Update(ref state);

            NativeQueue<DamageEvent> damageQueue = new NativeQueue<DamageEvent>(Allocator.TempJob);
            NativeQueue<ArrowLaunchCommand> arrowQueue = new NativeQueue<ArrowLaunchCommand>(Allocator.TempJob);
            NativeParallelHashSet<int> engagedSquads = new NativeParallelHashSet<int>(32, Allocator.TempJob);

            // 0단계: 현재 앞열이 적과 충돌하여 백병전(3) 중인 부대 ID들을 초고속 병렬 수집
            var detectJob = new DetectEngagedSquadsJob
            {
                EngagedSquads = engagedSquads.AsParallelWriter()
            };
            JobHandle detectHandle = detectJob.ScheduleParallel(state.Dependency);

            // 1단계: 이동, 회전, 돌격 가속, 척력 및 공격 쿨다운/타격 이벤트 수집 (병렬 스케줄링)
            var movementJob = new UnitMovementCombatJob
            {
                SpatialMap = spatialGrid.SpatialMap,
                SquadMap = spatialGrid.SquadMap,
                AllAliveUnits = spatialGrid.AllAliveUnits.AsDeferredJobArray(),
                AllAliveEntityMap = spatialGrid.AllAliveEntityMap,
                EngagedSquads = engagedSquads,
                DamageQueue = damageQueue.AsParallelWriter(),
                ArrowQueue = arrowQueue.AsParallelWriter(),
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                InvCellSize = SpatialHashGridSystem.INV_CELL_SIZE
            };

            JobHandle moveHandle = movementJob.ScheduleParallel(detectHandle);

            // 2단계: 큐에 쌓인 데미지/넉백 이벤트 일괄 적용 및 적 사망 처리 (안전한 단일 스케줄링)
            var applyDamageJob = new ApplyDamageJob
            {
                DamageQueue = damageQueue,
                CombatLookup = combatLookup,
                TagLookup = tagLookup
            };

            JobHandle damageHandle = applyDamageJob.Schedule(moveHandle);

            // 🏹 3단계: 멀티스레드 Job 완료 후 화살 발사 일괄 처리 및 렌더링 동기화
            damageHandle.Complete();

            while (arrowQueue.TryDequeue(out ArrowLaunchCommand cmd))
            {
                if (ArrowSimulationManager.Instance != null)
                {
                    ArrowSimulationManager.Instance.LaunchArrow(
                        cmd.isPlayer,
                        cmd.shooterPos,
                        cmd.targetPos,
                        cmd.projectileSpeed,
                        cmd.damage,
                        cmd.armorPiercingRatio,
                        cmd.armorShredAmount,
                        cmd.ignoreArmor == 1,
                        (TrajectoryMode)cmd.trajectoryMode,
                        cmd.gravityScale,
                        cmd.spreadRadius,
                        cmd.hasAllyObstruction == 1,
                        cmd.shooterRow
                    );
                }
            }

            if (ArrowSimulationManager.Instance != null)
            {
                ArrowSimulationManager.Instance.ManualUpdate(deltaTime);
            }

            damageQueue.Dispose();
            arrowQueue.Dispose();
            engagedSquads.Dispose();
            state.Dependency = default;
        }
    }

    /// <summary>
    /// 0단계: 앞열이 적과 충돌하여 백병전(MeleeEngaged)에 돌입한 부대 목록을 초고속으로 수집하는 Job
    /// </summary>
    [BurstCompile]
    public partial struct DetectEngagedSquadsJob : IJobEntity
    {
        public NativeParallelHashSet<int>.ParallelWriter EngagedSquads;

        private void Execute(in UnitEntityTag tag, in UnitCombatData combat)
        {
            if (tag.IsAlive == 1 && tag.SquadId != -1)
            {
                if (combat.CurrentState == 3)
                {
                    EngagedSquads.Add(tag.SquadId);
                }
            }
        }
    }

    /// <summary>
    /// 1단계: 유닛 이동, 회전, 돌격, 척력 및 타격 이벤트 발생 Job
    /// </summary>
    [BurstCompile]
    public partial struct UnitMovementCombatJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, EntitySpatialData> SpatialMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, EntitySpatialData> SquadMap;
        [ReadOnly] public NativeArray<EntitySpatialData> AllAliveUnits;
        [ReadOnly] public NativeParallelHashMap<Entity, EntitySpatialData> AllAliveEntityMap;
        [ReadOnly] public NativeParallelHashSet<int> EngagedSquads;
        public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
        public NativeQueue<ArrowLaunchCommand>.ParallelWriter ArrowQueue;
        public float DeltaTime;
        public float CurrentTime;
        public float InvCellSize;

        private void Execute(Entity entity, in UnitEntityTag tag, ref UnitMovementData movement, ref UnitCombatData combat, in UnitSeparationData separation)
        {
            if (tag.IsAlive == 0) return;

            float3 currentPos = movement.Position;
            float3 targetDest = movement.TargetPosition;

            bool isCharging = false;
            bool isCombatRunning = false;
            float distToEnemy = 999f;
            float meleeTargetDist = 999f;
            float3 enemyPos = currentPos;
            bool foundEnemy = false;
            Entity closestEnemyEntity = Entity.Null;
            float minEnemyDistSqr = 4000000f;
            float effectiveAttackRange = (combat.AttackRange > 0.1f) ? combat.AttackRange : 1.45f;
            float effectiveStoppingDist = (combat.CombatStoppingDistance > 0.1f) ? combat.CombatStoppingDistance : 1.05f;
            float effectiveOffset = (combat.EngagementOffset > 0.05f) ? combat.EngagementOffset : 0.40f;

            bool isFreeUnit = (tag.IsFreeUnit == 1 || tag.SquadId == -1);
            int closestEnemyIsFreeUnit = 0;
            bool isRangedShooting = false;

            // 💫 넘어짐(무력화) 상태 타이머 업데이트
            bool isKnockedDown = (combat.KnockdownTimer > 0f);
            if (isKnockedDown)
            {
                combat.KnockdownTimer = math.max(0f, combat.KnockdownTimer - DeltaTime);
            }

            // 🎯 [최적화] 매 프레임 적의 위치를 무차별 탐색하지 않고, 0.1초(10Hz)마다 1번씩만 적 위치 탐색/갱신!
            combat.TargetSearchTimer += DeltaTime;
            bool shouldSearchTarget = (combat.TargetSearchTimer >= 0.1f) || (combat.TargetEntity == Entity.Null && math.lengthsq(combat.CachedEnemyPos) < 0.001f);

            bool hasLockedTarget = false;

            if (shouldSearchTarget)
            {
                combat.TargetSearchTimer = 0f;

                // 🎯 [0단계: 기존 코앞 타겟 유지 - O(1) 해시 룩업 최적화]
                if (combat.TargetEntity != Entity.Null)
                {
                    bool targetStillAlive = false;
                    if (AllAliveEntityMap.TryGetValue(combat.TargetEntity, out EntitySpatialData targetData))
                    {
                        if (targetData.IsAlive == 1)
                        {
                            // 🚨 목표 부대가 명시적으로 지정되었는데, 현재 락온된 적이 그 부대원이 아니라면 락온 강제 해제!
                            if (combat.TargetSquadId == -1 || targetData.SquadId == combat.TargetSquadId)
                            {
                                float sqrDistToTarget = math.lengthsq(targetData.Position - currentPos);
                                if (sqrDistToTarget <= 2.1025f) // 1.45m * 1.45m
                                {
                                    enemyPos = targetData.Position;
                                    closestEnemyEntity = combat.TargetEntity;
                                    closestEnemyIsFreeUnit = targetData.IsFreeUnit;
                                    foundEnemy = true;
                                    minEnemyDistSqr = sqrDistToTarget;
                                    hasLockedTarget = true;
                                }
                                targetStillAlive = true;
                            }
                        }
                    }

                    // 🚨 기존 타겟이 이미 죽었거나 파괴되었으면 타겟 엔티티 즉시 해제!
                    if (!targetStillAlive)
                    {
                        combat.TargetEntity = Entity.Null;
                    }
                }

                // 🎯 [1단계: 부대 지휘관의 목표 부대(TargetSquadId) 집중 탐색]
                if (!hasLockedTarget && !isFreeUnit && combat.TargetSquadId != -1)
                {
                    float targetSquadMinCost = 4000000f; // 2000m - 지정 목표 부대 탐색 범위
                    float3 myFwd = math.mul(movement.Rotation, new float3(0, 0, 1));

                    if (SquadMap.TryGetFirstValue(combat.TargetSquadId, out EntitySpatialData other, out var it))
                    {
                        do
                        {
                            if (other.IsAlive == 1 && other.Faction != tag.Faction)
                            {
                                float3 diff = other.Position - currentPos;
                                diff.y = 0f;
                                float sqrDist = math.lengthsq(diff);

                                // 🎯 [정면 축 우선 1:1 정렬 (Frontal Alignment Scoring)]:
                                // 내 정면 진행 축과의 횡방향 이탈량에 페널티를 부여하여, 
                                // 측면/대각선 적으로 쏠리지 않고 내 정면에 마주한 적을 최우선 타겟으로 선택!
                                float fwdDist = math.dot(diff, myFwd);
                                float3 lateralVec = diff - (myFwd * fwdDist);
                                float lateralSqr = math.lengthsq(lateralVec);

                                float cost = sqrDist + (lateralSqr * 2.5f);
                                if (fwdDist < 0f) cost += 1000f; // 등 뒤의 적은 큰 페널티

                                if (cost < targetSquadMinCost)
                                {
                                    targetSquadMinCost = cost;
                                    enemyPos = other.Position;
                                    closestEnemyEntity = other.Entity;
                                    closestEnemyIsFreeUnit = other.IsFreeUnit;
                                    foundEnemy = true;
                                    minEnemyDistSqr = sqrDist;
                                }
                            }
                        } while (SquadMap.TryGetNextValue(out other, ref it));
                    }
                }

                // 🎯 [2단계: 목표 부대 미지정 시, 30m 레이더가 감지한 가장 가까운 적 탐색 - O(1) 해시 룩업 최적화]
                if (!hasLockedTarget && !foundEnemy && combat.TargetSquadId == -1)
                {
                    if (combat.RadarTargetEntity != Entity.Null)
                    {
                        if (AllAliveEntityMap.TryGetValue(combat.RadarTargetEntity, out EntitySpatialData radarData))
                        {
                            if (radarData.IsAlive == 1)
                            {
                                enemyPos = radarData.Position;
                                closestEnemyEntity = radarData.Entity;
                                closestEnemyIsFreeUnit = radarData.IsFreeUnit;
                                foundEnemy = true;
                                
                                float3 radarDiff = enemyPos - currentPos;
                                radarDiff.y = 0f;
                                minEnemyDistSqr = math.lengthsq(radarDiff);
                            }
                        }
                    }
                }

                if (foundEnemy)
                {
                    combat.CachedEnemyPos = enemyPos;
                    combat.TargetEntity = closestEnemyEntity;
                }
                else
                {
                    // 🛡️ 주변에 적이 완전히 없으면 캐시된 적 위치 및 타겟 엔티티 즉시 완전 소거! (유령 타겟 뭉침 원천 박멸)
                    combat.CachedEnemyPos = float3.zero;
                    combat.TargetEntity = Entity.Null;
                    if (combat.CurrentState == 3) combat.CurrentState = 0; // 백병전 잠금 즉시 해제
                }
            }
            else
            {
                // ⚡ [0.1초 주기 사이 프레임]: 무거운 전체 탐색 루프를 100% 생략하고 캐시된 적 위치를 즉시 사용!
                // 🚨 단, 유효한 타겟 엔티티가 살아있고 캐시 좌표가 있을 때만 유효한 적으로 인정 (유령 타겟팅 방지)
                if (combat.TargetEntity != Entity.Null && math.lengthsq(combat.CachedEnemyPos) > 0.001f)
                {
                    enemyPos = combat.CachedEnemyPos;
                    closestEnemyEntity = combat.TargetEntity;
                    foundEnemy = true;
                    float3 diff = enemyPos - currentPos;
                    diff.y = 0f;
                    minEnemyDistSqr = math.lengthsq(diff);
                }
                else
                {
                    foundEnemy = false;
                }
            }

            // 🔒 이 프레임의 임시 추격 부대 ID (적군 AI 타겟 탈취 방지용 로컬 잠금)
            int localTargetSquadId = combat.TargetSquadId; // 명시 지정 없으면 -1

            if (foundEnemy)
            {
                combat.TargetEntity = closestEnemyEntity;
                float3 diffToEnemy = enemyPos - currentPos;
                diffToEnemy.y = 0f;
                distToEnemy = math.length(diffToEnemy); // ⚡ 실제 유닛 간의 정확한 실시간 유클리드 거리

                // 🔒 이 프레임에 추격 중인 적의 SquadId를 임시 기록 (이 프레임 내 백병전 필터링용) - O(1) 해시 룩업 최적화
                // 🚨 주의: 개별 유닛 단위로 combat.TargetSquadId를 덮어쓰면 한 부대 안에서 병사들이 서로 다른 부대 ID를 잠가
                // 부대가 둘로 쪼개지는 치명적인 버그가 발생하므로, combat.TargetSquadId는 오직 지휘관만 수정하도록 유지합니다.
                if (localTargetSquadId == -1 && !isFreeUnit)
                {
                    if (AllAliveEntityMap.TryGetValue(closestEnemyEntity, out EntitySpatialData closestData))
                    {
                        if (closestData.SquadId != -1)
                        {
                            localTargetSquadId = closestData.SquadId;
                        }
                    }
                }

                // [A] 강제 단순 이동/후퇴(Move = 1) 상태: 자유유닛 및 부대원 100% 지정 목적지로 질주!
                if (combat.CurrentState == 1)
                {
                    targetDest = movement.TargetPosition;
                    isCharging = false;
                    isCombatRunning = false;
                }
                // [B] 🛡️ [V 비활성화: 접촉 방어 모드 (AutoAttackEnabled == 0)]
                // 🚨 주의: 부대가 이미 돌격/교전 중(CurrentState >= 2)이라면 후열 병사도 전방 지원을 위해 분기 C로 이동!
                else if (combat.AutoAttackEnabled == 0 && tag.Faction != 0 && combat.CurrentState < 2)
                {
                    bool isInMeleeContact = (distToEnemy <= effectiveAttackRange);
                    if (isInMeleeContact)
                    {
                        combat.CurrentState = 3; // MeleeEngaged
                        float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                        targetDest = enemyPos - (toEnemy * effectiveOffset);
                        isCharging = false;
                        isCombatRunning = false;
                    }
                    else
                    {
                        if (combat.CurrentState == 3) combat.CurrentState = 0; // 대형 복귀 (제자리 대기)
                        targetDest = movement.TargetPosition;
                        isCharging = false;
                        isCombatRunning = false;
                    }
                }
                else
                {
                    bool isAttacking = (combat.CurrentState == 2 || combat.CurrentState == 3 || tag.Faction == 0);

                    if (isFreeUnit)
                    {
                        float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                        targetDest = enemyPos - (toEnemy * math.max(0.70f, effectiveOffset));

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
                    else
                    {
                        // 🌊 [오브젝트 모드 일치화: 방진 유지 돌격 -> 충돌 난전 2단계 메커니즘]
                        bool isSquadAttacking = isAttacking || (foundEnemy && (combat.TargetSquadId != -1 || distToEnemy <= 25.0f));
                        bool isSquadEngaged = (tag.SquadId != -1 && EngagedSquads.Contains(tag.SquadId));

                        // 🏹 원거리 궁병(IsRangedUnit == 1)은 적이 백병전 거리 밖이고 탄약이 있으면 대형 슬롯 위치를 지키며 사격!
                        if (combat.IsRangedUnit == 1 && distToEnemy > combat.MeleeSwitchDistance && combat.CurrentAmmo > 0)
                        {
                            targetDest = movement.TargetPosition;
                            isCharging = false;
                            isCombatRunning = false;
                        }
                        else if (isSquadAttacking)
                        {
                            // 💥 [1단계: 접촉 전 돌격 (부대원 전원 비교전 상태)]
                            // 적과 실제로 충돌하기 전까지는 대형 슬롯(movement.TargetPosition)을 유지하며 방진을 짠 채 일제히 전진 돌격!
                            // (원거리에서 개별 적에게 쏠려 사선으로 달리는 현상을 원천 방지하고 대형 유지)
                            if (!isSquadEngaged && distToEnemy > math.max(2.5f, effectiveAttackRange) && combat.CurrentState != 3)
                            {
                                targetDest = movement.TargetPosition;
                                isCharging = true;
                                isCombatRunning = false;
                            }
                            // ⚔️ [2단계: 충돌 및 백병전 (부대원 중 1명이라도 충돌/교전 돌입 시)]
                            // 앞열이 적진과 부딪히는 순간 후열을 포함한 전군이 방진 슬롯 구속을 100% 풀고,
                            // 각자 정면의 적(enemyPos)을 향해 일제히 쇄도하며 난전(Brawl) 개시!
                            else
                            {
                                float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                                targetDest = enemyPos - (toEnemy * effectiveOffset);
                                isCharging = true;
                                isCombatRunning = false;
                            }
                        }
                        else
                        {
                            // 🛡️ [부대 조기 이탈 방지] 부대 지휘관의 일제 돌격 명령이 떨어지기 전(대기/이동 중)에는
                            // 외곽 병사가 12m 밖으로 혼자 뛰어나가지 않고, 근접 접촉 시에만 반격하며 대형을 유지합니다!
                            if (distToEnemy <= effectiveAttackRange && combat.AutoAttackEnabled == 1)
                            {
                                float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                                targetDest = enemyPos - (toEnemy * effectiveOffset);
                                isCharging = false;
                                isCombatRunning = false;
                            }
                            else
                            {
                                targetDest = movement.TargetPosition;
                                isCharging = false;
                                isCombatRunning = false;
                            }
                        }
                    }
                }

                if (movement.CurrentSpeed >= movement.MoveSpeed * 1.1f || isCharging)
                {
                    combat.ChargeImpactReady = 1;
                }

                // 🎯 [중간에 닿은 다른 적(사거리 이내) 우선 타격 판정]:
                // 목표 부대를 추격하며 달려가는 도중, 몸이 닿는 다른 적 부대나 자유 유닛이 있으면 즉시 타격!
                Entity meleeTargetEntity = closestEnemyEntity;
                meleeTargetDist = distToEnemy;
                float3 meleePushDir = diffToEnemy;

                int meleeSearchCells = math.max(1, (int)math.ceil(effectiveAttackRange * InvCellSize));
                float localMeleeMinSqr = effectiveAttackRange * effectiveAttackRange;
                int myMeleeCellHash = SpatialHashGridSystem.GetCellHash(currentPos, InvCellSize);
                int3 myMeleeCoord = new int3((int)math.floor(currentPos.x * InvCellSize), 0, (int)math.floor(currentPos.z * InvCellSize));

                for (int dx = -meleeSearchCells; dx <= meleeSearchCells; dx++)
                {
                    for (int dz = -meleeSearchCells; dz <= meleeSearchCells; dz++)
                    {
                        int neighborHash = SpatialHashGridSystem.HashCoords(myMeleeCoord + new int3(dx, 0, dz));
                        if (SpatialMap.TryGetFirstValue(neighborHash, out EntitySpatialData other, out var it))
                        {
                            do
                            {
                                if (other.IsAlive == 1 && other.Faction != tag.Faction)
                                {
                                    // 🚨 타겟 이탈 방지: 명시 지정 부대 또는 이 프레임 임시 잠금 부대 소속이 아니면 무시!
                                    // (적군 AI처럼 TargetSquadId == -1이어도 localTargetSquadId로 격리 적용)
                                    int effectiveTargetSquadId = (combat.TargetSquadId != -1) ? combat.TargetSquadId : localTargetSquadId;
                                    if (effectiveTargetSquadId != -1 && other.SquadId != effectiveTargetSquadId) continue;

                                    float3 diff = other.Position - currentPos;
                                    diff.y = 0f;
                                    float sqrDist = math.lengthsq(diff);
                                    if (sqrDist < localMeleeMinSqr)
                                    {
                                        localMeleeMinSqr = sqrDist;
                                        meleeTargetEntity = other.Entity;
                                        meleeTargetDist = math.sqrt(sqrDist);
                                        meleePushDir = diff;
                                    }
                                }
                            } while (SpatialMap.TryGetNextValue(out other, ref it));
                        }
                    }
                }

                // 🏹 [ECS 원거리 궁병 활 사격 판정]
                isRangedShooting = false;
                if (combat.IsRangedUnit == 1)
                {
                    // 🚨 백병전 교전 중(CurrentState == 3)이거나 5m 이내에 적이 들어왔으면 사격 100% 절대 금지!
                    bool isEngagedInMelee = (combat.CurrentState == 3);
                    bool isInsideMelee = (distToEnemy <= combat.MeleeSwitchDistance) || (meleeTargetDist <= combat.MeleeSwitchDistance);
                    bool hasAmmo = (combat.CurrentAmmo > 0);
                    // 🚀 [이동 중 사격 판정 (자유사격 이동 데드락 원천 방지)]:
                    // 1) 이동 중 사격 가능 유닛(CanFireWhileMoving == 1)은 이동 중에도 사격 허용
                    // 2) 이동 중 사격 불가(CanFireWhileMoving == 0) 유닛:
                    //    - 플레이어의 이동(CurrentState == 1) 명령 수행 중이고 아직 목적지에 도착하지 못했다면 사격 금지(이동 우선!)
                    //    - 목적지에 도착하여 멈춰 섰을 때(CurrentSpeed <= 0.25f)만 사격 허용!
                    float3 toDestVec = targetDest - currentPos;
                    toDestVec.y = 0f;
                    float distToDestTemp = math.length(toDestVec);
                    bool isMovingUnderOrder = (combat.CurrentState == 1) && (distToDestTemp > movement.StoppingDistance);
                    bool canShootMovement = (combat.CanFireWhileMoving == 1) || (!isMovingUnderOrder && movement.CurrentSpeed <= 0.25f);
                    // 🏹 [자유 사격(Fire at Will) 검사]: FireAtWill이 꺼져있으면 목표 부대 지정 시에만 사격
                    bool canShootFireAtWill = (combat.FireAtWill == 1) || (combat.TargetSquadId != -1);

                    // 📐 [3축 공간 및 사선 클리어런스 검사 (Clearance Check)]
                    // 1) 후방 공간 검사: 최후열이거나 최소 간격이 0(견착 무기: 쇠뇌/총)이거나 대형 세로 간격이 충분할 때 통과
                    bool isBackRow = (tag.Row >= combat.TotalRows - 1);
                    bool rearClear = isBackRow || (combat.MinRearSpacing <= 0.01f) || (combat.SquadSpacingZ >= combat.MinRearSpacing);

                    // 2) 측면 공간 검사: 대형 가로 간격 확보 여부
                    bool lateralClear = (combat.MinLateralSpacing <= 0.01f) || (combat.SquadSpacingX >= combat.MinLateralSpacing);

                    // 3) 전방 사선 및 언덕 경사면 클리어런스 (Front LoS & Hill Clearance)
                    bool frontClear = false;
                    if (tag.Row == 0)
                    {
                        // 1열은 앞에 아군이 없으므로 항상 통과
                        frontClear = true;
                    }
                    else if (tag.Row == 1 && combat.IsStaggeredFormation == 1 && combat.AllowStaggeredRank2DirectFire == 1)
                    {
                        // 체커보드(엇갈림) 2열은 전방 1열 사이 틈새로 직사 허용
                        frontClear = true;
                    }
                    else
                    {
                        // 3열 이상 또는 직렬 2열: 기하학적 높이차(언덕 경사면) + 탄도 발사각 앙각 판정
                        float distToFront = tag.Row * math.max(0.5f, combat.SquadSpacingZ);
                        float enemyDistSafe = math.max(1.0f, distToEnemy);

                        // 사수가 적보다 높은 언덕에 위치할 때의 지면 고도 우위
                        float slopeDy = (currentPos.y - enemyPos.y) * (distToFront / enemyDistSafe);

                        // 탄도 앙각에 따른 전열 머리 위 고도 상승치
                        float baseAngle;
                        if (combat.TrajectoryMode == 1) // Parabolic (곡사)
                        {
                            float angleRatio = math.clamp(distToEnemy / math.max(1.0f, combat.RangedAttackRange), 0f, 1f);
                            baseAngle = math.lerp(0.26f, 0.61f, angleRatio); // 15° ~ 35°
                        }
                        else // Direct (직사)
                        {
                            baseAngle = 0.035f; // 약 2° 조준각
                        }
                        float elevationDy = distToFront * math.tan(baseAngle);
                        float totalClearance = slopeDy + elevationDy;

                        frontClear = (totalClearance >= combat.HeadClearanceMargin);
                    }

                    bool isClearanceSatisfied = rearClear && lateralClear && frontClear;

                    if (!isEngagedInMelee && !isInsideMelee && hasAmmo && canShootFireAtWill && canShootMovement && isClearanceSatisfied && distToEnemy <= combat.RangedAttackRange && distToEnemy >= combat.RangedMinRange)
                    {
                        isRangedShooting = true;

                        // ⏱️ [순차 사격(Rolling Volley) 행간 지연 시간 계산]
                        float rowTimingOffset = (combat.EnableSequentialFire == 1) ? (tag.Row * combat.SequentialRowDelay) : 0f;

                        if (CurrentTime >= combat.LastRangedAttackTime + combat.RangedAttackCooldown + rowTimingOffset)
                        {
                            combat.LastRangedAttackTime = CurrentTime - rowTimingOffset;
                            combat.CurrentAmmo--;

                            float distRatio = math.clamp((distToEnemy - combat.OptimalRange) / math.max(1.0f, combat.RangedAttackRange - combat.OptimalRange), 0f, 1f);
                            float finalDmg = combat.RangedBaseDamage * (1.0f - (1.0f - combat.MinDamageRatioAtMax) * distRatio);
                            float spread = math.lerp(combat.MinSpreadRadius, combat.MaxSpreadRadius, distRatio);

                            // 🛡️ [앞에 아군 부대 존재 여부 감지]:
                            // 전방 적이 아군과 백병전 중일 때만 고각 곡사! (후열이라고 억지로 45도 이상 고각 쏘지 않고, 기준 탄도 + 행별 미세 분산 적용)
                            bool isEnemyInMelee = (localTargetSquadId != -1 && EngagedSquads.Contains(localTargetSquadId));
                            int hasAlly = isEnemyInMelee ? 1 : 0;

                            ArrowQueue.Enqueue(new ArrowLaunchCommand
                            {
                                isPlayer = tag.Faction,
                                shooterPos = currentPos,
                                targetPos = enemyPos,
                                projectileSpeed = combat.ProjectileSpeed,
                                damage = finalDmg,
                                armorPiercingRatio = combat.ArmorPiercingRatio,
                                armorShredAmount = combat.ArmorShredAmount,
                                ignoreArmor = combat.IgnoreArmor,
                                trajectoryMode = combat.TrajectoryMode,
                                gravityScale = combat.GravityScale,
                                spreadRadius = spread,
                                hasAllyObstruction = hasAlly,
                                shooterRow = tag.Row
                            });
                        }
                    }
                }

                // [A] 직접 칼/창이 닿는 유효 타격 사거리 판정
                bool isSidearmActive = (combat.UseSidearm == 1 && meleeTargetDist <= combat.SidearmSwitchDistance);
                float effectiveMeleeRange = isSidearmActive ? combat.SidearmAttackRange : effectiveAttackRange;

                bool isInMelee = (meleeTargetDist <= effectiveMeleeRange);
                if (isInMelee && !isRangedShooting)
                {
                    combat.CurrentState = 3; // MeleeEngaged
                }
                else if (combat.CurrentState == 3 && meleeTargetDist > combat.MeleeSwitchDistance)
                {
                    // 🛡️ 적이 백병전 거리(5m) 밖으로 완전히 달아났을 때만 백병전 해제
                    combat.CurrentState = (combat.AutoAttackEnabled == 0) ? 0 : 2;
                }

                // ⚔️ [백병전 타격 판정]: 원거리 사격 중이 아니고, 무력화 상태가 아니며 근접 교전 중일 때 타격!
                if (!isRangedShooting && !isKnockedDown && (isInMelee || combat.CurrentState == 3))
                {
                    float3 pushDir = meleePushDir;
                    if (math.lengthsq(pushDir) > 0.001f) pushDir = math.normalize(pushDir);
                    else pushDir = new float3(0, 0, 1);

                    float massRatio = combat.Mass / 100f;

                    if (isSidearmActive)
                    {
                        // 🗡️ [보조무기 공격]: 품 안으로 파고든 적에게 보조무기 타격
                        if (meleeTargetDist <= combat.SidearmAttackRange && CurrentTime >= combat.LastAttackTime + combat.SidearmAttackCooldown)
                        {
                            combat.LastAttackTime = CurrentTime;

                            float finalDamage = combat.SidearmDamage;
                            float baseSidearmKnock = (combat.SidearmBaseKnockback > 0f) ? combat.SidearmBaseKnockback : 0.2f;
                            float microKnockbackSpeed = baseSidearmKnock * massRatio * combat.SidearmKnockbackPower;

                            DamageQueue.Enqueue(new DamageEvent
                            {
                                Target = meleeTargetEntity,
                                Damage = finalDamage,
                                PushDir = pushDir,
                                KnockbackSpeed = microKnockbackSpeed
                            });
                        }
                    }
                    else
                    {
                        // ⚔️ [주무기 공격]: 최소 사거리 검사 및 스위트스팟 거리별 감쇠 계산
                        bool isInsideMinRange = (combat.MinAttackRange > 0.05f && meleeTargetDist < combat.MinAttackRange);
                        if (meleeTargetDist <= effectiveAttackRange && CurrentTime >= combat.LastAttackTime + combat.AttackCooldown)
                        {
                            combat.LastAttackTime = CurrentTime;

                            float finalDamage = combat.Damage;

                            // 🎯 최소 사거리 미만(초밀착)이거나 최적 사거리 미만 시 데미지 감쇠 적용
                            if (isInsideMinRange || (combat.OptimalRangeMin > 0.05f && meleeTargetDist < combat.OptimalRangeMin))
                            {
                                finalDamage *= combat.CloseRangeDamageRatio;
                            }

                            // 🛡️ [창벽 저지 및 돌격 반사 피해 (Charge Reflection)]:
                            // 유닛이 돌격 반사 능력(CanReflectCharge == 1)을 보유하고 있고,
                            // 제자리에 버티는 상태(접촉방어태세 또는 정지 상태)에서 적이 돌격해올 경우 발동
                            bool isBracing = (combat.AutoAttackEnabled == 0 || movement.CurrentSpeed < 1.0f);
                            if (combat.CanReflectCharge == 1 && isBracing && AllAliveEntityMap.TryGetValue(meleeTargetEntity, out EntitySpatialData targetSpatial))
                            {
                                if (targetSpatial.CurrentState >= 2)
                                {
                                    float reflectedChargeDamage = 15f;
                                    finalDamage += reflectedChargeDamage;
                                }
                            }

                            // 💥 [돌격 쇄도 속도 비례 돌격 피해]:
                            // 돌격 속도가 빠를수록 더 큰 충돌 피해를 입힘
                            // 공식: 돌격 추가 피해 = 돌격 보너스 × (돌격 쇄도 속도 ÷ 4.8m/s)
                            if (combat.ChargeImpactReady == 1 && movement.CurrentSpeed > 2.0f)
                            {
                                float speedScale = combat.ChargeSpeed / 4.8f;
                                finalDamage += combat.ChargeBonus * speedScale;
                                combat.ChargeImpactReady = 0;
                            }

                            // 최대 돌격 충돌 피해 상한 적용
                            if (combat.MaxChargeDamage > 0f)
                            {
                                finalDamage = math.min(finalDamage, combat.MaxChargeDamage);
                            }

                            // 💨 [현실적인 물리 넉백]: 일반 평타는 인스펙터 미세 저지(BaseMeleeKnockback), 전력 돌격 시에만 큰 넉백(1.8m/s)
                            bool isChargeHit = (combat.ChargeImpactReady == 1 && movement.CurrentSpeed > 2.0f);
                            float baseKnockback = isChargeHit ? 1.8f : ((combat.BaseMeleeKnockback > 0f) ? combat.BaseMeleeKnockback : 0.45f);
                            float knockbackSpeed = math.clamp(baseKnockback * massRatio, 0.2f, 2.0f) * combat.KnockbackPower;
                            DamageQueue.Enqueue(new DamageEvent
                            {
                                Target = meleeTargetEntity,
                                Damage = finalDamage,
                                PushDir = pushDir,
                                KnockbackSpeed = knockbackSpeed
                            });
                        }
                    }
                }
            }
            else
            {
                if (combat.CurrentState == 3)
                {
                    combat.CurrentState = 0; // 🛡️ 교전 상대 전멸 시 즉시 대기(Idle) 및 대형 복귀로 전환
                }
                targetDest = movement.TargetPosition;
                isCharging = false;
                isCombatRunning = false;
            }

            movement.IsCharging = isCharging ? 1 : 0;
            movement.IsCombatRunning = isCombatRunning ? 1 : 0;

            // 2. 목적지 방향 이동 벡터 및 거리 사전 계산
            float3 toDest = targetDest - currentPos;
            toDest.y = 0f;
            float distToDest = math.length(toDest);

            // 3. 이동 속도 및 가속도 결정 (오브젝트 모드 UnitMovementAndCombatJob 1:1 완벽 일치화)
            float combatRunSpd = (tag.IsFreeUnit == 1 || tag.SquadId == -1) ? 2.8f : 2.8f;
            float baseSpeed = isCharging ? combat.ChargeSpeed : (isCombatRunning ? math.max(combatRunSpd, movement.MoveSpeed) : movement.MoveSpeed);
            float maxDesiredSpeed = baseSpeed;

            // 🗡️ 백병전 정지 거리(effectiveStoppingDist 이내)에서는 발을 딛고 교전 (미끄러짐 및 스루 어택 방지)
            // 🚨 [옆 부대 적 접촉 시 정지 트랩 방지]:
            // 주 목표 부대가 지정되어 있다면(TargetSquadId != -1), 해당 목표 부대와의 거리(distToEnemy)가 유효할 때에만 제자리에 섭니다.
            // 옆 부대원과 우연히 몸이 스치더라도(meleeTargetDist), 자신의 목표를 향해 멈추지 않고 계속 전진합니다!
            bool isMeleeStopped = (distToEnemy <= effectiveStoppingDist) || (meleeTargetDist <= effectiveStoppingDist * 1.25f && (combat.TargetSquadId == -1 || distToEnemy <= effectiveStoppingDist * 1.8f));
            if (isMeleeStopped && combat.CurrentState == 3)
            {
                maxDesiredSpeed = 0f;
            }

            // 🏹 [ECS 궁병 제자리 사격 태세]: 이동 사격 불허 시 활을 쏘는 동안(isRangedShooting) 발 멈춤
            if (isRangedShooting && combat.CanFireWhileMoving == 0)
            {
                maxDesiredSpeed = 0f;
            }

            // 💫 넘어짐 무력화 중에는 이동 불가 (제자리에 쓰러짐)
            if (isKnockedDown)
            {
                maxDesiredSpeed = 0f;
            }

            float baseAccel = isCharging ? 8.0f : math.max(movement.Acceleration, 5.0f);

            // 4. 이동 벡터 및 척력 결합 (오브젝트 모드 UnitMovementAndCombatJob 1:1 완벽 일치화)
            float3 desiredMove = float3.zero;
            float3 desiredDir = float3.zero;

            if (distToDest > movement.StoppingDistance)
            {
                desiredDir = math.normalize(toDest);

                // 전방 척력 감속 완화 (밀집 대형에서도 최소 65% 속도 유지하여 시원하게 전진)
                float3 curSep = separation.SeparationForce;
                if (math.lengthsq(curSep) > 0.001f)
                {
                    float backwardRepulsion = math.dot(desiredDir, -math.normalize(curSep));
                    if (backwardRepulsion > 0.05f)
                    {
                        float brakeFactor = math.clamp(1.0f - backwardRepulsion * 0.35f, 0.65f, 1.0f);
                        maxDesiredSpeed *= brakeFactor;
                    }
                }

                movement.CurrentSpeed = MoveTowards(movement.CurrentSpeed, maxDesiredSpeed, baseAccel * DeltaTime);
                desiredMove = desiredDir * (movement.CurrentSpeed * DeltaTime);
            }
            else
            {
                movement.CurrentSpeed = MoveTowards(movement.CurrentSpeed, 0f, 10.0f * DeltaTime);
                desiredMove = float3.zero;
            }

            // 💨 넉백(Knockback) 물리 속도 감쇠 및 위치 이동량 계산
            float3 knockbackMove = combat.KnockbackVelocity * DeltaTime;
            combat.KnockbackVelocity = MoveTowards(combat.KnockbackVelocity, float3.zero, 8.0f * DeltaTime);

            // 🛡️ 소프트 척력 완충 및 하드 침범 차단 (유닛 겹침/포개짐 100% 원천 차단, 오브젝트 모드 1:1 일치)
            float3 sepForce = separation.SeparationForce;
            float3 sepMove = float3.zero;
            if (math.lengthsq(sepForce) > 0.001f)
            {
                if (math.lengthsq(desiredMove) > 0.001f)
                {
                    float3 sepNorm = math.normalize(sepForce);
                    float dot = math.dot(desiredMove, -sepNorm);
                    if (dot > 0f)
                    {
                        // 🌊 [데드락 원천 해제 & 빈틈 우회 돌파 (Deflection)]:
                        // 전방 아군 등에 막혔을 때 전진 속도를 0으로 죽이지 않고,
                        // 좌우 우회(Tangent) 성분을 부여하여 아군 사이 빈틈과 측면으로 자연스럽게 파고들며 쇄도!
                        float3 tangent = new float3(-sepNorm.z, 0f, sepNorm.x);
                        float sideSign = ((entity.Index & 1) == 0) ? 1.0f : -1.0f;
                        float3 sidePush = tangent * (sideSign * dot * 0.85f);

                        // 정면 차단 성분을 70%만 상쇄하고, 나머지 30% 전진력과 좌우 회피력을 결합하여 침투
                        desiredMove = (desiredMove - (-sepNorm * (dot * 0.70f))) + sidePush;
                    }
                }
                sepMove = sepForce * (1.0f * DeltaTime);
            }

            // 3. 최종 위치 갱신
            float3 finalMove = desiredMove + sepMove + knockbackMove;
            movement.Velocity = (DeltaTime > 0.0001f) ? (finalMove / DeltaTime) : float3.zero;
            movement.Position += finalMove;

            // 6. 회전 갱신 (5도 불감대 및 초당 180도 부드러운 회전)
            float3 lookDir = float3.zero;

            if (foundEnemy && distToEnemy <= 8.0f && (combat.CurrentState == 3 || distToEnemy <= effectiveAttackRange))
            {
                lookDir = enemyPos - movement.Position;
                lookDir.y = 0f;
            }
            else if (math.lengthsq(movement.Velocity) > 0.05f)
            {
                lookDir = movement.Velocity;
                lookDir.y = 0f;
            }

            if (math.lengthsq(lookDir) > 0.001f)
            {
                quaternion targetRot = quaternion.LookRotationSafe(lookDir, math.up());
                float angleDiff = CalculateAngleDegrees(movement.Rotation, targetRot);
                if (angleDiff > 5f)
                {
                    movement.Rotation = RotateTowards(movement.Rotation, targetRot, 180f * DeltaTime);
                }
            }
            else if (!movement.TargetRotation.Equals(quaternion.identity))
            {
                float angleDiff = CalculateAngleDegrees(movement.Rotation, movement.TargetRotation);
                if (angleDiff > 5f)
                {
                    movement.Rotation = RotateTowards(movement.Rotation, movement.TargetRotation, 180f * DeltaTime);
                }
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (math.abs(target - current) <= maxDelta) return target;
            return current + math.sign(target - current) * maxDelta;
        }

        private static float3 MoveTowards(float3 current, float3 target, float maxDelta)
        {
            float3 diff = target - current;
            float dist = math.length(diff);
            if (dist <= maxDelta || dist < 0.0001f) return target;
            return current + (diff / dist) * maxDelta;
        }

        private static float CalculateAngleDegrees(quaternion a, quaternion b)
        {
            float dot = math.dot(a, b);
            return math.acos(math.clamp(math.abs(dot), -1f, 1f)) * 2.0f * (180f / math.PI);
        }

        private static quaternion RotateTowards(quaternion from, quaternion to, float maxDegrees)
        {
            float angle = CalculateAngleDegrees(from, to);
            if (angle <= 0.001f) return math.normalize(to);
            float t = math.min(1.0f, maxDegrees / angle);
            return math.normalize(math.slerp(from, to, t));
        }
    }

    /// <summary>
    /// 2단계: 데미지 큐 적용 및 적 체력/넉백/사망 처리 Job
    /// </summary>
    [BurstCompile]
    public struct ApplyDamageJob : IJob
    {
        public NativeQueue<DamageEvent> DamageQueue;
        public ComponentLookup<UnitCombatData> CombatLookup;
        public ComponentLookup<UnitEntityTag> TagLookup;

        public void Execute()
        {
            while (DamageQueue.TryDequeue(out DamageEvent evt))
            {
                if (evt.Target != Entity.Null && CombatLookup.HasComponent(evt.Target) && TagLookup.HasComponent(evt.Target))
                {
                    var combat = CombatLookup[evt.Target];
                    var tag = TagLookup[evt.Target];

                    if (tag.IsAlive == 1)
                    {
                        // 🚨 [다대일 집중 공격 시 넉백 폭증 원천 차단]: 피격 유닛의 MaxMeleeKnockbackCap(기본 1.2m/s) 엄격 제한
                        float3 combinedKnockback = combat.KnockbackVelocity + (evt.PushDir * evt.KnockbackSpeed);
                        float maxAllowedKnockback = (combat.MaxMeleeKnockbackCap > 0f) ? combat.MaxMeleeKnockbackCap : 1.2f;
                        if (evt.KnockbackSpeed > 1.5f) maxAllowedKnockback = 2.2f; // 돌격 피격 시 상한 확대
                        if (math.lengthsq(combinedKnockback) > maxAllowedKnockback * maxAllowedKnockback)
                        {
                            combinedKnockback = math.normalize(combinedKnockback) * maxAllowedKnockback;
                        }
                        combat.KnockbackVelocity = combinedKnockback;

                        float damageReduction = math.clamp(combat.Armor, 0, 10000) / 10000f;
                        float effectiveDamage = (combat.Armor >= 10000) ? 0f : math.max(1.0f, evt.Damage * (1.0f - damageReduction));
                        combat.CurrentHp -= effectiveDamage;

                        if (combat.CurrentHp <= 0f)
                        {
                            combat.CurrentHp = 0f;
                            tag.IsAlive = 0;
                        }
                        else
                        {
                            // 💫 [강한 충격 피격 시 넘어짐 및 무력화 (Knockdown)]
                            // 넘어짐 면역(불굴 특수능력)이 아닐 때에만 넘어짐 발동!
                            float effectiveKnockdownThreshold = (combat.KnockdownThreshold > 0f) ? combat.KnockdownThreshold : 2.0f;
                            if (combat.IsImmuneToKnockdown == 0 && evt.KnockbackSpeed >= effectiveKnockdownThreshold)
                            {
                                float duration = (combat.KnockdownDuration > 0f) ? combat.KnockdownDuration : 3.0f;
                                combat.KnockdownTimer = duration;
                            }
                        }

                        CombatLookup[evt.Target] = combat;
                        TagLookup[evt.Target] = tag;
                    }
                }
            }
        }
    }
}
