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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            ref var spatialSystem = ref state.WorldUnmanaged.GetExistingSystemState<SpatialHashGridSystem>();
            var spatialGrid = state.WorldUnmanaged.GetUnsafeSystemRef<SpatialHashGridSystem>(spatialSystem.SystemHandle);

            combatLookup.Update(ref state);
            tagLookup.Update(ref state);

            NativeQueue<DamageEvent> damageQueue = new NativeQueue<DamageEvent>(Allocator.TempJob);

            // 1단계: 이동, 회전, 돌격 가속, 척력 및 공격 쿨다운/타격 이벤트 수집 (병렬 스케줄링)
            var movementJob = new UnitMovementCombatJob
            {
                SpatialMap = spatialGrid.SpatialMap,
                SquadMap = spatialGrid.SquadMap,
                AllAliveUnits = spatialGrid.AllAliveUnits.AsDeferredJobArray(),
                DamageQueue = damageQueue.AsParallelWriter(),
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                InvCellSize = SpatialHashGridSystem.INV_CELL_SIZE
            };

            JobHandle moveHandle = movementJob.ScheduleParallel(state.Dependency);

            // 2단계: 큐에 쌓인 데미지/넉백 이벤트 일괄 적용 및 적 사망 처리 (안전한 단일 스케줄링)
            var applyDamageJob = new ApplyDamageJob
            {
                DamageQueue = damageQueue,
                CombatLookup = combatLookup,
                TagLookup = tagLookup
            };

            JobHandle damageHandle = applyDamageJob.Schedule(moveHandle);
            state.Dependency = damageQueue.Dispose(damageHandle);
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
        public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
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

            bool isFreeUnit = (tag.IsFreeUnit == 1 || tag.SquadId == -1);
            int closestEnemyIsFreeUnit = 0;

            // 🎯 [0단계: 기존 코앞 타겟 유지 - Object 모드와 1:1 동일 로직]
            bool hasLockedTarget = false;
            if (combat.TargetEntity != Entity.Null)
            {
                for (int i = 0; i < AllAliveUnits.Length; i++)
                {
                    EntitySpatialData targetData = AllAliveUnits[i];
                    if (targetData.Entity == combat.TargetEntity)
                    {
                        if (targetData.IsAlive == 1)
                        {
                            // 🚨 목표 부대가 명시적으로 지정되었는데, 현재 락온된 적이 그 부대원이 아니라면 락온 강제 해제!
                            // (이전에 싸우던 옆 부대원에게 계속 쏠려서 목표 부대로 안 가는 현상 방지)
                            if (combat.TargetSquadId != -1 && targetData.SquadId != combat.TargetSquadId)
                            {
                                break;
                            }

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
                        }
                        break; // 타겟을 찾았으므로 루프 종료
                    }
                }
            }

            // 🎯 [1단계: 부대 지휘관의 목표 부대(TargetSquadId) 집중 탐색]
            // [수정]: 이동 중(AttackMove=2)에는 지정 목표 부대원로만 직진.
            if (!hasLockedTarget && !isFreeUnit && combat.TargetSquadId != -1)
            {
                float targetSquadMinSqr = 4000000f; // 2000m - 지정 목표 부대 탐색 범위

                if (SquadMap.TryGetFirstValue(combat.TargetSquadId, out EntitySpatialData other, out var it))
                {
                    do
                    {
                        if (other.IsAlive == 1 && other.Faction != tag.Faction)
                        {
                            float3 diff = other.Position - currentPos;
                            diff.y = 0f;
                            float sqrDist = math.lengthsq(diff);

                            if (sqrDist < targetSquadMinSqr)
                            {
                                targetSquadMinSqr = sqrDist;
                                enemyPos = other.Position;
                                closestEnemyEntity = other.Entity;
                                closestEnemyIsFreeUnit = other.IsFreeUnit;
                                foundEnemy = true;
                            }
                        }
                    } while (SquadMap.TryGetNextValue(out other, ref it));
                }

                if (foundEnemy)
                {
                    minEnemyDistSqr = targetSquadMinSqr;
                }
            }

            // 🎯 [2단계: 목표 부대 미지정 시, 300m 레이더가 감지한 가장 가까운 적 탐색]
            if (!hasLockedTarget && !foundEnemy && combat.TargetSquadId == -1)
            {
                if (combat.RadarTargetEntity != Entity.Null)
                {
                    for (int i = 0; i < AllAliveUnits.Length; i++)
                    {
                        EntitySpatialData radarData = AllAliveUnits[i];
                        if (radarData.Entity == combat.RadarTargetEntity)
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
                            break;
                        }
                    }
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

                // 🔒 이 프레임에 추격 중인 적의 SquadId를 임시 기록 (이 프레임 내 백병전 필터링용)
                // 🚨 주의: 개별 유닛 단위로 combat.TargetSquadId를 덮어쓰면 한 부대 안에서 병사들이 서로 다른 부대 ID를 잠가
                // 부대가 둘로 쪼개지는 치명적인 버그가 발생하므로, combat.TargetSquadId는 오직 지휘관만 수정하도록 유지합니다.
                if (localTargetSquadId == -1 && !isFreeUnit)
                {
                    for (int i = 0; i < AllAliveUnits.Length; i++)
                    {
                        if (AllAliveUnits[i].Entity == closestEnemyEntity)
                        {
                            if (AllAliveUnits[i].SquadId != -1)
                            {
                                localTargetSquadId = AllAliveUnits[i].SquadId;
                            }
                            break;
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
                    bool isInMeleeContact = (distToEnemy <= 1.45f);
                    if (isInMeleeContact)
                    {
                        combat.CurrentState = 3; // MeleeEngaged
                        float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                        targetDest = enemyPos - (toEnemy * 0.40f);
                        isCharging = false;
                        isCombatRunning = false;
                    }
                    else
                    {
                        if (combat.CurrentState == 3) combat.CurrentState = 1; // 대형 복귀
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
                    else
                    {
                        // 🌊 [후열 바둑판 방진 멈춤 100% 해제]:
                        // 부대가 공격 중이거나, 목표 부대가 있거나, 전방 25m 내 적이 발견되면 후열 병사 전원 일제 돌격!
                        bool shouldChaseEnemy = isAttacking || (foundEnemy && (combat.TargetSquadId != -1 || distToEnemy <= 25.0f));

                        if (shouldChaseEnemy)
                        {
                            // 🦅 유저 요청 완벽 반영: 전투 시 억지로 대형(TargetPosition)을 유지하려 들지 않고, 
                            // 완벽히 대형을 풀고 각자 가장 가까운 목표 부대원(enemyPos)에게 돌격!
                            // 대형이 붕괴되어도 물리 척력(Separation)에 의해 자연스럽게 전선이 형성됨.
                            float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                            targetDest = enemyPos - (toEnemy * 0.40f);
                            
                            isCharging = true;
                            isCombatRunning = false;
                        }
                        else
                        {
                            // 🛡️ [부대 조기 이탈 방지] 부대 지휘관의 일제 돌격 명령이 떨어지기 전(대기/이동 중)에는
                            // 외곽 병사가 12m 밖으로 혼자 뛰어나가지 않고, 근접 접촉(1.45m) 시에만 반격하며 대형을 유지합니다!
                            if (distToEnemy <= 1.45f && combat.AutoAttackEnabled == 1)
                            {
                                float3 toEnemy = (math.lengthsq(diffToEnemy) > 0.001f) ? math.normalize(diffToEnemy) : new float3(0, 0, 1);
                                targetDest = enemyPos - (toEnemy * 0.40f);
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

                // 🎯 [중간에 닿은 다른 적(사거리 1.85m 이내) 우선 타격 판정]:
                // 목표 부대를 추격하며 달려가는 도중, 몸이 닿는 다른 적 부대나 자유 유닛이 있으면 즉시 타격!
                Entity meleeTargetEntity = closestEnemyEntity;
                meleeTargetDist = distToEnemy;
                float3 meleePushDir = diffToEnemy;

                int meleeSearchCells = 1;
                float localMeleeMinSqr = 3.42f; // 1.85m
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

                // [A] 직접 칼이 닿는 유효 타격 사거리 판정 (1.85m)
                bool isInMelee = (meleeTargetDist <= 1.85f);
                if (isInMelee)
                {
                    if (combat.CurrentState != 1) combat.CurrentState = 3; // 💥 유저 이동 명령(Move=1)이 아닐 때만 MeleeEngaged로 전환!
                }
                else if (combat.CurrentState == 3)
                {
                    combat.CurrentState = (combat.AutoAttackEnabled == 0) ? 1 : 2;
                }

                // ⚔️ [백병전 타격 판정]: 사거리(1.85m) 내 적이 있고 공격 쿨다운 만족 시 타격 이벤트 Enqueue!
                // 접촉 방어 모드, 제자리 사수 반격, 돌격 교전, 스루 어택 모두 1.85m 내 적에게 정상 타격 적용
                if (meleeTargetDist <= 1.85f && CurrentTime >= combat.LastAttackTime + combat.AttackCooldown)
                {
                    combat.LastAttackTime = CurrentTime;

                    float finalDamage = combat.Damage;
                    float3 pushDir = meleePushDir;
                    if (math.lengthsq(pushDir) > 0.001f) pushDir = math.normalize(pushDir);
                    else pushDir = new float3(0, 0, 1);

                    float massRatio = combat.Mass / 100f;

                    if (combat.ChargeImpactReady == 1 && movement.CurrentSpeed > 0.5f)
                    {
                        float speedRatio = movement.CurrentSpeed / math.max(0.1f, combat.ChargeSpeed);
                        float impactBonus = combat.ChargeBonus * speedRatio * massRatio;
                        finalDamage = math.min(combat.Damage + impactBonus, 35f);
                        combat.ChargeImpactReady = 0;

                        float knockbackSpeed = 4.5f * speedRatio * massRatio;
                        DamageQueue.Enqueue(new DamageEvent
                        {
                            Target = meleeTargetEntity,
                            Damage = finalDamage,
                            PushDir = pushDir,
                            KnockbackSpeed = knockbackSpeed
                        });
                    }
                    else
                    {
                        float microKnockbackSpeed = 1.3f * massRatio;
                        DamageQueue.Enqueue(new DamageEvent
                        {
                            Target = meleeTargetEntity,
                            Damage = finalDamage,
                            PushDir = pushDir,
                            KnockbackSpeed = microKnockbackSpeed
                        });
                    }
                }
            }
            else
            {
                if (combat.CurrentState == 3)
                {
                    combat.CurrentState = (combat.AutoAttackEnabled == 0) ? 1 : 2;
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

            // 🗡️ 백병전 초근접(1.05m 이내)에서는 발을 딛고 칼싸움 (미끄러짐 및 스루 어택 방지)
            // 🚨 [옆 부대 적 접촉 시 정지 트랩 방지]:
            // 주 목표 부대가 지정되어 있다면(TargetSquadId != -1), 해당 목표 부대와의 거리(distToEnemy)가 유효할 때에만 제자리에 섭니다.
            // 옆 부대원과 우연히 몸이 스치더라도(meleeTargetDist), 자신의 목표를 향해 멈추지 않고 계속 전진합니다!
            bool isMeleeStopped = (distToEnemy <= 1.05f) || (meleeTargetDist <= 1.35f && (combat.TargetSquadId == -1 || distToEnemy <= 2.2f));
            if (isMeleeStopped && combat.CurrentState == 3)
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

            if (foundEnemy && distToEnemy <= 8.0f && (combat.CurrentState == 3 || distToEnemy <= 1.45f))
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
                quaternion targetRot = quaternion.LookRotation(math.normalize(lookDir), math.up());
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
            if (angle <= 0.001f) return to;
            float t = math.min(1.0f, maxDegrees / angle);
            return math.slerp(from, to, t);
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
                        combat.KnockbackVelocity += evt.PushDir * evt.KnockbackSpeed;
                        combat.CurrentHp -= evt.Damage;

                        if (combat.CurrentHp <= 0f)
                        {
                            combat.CurrentHp = 0f;
                            tag.IsAlive = 0;
                        }

                        CombatLookup[evt.Target] = combat;
                        TagLookup[evt.Target] = tag;
                    }
                }
            }
        }
    }
}
