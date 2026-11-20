using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 대륙 기후대 열거형 (TotalWarTerrainTheme.ClimateType과 1:1 대응)
    /// </summary>
    public enum ContinentClimateId : byte
    {
        Temperate = 0,   // 중원 온대 평원
        Nanman = 1,      // 남만 열대 정글
        Arid = 2,        // 서량 건조 지대
        Subtropical = 3, // 강남 아열대 수변
        Cold = 4         // 하북·요동 한랭 설원
    }

    /// <summary>
    /// 대륙 마스터 전략 거점 유형
    /// </summary>
    public enum StrategicNodeType : byte
    {
        CapitalCity = 0,
        WalledCity = 1,
        Town = 2,
        MountainGate = 3,
        Port = 4,
        RiverFord = 5
    }

    /// <summary>
    /// 섹터 내부에 포함된 지형·수계·네트워크 속성 플래그
    /// </summary>
    [Flags]
    public enum SectorContentFlags : byte
    {
        None = 0,
        HasLand = 1 << 0,
        HasSea = 1 << 1,
        HasRiver = 1 << 2,
        HasRoad = 1 << 3,
        HasForest = 1 << 4
    }

    /// <summary>
    /// 대륙 전체 메타데이터 (WorldMeta.json)
    /// </summary>
    [Serializable]
    public class WorldMetaData
    {
        public string continentId = "ThreeKingdoms_China";
        public string displayName = "삼국지 중원 대륙 (TW3K 풀 스케일)";
        public string sourceReference = "TotalWar:ThreeKingdoms 892x703";

        public int sectorCountX = 223;
        public int sectorCountZ = 175;
        public float sectorSizeMeters = 512f;

        public int heightmapResolution = 129;
        public int alphamapResolution = 128;

        public float maxTerrainHeight = 140f;
        public float seaLevelY = 20f;
        public float worldMinY = 0f;
        public int seed = 190;
        public bool useOnDemandSynthesis = true;
        public float campaignMapWidthMeters = 640f;
        public string generatedUtcIso = "";

        public float TotalWidthMeters => sectorCountX * sectorSizeMeters;
        public float TotalDepthMeters => sectorCountZ * sectorSizeMeters;
        public float CampaignScaleRatio => (TotalWidthMeters > 0.001f) ? (campaignMapWidthMeters / TotalWidthMeters) : (1f / 16f);
    }

    /// <summary>
    /// 114km 이상 초대형 대륙의 섹터를 런타임에 밀리초 단위로 즉석 합성하기 위한 초경량 참조 캐시 데이터 (WorldSourceCache.bin)
    /// </summary>
    public class WorldSourceCacheData
    {
        public int xmlW = 892;
        public int xmlH = 703;
        public byte[] cellKinds;        // 0: Ocean, 1: PlayableLand, 2: HighMountain
        public short[] coastDistanceQ16; // 해안선 부호 거리 (dist * 16, 바다 음수 / 육지 양수)
        public byte[] mountainDepth8;   // 내륙 산맥 깊이 (0..255)

        public int riverW;
        public int riverH;
        public byte[] riverStrength8;   // 0..255 (황하·장강·주강 강줄기 및 호수 강도)

        public int maskW;
        public int maskH;
        public byte[] roadMask8;        // 0..255 (대륙 마스터 도로 광역 마스크)
    }

    /// <summary>
    /// 전략 거점(도시, 관문, 항구, 도하 여울목) 정의 데이터
    /// </summary>
    [Serializable]
    public class StrategicNodeData
    {
        public string nodeId;
        public string displayName;
        public StrategicNodeType nodeType;
        public WorldCoord coord;
        public ContinentClimateId climate;
        public string initialFactionId = "Neutral";
        public int garrisonStrength = 1000;
        public bool hasPortAccess = false;
    }

    /// <summary>
    /// 대륙 거점 간 마스터 도로 스플라인 데이터
    /// </summary>
    [Serializable]
    public class MasterRoadData
    {
        public string roadId;
        public string fromNodeId;
        public string toNodeId;
        public float roadWidth = 7.5f;
        public List<WorldCoord> points = new List<WorldCoord>();
    }

    /// <summary>
    /// 대륙 대하천(황하, 장강, 주강 등) 마스터 스플라인 데이터
    /// </summary>
    [Serializable]
    public class MasterRiverData
    {
        public string riverId;
        public string displayName;
        public float riverWidth = 28f;
        public float riverDepth = 3.5f;
        public List<WorldCoord> points = new List<WorldCoord>();
    }

    /// <summary>
    /// 대륙 숲 군락지 마스터 데이터
    /// </summary>
    [Serializable]
    public class MasterForestData
    {
        public string forestId;
        public WorldCoord center;
        public float radius = 38f;
        public ContinentClimateId climate = ContinentClimateId.Temperate;
        public float treeDensity = 1.0f;
    }

    /// <summary>
    /// 대륙 전체의 전략 거점·도로·강·숲 마스터 네트워크 (MasterNetworks.json)
    /// </summary>
    [Serializable]
    public class MasterNetworkData
    {
        public List<StrategicNodeData> nodes = new List<StrategicNodeData>();
        public List<MasterRoadData> roads = new List<MasterRoadData>();
        public List<MasterRiverData> rivers = new List<MasterRiverData>();
        public List<MasterForestData> forests = new List<MasterForestData>();
    }

    /// <summary>
    /// 단일 섹터(512m x 512m)의 초고속 바이너리 지형·스플랫·수심·기후 데이터 (Sector_X_Z.bin)
    /// 인접 섹터와 높이 경계 1픽셀을 동일 수학 함수로 공유해 타일 이음새(Seam)가 0입니다.
    /// </summary>
    public class SectorBinaryData
    {
        public int sectorX;
        public int sectorZ;
        public int heightRes;
        public int splatRes;
        public ContinentClimateId dominantClimate;
        public SectorContentFlags flags;
        public float minHeight;
        public float maxHeight;
        public int vegetationSeed;

        // [z * heightRes + x] 순서, 0..65535 정규화 고도
        public ushort[] heights16;

        // [(z * splatRes + x) * 5 + channel], 채널: [0]잔디 [1]절벽 [2]흙/도로 [3]숲 [4]자갈
        public byte[] splatWeights5;

        // [z * splatRes + x], 0.1m 단위 수심 (0..255 => 0.0m ~ 25.5m)
        public byte[] waterDepth8;

        // [z * splatRes + x], 하위 3비트: 기후대(0..4), 비트3: 강, 비트4: 도로, 비트5: 숲
        public byte[] biomeAndFlags;

        /// <summary>
        /// Unity TerrainData.SetHeights() 규격인 [z, x] 정규화(0..1) 2D 배열로 변환합니다.
        /// </summary>
        public float[,] ToUnityTerrainHeights()
        {
            float[,] result = new float[heightRes, heightRes];
            if (heights16 == null || heights16.Length < heightRes * heightRes) return result;

            const float inv65535 = 1f / 65535f;
            for (int z = 0; z < heightRes; z++)
            {
                int row = z * heightRes;
                for (int x = 0; x < heightRes; x++)
                {
                    result[z, x] = heights16[row + x] * inv65535;
                }
            }
            return result;
        }

        /// <summary>
        /// Unity TerrainData.SetAlphamaps() 규격인 [z, x, 5] 3D 알파맵 배열로 변환합니다.
        /// </summary>
        public float[,,] ToUnityAlphamaps()
        {
            float[,,] result = new float[splatRes, splatRes, 5];
            if (splatWeights5 == null || splatWeights5.Length < splatRes * splatRes * 5) return result;

            for (int z = 0; z < splatRes; z++)
            {
                for (int x = 0; x < splatRes; x++)
                {
                    int baseIdx = (z * splatRes + x) * 5;
                    float sum = 0f;
                    for (int c = 0; c < 5; c++)
                    {
                        float w = splatWeights5[baseIdx + c];
                        result[z, x, c] = w;
                        sum += w;
                    }

                    if (sum > 0.001f)
                    {
                        float invSum = 1f / sum;
                        for (int c = 0; c < 5; c++)
                        {
                            result[z, x, c] *= invSum;
                        }
                    }
                    else
                    {
                        result[z, x, 0] = 1f;
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 대륙 메타데이터, 마스터 네트워크 및 섹터 바이너리 파일 입출력 유틸리티
    /// </summary>
    public static class SectorBinaryIO
    {
        private const string SectorMagic = "MTWS";
        private const int SectorFormatVersion = 1;
        private const string SourceCacheMagic = "MTWC";
        private const int SourceCacheFormatVersion = 1;

        public static string GetWorldFolder(string outputRoot, string continentId)
        {
            return Path.Combine(outputRoot, continentId).Replace('\\', '/');
        }

        public static string GetSectorsFolder(string worldFolder)
        {
            return Path.Combine(worldFolder, "Sectors").Replace('\\', '/');
        }

        public static string GetSectorFilePath(string worldFolder, int sectorX, int sectorZ)
        {
            return Path.Combine(GetSectorsFolder(worldFolder), $"Sector_{sectorX}_{sectorZ}.bin").Replace('\\', '/');
        }

        public static string GetWorldMetaPath(string worldFolder)
        {
            return Path.Combine(worldFolder, "WorldMeta.json").Replace('\\', '/');
        }

        public static string GetMasterNetworksPath(string worldFolder)
        {
            return Path.Combine(worldFolder, "MasterNetworks.json").Replace('\\', '/');
        }

        public static string GetSourceCachePath(string worldFolder)
        {
            return Path.Combine(worldFolder, "WorldSourceCache.bin").Replace('\\', '/');
        }

        public static void SaveSourceCache(string worldFolder, WorldSourceCacheData cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            Directory.CreateDirectory(worldFolder);

            string path = GetSourceCachePath(worldFolder);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            using (BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII))
            {
                bw.Write(Encoding.ASCII.GetBytes(SourceCacheMagic));
                bw.Write(SourceCacheFormatVersion);

                bw.Write(cache.xmlW);
                bw.Write(cache.xmlH);
                int xmlLen = cache.xmlW * cache.xmlH;
                bw.Write(cache.cellKinds, 0, xmlLen);
                for (int i = 0; i < xmlLen; i++)
                {
                    bw.Write(cache.coastDistanceQ16[i]);
                }
                bw.Write(cache.mountainDepth8, 0, xmlLen);

                bw.Write(cache.riverW);
                bw.Write(cache.riverH);
                int riverLen = cache.riverW * cache.riverH;
                if (riverLen > 0 && cache.riverStrength8 != null)
                {
                    bw.Write(cache.riverStrength8, 0, riverLen);
                }

                bw.Write(cache.maskW);
                bw.Write(cache.maskH);
                int maskLen = cache.maskW * cache.maskH;
                if (maskLen > 0 && cache.roadMask8 != null)
                {
                    bw.Write(cache.roadMask8, 0, maskLen);
                }
            }
        }

        public static WorldSourceCacheData LoadSourceCache(string worldFolder)
        {
            string path = GetSourceCachePath(worldFolder);
            if (!File.Exists(path)) return null;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            using (BinaryReader br = new BinaryReader(fs, Encoding.ASCII))
            {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != SourceCacheMagic)
                {
                    throw new InvalidDataException($"잘못된 대륙 참조 캐시 헤더입니다: {path}");
                }

                int version = br.ReadInt32();
                if (version != SourceCacheFormatVersion)
                {
                    throw new InvalidDataException($"지원하지 않는 대륙 참조 캐시 버전({version})입니다: {path}");
                }

                int xmlW = br.ReadInt32();
                int xmlH = br.ReadInt32();
                int xmlLen = xmlW * xmlH;

                WorldSourceCacheData cache = new WorldSourceCacheData
                {
                    xmlW = xmlW,
                    xmlH = xmlH,
                    cellKinds = br.ReadBytes(xmlLen),
                    coastDistanceQ16 = new short[xmlLen]
                };

                for (int i = 0; i < xmlLen; i++)
                {
                    cache.coastDistanceQ16[i] = br.ReadInt16();
                }
                cache.mountainDepth8 = br.ReadBytes(xmlLen);

                cache.riverW = br.ReadInt32();
                cache.riverH = br.ReadInt32();
                int riverLen = cache.riverW * cache.riverH;
                cache.riverStrength8 = (riverLen > 0) ? br.ReadBytes(riverLen) : Array.Empty<byte>();

                cache.maskW = br.ReadInt32();
                cache.maskH = br.ReadInt32();
                int maskLen = cache.maskW * cache.maskH;
                cache.roadMask8 = (maskLen > 0) ? br.ReadBytes(maskLen) : Array.Empty<byte>();

                return cache;
            }
        }

        public static void SaveWorldMeta(string worldFolder, WorldMetaData meta)
        {
            Directory.CreateDirectory(worldFolder);
            string json = JsonUtility.ToJson(meta, true);
            File.WriteAllText(GetWorldMetaPath(worldFolder), json, Encoding.UTF8);
        }

        public static WorldMetaData LoadWorldMeta(string worldFolder)
        {
            string path = GetWorldMetaPath(worldFolder);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<WorldMetaData>(json);
        }

        public static void SaveMasterNetworks(string worldFolder, MasterNetworkData networks)
        {
            Directory.CreateDirectory(worldFolder);
            string json = JsonUtility.ToJson(networks, true);
            File.WriteAllText(GetMasterNetworksPath(worldFolder), json, Encoding.UTF8);
        }

        public static MasterNetworkData LoadMasterNetworks(string worldFolder)
        {
            string path = GetMasterNetworksPath(worldFolder);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<MasterNetworkData>(json);
        }

        /// <summary>
        /// 단일 섹터 데이터를 고속 바이너리 파일(Sector_X_Z.bin)로 디스크에 저장합니다.
        /// </summary>
        public static void SaveSector(string worldFolder, SectorBinaryData sector)
        {
            if (sector == null) throw new ArgumentNullException(nameof(sector));

            string sectorsDir = GetSectorsFolder(worldFolder);
            Directory.CreateDirectory(sectorsDir);

            string filePath = GetSectorFilePath(worldFolder, sector.sectorX, sector.sectorZ);
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            using (BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII))
            {
                bw.Write(Encoding.ASCII.GetBytes(SectorMagic));
                bw.Write(SectorFormatVersion);

                bw.Write(sector.sectorX);
                bw.Write(sector.sectorZ);
                bw.Write(sector.heightRes);
                bw.Write(sector.splatRes);
                bw.Write((byte)sector.dominantClimate);
                bw.Write((byte)sector.flags);
                bw.Write(sector.minHeight);
                bw.Write(sector.maxHeight);
                bw.Write(sector.vegetationSeed);

                int hCount = sector.heightRes * sector.heightRes;
                for (int i = 0; i < hCount; i++)
                {
                    bw.Write(sector.heights16[i]);
                }

                bw.Write(sector.splatWeights5, 0, sector.splatRes * sector.splatRes * 5);
                bw.Write(sector.waterDepth8, 0, sector.splatRes * sector.splatRes);
                bw.Write(sector.biomeAndFlags, 0, sector.splatRes * sector.splatRes);
            }
        }

        /// <summary>
        /// 지정된 좌표의 섹터 바이너리 파일을 로드합니다.
        /// </summary>
        public static SectorBinaryData LoadSector(string worldFolder, int sectorX, int sectorZ)
        {
            string filePath = GetSectorFilePath(worldFolder, sectorX, sectorZ);
            if (!File.Exists(filePath)) return null;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            using (BinaryReader br = new BinaryReader(fs, Encoding.ASCII))
            {
                byte[] magicBytes = br.ReadBytes(4);
                string magic = Encoding.ASCII.GetString(magicBytes);
                if (magic != SectorMagic)
                {
                    throw new InvalidDataException($"잘못된 섹터 파일 헤더입니다: {filePath}");
                }

                int version = br.ReadInt32();
                if (version != SectorFormatVersion)
                {
                    throw new InvalidDataException($"지원하지 않는 섹터 파일 버전({version})입니다: {filePath}");
                }

                SectorBinaryData sector = new SectorBinaryData
                {
                    sectorX = br.ReadInt32(),
                    sectorZ = br.ReadInt32(),
                    heightRes = br.ReadInt32(),
                    splatRes = br.ReadInt32(),
                    dominantClimate = (ContinentClimateId)br.ReadByte(),
                    flags = (SectorContentFlags)br.ReadByte(),
                    minHeight = br.ReadSingle(),
                    maxHeight = br.ReadSingle(),
                    vegetationSeed = br.ReadInt32()
                };

                int hCount = sector.heightRes * sector.heightRes;
                sector.heights16 = new ushort[hCount];
                for (int i = 0; i < hCount; i++)
                {
                    sector.heights16[i] = br.ReadUInt16();
                }

                int sCount = sector.splatRes * sector.splatRes;
                sector.splatWeights5 = br.ReadBytes(sCount * 5);
                sector.waterDepth8 = br.ReadBytes(sCount);
                sector.biomeAndFlags = br.ReadBytes(sCount);

                return sector;
            }
        }
    }
}
