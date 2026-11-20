using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 전역 연속 좌표 (u, v) 또는 절대 미터 좌표에서 샘플링된 지형 · 수계 · 기후 상태
    /// </summary>
    public struct GlobalSurfaceSample
    {
        public float heightMeters;
        public float waterDepthMeters;
        public ContinentClimateId climate;
        public bool isOcean;
        public float riverStrength;
        public float forestDensity;
    }

    /// <summary>
    /// 대륙 전체(114km 이상 풀 스케일 포함)의 임의 섹터 (sectorX, sectorZ)를
    /// WorldMetaData + 초경량 WorldSourceCacheData + MasterNetworkData로부터
    /// 런타임에 밀리초 단위로 즉석 합성(On-the-Fly Synthesis)하는 공용 엔진입니다.
    /// 전 섹터를 미리 디스크에 굽지 않아도, 전투 중인 3x3 창 영역만 즉석 생성하며 경계 이음새(Seam)가 0%입니다.
    /// </summary>
    public static class ContinentSectorSynthesizer
    {
        /// <summary>
        /// 대륙 정규화 좌표 (u, v in [0..1])에서의 지형 고도, 수심, 기후, 강줄기 및 자연 숲 밀도를 계산합니다.
        /// 인접 섹터 경계 1픽셀이 완전히 동일한 전역 미터 좌표를 입력받으므로 타일 경계 오차가 0.000m입니다.
        /// </summary>
        public static GlobalSurfaceSample EvaluateGlobalSample(
            float u,
            float v,
            WorldMetaData meta,
            WorldSourceCacheData cache,
            bool includeTacticalDetail = true)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            float totalW = Mathf.Max(512f, meta.TotalWidthMeters);
            float totalD = Mathf.Max(512f, meta.TotalDepthMeters);
            float absX = u * totalW;
            float absZ = v * totalD;

            int xmlW = (cache != null && cache.xmlW > 1) ? cache.xmlW : 224;
            int xmlH = (cache != null && cache.xmlH > 1) ? cache.xmlH : 176;

            // XML Y=0은 북쪽이므로 v(0=남, 1=북)에 대해 반전 매핑
            float fx = u * (xmlW - 1);
            float fz = (1f - v) * (xmlH - 1);
            int ix = Mathf.Clamp(Mathf.RoundToInt(fx), 0, xmlW - 1);
            int iy = Mathf.Clamp(Mathf.RoundToInt(fz), 0, xmlH - 1);
            int cellIdx = iy * xmlW + ix;

            float coastDist = 10f;
            float mtnDepth = 0f;
            bool isOcean = false;

            if (cache != null && cache.cellKinds != null && cache.cellKinds.Length >= xmlW * xmlH)
            {
                coastDist = SampleBilinearShortQ16(cache.coastDistanceQ16, xmlW, xmlH, fx, fz);
                mtnDepth = SampleBilinearByteRaw(cache.mountainDepth8, xmlW, xmlH, fx, fz);
                isOcean = (cache.cellKinds[cellIdx] == 0) || coastDist < -0.25f;
            }
            else
            {
                isOcean = (u > 0.75f || v < 0.12f);
                coastDist = isOcean ? -5f : 15f;
            }

            // 강줄기(lf_sea_height_map 기반)는 py=0이 남쪽이므로 반전 없이 v로 직접 매핑
            float riverStrength = 0f;
            if (cache != null && cache.riverStrength8 != null && cache.riverW > 1 && cache.riverH > 1)
            {
                float sx = u * (cache.riverW - 1);
                float sy = v * (cache.riverH - 1);
                riverStrength = SampleBilinearByteNorm(cache.riverStrength8, cache.riverW, cache.riverH, sx, sy);
            }

            // 매크로 대륙 노이즈(저주파: 캠페인 지도 앨리어싱 방지) + 전술 미터 노이즈(고주파: 3D 전장 기복 생성)
            float seedOffX = (meta.seed * 17.31f) % 1000f;
            float seedOffZ = (meta.seed * 31.73f) % 1000f;
            float mu = u * 13.5f + seedOffX * 0.1f;
            float mv = v * 10.5f + seedOffZ * 0.1f;
            float fbmMacro = FractalNoise(mu, mv, 3);
            float macroRidge = 1f - Mathf.Abs(Mathf.PerlinNoise(mu * 1.45f + 13.7f, mv * 1.45f + 7.3f) * 2f - 1f);

            float fbmHills = fbmMacro;
            float fbmDetail = 0.5f;
            float ridge = macroRidge;

            if (includeTacticalDetail)
            {
                float nxMetric = absX * 0.0022f + seedOffX;
                float nzMetric = absZ * 0.0022f + seedOffZ;
                fbmHills = Mathf.Lerp(fbmMacro, FractalNoise(nxMetric, nzMetric, 3), 0.65f);
                fbmDetail = FractalNoise(absX * 0.0095f + seedOffX, absZ * 0.0095f + seedOffZ, 2);
                float metricRidge = 1f - Mathf.Abs(Mathf.PerlinNoise(nxMetric * 1.15f, nzMetric * 1.15f) * 2f - 1f);
                ridge = Mathf.Lerp(macroRidge, metricRidge, 0.55f);
            }

            float seaY = meta.seaLevelY;
            float height;
            float waterDepth = 0f;

            if (isOcean)
            {
                float depthFactor = Mathf.Clamp(-coastDist * 0.35f, 0.5f, 18.0f);
                height = seaY - depthFactor - (1f - fbmHills) * 1.5f;
                height = Mathf.Clamp(height, 1.0f, seaY - 0.35f);
                waterDepth = Mathf.Max(0f, seaY - height);
            }
            else
            {
                float coastalRise = Mathf.Clamp01(coastDist * 0.08f) * 6.0f;
                float mtnMask = Mathf.Clamp01(mtnDepth * 0.055f);
                float mtnElevation = mtnMask * (24f + 46f * ridge * ridge + 14f * Mathf.Clamp(mtnDepth * 0.02f, 0f, 1.5f));
                float plainElevation = 2.0f + coastalRise + fbmHills * 12.0f + fbmDetail * 3.8f;

                height = seaY + plainElevation + mtnElevation;

                if (riverStrength > 0.08f)
                {
                    float carve = Mathf.Clamp01((riverStrength - 0.08f) * 1.3f);
                    float targetBedY = seaY - Mathf.Lerp(0.4f, 3.4f, carve);
                    float riverBlend = Mathf.Clamp01(1f - mtnMask * 0.65f);
                    height = Mathf.Lerp(
                        height,
                        Mathf.Min(height - 3.6f * carve, targetBedY + 0.8f * (1f - carve)),
                        carve * riverBlend * 0.85f);

                    waterDepth = (height < seaY) ? (seaY - height) : Mathf.Max(waterDepth, carve * 2.5f);
                }

                height = Mathf.Clamp(height, seaY - 4f, meta.maxTerrainHeight - 2f);
            }

            ContinentClimateId climate = EvaluateClimateAtUV(u, v, height, seaY, meta.seed);

            // 숲 밀도: 캠페인 텍스처(includeTacticalDetail=false)에서는 저주파 뭉개짐 없는 매크로 분포, 3D 전장에서는 전술 숲 군락 적용
            float forestDensity = 0f;
            if (!isOcean && riverStrength < 0.22f && height >= seaY + 1.2f && height <= seaY + 62f)
            {
                float climateBias = 0.55f;
                if (climate == ContinentClimateId.Arid) climateBias = 0.18f;
                else if (climate == ContinentClimateId.Nanman) climateBias = 0.86f;
                else if (climate == ContinentClimateId.Subtropical) climateBias = 0.72f;
                else if (climate == ContinentClimateId.Cold) climateBias = 0.42f;

                float forestNoise = includeTacticalDetail
                    ? FractalNoise(absX * 0.0052f + seedOffX * 1.7f, absZ * 0.0052f + seedOffZ * 1.3f, 2)
                    : FractalNoise(u * 18.0f + seedOffX * 0.17f, v * 14.0f + seedOffZ * 0.13f, 2);

                float threshold = Mathf.Lerp(0.68f, 0.44f, climateBias);
                if (forestNoise > threshold)
                {
                    forestDensity = Mathf.Clamp01((forestNoise - threshold) / (1f - threshold));
                }
            }

            return new GlobalSurfaceSample
            {
                heightMeters = height,
                waterDepthMeters = waterDepth,
                climate = climate,
                isOcean = isOcean,
                riverStrength = riverStrength,
                forestDensity = forestDensity
            };
        }

        private static void GetWarpedClimateUV(float u, float v, int seed, out float wu, out float wv)
        {
            float sx = (seed * 0.137f) % 100f;
            float sz = (seed * 0.293f) % 100f;
            float warpU = (FractalNoise(u * 4.8f + sx, v * 4.8f + sz, 2) - 0.5f) * 0.11f;
            float warpV = (FractalNoise(u * 4.8f + sx + 41.3f, v * 4.8f + sz + 29.7f, 2) - 0.5f) * 0.11f;
            wu = Mathf.Clamp01(u + warpU);
            wv = Mathf.Clamp01(v + warpV);
        }

        public static ContinentClimateId EvaluateClimateAtUV(float u, float v, float heightMeters, float seaLevelY, int seed = 190)
        {
            GetWarpedClimateUV(u, v, seed, out float wu, out float wv);
            if (wv > 0.72f || heightMeters > seaLevelY + 68f) return ContinentClimateId.Cold;
            if (wu < 0.38f && wv > 0.48f) return ContinentClimateId.Arid;
            if (wu < 0.45f && wv < 0.30f) return ContinentClimateId.Nanman;
            if (wv < 0.45f && wu >= 0.40f) return ContinentClimateId.Subtropical;
            return ContinentClimateId.Temperate;
        }

        /// <summary>
        /// 직선 사각형 경계 없이 5대 기후대 색상이 유기적으로 섞이는 캠페인 알베도 기본 색상을 계산합니다.
        /// </summary>
        public static Color EvaluateBlendedClimateColor(float u, float v, float heightMeters, float seaLevelY, int seed = 190)
        {
            GetWarpedClimateUV(u, v, seed, out float wu, out float wv);

            Color temperateCol = new Color(0.39f, 0.53f, 0.26f);
            Color aridCol = new Color(0.69f, 0.58f, 0.40f);
            Color nanmanCol = new Color(0.20f, 0.40f, 0.19f);
            Color subtropicalCol = new Color(0.28f, 0.49f, 0.23f);
            Color coldCol = new Color(0.75f, 0.79f, 0.82f);

            float subtropicalBlend = Mathf.SmoothStep(0.50f, 0.39f, wv) * Mathf.SmoothStep(0.34f, 0.45f, wu);
            Color baseCol = Color.Lerp(temperateCol, subtropicalCol, subtropicalBlend);

            float nanmanBlend = Mathf.SmoothStep(0.34f, 0.25f, wv) * Mathf.SmoothStep(0.50f, 0.39f, wu);
            baseCol = Color.Lerp(baseCol, nanmanCol, nanmanBlend);

            float aridBlend = Mathf.SmoothStep(0.42f, 0.33f, wu) * Mathf.SmoothStep(0.43f, 0.53f, wv);
            baseCol = Color.Lerp(baseCol, aridCol, aridBlend);

            float coldLatBlend = Mathf.SmoothStep(0.67f, 0.76f, wv);
            float coldAltBlend = Mathf.SmoothStep(seaLevelY + 58f, seaLevelY + 76f, heightMeters);
            baseCol = Color.Lerp(baseCol, coldCol, Mathf.Max(coldLatBlend, coldAltBlend));

            return baseCol;
        }

        /// <summary>
        /// 커스텀 섹터 파일이 디스크에 있으면 우선 로드하고, 없으면 즉석 합성으로 섹터 데이터를 반환합니다.
        /// </summary>
        public static SectorBinaryData LoadOrSynthesizeSector(
            string worldFolder,
            WorldMetaData meta,
            WorldSourceCacheData cache,
            MasterNetworkData networks,
            int sectorX,
            int sectorZ)
        {
            if (!string.IsNullOrEmpty(worldFolder))
            {
                SectorBinaryData onDisk = SectorBinaryIO.LoadSector(worldFolder, sectorX, sectorZ);
                if (onDisk != null) return onDisk;
            }

            return SynthesizeSector(meta, cache, networks, sectorX, sectorZ);
        }

        /// <summary>
        /// 지정된 섹터 (sx, sz)의 16-bit 높이맵, 5슬롯 스플랫(정밀 도로 스플라인 반영), 수심, 기후 플래그를 메모리에서 즉석 생성합니다.
        /// </summary>
        public static SectorBinaryData SynthesizeSector(
            WorldMetaData meta,
            WorldSourceCacheData cache,
            MasterNetworkData networks,
            int sx,
            int sz)
        {
            int hRes = meta.heightmapResolution;
            int sRes = meta.alphamapResolution;
            float sectorSize = meta.sectorSizeMeters;
            float invHResM1 = 1f / Mathf.Max(1, hRes - 1);
            float invSResM1 = 1f / Mathf.Max(1, sRes - 1);
            float totalW = Mathf.Max(sectorSize, meta.TotalWidthMeters);
            float totalD = Mathf.Max(sectorSize, meta.TotalDepthMeters);

            SectorBinaryData sector = new SectorBinaryData
            {
                sectorX = sx,
                sectorZ = sz,
                heightRes = hRes,
                splatRes = sRes,
                heights16 = new ushort[hRes * hRes],
                splatWeights5 = new byte[sRes * sRes * 5],
                waterDepth8 = new byte[sRes * sRes],
                biomeAndFlags = new byte[sRes * sRes],
                vegetationSeed = meta.seed * 7919 + sx * 131 + sz * 313,
                minHeight = float.MaxValue,
                maxHeight = float.MinValue
            };

            float sectorOriginX = sx * sectorSize;
            float sectorOriginZ = sz * sectorSize;
            float invMaxH = 1f / Mathf.Max(1f, meta.maxTerrainHeight);

            float[] worldHeights = new float[hRes * hRes];
            int[] climateHistogram = new int[5];
            SectorContentFlags flags = SectorContentFlags.None;

            // 이 섹터 영역(여유 30m 포함)을 지나는 마스터 도로 선분만 사전 필터링 (O(1) 근접 도로 정밀 도색용)
            List<RoadSegment2D> localRoadSegments = CollectSectorRoadSegments(
                networks, sectorSize, sectorOriginX, sectorOriginZ, sectorSize);

            // 1. 경계 이음새 0% 연속 높이맵 생성
            for (int iz = 0; iz < hRes; iz++)
            {
                float lz = (iz * invHResM1) * sectorSize;
                float v = (sectorOriginZ + lz) / totalD;

                for (int ix = 0; ix < hRes; ix++)
                {
                    float lx = (ix * invHResM1) * sectorSize;
                    float u = (sectorOriginX + lx) / totalW;

                    GlobalSurfaceSample sample = EvaluateGlobalSample(u, v, meta, cache);
                    worldHeights[iz * hRes + ix] = sample.heightMeters;

                    if (sample.heightMeters < sector.minHeight) sector.minHeight = sample.heightMeters;
                    if (sample.heightMeters > sector.maxHeight) sector.maxHeight = sample.heightMeters;

                    float normH = Mathf.Clamp01(sample.heightMeters * invMaxH);
                    sector.heights16[iz * hRes + ix] = (ushort)Mathf.RoundToInt(normH * 65535f);

                    climateHistogram[(int)sample.climate]++;
                    if (sample.isOcean) flags |= SectorContentFlags.HasSea;
                    else flags |= SectorContentFlags.HasLand;
                    if (sample.riverStrength > 0.18f) flags |= SectorContentFlags.HasRiver;
                }
            }

            int bestClimate = 0;
            for (int c = 1; c < 5; c++)
            {
                if (climateHistogram[c] > climateHistogram[bestClimate]) bestClimate = c;
            }
            sector.dominantClimate = (ContinentClimateId)bestClimate;

            // 2. 5슬롯 스플랫([0]잔디 [1]절벽 [2]흙/도로 [3]숲 [4]자갈), 수심, 바이옴 플래그 생성
            float stepMeters = sectorSize * invHResM1;

            for (int iz = 0; iz < sRes; iz++)
            {
                float normZ = iz * invSResM1;
                float worldZ = sectorOriginZ + normZ * sectorSize;
                float v = worldZ / totalD;
                int hz = Mathf.Clamp(Mathf.RoundToInt(normZ * (hRes - 1)), 1, hRes - 2);

                for (int ix = 0; ix < sRes; ix++)
                {
                    float normX = ix * invSResM1;
                    float worldX = sectorOriginX + normX * sectorSize;
                    float u = worldX / totalW;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (hRes - 1)), 1, hRes - 2);

                    GlobalSurfaceSample sample = EvaluateGlobalSample(u, v, meta, cache);
                    float road = EvaluateLocalRoadWeight(worldX, worldZ, localRoadSegments);
                    float forest = sample.forestDensity;

                    if (road > 0.25f) flags |= SectorContentFlags.HasRoad;
                    if (forest > 0.25f) flags |= SectorContentFlags.HasForest;

                    float dxH = (worldHeights[hz * hRes + (hx + 1)] - worldHeights[hz * hRes + (hx - 1)]) / (2f * stepMeters);
                    float dzH = (worldHeights[(hz + 1) * hRes + hx] - worldHeights[(hz - 1) * hRes + hx]) / (2f * stepMeters);
                    float slope = Mathf.Sqrt(dxH * dxH + dzH * dzH);

                    float w4Gravel = 0f;
                    if (sample.isOcean || sample.heightMeters <= meta.seaLevelY + 2.2f || sample.riverStrength > 0.22f)
                    {
                        w4Gravel = Mathf.Clamp01(0.65f + sample.riverStrength * 0.35f);
                    }

                    float w2Road = Mathf.Clamp01(road * 1.15f);
                    float w1Cliff = Mathf.Clamp01((slope - 0.36f) * 2.4f);
                    float w3Forest = (w4Gravel < 0.3f && w2Road < 0.2f && w1Cliff < 0.5f) ? Mathf.Clamp01(forest * 0.9f) : 0f;
                    float w0Grass = Mathf.Max(0f, 1f - Mathf.Clamp01(w1Cliff + w2Road + w3Forest + w4Gravel));

                    float sum = w0Grass + w1Cliff + w2Road + w3Forest + w4Gravel;
                    if (sum < 0.001f) { w0Grass = 1f; sum = 1f; }
                    float invSum = 255f / sum;

                    int baseSplat = (iz * sRes + ix) * 5;
                    sector.splatWeights5[baseSplat + 0] = (byte)Mathf.RoundToInt(w0Grass * invSum);
                    sector.splatWeights5[baseSplat + 1] = (byte)Mathf.RoundToInt(w1Cliff * invSum);
                    sector.splatWeights5[baseSplat + 2] = (byte)Mathf.RoundToInt(w2Road * invSum);
                    sector.splatWeights5[baseSplat + 3] = (byte)Mathf.RoundToInt(w3Forest * invSum);
                    sector.splatWeights5[baseSplat + 4] = (byte)Mathf.RoundToInt(w4Gravel * invSum);

                    int sIdx = iz * sRes + ix;
                    sector.waterDepth8[sIdx] = (byte)Mathf.Clamp(Mathf.RoundToInt(sample.waterDepthMeters * 10f), 0, 255);

                    byte bFlags = (byte)((byte)sample.climate & 0x07);
                    if (sample.riverStrength > 0.20f) bFlags |= (1 << 3);
                    if (road > 0.25f) bFlags |= (1 << 4);
                    if (forest > 0.25f) bFlags |= (1 << 5);
                    sector.biomeAndFlags[sIdx] = bFlags;
                }
            }

            sector.flags = flags;
            return sector;
        }

        /// <summary>
        /// 특정 섹터 (sx, sz) 내부에 스폰할 결정론적 전술 숲 구역(MasterForestData) 목록을 즉석 추출합니다. (Phase 3 스트리밍용)
        /// </summary>
        public static List<MasterForestData> ExtractSectorForests(
            WorldMetaData meta,
            WorldSourceCacheData cache,
            MasterNetworkData networks,
            int sx,
            int sz,
            int gridSubdivisions = 5)
        {
            List<MasterForestData> result = new List<MasterForestData>();
            float sectorSize = meta.sectorSizeMeters;
            float totalW = Mathf.Max(sectorSize, meta.TotalWidthMeters);
            float totalD = Mathf.Max(sectorSize, meta.TotalDepthMeters);

            float originX = sx * sectorSize;
            float originZ = sz * sectorSize;
            float cellW = sectorSize / Mathf.Max(1, gridSubdivisions);
            var localRoads = CollectSectorRoadSegments(networks, sectorSize, originX, originZ, sectorSize);

            int sectorSeed = meta.seed * 48611 + sx * 73856093 + sz * 19349663;
            System.Random rng = new System.Random(sectorSeed);

            for (int gz = 0; gz < gridSubdivisions; gz++)
            {
                for (int gx = 0; gx < gridSubdivisions; gx++)
                {
                    float lx = (gx + 0.2f + (float)rng.NextDouble() * 0.6f) * cellW;
                    float lz = (gz + 0.2f + (float)rng.NextDouble() * 0.6f) * cellW;
                    float wx = originX + lx;
                    float wz = originZ + lz;
                    float u = wx / totalW;
                    float v = wz / totalD;

                    GlobalSurfaceSample s = EvaluateGlobalSample(u, v, meta, cache);
                    if (s.forestDensity < 0.28f) continue;

                    float road = EvaluateLocalRoadWeight(wx, wz, localRoads);
                    if (road > 0.15f) continue;

                    result.Add(new MasterForestData
                    {
                        forestId = $"Forest_{sx}_{sz}_{gx}_{gz}",
                        center = new WorldCoord(sx, sz, lx, lz),
                        radius = Mathf.Lerp(26f, 48f, s.forestDensity),
                        climate = s.climate,
                        treeDensity = Mathf.Clamp(0.55f + s.forestDensity * 0.45f, 0.4f, 1.0f)
                    });
                }
            }

            return result;
        }

        private readonly struct RoadSegment2D
        {
            public readonly float ax, az, bx, bz, halfWidth;
            public RoadSegment2D(float ax, float az, float bx, float bz, float halfWidth)
            {
                this.ax = ax; this.az = az; this.bx = bx; this.bz = bz; this.halfWidth = halfWidth;
            }
        }

        private static List<RoadSegment2D> CollectSectorRoadSegments(
            MasterNetworkData networks,
            float sectorSize,
            float originX,
            float originZ,
            float size)
        {
            List<RoadSegment2D> segs = null;
            if (networks == null || networks.roads == null) return segs;

            float minX = originX - 24f;
            float maxX = originX + size + 24f;
            float minZ = originZ - 24f;
            float maxZ = originZ + size + 24f;

            for (int r = 0; r < networks.roads.Count; r++)
            {
                MasterRoadData road = networks.roads[r];
                if (road == null || road.points == null || road.points.Count < 2) continue;
                float halfW = Mathf.Max(4f, road.roadWidth * 0.75f);

                for (int i = 0; i < road.points.Count - 1; i++)
                {
                    double2 pa = road.points[i].ToDouble2(sectorSize);
                    double2 pb = road.points[i + 1].ToDouble2(sectorSize);

                    float segMinX = (float)math.min(pa.x, pb.x);
                    float segMaxX = (float)math.max(pa.x, pb.x);
                    float segMinZ = (float)math.min(pa.y, pb.y);
                    float segMaxZ = (float)math.max(pa.y, pb.y);

                    if (segMaxX < minX || segMinX > maxX || segMaxZ < minZ || segMinZ > maxZ) continue;

                    if (segs == null) segs = new List<RoadSegment2D>(8);
                    segs.Add(new RoadSegment2D((float)pa.x, (float)pa.y, (float)pb.x, (float)pb.y, halfW));
                }
            }
            return segs;
        }

        private static float EvaluateLocalRoadWeight(float wx, float wz, List<RoadSegment2D> segs)
        {
            if (segs == null || segs.Count == 0) return 0f;

            float bestWeight = 0f;
            for (int i = 0; i < segs.Count; i++)
            {
                RoadSegment2D s = segs[i];
                float abX = s.bx - s.ax;
                float abZ = s.bz - s.az;
                float lenSq = abX * abX + abZ * abZ;
                float t = (lenSq > 1e-4f) ? Mathf.Clamp01(((wx - s.ax) * abX + (wz - s.az) * abZ) / lenSq) : 0f;

                float dx = wx - (s.ax + abX * t);
                float dz = wz - (s.az + abZ * t);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < s.halfWidth)
                {
                    float w = 1f - dist / s.halfWidth;
                    if (w > bestWeight) bestWeight = w;
                }
            }
            return bestWeight;
        }

        public static float SampleGridMask(byte[] mask, int w, int h, float u, float v)
        {
            if (mask == null || w <= 1 || h <= 1) return 0f;
            float fx = Mathf.Clamp01(u) * (w - 1);
            float fz = Mathf.Clamp01(v) * (h - 1);
            return SampleBilinearByteNorm(mask, w, h, fx, fz);
        }

        private static float SampleBilinearShortQ16(short[] field, int w, int h, float fx, float fz)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, w - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, h - 2);
            float tx = fx - x0;
            float tz = fz - z0;

            const float inv16 = 1f / 16f;
            float v00 = field[z0 * w + x0] * inv16;
            float v10 = field[z0 * w + (x0 + 1)] * inv16;
            float v01 = field[(z0 + 1) * w + x0] * inv16;
            float v11 = field[(z0 + 1) * w + (x0 + 1)] * inv16;

            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), tz);
        }

        private static float SampleBilinearByteRaw(byte[] field, int w, int h, float fx, float fz)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, w - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, h - 2);
            float tx = fx - x0;
            float tz = fz - z0;

            float v00 = field[z0 * w + x0];
            float v10 = field[z0 * w + (x0 + 1)];
            float v01 = field[(z0 + 1) * w + x0];
            float v11 = field[(z0 + 1) * w + (x0 + 1)];

            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), tz);
        }

        private static float SampleBilinearByteNorm(byte[] field, int w, int h, float fx, float fz)
        {
            return SampleBilinearByteRaw(field, w, h, fx, fz) / 255f;
        }

        private static float FractalNoise(float x, float z, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Mathf.PerlinNoise(x * freq, z * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.07f;
            }
            return (norm > 0f) ? (sum / norm) : 0.5f;
        }
    }
}
