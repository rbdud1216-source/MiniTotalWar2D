using UnityEngine;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 통합 수계 지형 유형 (수면 없음, 대양/해안, 스플라인 하천, 전술 수계 구역)
    /// </summary>
    public enum WaterBodyType
    {
        None = 0,
        OpenSea = 1,
        River = 2,
        TacticalZone = 3
    }

    /// <summary>
    /// 특정 월드 좌표에서의 통합 수심, 수면 높이, 법선 및 유속 샘플링 결과
    /// 보병 도하 판정 및 향후 바다/큰 강 위 군함·함대 부력 시뮬레이션에 공용으로 사용됩니다.
    /// </summary>
    public struct WaterSampleResult
    {
        public bool isInWater;
        public WaterBodyType bodyType;
        public float waterSurfaceY;
        public float terrainFloorY;
        public float waterDepth;
        public Vector3 surfaceNormal;
        public Vector3 flowVelocity;
        public bool isPassableForInfantry;
        public bool isNavigableForShip;
    }

    /// <summary>
    /// 바다(TotalWarSeaPlane), 하천(TotalWarRiverSpline), 전술 수계(TacticalWaterZone)를
    /// 단일 인터페이스로 통합 조회하는 수계 쿼리 API입니다.
    /// </summary>
    public static class WorldWaterQueryAPI
    {
        public const float DefaultShipDraftMeters = 1.8f;
        public const float DefaultInfantryWadingMaxDepth = 1.35f;

        /// <summary>
        /// 지정된 월드 XZ 좌표의 수계 정보(수심, 수면 높이, 파도 법선, 유속, 보병 도하/군함 항해 가능 여부)를 종합 산출합니다.
        /// </summary>
        public static WaterSampleResult SampleWaterAt(Vector3 worldPos, float shipDraftMeters = DefaultShipDraftMeters)
        {
            float terrainY = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z);

            WaterSampleResult best = new WaterSampleResult
            {
                isInWater = false,
                bodyType = WaterBodyType.None,
                waterSurfaceY = terrainY,
                terrainFloorY = terrainY,
                waterDepth = 0f,
                surfaceNormal = Vector3.up,
                flowVelocity = Vector3.zero,
                isPassableForInfantry = true,
                isNavigableForShip = false
            };

            // 1. 스플라인 하천 조회 (가장 높은 우선순위: 고지대 계곡/강줄기)
            var rivers = TotalWarRiverSpline.ActiveRivers;
            if (rivers != null)
            {
                for (int i = 0; i < rivers.Count; i++)
                {
                    TotalWarRiverSpline river = rivers[i];
                    if (river == null || !river.isActiveAndEnabled) continue;

                    if (river.TrySampleRiverAt(worldPos, out float riverSurfaceY, out float riverDepth, out Vector3 flowDir))
                    {
                        float actualDepth = Mathf.Max(riverDepth, riverSurfaceY - terrainY);
                        if (!best.isInWater || riverSurfaceY > best.waterSurfaceY)
                        {
                            best.isInWater = actualDepth > 0.02f;
                            best.bodyType = WaterBodyType.River;
                            best.waterSurfaceY = riverSurfaceY;
                            best.waterDepth = Mathf.Max(0f, actualDepth);
                            best.surfaceNormal = Vector3.up;
                            best.flowVelocity = flowDir * 1.2f;
                            best.isPassableForInfantry = best.waterDepth <= DefaultInfantryWadingMaxDepth;
                            best.isNavigableForShip = best.waterDepth >= shipDraftMeters;
                        }
                    }
                }
            }

            // 2. 전술 수계 존 (여울목, 늪지, 연못, 웅덩이) 조회
            var zones = TacticalWaterZone.ActiveZones;
            if (zones != null)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    TacticalWaterZone zone = zones[i];
                    if (zone == null || !zone.isActiveAndEnabled) continue;

                    if (zone.ContainsXZ(worldPos))
                    {
                        float zoneSurfaceY = zone.transform.position.y + zone.surfaceYOffset;
                        float zoneDepth = Mathf.Max(zone.waterDepth, zoneSurfaceY - terrainY);

                        if (!best.isInWater || zoneSurfaceY > best.waterSurfaceY)
                        {
                            best.isInWater = zoneDepth > 0.01f;
                            best.bodyType = WaterBodyType.TacticalZone;
                            best.waterSurfaceY = zoneSurfaceY;
                            best.waterDepth = Mathf.Max(0f, zoneDepth);
                            best.surfaceNormal = Vector3.up;
                            best.flowVelocity = Vector3.zero;
                            best.isPassableForInfantry = zone.isPassable && best.waterDepth <= DefaultInfantryWadingMaxDepth;
                            best.isNavigableForShip = (zone.zoneType == TacticalWaterZone.WaterZoneType.Pond) && best.waterDepth >= shipDraftMeters;
                        }
                    }
                }
            }

            // 3. 전역 대양 및 해안 수면(TotalWarSeaPlane) Gerstner 파도 높이 조회
            TotalWarSeaPlane sea = TotalWarSeaPlane.Instance;
            if (sea != null && sea.isActiveAndEnabled)
            {
                if (sea.SampleWaveHeightAndNormal(worldPos, out float seaSurfaceY, out Vector3 seaNormal))
                {
                    float seaDepth = seaSurfaceY - terrainY;
                    bool hasTerrainBelow = TerrainHeightManager.HasInstance
                        && TerrainHeightManager.Instance.IsDataReady
                        && TerrainHeightManager.Instance.HeightData.ContainsXZ(worldPos.x, worldPos.z);

                    // 터레인 외곽 맵 밖 대양은 충분히 깊은 심해(40m)로 판별해 함대 기동 허용
                    if (!hasTerrainBelow)
                    {
                        seaDepth = Mathf.Max(seaDepth, 40f);
                    }

                    if (seaDepth > 0.02f && (!best.isInWater || seaSurfaceY >= best.waterSurfaceY))
                    {
                        best.isInWater = true;
                        best.bodyType = WaterBodyType.OpenSea;
                        best.waterSurfaceY = seaSurfaceY;
                        best.waterDepth = seaDepth;
                        best.surfaceNormal = seaNormal;
                        best.flowVelocity = Vector3.zero;
                        best.isPassableForInfantry = seaDepth <= DefaultInfantryWadingMaxDepth;
                        best.isNavigableForShip = seaDepth >= shipDraftMeters;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// 지정된 월드 좌표의 수심(미터)을 반환합니다. 물 밖이면 0을 반환합니다.
        /// </summary>
        public static float GetWaterDepthAt(Vector3 worldPos)
        {
            return SampleWaterAt(worldPos).waterDepth;
        }

        /// <summary>
        /// 지정된 월드 좌표의 수면 Y 높이를 반환합니다. 수계가 없으면 지형 높이를 반환합니다.
        /// </summary>
        public static float GetWaterSurfaceHeightAt(Vector3 worldPos)
        {
            return SampleWaterAt(worldPos).waterSurfaceY;
        }

        /// <summary>
        /// 군함/선박의 부력 및 기울기 시뮬레이션을 위한 수면 높이와 파도 법선 벡터를 조회합니다.
        /// </summary>
        public static bool SampleWaveHeightAndNormal(Vector3 worldPos, out float surfaceY, out Vector3 normal)
        {
            WaterSampleResult sample = SampleWaterAt(worldPos);
            surfaceY = sample.waterSurfaceY;
            normal = sample.surfaceNormal;
            return sample.isInWater;
        }

        /// <summary>
        /// 지정된 흘수(shipDraftMeters)를 가진 선박/군함이 해당 좌표에서 좌초 없이 항해 가능한지 반환합니다.
        /// </summary>
        public static bool IsNavigableForShip(Vector3 worldPos, float shipDraftMeters = DefaultShipDraftMeters)
        {
            return SampleWaterAt(worldPos, shipDraftMeters).isNavigableForShip;
        }

        /// <summary>
        /// 지정된 월드 좌표의 물 유속 벡터를 반환합니다.
        /// </summary>
        public static Vector3 GetWaterFlowVelocityAt(Vector3 worldPos)
        {
            return SampleWaterAt(worldPos).flowVelocity;
        }
    }
}
