using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// 기존 Squad/PlayerController/BattleManager와 DOTS ECS Entity 시뮬레이션을 연결하는 고성능 브릿지 매니저
    /// </summary>
    public class SquadECSSimulationBridge : MonoBehaviour
    {
        public static SquadECSSimulationBridge Instance { get; private set; }

        private EntityManager entityManager;
        private World defaultWorld;
        private bool isInitialized = false;

        public bool IsInitialized => isInitialized;
        public EntityManager EntityManager => entityManager;
        public Dictionary<Unit, Entity> UnitToEntityMap => unitToEntityMap;

        private readonly Dictionary<Unit, Entity> unitToEntityMap = new Dictionary<Unit, Entity>();
        private readonly Dictionary<Entity, Unit> entityToUnitMap = new Dictionary<Entity, Unit>();

        // ⚡ [최적화] 매 프레임 GC 할당을 방지하기 위한 캐싱 구조체 및 딕셔너리
        private struct SquadSyncInfo
        {
            public int TargetSquadId;
            public int CmdState;
            public float Speed;
            public float Acceleration;
        }

        private readonly Dictionary<int, SquadSyncInfo> squadSyncMap = new Dictionary<int, SquadSyncInfo>(64);
        private EntityQuery unitSyncQuery;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            InitializeECS();
        }

        private void InitializeECS()
        {
            if (isInitialized) return;

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld != null && defaultWorld.IsCreated)
            {
                entityManager = defaultWorld.EntityManager;
                unitSyncQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<UnitEntityTag>(),
                    ComponentType.ReadWrite<UnitCombatData>(),
                    ComponentType.ReadWrite<UnitMovementData>()
                );
                isInitialized = true;
                Debug.Log("[SquadECSSimulationBridge] 🚀 Unity 6 DOTS ECS 월드 및 EntityManager 초기화 완료!");
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                InitializeECS();
                if (!isInitialized) return;
            }

            // 1. 하이브리드 게임오브젝트 모드 유닛 양방향 동기화
            SyncEntitiesToGameObjects();

            // 2. ⚡ 순수 ECS 모드 매 프레임 부대 상태(TargetSquadId, MoveSpeed, CommandState) 실시간 동기화
            if (BattleManager.Instance != null && BattleManager.Instance.usePureECS)
            {
                var squads = BattleManager.Instance.GetAllSquads();
                if (squads != null && squads.Count > 0)
                {
                    // 🛡️ [부대 교전 연동 (Combat Cascade) - O(1) 초고속 판정]:
                    // 부대가 이미 전군 돌격(AttackMove) 중이 아닌 경우:
                    // SpatialHashGridSystem에서 O(1)로 집계된 MeleeEngagedCount를 확인하여 교전 돌입 시 전군 돌격 발동!
                    for (int s = 0; s < squads.Count; s++)
                    {
                        Squad squad = squads[s];
                        if (squad == null) continue;

                        if (squad.autoAttackEnabled && squad.currentCommandState != UnitCommandState.AttackMove && squad.currentCommandState != UnitCommandState.Move)
                        {
                            int mySquadId = squad.GetInstanceID();
                            if (SpatialHashGridSystem.TryGetSquadAggregateData(mySquadId, out _, out _, out int meleeEngagedCount))
                            {
                                if (meleeEngagedCount > 0)
                                {
                                    // 🚨 핵심: 부대에 이미 유효한 지정 목표 적 부대가 있다면 끝까지 고수!
                                    if (squad.currentTargetSquad != null && squad.currentTargetSquad.MemberCount > 0)
                                    {
                                        squad.CommandAttackSquad(squad.currentTargetSquad);
                                    }
                                    else
                                    {
                                        // 가장 가까운 적 부대를 찾아 즉시 돌격 명령 (20개 부대 순회이므로 초고속)
                                        Squad closestEnemy = null;
                                        float minDistSqr = float.MaxValue;
                                        Vector3 squadPos = squad.transform.position;

                                        for (int k = 0; k < squads.Count; k++)
                                        {
                                            Squad enemy = squads[k];
                                            if (enemy != null && enemy.isPlayer != squad.isPlayer && enemy.MemberCount > 0)
                                            {
                                                float dSqr = (enemy.transform.position - squadPos).sqrMagnitude;
                                                if (dSqr < minDistSqr)
                                                {
                                                    minDistSqr = dSqr;
                                                    closestEnemy = enemy;
                                                }
                                            }
                                        }

                                        if (closestEnemy != null)
                                        {
                                            squad.CommandAttackSquad(closestEnemy);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // ⚡ [부대 정보 O(1) 매핑 딕셔너리 구축 (부대 20개 순회 = O(M))]
                    squadSyncMap.Clear();
                    for (int s = 0; s < squads.Count; s++)
                    {
                        Squad squad = squads[s];
                        if (squad == null) continue;

                        int targetSquadId = (squad.currentTargetSquad != null && squad.currentTargetSquad.MemberCount > 0)
                            ? squad.currentTargetSquad.GetInstanceID()
                            : -1;
                        int cmdState = (int)squad.currentCommandState;
                        float spd = squad.targetSpeed > 0 ? squad.targetSpeed : (squad.isRunning ? 2.8f : 1.2f);
                        float accel = squad.isRunning ? 8.0f : 5.5f;

                        squadSyncMap[squad.GetInstanceID()] = new SquadSyncInfo
                        {
                            TargetSquadId = targetSquadId,
                            CmdState = cmdState,
                            Speed = spd,
                            Acceleration = accel
                        };
                    }

                    // ⚡ [엔티티 단 1회 직렬 순회 - 160,000회 중첩 루프를 4,000회 단일 루프로 40배 절감]
                    using (var entities = unitSyncQuery.ToEntityArray(Allocator.Temp))
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            Entity e = entities[i];
                            var tag = entityManager.GetComponentData<UnitEntityTag>(e);
                            if (tag.IsAlive == 0 || tag.SquadId == -1) continue;
                            if (!squadSyncMap.TryGetValue(tag.SquadId, out var syncInfo)) continue;

                            var combat = entityManager.GetComponentData<UnitCombatData>(e);
                            bool needUpdateCombat = false;

                            // 1. TargetSquadId 불일치 시 갱신
                            if (combat.TargetSquadId != syncInfo.TargetSquadId)
                            {
                                combat.TargetSquadId = syncInfo.TargetSquadId;
                                needUpdateCombat = true;
                            }

                            // 2. 부대 명령 상태 동기화
                            if (syncInfo.CmdState == 1) // Move: 즉시 교전 해제 및 강제 이동
                            {
                                if (combat.CurrentState != 1)
                                {
                                    combat.CurrentState = 1;
                                    combat.TargetEntity = Entity.Null;
                                    combat.CachedEnemyPos = float3.zero;
                                    combat.ChargeImpactReady = 0;
                                    needUpdateCombat = true;
                                }
                            }
                            else if (syncInfo.CmdState == 2) // AttackMove: Idle(0) 포함 MeleeEngaged(3) 이외에는 모두 2로 전환
                            {
                                if (combat.CurrentState != 2 && combat.CurrentState != 3)
                                {
                                    combat.CurrentState = 2;
                                    needUpdateCombat = true;
                                }
                            }
                            else if (syncInfo.CmdState == 0) // Idle: 지휘관 정지/재정비 상태
                            {
                                if (combat.CurrentState != 0 && (syncInfo.TargetSquadId == -1 || combat.CurrentState != 3))
                                {
                                    combat.CurrentState = 0;
                                    combat.TargetEntity = Entity.Null;
                                    combat.CachedEnemyPos = float3.zero;
                                    combat.ChargeImpactReady = 0;
                                    needUpdateCombat = true;
                                }
                            }

                            // 🚨 변경이 있을 때만 SetComponentData 호출 (매 프레임 무차별 쓰기 제거)
                            if (needUpdateCombat)
                            {
                                entityManager.SetComponentData(e, combat);
                            }

                            // 3. 이동 속도 및 가속도 동기화 (실제 변경 시에만 SetComponentData 호출)
                            var mov = entityManager.GetComponentData<UnitMovementData>(e);
                            bool isEngaged = (combat.CurrentState == 2 || combat.CurrentState == 3);
                            float desiredSpeed = isEngaged ? mov.MoveSpeed : syncInfo.Speed;
                            float desiredAccel = syncInfo.Acceleration;

                            if (math.abs(mov.MoveSpeed - desiredSpeed) > 0.001f || math.abs(mov.Acceleration - desiredAccel) > 0.001f)
                            {
                                mov.MoveSpeed = desiredSpeed;
                                mov.Acceleration = desiredAccel;
                                entityManager.SetComponentData(e, mov);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unit을 ECS Entity로 등록 및 생성
        /// </summary>
        public Entity RegisterUnitEntity(Unit unit)
        {
            if (unit == null) return Entity.Null;
            if (!isInitialized) InitializeECS();

            if (unitToEntityMap.TryGetValue(unit, out Entity existingEntity))
            {
                return existingEntity;
            }

            EntityArchetype archetype = entityManager.CreateArchetype(
                typeof(UnitEntityTag),
                typeof(UnitMovementData),
                typeof(UnitCombatData),
                typeof(UnitSeparationData),
                typeof(SpatialGridCell)
            );

            Entity entity = entityManager.CreateEntity(archetype);

            bool isFree = (unit.mySquad == null);
            float defaultSpeed = (unit.mySquad != null) ? unit.mySquad.targetSpeed : (unit.isRunning ? unit.runSpeed : unit.walkSpeed);
            if (defaultSpeed <= 0f) defaultSpeed = isFree ? 1.2f : 1.0f;

            entityManager.SetComponentData(entity, new UnitEntityTag
            {
                Faction = unit.isPlayer ? 1 : 0,
                SquadId = (unit.mySquad != null) ? unit.mySquad.GetInstanceID() : -1,
                IsFreeUnit = isFree ? 1 : 0,
                IsAlive = (unit.currentHp > 0) ? 1 : 0
            });

            entityManager.SetComponentData(entity, new UnitMovementData
            {
                Position = unit.transform.position,
                Rotation = unit.transform.rotation,
                Velocity = float3.zero,
                TargetPosition = (unit.FixedTargetPos != Vector3.zero) ? (float3)unit.FixedTargetPos : (float3)unit.transform.position,
                TargetRotation = unit.TargetRotation,
                MoveSpeed = defaultSpeed,
                CurrentSpeed = 0f,
                Acceleration = unit.isRunning ? 8.0f : 5.0f,
                StoppingDistance = 0.2f,
                IsCharging = 0,
                IsCombatRunning = 0
            });

            entityManager.SetComponentData(entity, new UnitCombatData
            {
                CurrentHp = unit.currentHp,
                MaxHp = unit.maxHp,
                Damage = unit.damage,
                AttackCooldown = unit.attackCooldown,
                LastAttackTime = Time.time - UnityEngine.Random.Range(0f, unit.attackCooldown),
                DetectRange = unit.detectRange,
                AttackRange = (unit.attackRange > 0.1f) ? unit.attackRange : 1.45f,
                MinAttackRange = unit.minAttackRange,
                OptimalRangeMin = unit.optimalRangeMin,
                CloseRangeDamageRatio = (unit.closeRangeDamageRatio > 0.05f) ? unit.closeRangeDamageRatio : 1.0f,
                KnockbackPower = (unit.knockbackPower > 0f) ? unit.knockbackPower : 1.0f,
                UseSidearm = unit.useSidearm ? 1 : 0,
                SidearmSwitchDistance = (unit.sidearmSwitchDistance > 0.1f) ? unit.sidearmSwitchDistance : 1.2f,
                SidearmAttackRange = (unit.sidearmAttackRange > 0.1f) ? unit.sidearmAttackRange : 1.0f,
                SidearmDamage = (unit.sidearmDamage > 0f) ? unit.sidearmDamage : 4.0f,
                SidearmAttackCooldown = (unit.sidearmAttackCooldown > 0.05f) ? unit.sidearmAttackCooldown : 0.8f,
                SidearmKnockbackPower = unit.sidearmKnockbackPower,
                CombatStoppingDistance = (unit.combatStoppingDistance > 0.1f) ? unit.combatStoppingDistance : 1.05f,
                EngagementOffset = (unit.engagementOffset > 0.05f) ? unit.engagementOffset : 0.40f,
                CurrentState = (int)unit.currentState,
                TargetEntity = Entity.Null,
                KnockbackVelocity = float3.zero,
                Mass = (unit.mass > 0f) ? unit.mass : 100f,
                ChargeSpeed = (unit.chargeSpeed > 0f) ? unit.chargeSpeed : 4.8f,
                ChargeBonus = (unit.chargeBonus > 0f) ? unit.chargeBonus : 15f,
                MaxChargeDamage = (unit.maxChargeDamage > 0f) ? unit.maxChargeDamage : 35f,
                ChargeImpactReady = 1,
                EngagementStartTime = 0f,
                AutoAttackEnabled = unit.autoAttackEnabled ? 1 : 0,
                TargetSquadId = (unit.mySquad != null && unit.mySquad.currentTargetSquad != null) ? unit.mySquad.currentTargetSquad.GetInstanceID() : -1,
                CachedEnemyPos = float3.zero,
                TargetSearchTimer = (float)(entity.Index % 10) * 0.01f
            });

            entityManager.SetComponentData(entity, new UnitSeparationData
            {
                PersonalRadius = isFree ? 1.15f : 1.00f,
                SeparationForce = float3.zero
            });

            unitToEntityMap[unit] = entity;
            entityToUnitMap[entity] = unit;

            return entity;
        }

        public void UnregisterUnitEntity(Unit unit)
        {
            if (unit == null) return;
            if (unitToEntityMap.TryGetValue(unit, out Entity entity))
            {
                if (isInitialized && entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
                unitToEntityMap.Remove(unit);
                entityToUnitMap.Remove(entity);
            }
        }

        /// <summary>
        /// 목표 이동 위치 및 회전각, 명령 상태를 ECS Entity에 실시간 업데이트
        /// </summary>
        public void UpdateEntityTarget(Unit unit, Vector3 destination, Quaternion rotation, UnitCommandState state)
        {
            if (unit == null) return;
            if (!isInitialized) InitializeECS();

            if (unitToEntityMap.TryGetValue(unit, out Entity entity))
            {
                if (entityManager.Exists(entity))
                {
                    var mov = entityManager.GetComponentData<UnitMovementData>(entity);
                    mov.TargetPosition = destination;
                    mov.TargetRotation = rotation;
                    if (state == UnitCommandState.Idle)
                    {
                        mov.CurrentSpeed = 0f;
                    }
                    entityManager.SetComponentData(entity, mov);

                    var combat = entityManager.GetComponentData<UnitCombatData>(entity);
                    combat.CurrentState = (int)state;
                    combat.AutoAttackEnabled = unit.autoAttackEnabled ? 1 : 0;

                    // 정지(Idle) 또는 단순 이동(Move) 시 교전 락 즉시 해제
                    if (state == UnitCommandState.Move || state == UnitCommandState.Idle)
                    {
                        combat.EngagementStartTime = 0f;
                        combat.TargetEntity = Entity.Null;
                        combat.ChargeImpactReady = 0;
                        combat.KnockbackVelocity = float3.zero;
                    }

                    entityManager.SetComponentData(entity, combat);
                }
            }
        }

        /// <summary>
        /// 유닛의 달리기/걷기 속도 및 가속도를 ECS Entity에 실시간 업데이트
        /// </summary>
        public void UpdateUnitSpeed(Unit unit, float speed, float accel)
        {
            if (unit == null) return;
            if (!isInitialized) InitializeECS();

            if (unitToEntityMap.TryGetValue(unit, out Entity entity))
            {
                if (entityManager.Exists(entity))
                {
                    var mov = entityManager.GetComponentData<UnitMovementData>(entity);
                    mov.MoveSpeed = speed;
                    mov.Acceleration = accel;
                    entityManager.SetComponentData(entity, mov);
                }
            }
        }

        /// <summary>
        /// 부대 지휘관이 지정한 목표 적 부대 ID(TargetSquadId)를 소속 부대원 전원에게 일괄 주입 (1:1 정확 매핑)
        /// </summary>
        public void UpdateSquadTargetSquadId(Squad squad, int targetSquadId)
        {
            if (squad == null) return;
            if (!isInitialized) InitializeECS();

            int mySquadId = squad.GetInstanceID();

            // 1. 하이브리드 모드 유닛 갱신
            if (squad.members != null && squad.members.Count > 0)
            {
                for (int i = 0; i < squad.members.Count; i++)
                {
                    Unit u = squad.members[i];
                    if (u != null && unitToEntityMap.TryGetValue(u, out Entity entity))
                    {
                        if (entityManager.Exists(entity))
                        {
                            var combat = entityManager.GetComponentData<UnitCombatData>(entity);
                            combat.TargetSquadId = targetSquadId;
                            entityManager.SetComponentData(entity, combat);
                        }
                    }
                }
            }

            // 2. 순수 ECS 모드 Entity 일괄 갱신 (GameObject가 0개여도 200명 전원 100% 완벽 주입)
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitEntityTag>(),
                ComponentType.ReadWrite<UnitCombatData>()
            );

            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var tag = entityManager.GetComponentData<UnitEntityTag>(entities[i]);
                    if (tag.SquadId == mySquadId && tag.IsAlive == 1)
                    {
                        var combat = entityManager.GetComponentData<UnitCombatData>(entities[i]);
                        combat.TargetSquadId = targetSquadId;
                        entityManager.SetComponentData(entities[i], combat);
                    }
                }
            }
        }

        private void SyncEntitiesToGameObjects()
        {
            foreach (var kvp in unitToEntityMap)
            {
                Unit unit = kvp.Key;
                Entity entity = kvp.Value;

                if (unit != null && entityManager.Exists(entity))
                {
                    var mov = entityManager.GetComponentData<UnitMovementData>(entity);
                    var combat = entityManager.GetComponentData<UnitCombatData>(entity);

                    // 🎯 목표 부대 ID 실시간 동기화
                    if (unit.mySquad != null)
                    {
                        int currentTargetId = (unit.mySquad.currentTargetSquad != null) ? unit.mySquad.currentTargetSquad.GetInstanceID() : -1;
                        if (combat.TargetSquadId != currentTargetId)
                        {
                            combat.TargetSquadId = currentTargetId;
                            entityManager.SetComponentData(entity, combat);
                        }
                    }

                    unit.transform.position = mov.Position;
                    unit.transform.rotation = mov.Rotation;
                    unit.currentHp = combat.CurrentHp;
                    unit.currentState = (UnitCommandState)combat.CurrentState;

                    if (combat.CurrentHp <= 0 && unit.gameObject.activeSelf)
                    {
                        unit.TakeDamage(9999f); // 사망 이벤트 트리거
                    }
                }
            }
        }
    }
}
