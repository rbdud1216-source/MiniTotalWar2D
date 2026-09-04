using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// Spatial Grid를 통해 주변 적군 유닛을 O(1) 초고속으로 타겟팅하는 시스템
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitSeparationSystem))]
    public partial struct UnitTargetSearchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitEntityTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var spatialSystem = ref state.WorldUnmanaged.GetExistingSystemState<SpatialHashGridSystem>();
            var spatialGrid = state.WorldUnmanaged.GetUnsafeSystemRef<SpatialHashGridSystem>(spatialSystem.SystemHandle);

            var searchJob = new SearchEnemyTargetGridJob
            {
                SpatialMap = spatialGrid.SpatialMap,
                InvCellSize = SpatialHashGridSystem.INV_CELL_SIZE
            };

            state.Dependency = searchJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct SearchEnemyTargetGridJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, EntitySpatialData> SpatialMap;
        public float InvCellSize;

        private void Execute(Entity entity, in UnitEntityTag tag, in UnitMovementData movement, ref UnitCombatData combat)
        {
            if (tag.IsAlive == 0)
            {
                combat.RadarTargetEntity = Entity.Null;
                return;
            }

            float3 myPos = movement.Position;
            int myFaction = tag.Faction;
            Entity bestTarget = Entity.Null;
            
            int targetSquadId = combat.TargetSquadId;
            float radarRange = 30.0f; // 🎯 후열 5~6열(15m 후방) 병사도 전방 적군을 100% 감지하도록 30m로 확장!
            float minSqrDist = radarRange * radarRange; 
            
            int searchCells = 15; // 15 * 2.0m = 30m
            int3 myCoord = new int3((int)math.floor(myPos.x * InvCellSize), 0, (int)math.floor(myPos.z * InvCellSize));

            for (int dx = -searchCells; dx <= searchCells; dx++)
            {
                for (int dz = -searchCells; dz <= searchCells; dz++)
                {
                    int neighborHash = SpatialHashGridSystem.HashCoords(myCoord + new int3(dx, 0, dz));
                    if (SpatialMap.TryGetFirstValue(neighborHash, out EntitySpatialData other, out var it))
                    {
                        do
                        {
                            if (other.Entity == entity || other.Faction == myFaction) continue;

                            // 🎯 [타겟 이탈 방지] 지휘관이 목표 부대(targetSquadId != -1)를 지정한 상태라면, 다른 부대원은 레이더 타겟에서 완전히 제외!
                            if (targetSquadId != -1 && other.SquadId != targetSquadId) continue;

                            float3 diff = other.Position - myPos;
                            diff.y = 0f;
                            float sqrDist = math.lengthsq(diff);

                            // 🛡️ [측면 옆 부대 오탐색 100% 차단]:
                            // 목표 부대가 지정되지 않은 대기/이동 중에는 시선 정면(forwardDot > 0.25f)의 적만 탐색!
                            // 좌우 측면에 있는 인접 부대원을 레이더 타겟으로 잡는 현상을 원천 방지합니다.
                            if (targetSquadId == -1 && tag.IsFreeUnit == 0 && sqrDist > 0.001f)
                            {
                                float3 myFwd = math.mul(movement.Rotation, new float3(0, 0, 1));
                                float forwardDot = math.dot(myFwd, diff / math.sqrt(sqrDist));
                                if (forwardDot < 0.25f) continue;
                            }

                            if (sqrDist < minSqrDist)
                            {
                                minSqrDist = sqrDist;
                                bestTarget = other.Entity;
                            }
                        } while (SpatialMap.TryGetNextValue(out other, ref it));
                    }
                }
            }

            combat.RadarTargetEntity = bestTarget;
        }
    }
}
