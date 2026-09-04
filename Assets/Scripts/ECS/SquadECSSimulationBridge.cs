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

            // 2. ⚡ 순수 ECS 모드 매 프레임 부대 상태(TargetSquadId, MoveSpeed, CommandState) 실시간 완벽 동기화 (오브젝트 모드와 100% 동일화)
            if (BattleManager.Instance != null && BattleManager.Instance.usePureECS)
            {
                var squads = BattleManager.Instance.GetAllSquads();
                if (squads != null && squads.Count > 0)
                {
                    var query = entityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<UnitEntityTag>(),
                        ComponentType.ReadWrite<UnitCombatData>(),
                        ComponentType.ReadWrite<UnitMovementData>()
                    );

                    using (var entities = query.ToEntityArray(Allocator.Temp))
                    {
                        for (int s = 0; s < squads.Count; s++)
                        {
                            Squad squad = squads[s];
                            if (squad == null) continue;

                            int mySquadId = squad.GetInstanceID();

                            // 🛡️ [부대 교전 연동 (Combat Cascade) - 다수결 및 정면 지향 검증]:
                            // 부대가 이미 AttackMove로 전군 돌격 중이 아닌 경우(대기/방어/정리 상태):
                            // 부대원 중 일부라도 적과 백병전(MeleeEngaged)에 돌입하면 지휘관이 즉시 전군 돌격(CommandAttackSquad)을 발동!
                            // (후열 병사들이 슬롯에 멍하니 정렬해 있는 비현실적 현상 100% 원천 차단)
                            if (squad.currentCommandState != UnitCommandState.AttackMove && squad.currentCommandState != UnitCommandState.Move)
                            {
                                int engagedEnemySquadId = -1;
                                int maxEngagedCount = 0;
                                System.Collections.Generic.Dictionary<int, int> enemyCounts = new System.Collections.Generic.Dictionary<int, int>();

                                for (int i = 0; i < entities.Length; i++)
                                {
                                    var tag = entityManager.GetComponentData<UnitEntityTag>(entities[i]);
                                    if (tag.SquadId == mySquadId && tag.IsAlive == 1)
                                    {
                                        var cbt = entityManager.GetComponentData<UnitCombatData>(entities[i]);
                                        if (cbt.CurrentState == 3 && cbt.TargetEntity != Unity.Entities.Entity.Null)
                                        {
                                            if (entityManager.Exists(cbt.TargetEntity) && entityManager.HasComponent<UnitEntityTag>(cbt.TargetEntity))
                                            {
                                                int eSquadId = entityManager.GetComponentData<UnitEntityTag>(cbt.TargetEntity).SquadId;
                                                if (eSquadId != -1 && eSquadId != mySquadId)
                                                {
                                                    if (!enemyCounts.ContainsKey(eSquadId))
                                                        enemyCounts[eSquadId] = 0;
                                                    enemyCounts[eSquadId]++;

                                                    if (enemyCounts[eSquadId] > maxEngagedCount)
                                                    {
                                                        maxEngagedCount = enemyCounts[eSquadId];
                                                        engagedEnemySquadId = eSquadId;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // 🚨 핵심: 부대에 이미 유효한 지정 목표 적 부대가 있다면, 외곽 병사가 옆 부대와 스쳤더라도
                                // 절대로 그 옆 부대로 지휘관 타겟을 바꾸지 않고 기존 정면 목표 부대를 100% 끝까지 고수합니다!
                                if (squad.currentTargetSquad != null && squad.currentTargetSquad.MemberCount > 0)
                                {
                                    engagedEnemySquadId = squad.currentTargetSquad.GetInstanceID();
                                }

                                if (engagedEnemySquadId != -1)
                                {
                                    Squad enemySquad = null;
                                    for (int k = 0; k < squads.Count; k++)
                                    {
                                        if (squads[k] != null && squads[k].GetInstanceID() == engagedEnemySquadId)
                                        {
                                            enemySquad = squads[k];
                                            break;
                                        }
                                    }

                                    if (enemySquad != null)
                                    {
                                        squad.CommandAttackSquad(enemySquad);
                                    }
                                }
                            }

                            int targetSquadId = (squad.currentTargetSquad != null && squad.currentTargetSquad.MemberCount > 0) 
                                ? squad.currentTargetSquad.GetInstanceID() 
                                : -1;
                            int cmdState = (int)squad.currentCommandState;
                            float spd = squad.targetSpeed > 0 ? squad.targetSpeed : (squad.isRunning ? 2.8f : 1.2f);
                            float accel = squad.isRunning ? 8.0f : 5.5f;

                            for (int i = 0; i < entities.Length; i++)
                            {
                                var tag = entityManager.GetComponentData<UnitEntityTag>(entities[i]);
                                if (tag.SquadId == mySquadId && tag.IsAlive == 1)
                                {
                                    var combat = entityManager.GetComponentData<UnitCombatData>(entities[i]);
                                    var mov = entityManager.GetComponentData<UnitMovementData>(entities[i]);

                                    bool needUpdateCombat = false;

                                    // [수정] TargetSquadId 불일치 시 항상 강제 갱신
                                    if (combat.TargetSquadId != targetSquadId)
                                    {
                                        combat.TargetSquadId = targetSquadId;
                                        needUpdateCombat = true;
                                    }

                                    // [수정] 부대 명령 상태 동기화 (기존 조건보다 완화하여 누락 방지)
                                    if (cmdState == 1) // Move: 즉시 교전 해제
                                    {
                                        if (combat.CurrentState != 1)
                                        {
                                            combat.CurrentState = 1;
                                            needUpdateCombat = true;
                                        }
                                    }
                                    else if (cmdState == 2) // AttackMove: Idle(0) 포함 MeleeEngaged(3) 이외에는 모두 2로 전환
                                    {
                                        if (combat.CurrentState != 2 && combat.CurrentState != 3)
                                        {
                                            combat.CurrentState = 2;
                                            needUpdateCombat = true;
                                        }
                                    }
                                    else if (cmdState == 0) // Idle: 교전 상태가 아닌 경우에만 0으로 복귀
                                    {
                                        if (combat.CurrentState != 0 && combat.CurrentState != 3)
                                        {
                                            combat.CurrentState = 0;
                                            needUpdateCombat = true;
                                        }
                                    }

                                    if (needUpdateCombat)
                                    {
                                        entityManager.SetComponentData(entities[i], combat);
                                    }

                                    // [수정] AttackMove(2) 또는 MeleeEngaged(3) 상태에서는 MoveSpeed를 강제 덮어쓰지 않음
                                    // (이 두 상태에서는 UnitCombatAndMovementSystem이 ChargeSpeed를 직접 제어함)
                                    bool isEngaged = (combat.CurrentState == 2 || combat.CurrentState == 3);
                                    if (!isEngaged)
                                    {
                                        mov.MoveSpeed = spd;
                                        mov.Acceleration = accel;
                                        entityManager.SetComponentData(entities[i], mov);
                                    }
                                    else
                                    {
                                        // 가속도만 업데이트 (돌격 속도를 UnitCombatAndMovementSystem이 다루리도 Acceleration은 업데이트 필요)
                                        mov.Acceleration = accel;
                                        entityManager.SetComponentData(entities[i], mov);
                                    }
                                }
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
                AttackRange = 0.8f,
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
                TargetSquadId = (unit.mySquad != null && unit.mySquad.currentTargetSquad != null) ? unit.mySquad.currentTargetSquad.GetInstanceID() : -1
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
