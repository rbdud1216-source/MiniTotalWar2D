using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// Spatial Grid를 기반으로 O(1) 인접 9개 셀을 조회하여 겹침 0% 척력(Separation)을 병렬 계산하는 시스템
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialHashGridSystem))]
    public partial struct UnitSeparationSystem : ISystem
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

            var separationJob = new CalculateSeparationGridJob
            {
                SpatialMap = spatialGrid.SpatialMap,
                InvCellSize = SpatialHashGridSystem.INV_CELL_SIZE
            };

            state.Dependency = separationJob.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct CalculateSeparationGridJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, EntitySpatialData> SpatialMap;
        public float InvCellSize;

        private void Execute(Entity entity, in UnitEntityTag tag, in UnitMovementData movement, in UnitCombatData combat, ref UnitSeparationData separation)
        {
            if (tag.IsAlive == 0)
            {
                separation.SeparationForce = float3.zero;
                return;
            }

            float3 myPos = movement.Position;
            float myRadius = (tag.IsFreeUnit == 1) ? 1.15f : 1.00f;
            float3 totalSep = float3.zero;

            int3 centerCoord = new int3((int)math.floor(myPos.x * InvCellSize), 0, (int)math.floor(myPos.z * InvCellSize));

            // 인접 9개 셀 (3x3) 순회 (O(1) 초고속 탐색)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int3 neighborCoord = centerCoord + new int3(dx, 0, dz);
                    int hash = unchecked((neighborCoord.x * 73856093) ^ (neighborCoord.z * 19349663));

                    if (SpatialMap.TryGetFirstValue(hash, out EntitySpatialData other, out var iterator))
                    {
                        do
                        {
                            if (other.Entity == entity || other.IsAlive == 0) continue;

                            float3 diff = myPos - other.Position;
                            diff.y = 0f;
                            float distSqr = math.lengthsq(diff);

                            bool isEnemy = (other.Faction != tag.Faction);
                            bool isCombatSqueezing = (!isEnemy && combat.CurrentState >= 2 && other.CurrentState >= 2);
                            bool isSameSquad = (tag.SquadId != -1 && other.SquadId == tag.SquadId);
                            float targetRadius;

                            if (isCombatSqueezing)
                            {
                                targetRadius = 0.7f; // 💥 교전/돌격 중 아군 간 반경 축소하여 빈틈 돌파!
                            }
                            else if (isSameSquad)
                            {
                                targetRadius = 0.65f; // 동일 부대 평시 행군 간 완화
                            }
                            else
                            {
                                float otherRadius = (other.IsFreeUnit == 1) ? 1.15f : 1.00f;
                                targetRadius = (myRadius + otherRadius) * 0.5f;
                            }

                            if (distSqr < targetRadius * targetRadius)
                            {
                                float dist = math.sqrt(distSqr);
                                if (dist < 0.001f)
                                {
                                    float angle = ((entity.Index * 37 + other.Entity.Index * 17) % 360) * (math.PI / 180f);
                                    diff = new float3(math.cos(angle) * 0.05f, 0f, math.sin(angle) * 0.05f);
                                    dist = 0.05f;
                                }

                                float weight = (targetRadius - dist) / targetRadius;
                                float overlap = targetRadius - dist;
                                float penetrationBoost = 1.3f + (overlap * overlap * 25.0f);
                                totalSep += (diff / dist) * (weight * penetrationBoost);
                            }
                        }
                        while (SpatialMap.TryGetNextValue(out other, ref iterator));
                    }
                }
            }

            // 🛡️ [양 날개 외곽 유닛의 외측 횡방향 퍼짐 완화]:
            // 횡대 외곽 병사(Col <= 1 또는 Col >= 18)가 안쪽 아군들로부터 일방적인 외측 척력을 받아
            // 옆 부대 쪽으로 2~4m 벌어지며 침범하는 현상을 방지하기 위해 외측 횡방향 척력을 감쇄합니다.
            if (tag.IsFreeUnit == 0 && tag.SquadId != -1)
            {
                float3 myRight = math.mul(movement.Rotation, new float3(1, 0, 0));
                float lateralSep = math.dot(totalSep, myRight);

                // 좌측 날개(Col <= 1)가 왼쪽(바깥쪽, lateralSep < 0)으로 밀릴 때 감쇄
                if (tag.Col <= 1 && lateralSep < 0f)
                {
                    totalSep -= myRight * (lateralSep * 0.65f);
                }
                // 우측 날개(Col >= 18)가 오른쪽(바깥쪽, lateralSep > 0)으로 밀릴 때 감쇄
                else if (tag.Col >= 18 && lateralSep > 0f)
                {
                    totalSep -= myRight * (lateralSep * 0.65f);
                }
            }

            // 척력 과도 누적 방지 (최대 5.0f 클램프)
            if (math.lengthsq(totalSep) > 25.0f)
            {
                totalSep = math.normalize(totalSep) * 5.0f;
            }

            separation.SeparationForce = totalSep;
        }
    }
}
