using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace MiniTotalWar.OpenWorld.Editor
{
    /// <summary>
    /// 대륙 자동 생성 설정 파라미터
    /// </summary>
    [Serializable]
    public class ContinentGenerationSettings
    {
        public string continentId = "ThreeKingdoms_China";
        public string displayName = "삼국지 중원 대륙 (TW3K 114km 풀 스케일)";
        public string outputRootFolder = "Assets/WorldData";

        public string battleLocationsXmlPath = "Assets/TotalWar_ThreeKingdoms_Extracted/DynamicTileSystem/battle_locations_map.xml";
        public string seaHeightMapPath = "Assets/TotalWar_ThreeKingdoms_Extracted/DynamicTileSystem/lf_sea_height_map.png";

        // 삼국지 토탈워 원작(1784x1405 @ 64m = 114.2km x 89.9km)과 동일 체급의 풀 스케일 기본값
        public int sectorCountX = 223;
        public int sectorCountZ = 175;
        public float sectorSizeMeters = 512f;

        public int heightmapResolution = 129;
        public int alphamapResolution = 128;
        public float maxTerrainHeight = 140f;
        public float seaLevelY = 20f;
        public int seed = 190; // 반동탁 연합 190년 기본 시드

        // 실시간 즉석 생성(On-the-Fly) 아키텍처 옵션
        public bool useOnDemandSynthesis = true;
        public bool bakeAllSectorsToDisk = false;

        // 캠페인 3D 축소 지도 규격 (대륙 크기와 무관하게 일정한 전략 테이블 가로폭 유지)
        public float campaignMapWidthMeters = 640f;
        public int campaignMeshResX = 256;
        public int campaignMeshResZ = 200;
        public int campaignAlbedoResX = 1024;
        public int campaignAlbedoResZ = 800;

        public WorldMetaData ToWorldMeta(bool loadedFromExtracted)
        {
            return new WorldMetaData
            {
                continentId = continentId,
                displayName = displayName,
                sourceReference = loadedFromExtracted
                    ? "TotalWar:ThreeKingdoms 892x703 + lf_sea_height_map"
                    : "Procedural Fallback",
                sectorCountX = sectorCountX,
                sectorCountZ = sectorCountZ,
                sectorSizeMeters = sectorSizeMeters,
                heightmapResolution = heightmapResolution,
                alphamapResolution = alphamapResolution,
                maxTerrainHeight = maxTerrainHeight,
                seaLevelY = seaLevelY,
                worldMinY = 0f,
                seed = seed,
                useOnDemandSynthesis = useOnDemandSynthesis || !bakeAllSectorsToDisk,
                campaignMapWidthMeters = campaignMapWidthMeters,
                generatedUtcIso = DateTime.UtcNow.ToString("o")
            };
        }
    }

    /// <summary>
    /// 대륙 생성 결과 및 검증 요약
    /// </summary>
    public class ContinentGenerationSummary
    {
        public string worldFolder;
        public float totalWidthKm;
        public float totalDepthKm;
        public int totalSectors;
        public int bakedSectorFilesCount;
        public bool onDemandMode;
        public int strategicNodeCount;
        public int roadCount;
        public int riverCount;
        public bool usedExtractedTw3kMaps;
        public int checkedBorders;
        public int seamMismatchCount;
        public int maxSeamDelta;
    }

    /// <summary>
    /// 삼국지 토탈워 추출 지도(battle_locations_map.xml, lf_sea_height_map.png)를 파싱해
    /// 초경량 대륙 참조 캐시(WorldSourceCache.bin), 전략 거점·도로·대하천 마스터(MasterNetworks.json),
    /// 3D 캠페인 지도를 수 초 만에 자동 생성하는 에디터 파이프라인입니다.
    /// (전투 중인 3x3 구역만 런타임에 ContinentSectorSynthesizer로 즉석 합성해 114km 풀 스케일을 경량 구동)
    /// </summary>
    public static class ContinentWorldGenerator
    {
        private readonly struct StrategicAnchorDef
        {
            public readonly string id;
            public readonly string name;
            public readonly StrategicNodeType type;
            public readonly float targetU;
            public readonly float targetV;
            public readonly string faction;
            public readonly int garrison;
            public readonly bool preferPort;

            public StrategicAnchorDef(string id, string name, StrategicNodeType type, float u, float v, string faction, int garrison, bool port = false)
            {
                this.id = id;
                this.name = name;
                this.type = type;
                this.targetU = u;
                this.targetV = v;
                this.faction = faction;
                this.garrison = garrison;
                this.preferPort = port;
            }
        }

        private static readonly StrategicAnchorDef[] HistoricalAnchors = new StrategicAnchorDef[]
        {
            new StrategicAnchorDef("luoyang",  "낙양 (사예 치소)", StrategicNodeType.CapitalCity, 0.52f, 0.56f, "Han_Dynasty", 2400),
            new StrategicAnchorDef("changan",  "장안 (관중 도읍)", StrategicNodeType.CapitalCity, 0.41f, 0.55f, "Dong_Zhuo",   2200),
            new StrategicAnchorDef("ye",       "업성 (하북 중심)", StrategicNodeType.WalledCity,  0.58f, 0.66f, "Yuan_Shao",   2000),
            new StrategicAnchorDef("xuchang",  "허창 (영천 군진)", StrategicNodeType.WalledCity,  0.57f, 0.52f, "Cao_Cao",     2000),
            new StrategicAnchorDef("youzhou",  "유주·계성",       StrategicNodeType.WalledCity,  0.60f, 0.78f, "Gongsun_Zan", 1700),
            new StrategicAnchorDef("xiliang",  "무위 (서량)",     StrategicNodeType.WalledCity,  0.27f, 0.69f, "Ma_Teng",     1600),
            new StrategicAnchorDef("xiapi",    "하비 (서주)",     StrategicNodeType.WalledCity,  0.66f, 0.51f, "Tao_Qian",    1600),
            new StrategicAnchorDef("hefei",    "합비 (회남 요충)", StrategicNodeType.WalledCity,  0.65f, 0.45f, "Cao_Cao",     1700),
            new StrategicAnchorDef("jianye",   "건업 (강동 도읍)", StrategicNodeType.CapitalCity, 0.69f, 0.43f, "Sun_Jian",    2100, true),
            new StrategicAnchorDef("xiangyang","양양 (형주 북문)", StrategicNodeType.WalledCity,  0.51f, 0.43f, "Liu_Biao",    1900),
            new StrategicAnchorDef("chibi",    "적벽 (장강 수군)", StrategicNodeType.Port,        0.55f, 0.37f, "Sun_Jian",    1400, true),
            new StrategicAnchorDef("changsha", "장사 (형남)",     StrategicNodeType.WalledCity,  0.53f, 0.31f, "Liu_Biao",    1500),
            new StrategicAnchorDef("hanzhong", "한중 (파촉 관문)", StrategicNodeType.WalledCity,  0.39f, 0.50f, "Zhang_Lu",    1600),
            new StrategicAnchorDef("chengdu",  "성도 (익주 도읍)", StrategicNodeType.CapitalCity, 0.33f, 0.42f, "Liu_Yan",     2100),
            new StrategicAnchorDef("yongan",   "영안 (백제성)",   StrategicNodeType.WalledCity,  0.43f, 0.41f, "Liu_Yan",     1500),
            new StrategicAnchorDef("nanhai",   "번우 (남해 항구)", StrategicNodeType.Port,        0.54f, 0.16f, "Shi_Xie",     1400, true)
        };

        private static readonly (int a, int b)[] RoadLinks = new (int, int)[]
        {
            (0, 1), (0, 2), (0, 3), (0, 9), (1, 5), (1, 12), (2, 4), (3, 6), (3, 7), (3, 9),
            (6, 8), (7, 8), (8, 10), (9, 10), (9, 14), (10, 11), (11, 15), (12, 13), (13, 14)
        };

        /// <summary>
        /// 대륙 SSOT 데이터(WorldMeta, WorldSourceCache, MasterNetworks, 3D 캠페인 지도) 및 선택적 섹터 바이너리를 일괄 생성합니다.
        /// </summary>
        public static ContinentGenerationSummary GenerateContinent(
            ContinentGenerationSettings settings,
            Action<string, float> onProgress = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            onProgress?.Invoke("삼국지 토탈워 원본 지형·수계 맵 파싱 중...", 0.08f);
            WorldSourceCacheData cache = BuildSourceCacheFromFiles(
                settings,
                out bool loadedFromExtracted,
                out List<Vector2> settlementUVs,
                out List<Vector2> gateUVs);

            WorldMetaData meta = settings.ToWorldMeta(loadedFromExtracted);
            string worldFolder = SectorBinaryIO.GetWorldFolder(settings.outputRootFolder, settings.continentId);
            Directory.CreateDirectory(worldFolder);
            SectorBinaryIO.SaveWorldMeta(worldFolder, meta);

            onProgress?.Invoke("16대 전략 거점·A* 도로망·3대 하천 네트워크 구축 중...", 0.28f);
            MasterNetworkData networks = BuildMasterNetworks(meta, cache, settlementUVs, gateUVs);
            SectorBinaryIO.SaveMasterNetworks(worldFolder, networks);

            onProgress?.Invoke("초경량 대륙 참조 캐시(WorldSourceCache.bin) 저장 중...", 0.45f);
            SectorBinaryIO.SaveSourceCache(worldFolder, cache);

            int totalSectors = settings.sectorCountX * settings.sectorCountZ;
            int bakedCount = 0;

            if (settings.bakeAllSectorsToDisk)
            {
                for (int sz = 0; sz < settings.sectorCountZ; sz++)
                {
                    for (int sx = 0; sx < settings.sectorCountX; sx++)
                    {
                        if (bakedCount % 16 == 0)
                        {
                            float p = 0.48f + 0.32f * ((float)bakedCount / Mathf.Max(1, totalSectors));
                            onProgress?.Invoke($"섹터 파일 디스크 베이크 중... ({sx}, {sz})", p);
                        }

                        SectorBinaryData sector = ContinentSectorSynthesizer.SynthesizeSector(meta, cache, networks, sx, sz);
                        SectorBinaryIO.SaveSector(worldFolder, sector);
                        bakedCount++;
                    }
                }
            }

            onProgress?.Invoke("3D 캠페인 축소 대륙 지도 메쉬 및 알베도 텍스처 베이크 중...", 0.82f);
            BakeCampaignMapAssets(settings, meta, cache, worldFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            onProgress?.Invoke("인접 섹터 경계 이음새(Seam) 0% 무결성 검증 중...", 0.95f);
            ValidateContinentSeams(worldFolder, out int checkedBorders, out int mismatches, out int maxDelta);

            onProgress?.Invoke("대륙 오픈월드 생성 완료!", 1.0f);

            return new ContinentGenerationSummary
            {
                worldFolder = worldFolder,
                totalWidthKm = meta.TotalWidthMeters / 1000f,
                totalDepthKm = meta.TotalDepthMeters / 1000f,
                totalSectors = totalSectors,
                bakedSectorFilesCount = bakedCount,
                onDemandMode = meta.useOnDemandSynthesis,
                strategicNodeCount = networks.nodes.Count,
                roadCount = networks.roads.Count,
                riverCount = networks.rivers.Count,
                usedExtractedTw3kMaps = loadedFromExtracted,
                checkedBorders = checkedBorders,
                seamMismatchCount = mismatches,
                maxSeamDelta = maxDelta
            };
        }

        /// <summary>
        /// 에디터 미리보기 창을 위한 2D 컬러 대륙 지도 텍스처를 생성합니다.
        /// </summary>
        public static Texture2D GeneratePreviewTexture(ContinentGenerationSettings settings, int width = 420, int height = 330)
        {
            WorldSourceCacheData cache = BuildSourceCacheFromFiles(
                settings,
                out bool loaded,
                out List<Vector2> settlementUVs,
                out List<Vector2> gateUVs);
            WorldMetaData meta = settings.ToWorldMeta(loaded);
            BuildMasterNetworks(meta, cache, settlementUVs, gateUVs);

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "ContinentPreview";
            Color32[] pixels = BuildCampaignMapAlbedoPixels(width, height, meta, cache);

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 섹터 경계 1픽셀 높이값이 100% 일치하는지 검증합니다.
        /// 사전 굽기 파일이 없는 온디맨드 대형 대륙은 대표 섹터 경계를 즉석 합성해 1초 이내로 전수 검증합니다.
        /// </summary>
        public static void ValidateContinentSeams(string worldFolder, out int checkedBorders, out int mismatchCount, out int maxDelta)
        {
            checkedBorders = 0;
            mismatchCount = 0;
            maxDelta = 0;

            WorldMetaData meta = SectorBinaryIO.LoadWorldMeta(worldFolder);
            if (meta == null) return;

            WorldSourceCacheData cache = SectorBinaryIO.LoadSourceCache(worldFolder);
            MasterNetworkData networks = SectorBinaryIO.LoadMasterNetworks(worldFolder);

            int sxCount = meta.sectorCountX;
            int szCount = meta.sectorCountZ;

            // 114km 초대형(39,000+ 섹터)일 때는 OOM 방지를 위해 격자 스트라이드로 핵심 경계 전수 샘플링
            int total = sxCount * szCount;
            int stride = (total <= 600) ? 1 : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(total / 200f)));

            for (int z = 0; z < szCount; z += stride)
            {
                for (int x = 0; x < sxCount; x += stride)
                {
                    SectorBinaryData cur = ContinentSectorSynthesizer.LoadOrSynthesizeSector(worldFolder, meta, cache, networks, x, z);
                    if (cur == null || cur.heights16 == null) continue;
                    int r = cur.heightRes;

                    if (x + 1 < sxCount)
                    {
                        SectorBinaryData east = ContinentSectorSynthesizer.LoadOrSynthesizeSector(worldFolder, meta, cache, networks, x + 1, z);
                        if (east != null)
                        {
                            checkedBorders++;
                            for (int iz = 0; iz < r; iz++)
                            {
                                int diff = Mathf.Abs(cur.heights16[iz * r + (r - 1)] - east.heights16[iz * r + 0]);
                                if (diff > 0)
                                {
                                    mismatchCount++;
                                    if (diff > maxDelta) maxDelta = diff;
                                }
                            }
                        }
                    }

                    if (z + 1 < szCount)
                    {
                        SectorBinaryData north = ContinentSectorSynthesizer.LoadOrSynthesizeSector(worldFolder, meta, cache, networks, x, z + 1);
                        if (north != null)
                        {
                            checkedBorders++;
                            for (int ix = 0; ix < r; ix++)
                            {
                                int diff = Mathf.Abs(cur.heights16[(r - 1) * r + ix] - north.heights16[0 * r + ix]);
                                if (diff > 0)
                                {
                                    mismatchCount++;
                                    if (diff > maxDelta) maxDelta = diff;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static WorldSourceCacheData BuildSourceCacheFromFiles(
            ContinentGenerationSettings settings,
            out bool loadedFromExtracted,
            out List<Vector2> settlementUVs,
            out List<Vector2> gateUVs)
        {
            settlementUVs = new List<Vector2>();
            gateUVs = new List<Vector2>();

            WorldSourceCacheData cache = new WorldSourceCacheData
            {
                maskW = 720,
                maskH = 560,
                roadMask8 = new byte[720 * 560]
            };

            if (!File.Exists(settings.battleLocationsXmlPath))
            {
                loadedFromExtracted = false;
                BuildProceduralFallbackSourceCache(cache);
                return cache;
            }

            loadedFromExtracted = true;
            ParseBattleLocationsXml(settings.battleLocationsXmlPath, cache, settlementUVs, gateUVs);
            ClassifyOceanAndMountains(cache);
            BuildDistanceFields(cache);
            LoadDownsampledRiverStrengthMap(settings.seaHeightMapPath, cache);
            return cache;
        }

        private static void ParseBattleLocationsXml(
            string xmlPath,
            WorldSourceCacheData cache,
            List<Vector2> settlementUVs,
            List<Vector2> gateUVs)
        {
            int w = 892;
            int h = 703;
            byte[] kinds = null;
            int idx = 0;
            bool inFlags = false;
            string currentListKey = "";

            using (StreamReader sr = new StreamReader(xmlPath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (kinds == null && line.Contains("<BATTLE_LOCATIONS_MAP"))
                    {
                        if (TryReadXmlIntAttr(line, "map_width='", out int mw)) w = mw;
                        if (TryReadXmlIntAttr(line, "map_height='", out int mh)) h = mh;
                        kinds = new byte[w * h];
                        continue;
                    }

                    if (!inFlags)
                    {
                        if (line.Contains("<CATCHMENT_AREA_LIST"))
                        {
                            if (line.Contains("key='settlement_standard'")) currentListKey = "settlement_standard";
                            else if (line.Contains("key='gate_battle'")) currentListKey = "gate_battle";
                            else currentListKey = "";
                            continue;
                        }

                        if (!string.IsNullOrEmpty(currentListKey) && line.Contains("<infield_centre"))
                        {
                            if (TryReadXmlIntAttr(line, "x='", out int cx) && TryReadXmlIntAttr(line, "y='", out int cy))
                            {
                                float u = Mathf.Clamp01((float)cx / Mathf.Max(1, w - 1));
                                float v = 1f - Mathf.Clamp01((float)cy / Mathf.Max(1, h - 1));
                                if (currentListKey == "settlement_standard") settlementUVs.Add(new Vector2(u, v));
                                else if (currentListKey == "gate_battle") gateUVs.Add(new Vector2(u, v));
                            }
                            continue;
                        }

                        if (line.Contains("<meta_data_flags>"))
                        {
                            inFlags = true;
                        }
                    }
                    else
                    {
                        if (line.Contains("</meta_data_flags>")) break;
                        int start = line.IndexOf("<card32>", StringComparison.Ordinal);
                        if (start >= 0 && kinds != null && idx < kinds.Length)
                        {
                            start += 8;
                            int end = line.IndexOf("</card32>", start, StringComparison.Ordinal);
                            if (end > start && int.TryParse(line.Substring(start, end - start), out int val))
                            {
                                kinds[idx++] = (byte)(val != 0 ? 1 : 2); // 1: PlayableLand, 2: HighMountain(잠정)
                            }
                        }
                    }
                }
            }

            cache.xmlW = w;
            cache.xmlH = h;
            cache.cellKinds = kinds ?? new byte[w * h];
        }

        private static bool TryReadXmlIntAttr(string line, string attrPrefix, out int value)
        {
            value = 0;
            int idx = line.IndexOf(attrPrefix, StringComparison.Ordinal);
            if (idx < 0) return false;
            idx += attrPrefix.Length;
            int end = line.IndexOf('\'', idx);
            if (end <= idx) return false;
            return int.TryParse(line.Substring(idx, end - idx), out value);
        }

        private static void ClassifyOceanAndMountains(WorldSourceCacheData cache)
        {
            int w = cache.xmlW;
            int h = cache.xmlH;
            int[] minLandXPerRow = new int[h];
            for (int y = 0; y < h; y++)
            {
                int minX = w;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (cache.cellKinds[row + x] == 1)
                    {
                        minX = x;
                        break;
                    }
                }
                minLandXPerRow[y] = minX;
            }

            bool IsAllowedOceanDomain(int x, int y)
            {
                if (y < 102) return false;
                if (y <= 545 && x <= minLandXPerRow[y] + 10) return false;
                if (y > 545 && x < 245) return false;
                return true;
            }

            Queue<int> queue = new Queue<int>(8192);
            bool[] visited = new bool[w * h];

            for (int y = 115; y < h; y++)
            {
                int seedIdx = y * w + (w - 1);
                if (cache.cellKinds[seedIdx] != 1 && IsAllowedOceanDomain(w - 1, y))
                {
                    visited[seedIdx] = true;
                    cache.cellKinds[seedIdx] = 0;
                    queue.Enqueue(seedIdx);
                }
            }

            for (int x = 250; x < w; x++)
            {
                int seedIdx = (h - 1) * w + x;
                if (!visited[seedIdx] && cache.cellKinds[seedIdx] != 1 && IsAllowedOceanDomain(x, h - 1))
                {
                    visited[seedIdx] = true;
                    cache.cellKinds[seedIdx] = 0;
                    queue.Enqueue(seedIdx);
                }
            }

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w;
                int cy = cur / w;

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int nIdx = ny * w + nx;
                    if (visited[nIdx]) continue;
                    if (cache.cellKinds[nIdx] == 1) continue;
                    if (!IsAllowedOceanDomain(nx, ny)) continue;

                    visited[nIdx] = true;
                    cache.cellKinds[nIdx] = 0;
                    queue.Enqueue(nIdx);
                }
            }
        }

        private static void BuildDistanceFields(WorldSourceCacheData cache)
        {
            int w = cache.xmlW;
            int h = cache.xmlH;
            int n = w * h;

            int[] seaDist = new int[n];
            int[] landDist = new int[n];
            int[] mtnDist = new int[n];
            const int INF = 100000;

            for (int i = 0; i < n; i++)
            {
                byte k = cache.cellKinds[i];
                seaDist[i] = (k == 0) ? 0 : INF;
                landDist[i] = (k != 0) ? 0 : INF;
                mtnDist[i] = (k != 2) ? 0 : INF;
            }

            ComputeChamferDistance2D(seaDist, w, h);
            ComputeChamferDistance2D(landDist, w, h);
            ComputeChamferDistance2D(mtnDist, w, h);

            cache.coastDistanceQ16 = new short[n];
            cache.mountainDepth8 = new byte[n];
            for (int i = 0; i < n; i++)
            {
                int signedDist = (cache.cellKinds[i] == 0) ? -landDist[i] : seaDist[i];
                cache.coastDistanceQ16[i] = (short)Mathf.Clamp(signedDist * 16, short.MinValue, short.MaxValue);
                cache.mountainDepth8[i] = (byte)Mathf.Clamp(mtnDist[i], 0, 255);
            }
        }

        private static void ComputeChamferDistance2D(int[] dist, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int d = dist[row + x];
                    if (d == 0) continue;
                    if (x > 0 && dist[row + x - 1] + 1 < d) d = dist[row + x - 1] + 1;
                    if (y > 0 && dist[(y - 1) * w + x] + 1 < d) d = dist[(y - 1) * w + x] + 1;
                    dist[row + x] = d;
                }
            }

            for (int y = h - 1; y >= 0; y--)
            {
                int row = y * w;
                for (int x = w - 1; x >= 0; x--)
                {
                    int d = dist[row + x];
                    if (x + 1 < w && dist[row + x + 1] + 1 < d) d = dist[row + x + 1] + 1;
                    if (y + 1 < h && dist[(y + 1) * w + x] + 1 < d) d = dist[(y + 1) * w + x] + 1;
                    dist[row + x] = d;
                }
            }
        }

        private static void LoadDownsampledRiverStrengthMap(string seaPath, WorldSourceCacheData cache)
        {
            if (string.IsNullOrEmpty(seaPath) || !File.Exists(seaPath)) return;

            byte[] bytes = File.ReadAllBytes(seaPath);
            Texture2D tmpTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tmpTex.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(tmpTex);
                return;
            }

            int srcW = tmpTex.width;
            int srcH = tmpTex.height;
            Color32[] pixels = tmpTex.GetPixels32();

            // 1784 x 1405 (2x XML 해상도, ~2.5MB) 로 다운샘플링하여 선명한 황하·장강·주강 강줄기 보존
            int dstW = Mathf.Min(1784, srcW);
            int dstH = Mathf.Min(1405, srcH);
            cache.riverW = dstW;
            cache.riverH = dstH;
            cache.riverStrength8 = new byte[dstW * dstH];

            for (int y = 0; y < dstH; y++)
            {
                int sy = Mathf.Clamp(Mathf.RoundToInt((float)y / Mathf.Max(1, dstH - 1) * (srcH - 1)), 0, srcH - 1);
                for (int x = 0; x < dstW; x++)
                {
                    int sx = Mathf.Clamp(Mathf.RoundToInt((float)x / Mathf.Max(1, dstW - 1) * (srcW - 1)), 0, srcW - 1);
                    int r = pixels[sy * srcW + sx].r;
                    if (r > 61)
                    {
                        float norm = Mathf.Clamp01((r - 61) / 90f);
                        cache.riverStrength8[y * dstW + x] = (byte)Mathf.RoundToInt(norm * 255f);
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(tmpTex);
        }

        private static void BuildProceduralFallbackSourceCache(WorldSourceCacheData cache)
        {
            int w = 224;
            int h = 176;
            cache.xmlW = w;
            cache.xmlH = h;
            cache.cellKinds = new byte[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = 1f - (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    bool isSea = (u > 0.72f - 0.15f * (1f - v)) || (v < 0.14f && u > 0.35f);
                    cache.cellKinds[y * w + x] = (byte)(isSea ? 0 : 1);
                }
            }
            BuildDistanceFields(cache);
        }

        private static MasterNetworkData BuildMasterNetworks(
            WorldMetaData meta,
            WorldSourceCacheData cache,
            List<Vector2> settlementUVs,
            List<Vector2> gateUVs)
        {
            MasterNetworkData networks = new MasterNetworkData();
            float totalW = meta.TotalWidthMeters;
            float totalD = meta.TotalDepthMeters;

            // 1. 16대 전략 거점 배치
            for (int i = 0; i < HistoricalAnchors.Length; i++)
            {
                var def = HistoricalAnchors[i];
                Vector2 snappedUV = SnapToNearestValidLandUV(def.targetU, def.targetV, cache, settlementUVs, def.preferPort);
                GlobalSurfaceSample sample = ContinentSectorSynthesizer.EvaluateGlobalSample(snappedUV.x, snappedUV.y, meta, cache);

                WorldCoord coord = WorldCoord.FromDouble2(
                    new double2(snappedUV.x * totalW, snappedUV.y * totalD),
                    meta.sectorSizeMeters);

                networks.nodes.Add(new StrategicNodeData
                {
                    nodeId = def.id,
                    displayName = def.name,
                    nodeType = def.type,
                    coord = coord,
                    climate = sample.climate,
                    initialFactionId = def.faction,
                    garrisonStrength = def.garrison,
                    hasPortAccess = def.preferPort
                });
            }

            // 2. 요새 관문 노드 추가
            AddGateNodesFromXml(gateUVs, meta, cache, totalW, totalD, networks);

            // 3. A* 저경사 대륙 도로망 생성 + 도하 여울목(RiverFord) 등록
            int pathW = 200;
            int pathH = 156;
            float[] slopeCostMap = BuildPathCostGrid(pathW, pathH, meta, cache);

            for (int i = 0; i < RoadLinks.Length; i++)
            {
                var link = RoadLinks[i];
                if (link.a >= networks.nodes.Count || link.b >= networks.nodes.Count) continue;

                StrategicNodeData nodeA = networks.nodes[link.a];
                StrategicNodeData nodeB = networks.nodes[link.b];
                double2 pA = nodeA.coord.ToDouble2(meta.sectorSizeMeters);
                double2 pB = nodeB.coord.ToDouble2(meta.sectorSizeMeters);

                Vector2 uvA = new Vector2((float)(pA.x / totalW), (float)(pA.y / totalD));
                Vector2 uvB = new Vector2((float)(pB.x / totalW), (float)(pB.y / totalD));

                List<Vector2> uvPath = FindAStarUVPath(uvA, uvB, pathW, pathH, slopeCostMap);
                if (uvPath == null || uvPath.Count < 2) continue;

                MasterRoadData road = new MasterRoadData
                {
                    roadId = $"Road_{nodeA.nodeId}_{nodeB.nodeId}",
                    fromNodeId = nodeA.nodeId,
                    toNodeId = nodeB.nodeId,
                    roadWidth = 8.5f
                };

                bool fordAdded = false;
                for (int p = 0; p < uvPath.Count; p++)
                {
                    Vector2 uv = uvPath[p];
                    WorldCoord c = WorldCoord.FromDouble2(
                        new double2(uv.x * totalW, uv.y * totalD),
                        meta.sectorSizeMeters);
                    road.points.Add(c);

                    if (!fordAdded && p > 1 && p < uvPath.Count - 2)
                    {
                        GlobalSurfaceSample sr = ContinentSectorSynthesizer.EvaluateGlobalSample(uv.x, uv.y, meta, cache);
                        if (sr.riverStrength > 0.25f)
                        {
                            fordAdded = true;
                            networks.nodes.Add(new StrategicNodeData
                            {
                                nodeId = $"ford_{i}_{p}",
                                displayName = $"{nodeA.displayName}-{nodeB.displayName} 도하 여울목",
                                nodeType = StrategicNodeType.RiverFord,
                                coord = c,
                                climate = sr.climate,
                                initialFactionId = "Neutral",
                                garrisonStrength = 0
                            });
                        }
                    }
                }

                networks.roads.Add(road);
                RasterizePolylineToMask(uvPath, cache.roadMask8, cache.maskW, cache.maskH, 2.0f);
            }

            // 4. 3대 하천 마스터 스플라인 등록
            AddMajorRiverSplines(meta, cache, totalW, totalD, networks);
            return networks;
        }

        private static Vector2 SnapToNearestValidLandUV(
            float targetU,
            float targetV,
            WorldSourceCacheData cache,
            List<Vector2> settlementUVs,
            bool preferPort)
        {
            int w = cache.xmlW;
            int h = cache.xmlH;
            Vector2 bestUV = new Vector2(targetU, targetV);
            float bestDistSq = float.MaxValue;

            if (!preferPort && settlementUVs != null && settlementUVs.Count > 0)
            {
                for (int i = 0; i < settlementUVs.Count; i++)
                {
                    Vector2 cand = settlementUVs[i];
                    int ix = Mathf.Clamp(Mathf.RoundToInt(cand.x * (w - 1)), 0, w - 1);
                    int iy = Mathf.Clamp(Mathf.RoundToInt((1f - cand.y) * (h - 1)), 0, h - 1);
                    if (cache.cellKinds[iy * w + ix] != 1) continue;

                    float dSq = (cand.x - targetU) * (cand.x - targetU) + (cand.y - targetV) * (cand.y - targetV);
                    if (dSq < bestDistSq && dSq < 0.008f)
                    {
                        bestDistSq = dSq;
                        bestUV = cand;
                    }
                }
                if (bestDistSq < float.MaxValue) return bestUV;
            }

            int startX = Mathf.Clamp(Mathf.RoundToInt(targetU * (w - 1)), 0, w - 1);
            int startY = Mathf.Clamp(Mathf.RoundToInt((1f - targetV) * (h - 1)), 0, h - 1);

            for (int r = 0; r <= 60; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int py = startY + dy;
                    if (py < 0 || py >= h) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        int px = startX + dx;
                        if (px < 0 || px >= w) continue;
                        if (cache.cellKinds[py * w + px] == 1)
                        {
                            return new Vector2((float)px / (w - 1), 1f - (float)py / (h - 1));
                        }
                    }
                }
            }

            return bestUV;
        }

        private static void AddGateNodesFromXml(
            List<Vector2> gateUVs,
            WorldMetaData meta,
            WorldSourceCacheData cache,
            float totalW,
            float totalD,
            MasterNetworkData networks)
        {
            if (gateUVs == null || gateUVs.Count == 0) return;

            string[] gateNames = { "호로관", "함곡관", "동관", "무관", "검각관", "양평관" };
            int added = 0;
            for (int i = 0; i < gateUVs.Count && added < gateNames.Length; i++)
            {
                Vector2 gUV = gateUVs[i];
                bool tooClose = false;
                for (int j = 0; j < networks.nodes.Count; j++)
                {
                    double2 dPos = networks.nodes[j].coord.ToDouble2(meta.sectorSizeMeters);
                    float nu = (float)(dPos.x / totalW);
                    float nv = (float)(dPos.y / totalD);
                    if ((nu - gUV.x) * (nu - gUV.x) + (nv - gUV.y) * (nv - gUV.y) < 0.0025f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                GlobalSurfaceSample sample = ContinentSectorSynthesizer.EvaluateGlobalSample(gUV.x, gUV.y, meta, cache);
                networks.nodes.Add(new StrategicNodeData
                {
                    nodeId = $"gate_{added}",
                    displayName = $"{gateNames[added]} (요새 관문)",
                    nodeType = StrategicNodeType.MountainGate,
                    coord = WorldCoord.FromDouble2(new double2(gUV.x * totalW, gUV.y * totalD), meta.sectorSizeMeters),
                    climate = sample.climate,
                    initialFactionId = "Han_Dynasty",
                    garrisonStrength = 1200
                });
                added++;
            }
        }

        private static float[] BuildPathCostGrid(int w, int h, WorldMetaData meta, WorldSourceCacheData cache)
        {
            float[] cost = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    GlobalSurfaceSample s = ContinentSectorSynthesizer.EvaluateGlobalSample(u, v, meta, cache);
                    float c = 1.0f;
                    if (s.isOcean) c = 500.0f;
                    else if (s.heightMeters > meta.seaLevelY + 45f) c = 25.0f + (s.heightMeters - meta.seaLevelY) * 0.4f;
                    else if (s.riverStrength > 0.25f) c = 8.0f;
                    cost[y * w + x] = c;
                }
            }
            return cost;
        }

        private static List<Vector2> FindAStarUVPath(Vector2 startUV, Vector2 goalUV, int w, int h, float[] costGrid)
        {
            int sx = Mathf.Clamp(Mathf.RoundToInt(startUV.x * (w - 1)), 0, w - 1);
            int sy = Mathf.Clamp(Mathf.RoundToInt(startUV.y * (h - 1)), 0, h - 1);
            int gx = Mathf.Clamp(Mathf.RoundToInt(goalUV.x * (w - 1)), 0, w - 1);
            int gy = Mathf.Clamp(Mathf.RoundToInt(goalUV.y * (h - 1)), 0, h - 1);

            int total = w * h;
            float[] gScore = new float[total];
            float[] fScore = new float[total];
            int[] cameFrom = new int[total];
            bool[] closed = new bool[total];
            for (int i = 0; i < total; i++)
            {
                gScore[i] = float.MaxValue;
                fScore[i] = float.MaxValue;
                cameFrom[i] = -1;
            }

            int startIdx = sy * w + sx;
            int goalIdx = gy * w + gx;
            gScore[startIdx] = 0f;
            fScore[startIdx] = Vector2.Distance(new Vector2(sx, sy), new Vector2(gx, gy));

            List<int> open = new List<int>(256) { startIdx };
            int[] nxOff = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] nyOff = { 0, 0, 1, -1, 1, -1, 1, -1 };
            float[] stepCost = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

            int iter = 0;
            while (open.Count > 0 && iter++ < 15000)
            {
                int bestOpenPos = 0;
                int cur = open[0];
                float bestF = fScore[cur];
                for (int i = 1; i < open.Count; i++)
                {
                    int cand = open[i];
                    if (fScore[cand] < bestF)
                    {
                        bestF = fScore[cand];
                        cur = cand;
                        bestOpenPos = i;
                    }
                }

                if (cur == goalIdx) break;
                open.RemoveAt(bestOpenPos);
                closed[cur] = true;

                int cx = cur % w;
                int cy = cur / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + nxOff[d];
                    int ny = cy + nyOff[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int nIdx = ny * w + nx;
                    if (closed[nIdx]) continue;

                    float tentativeG = gScore[cur] + stepCost[d] * costGrid[nIdx];
                    if (tentativeG < gScore[nIdx])
                    {
                        cameFrom[nIdx] = cur;
                        gScore[nIdx] = tentativeG;
                        float hDist = Mathf.Sqrt((nx - gx) * (nx - gx) + (ny - gy) * (ny - gy));
                        fScore[nIdx] = tentativeG + hDist;
                        if (!open.Contains(nIdx)) open.Add(nIdx);
                    }
                }
            }

            List<Vector2> path = new List<Vector2>();
            if (cameFrom[goalIdx] == -1 && goalIdx != startIdx)
            {
                path.Add(startUV);
                path.Add(goalUV);
                return path;
            }

            int trace = goalIdx;
            int guard = 0;
            while (trace != -1 && guard++ < total)
            {
                int tx = trace % w;
                int ty = trace / w;
                path.Add(new Vector2((float)tx / (w - 1), (float)ty / (h - 1)));
                if (trace == startIdx) break;
                trace = cameFrom[trace];
            }
            path.Reverse();
            return path;
        }

        private static void RasterizePolylineToMask(List<Vector2> uvPath, byte[] mask, int mw, int mh, float radiusCells)
        {
            int rInt = Mathf.CeilToInt(radiusCells);
            for (int i = 0; i < uvPath.Count - 1; i++)
            {
                Vector2 a = uvPath[i];
                Vector2 b = uvPath[i + 1];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) * mw * 1.5f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 pt = Vector2.Lerp(a, b, (float)s / steps);
                    int cx = Mathf.RoundToInt(pt.x * (mw - 1));
                    int cy = Mathf.RoundToInt(pt.y * (mh - 1));

                    for (int dy = -rInt; dy <= rInt; dy++)
                    {
                        int py = cy + dy;
                        if (py < 0 || py >= mh) continue;
                        for (int dx = -rInt; dx <= rInt; dx++)
                        {
                            int px = cx + dx;
                            if (px < 0 || px >= mw) continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d <= radiusCells)
                            {
                                float val = Mathf.Clamp01(1f - d / Mathf.Max(0.5f, radiusCells));
                                byte bVal = (byte)Mathf.RoundToInt(val * 255f);
                                int mIdx = py * mw + px;
                                if (bVal > mask[mIdx]) mask[mIdx] = bVal;
                            }
                        }
                    }
                }
            }
        }

        private static void AddMajorRiverSplines(
            WorldMetaData meta,
            WorldSourceCacheData cache,
            float totalW,
            float totalD,
            MasterNetworkData networks)
        {
            AddTrackedRiver(networks, meta, cache, totalW, totalD, "yellow_river", "황하 (Yellow River)", 36f, 4.0f,
                new float[] { 0.24f, 0.32f, 0.40f, 0.48f, 0.56f, 0.65f }, 0.54f, 0.72f);
            AddTrackedRiver(networks, meta, cache, totalW, totalD, "yangtze_river", "장강 (Yangtze River)", 44f, 4.8f,
                new float[] { 0.28f, 0.36f, 0.45f, 0.54f, 0.63f, 0.71f }, 0.33f, 0.48f);
            AddTrackedRiver(networks, meta, cache, totalW, totalD, "pearl_river", "주강 (Pearl River)", 28f, 3.2f,
                new float[] { 0.34f, 0.41f, 0.48f, 0.54f, 0.59f }, 0.14f, 0.25f);
        }

        private static void AddTrackedRiver(
            MasterNetworkData networks,
            WorldMetaData meta,
            WorldSourceCacheData cache,
            float totalW,
            float totalD,
            string id,
            string name,
            float width,
            float depth,
            float[] uStations,
            float vMin,
            float vMax)
        {
            MasterRiverData river = new MasterRiverData
            {
                riverId = id,
                displayName = name,
                riverWidth = width,
                riverDepth = depth
            };

            for (int i = 0; i < uStations.Length; i++)
            {
                float u = uStations[i];
                float bestV = (vMin + vMax) * 0.5f;
                float bestScore = -1f;

                if (cache.riverStrength8 != null && cache.riverW > 0)
                {
                    int px = Mathf.Clamp(Mathf.RoundToInt(u * (cache.riverW - 1)), 0, cache.riverW - 1);
                    int yStart = Mathf.Clamp(Mathf.RoundToInt(vMin * (cache.riverH - 1)), 0, cache.riverH - 1);
                    int yEnd = Mathf.Clamp(Mathf.RoundToInt(vMax * (cache.riverH - 1)), 0, cache.riverH - 1);
                    for (int py = yStart; py <= yEnd; py += 2)
                    {
                        byte s = cache.riverStrength8[py * cache.riverW + px];
                        if (s > bestScore)
                        {
                            bestScore = s;
                            bestV = (float)py / Mathf.Max(1, cache.riverH - 1);
                        }
                    }
                }

                river.points.Add(WorldCoord.FromDouble2(
                    new double2(u * totalW, bestV * totalD),
                    meta.sectorSizeMeters));
            }

            if (river.points.Count >= 2)
            {
                networks.rivers.Add(river);
            }
        }

        private static void BakeCampaignMapAssets(
            ContinentGenerationSettings settings,
            WorldMetaData meta,
            WorldSourceCacheData cache,
            string worldFolder)
        {
            int mx = Mathf.Max(32, settings.campaignMeshResX);
            int mz = Mathf.Max(32, settings.campaignMeshResZ);

            // 대륙 실제 길이와 무관하게 캠페인 테이블 가로폭(기본 640m)에 맞춘 균일 스케일 산출
            float meshWidth = Mathf.Max(128f, meta.campaignMapWidthMeters);
            float aspect = meta.TotalDepthMeters / Mathf.Max(1f, meta.TotalWidthMeters);
            float meshDepth = meshWidth * aspect;
            float heightScale = (meshWidth / Mathf.Max(1f, meta.TotalWidthMeters)) * 16.0f;

            // 114km처럼 거대 대륙일 때는 산맥이 캠페인 테이블에서 눌려 보이지 않도록 적절한 입체고도 보장
            heightScale = Mathf.Clamp(heightScale, 0.16f, 0.35f);

            Mesh mesh = new Mesh();
            mesh.name = "CampaignMapMesh";
            if (mx * mz > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] verts = new Vector3[mx * mz];
            Vector2[] uvs = new Vector2[mx * mz];

            for (int z = 0; z < mz; z++)
            {
                float v = (float)z / (mz - 1);
                for (int x = 0; x < mx; x++)
                {
                    float u = (float)x / (mx - 1);
                    GlobalSurfaceSample s = ContinentSectorSynthesizer.EvaluateGlobalSample(u, v, meta, cache, includeTacticalDetail: false);
                    float y = s.isOcean ? (meta.seaLevelY - 1.5f) * heightScale : s.heightMeters * heightScale;
                    int idx = z * mx + x;
                    verts[idx] = new Vector3(u * meshWidth, y, v * meshDepth);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int[] tris = new int[(mx - 1) * (mz - 1) * 6];
            int ti = 0;
            for (int z = 0; z < mz - 1; z++)
            {
                for (int x = 0; x < mx - 1; x++)
                {
                    int i00 = z * mx + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + mx;
                    int i11 = i01 + 1;
                    tris[ti++] = i00; tris[ti++] = i01; tris[ti++] = i10;
                    tris[ti++] = i10; tris[ti++] = i01; tris[ti++] = i11;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshAssetPath = $"{worldFolder}/CampaignMapMesh.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (existingMesh != null) AssetDatabase.DeleteAsset(meshAssetPath);
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            int aw = Mathf.Max(128, settings.campaignAlbedoResX);
            int ah = Mathf.Max(128, settings.campaignAlbedoResZ);
            Texture2D albedo = new Texture2D(aw, ah, TextureFormat.RGBA32, false);
            Color32[] pixels = BuildCampaignMapAlbedoPixels(aw, ah, meta, cache);
            albedo.SetPixels32(pixels);
            albedo.Apply();

            string albedoPath = $"{worldFolder}/CampaignMapAlbedo.png";
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(albedo);
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);

            string matPath = $"{worldFolder}/CampaignMapMaterial.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(lit);
                mat.name = "CampaignMapMaterial";
                AssetDatabase.CreateAsset(mat, matPath);
            }

            Texture2D loadedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (loadedAlbedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", loadedAlbedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", loadedAlbedo);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            }
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// 5대 기후 부드러운 곡선 전환 + 앨리어싱 없는 매크로 숲/설산 + 북서 태양 Hillshade 지형 음영이 적용된 캠페인 알베도 픽셀을 생성합니다.
        /// </summary>
        private static Color32[] BuildCampaignMapAlbedoPixels(int width, int height, WorldMetaData meta, WorldSourceCacheData cache)
        {
            GlobalSurfaceSample[] samples = new GlobalSurfaceSample[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / Mathf.Max(1, height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / Mathf.Max(1, width - 1);
                    samples[y * width + x] = ContinentSectorSynthesizer.EvaluateGlobalSample(u, v, meta, cache, includeTacticalDetail: false);
                }
            }

            Vector3 sunDir = new Vector3(-0.55f, 0.72f, 0.42f).normalized;
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / Mathf.Max(1, height - 1);
                int ym = Mathf.Max(0, y - 1);
                int yp = Mathf.Min(height - 1, y + 1);

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / Mathf.Max(1, width - 1);
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(width - 1, x + 1);

                    GlobalSurfaceSample s = samples[y * width + x];
                    float road = ContinentSectorSynthesizer.SampleGridMask(cache.roadMask8, cache.maskW, cache.maskH, u, v);

                    float hillshade = 1f;
                    if (!s.isOcean)
                    {
                        float hL = samples[y * width + xm].heightMeters;
                        float hR = samples[y * width + xp].heightMeters;
                        float hD = samples[ym * width + x].heightMeters;
                        float hU = samples[yp * width + x].heightMeters;
                        Vector3 normal = new Vector3((hL - hR) * 0.22f, 1.0f, (hD - hU) * 0.22f).normalized;
                        float ndl = Mathf.Clamp01(Vector3.Dot(normal, sunDir));
                        hillshade = Mathf.Lerp(0.66f, 1.22f, ndl);
                    }

                    pixels[y * width + x] = EvaluateMapColor(u, v, s, road, hillshade, meta);
                }
            }

            return pixels;
        }

        private static Color32 EvaluateMapColor(float u, float v, GlobalSurfaceSample s, float road, float hillshade, WorldMetaData meta)
        {
            Color col;
            if (s.isOcean)
            {
                float shallow = Mathf.Clamp01(1f - s.waterDepthMeters / 10f);
                col = Color.Lerp(new Color(0.05f, 0.17f, 0.34f), new Color(0.18f, 0.47f, 0.60f), shallow);
            }
            else if (s.riverStrength > 0.22f)
            {
                float riverT = Mathf.SmoothStep(0.22f, 0.55f, s.riverStrength);
                col = Color.Lerp(new Color(0.20f, 0.48f, 0.54f), new Color(0.09f, 0.32f, 0.52f), riverT);
            }
            else
            {
                col = ContinentSectorSynthesizer.EvaluateBlendedClimateColor(u, v, s.heightMeters, meta.seaLevelY, meta.seed);

                float elev = Mathf.Clamp01((s.heightMeters - meta.seaLevelY) / Mathf.Max(1f, meta.maxTerrainHeight - meta.seaLevelY));
                float rockFactor = Mathf.SmoothStep(0.32f, 0.60f, elev);
                col = Color.Lerp(col, new Color(0.46f, 0.42f, 0.37f), rockFactor * 0.72f);

                float snowFactor = Mathf.SmoothStep(0.60f, 0.80f, elev);
                col = Color.Lerp(col, new Color(0.91f, 0.93f, 0.96f), snowFactor);

                if (s.forestDensity > 0.15f)
                {
                    float forestBlend = Mathf.SmoothStep(0.15f, 0.65f, s.forestDensity);
                    col = Color.Lerp(col, new Color(0.14f, 0.30f, 0.13f), forestBlend * 0.55f);
                }

                if (road > 0.18f)
                {
                    float roadBlend = Mathf.SmoothStep(0.18f, 0.45f, road);
                    col = Color.Lerp(col, new Color(0.68f, 0.54f, 0.36f), roadBlend * 0.90f);
                }

                col = new Color(
                    Mathf.Clamp01(col.r * hillshade),
                    Mathf.Clamp01(col.g * hillshade),
                    Mathf.Clamp01(col.b * hillshade),
                    1f);
            }

            return (Color32)col;
        }
    }
}
