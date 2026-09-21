using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// 공간 해시 그리드에 저장할 경량 유닛 정보
    /// </summary>
    public struct EntitySpatialData
    {
        public Entity Entity;
        public float3 Position;
        public int Faction;
        public int IsAlive;
        public int IsFreeUnit;
        public int SquadId;
        public float PersonalRadius;
        public int CurrentState;
    }

    /// <summary>
    /// 부대별 중심 좌표 및 생존자 수, 백병전 교전 인원을 O(1)로 일괄 집계하는 구조체
    /// </summary>
    public struct SquadAggregateData
    {
        public float3 PositionSum;
        public int AliveCount;
        public int MeleeEngagedCount;
    }

    /// <summary>
    /// 2.0m 균일 격자(Uniform Spatial Grid)를 구축하여 O(1) 초고속 인접 유닛 탐색을 지원하는 시스템
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct SpatialHashGridSystem : ISystem
    {
        public const float CELL_SIZE = 2.0f;
        public const float INV_CELL_SIZE = 1.0f / CELL_SIZE;

        public NativeParallelMultiHashMap<int, EntitySpatialData> SpatialMap;
        public NativeParallelMultiHashMap<int, EntitySpatialData> SquadMap;
        public NativeList<EntitySpatialData> AllAliveUnits;
        public NativeParallelHashMap<Entity, EntitySpatialData> AllAliveEntityMap;
        public NativeParallelHashMap<int, SquadAggregateData> SquadAggregates;

        public void OnCreate(ref SystemState state)
        {
            SpatialMap = new NativeParallelMultiHashMap<int, EntitySpatialData>(50000, Allocator.Persistent);
            SquadMap = new NativeParallelMultiHashMap<int, EntitySpatialData>(50000, Allocator.Persistent);
            AllAliveUnits = new NativeList<EntitySpatialData>(5000, Allocator.Persistent);
            AllAliveEntityMap = new NativeParallelHashMap<Entity, EntitySpatialData>(5000, Allocator.Persistent);
            SquadAggregates = new NativeParallelHashMap<int, SquadAggregateData>(128, Allocator.Persistent);
            state.RequireForUpdate<UnitEntityTag>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SpatialMap.IsCreated) SpatialMap.Dispose();
            if (SquadMap.IsCreated) SquadMap.Dispose();
            if (AllAliveUnits.IsCreated) AllAliveUnits.Dispose();
            if (AllAliveEntityMap.IsCreated) AllAliveEntityMap.Dispose();
            if (SquadAggregates.IsCreated) SquadAggregates.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            SpatialMap.Clear();
            SquadMap.Clear();
            AllAliveUnits.Clear();
            AllAliveEntityMap.Clear();
            SquadAggregates.Clear();

            // 1. SpatialMap & SquadMap 동시 병렬 초고속 해싱 구축
            var buildJob = new BuildSpatialGridJob
            {
                SpatialMap = SpatialMap.AsParallelWriter(),
                SquadMap = SquadMap.AsParallelWriter(),
                InvCellSize = INV_CELL_SIZE
            };
            state.Dependency = buildJob.ScheduleParallel(state.Dependency);

            // 2. AllAliveUnits 및 부대별 중심점/인원수를 단일 패스로 100% 누락 없이 안전하게 직렬 적재
            var collectJob = new CollectAllAliveUnitsJob
            {
                AllAliveUnits = AllAliveUnits,
                AllAliveEntityMap = AllAliveEntityMap,
                SquadAggregates = SquadAggregates
            };
            state.Dependency = collectJob.Schedule(state.Dependency);
            state.Dependency.Complete(); // ⚡ 인접 시스템들의 ReadOnly 안전 보장
        }

        /// <summary>
        /// 부대 지휘관(Squad)이 메인 스레드에서 O(1)로 자기 부대의 중심 좌표, 생존 인원수, 교전 인원수를 즉시 조회합니다.
        /// (4,000개 유닛을 풀스캔하는 80,000번의 RPC 병목을 0번으로 완전 제거)
        /// </summary>
        public static bool TryGetSquadAggregateData(int squadId, out float3 visualCenter, out int aliveCount, out int meleeEngagedCount)
        {
            visualCenter = float3.zero;
            aliveCount = 0;
            meleeEngagedCount = 0;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;

            var systemHandle = world.GetExistingSystem<SpatialHashGridSystem>();
            if (systemHandle == SystemHandle.Null) return false;

            ref var spatialGrid = ref world.Unmanaged.GetUnsafeSystemRef<SpatialHashGridSystem>(systemHandle);
            if (spatialGrid.SquadAggregates.IsCreated && spatialGrid.SquadAggregates.TryGetValue(squadId, out var agg))
            {
                aliveCount = agg.AliveCount;
                meleeEngagedCount = agg.MeleeEngagedCount;
                if (aliveCount > 0)
                {
                    visualCenter = agg.PositionSum / aliveCount;
                }
                return true;
            }

            return false;
        }

        public static int HashCoords(int3 coord)
        {
            unchecked
            {
                return (coord.x * 73856093) ^ (coord.z * 19349663);
            }
        }

        public static int GetCellHash(float3 position, float invCellSize)
        {
            int3 coord = new int3((int)math.floor(position.x * invCellSize), 0, (int)math.floor(position.z * invCellSize));
            return HashCoords(coord);
        }
    }

    [BurstCompile]
    public partial struct BuildSpatialGridJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int, EntitySpatialData>.ParallelWriter SpatialMap;
        public NativeParallelMultiHashMap<int, EntitySpatialData>.ParallelWriter SquadMap;
        public float InvCellSize;

        private void Execute(Entity entity, in UnitEntityTag tag, in UnitMovementData movement, in UnitSeparationData separation, in UnitCombatData combat)
        {
            if (tag.IsAlive == 0) return;

            int3 coord = new int3((int)math.floor(movement.Position.x * InvCellSize), 0, (int)math.floor(movement.Position.z * InvCellSize));
            int hash = unchecked((coord.x * 73856093) ^ (coord.z * 19349663));

            var data = new EntitySpatialData
            {
                Entity = entity,
                Position = movement.Position,
                Faction = tag.Faction,
                IsAlive = tag.IsAlive,
                IsFreeUnit = tag.IsFreeUnit,
                SquadId = tag.SquadId,
                PersonalRadius = separation.PersonalRadius,
                CurrentState = combat.CurrentState
            };

            SpatialMap.Add(hash, data);
            
            if (tag.SquadId != -1)
            {
                SquadMap.Add(tag.SquadId, data);
            }
        }
    }

    [BurstCompile]
    public partial struct CollectAllAliveUnitsJob : IJobEntity
    {
        public NativeList<EntitySpatialData> AllAliveUnits;
        public NativeParallelHashMap<Entity, EntitySpatialData> AllAliveEntityMap;
        public NativeParallelHashMap<int, SquadAggregateData> SquadAggregates;

        private void Execute(Entity entity, in UnitEntityTag tag, in UnitMovementData movement, in UnitSeparationData separation, in UnitCombatData combat)
        {
            if (tag.IsAlive == 0) return;

            var spatialData = new EntitySpatialData
            {
                Entity = entity,
                Position = movement.Position,
                Faction = tag.Faction,
                IsAlive = tag.IsAlive,
                IsFreeUnit = tag.IsFreeUnit,
                SquadId = tag.SquadId,
                PersonalRadius = separation.PersonalRadius,
                CurrentState = combat.CurrentState
            };

            AllAliveUnits.Add(spatialData);
            AllAliveEntityMap.TryAdd(entity, spatialData);

            if (tag.SquadId != -1)
            {
                if (SquadAggregates.TryGetValue(tag.SquadId, out var agg))
                {
                    agg.PositionSum += movement.Position;
                    agg.AliveCount++;
                    if (combat.CurrentState == 3) agg.MeleeEngagedCount++;
                    SquadAggregates[tag.SquadId] = agg;
                }
                else
                {
                    SquadAggregates.TryAdd(tag.SquadId, new SquadAggregateData
                    {
                        PositionSum = movement.Position,
                        AliveCount = 1,
                        MeleeEngagedCount = (combat.CurrentState == 3) ? 1 : 0
                    });
                }
            }
        }
    }
}
