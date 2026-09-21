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
        private float searchTimer;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitEntityTag>();
            searchTimer = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            searchTimer += deltaTime;

            // 🎯 [최적화] 매 프레임 30m 반경 961개 셀 탐색을 중단하고, 0.1초(10Hz)마다 1번씩만 레이더 탐색 스케줄링!
            if (searchTimer < 0.1f)
            {
                return;
            }
            searchTimer -= 0.1f;

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
            // 🎯 [최적화] 사망한 유닛 또는 목표 부대(TargetSquadId != -1)가 이미 지정된 유닛은 
            // UnitCombatAndMovementSystem의 1단계 SquadMap 추격을 수행하므로 961개 셀 레이더 탐색 100% 즉시 생략!
            if (tag.IsAlive == 0 || combat.TargetSquadId != -1)
            {
                combat.RadarTargetEntity = Entity.Null;
                return;
            }

            float3 myPos = movement.Position;
            int myFaction = tag.Faction;
            Entity bestTarget = Entity.Null;
            
            float radarRange = 14.0f; // 🎯 목표 부대 미지정 유닛의 인접 적군 감지 반경 (14m)
            float minSqrDist = radarRange * radarRange; 
            
            int searchCells = 7; // 7 * 2.0m = 14m (15x15 = 225개 셀로 76% 대폭 절감)
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

                            float3 diff = other.Position - myPos;
                            diff.y = 0f;
                            float sqrDist = math.lengthsq(diff);

                            // 🛡️ [측면 옆 부대 오탐색 100% 차단]:
                            // 목표 부대가 지정되지 않은 대기/이동 중에는 시선 정면(forwardDot > 0.25f)의 적만 탐색!
                            if (tag.IsFreeUnit == 0 && sqrDist > 0.001f)
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
