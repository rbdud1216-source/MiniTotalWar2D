using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniTotalWar
{
    /// <summary>
    /// 🌍 [삼국지 토탈워 전장 기후 및 터레인 테마 프리셋]
    /// 삼탈워 원작의 기후(Climate: Arid, Temperate, Nanman, Subtropical, Cold)별 지형 텍스처 레이어와 
    /// 238종 디테일 잔디 모델의 1:1 종속 매핑 관계를 보존하여 관리하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTerrainTheme", menuName = "TotalWar/Terrain Theme Preset")]
    public class TotalWarTerrainTheme : ScriptableObject
    {
        public enum ClimateType
        {
            [InspectorName("중원 온대 평원 (Temperate)")]
            Temperate,      // 중원 온대 평원 (tmp_grass0, tmp_forest0 등)
            [InspectorName("남만 열대 정글 (Nanman)")]
            Nanman,         // 남만 밀림 (nan_grass0, nan_mud0 등)
            [InspectorName("서량 건조 사막 (Arid)")]
            Arid,           // 서량 건조 지대 (ard_grass0, ard_grass1 등)
            [InspectorName("강남 아열대 수변 (Subtropical)")]
            Subtropical,    // 강남 수변 및 아열대 (sbt_grass0, sbt_forest0 등)
            [InspectorName("하북 한랭 설원 (Cold)")]
            Cold            // 하북/요동 한랭 지대 (cld_grass0, cld_snow0 등)
        }

        [System.Serializable]
        public struct DetailGrassConfig
        {
            [Tooltip("디테일 잔디 3D 메시 또는 프리팹")]
            public GameObject grassPrefab;
            [Tooltip("마른 들풀/가을용 대체 프리팹 (선택)")]
            public GameObject dryGrassPrefab;
            [Range(0.1f, 2f)]
            [Tooltip("잔디 기본 밀도 가중치")]
            public float densityFactor;
            [Tooltip("잔디 최소/최대 너비 범위")]
            public Vector2 widthRange;
            [Tooltip("잔디 최소/최대 높이 범위")]
            public Vector2 heightRange;
            [Tooltip("지형 경사면 정렬 가중치 (0: 항상 하늘 방향, 1: 지형 노멀과 일치)")]
            [Range(0f, 1f)]
            public float alignToGround;
        }

        [Header("🌍 기후 환경 분류")]
        [Tooltip("전장 기후 분류")]
        public ClimateType climate = ClimateType.Temperate;

        [Header("🎨 사계절 지형 레이어 (Terrain Layers)")]
        [Tooltip("여름 기본 녹음 잔디 레이어 (su)")]
        public TerrainLayer summerGrassLayer;
        [Tooltip("가을 낙엽 및 마른 잔디 레이어 (au)")]
        public TerrainLayer autumnGrassLayer;
        [Tooltip("겨울 얼어붙은 지면 레이어 (wi)")]
        public TerrainLayer winterGrassLayer;
        [Tooltip("숲속 그늘 바닥 부엽토 레이어")]
        public TerrainLayer forestFloorLayer;
        [Tooltip("절벽 및 암반 레이어")]
        public TerrainLayer cliffRockLayer;
        [Tooltip("흙길 및 바닥 비포장로 레이어")]
        public TerrainLayer dirtRoadLayer;

        [Header("🌾 기후 종속 디테일 잔디 프리셋 (Detail Grass: 10~15종 변형)")]
        [Tooltip("해당 기후의 바닥 레이어에 종속 살포되는 디테일 잔디 목록")]
        public List<DetailGrassConfig> detailGrassConfigs = new List<DetailGrassConfig>();

        [Header("⚙️ 전역 렌더링 파라미터")]
        [Range(30f, 250f)]
        [Tooltip("디테일 잔디 최대 표시 거리 (m)")]
        public float detailDistance = 90f;
        [Range(0f, 1f)]
        [Tooltip("디테일 잔디 밀도 (Density)")]
        public float detailDensity = 0.65f;

        /// <summary>
        /// 지정한 Terrain 컴포넌트에 이 기후 테마와 계절 텍스처를 적용합니다.
        /// </summary>
        public void ApplyThemeToTerrain(Terrain terrain, TacticalForestZone.Season season, float snowAmount = 0f)
        {
            if (terrain == null || terrain.terrainData == null) return;

            TerrainData tData = terrain.terrainData;

            // 1. 계절에 맞는 기본 지형 레이어 선정
            TerrainLayer primaryGrass = summerGrassLayer;
            if (season == TacticalForestZone.Season.Autumn && autumnGrassLayer != null)
            {
                primaryGrass = autumnGrassLayer;
            }
            else if (season == TacticalForestZone.Season.Winter && winterGrassLayer != null)
            {
                primaryGrass = winterGrassLayer;
            }

            // 레이어 배열 구성 (기존 레이어 슬롯 보존)
            List<TerrainLayer> newLayers = new List<TerrainLayer>();
            if (primaryGrass != null) newLayers.Add(primaryGrass);
            if (forestFloorLayer != null) newLayers.Add(forestFloorLayer);
            if (cliffRockLayer != null) newLayers.Add(cliffRockLayer);
            if (dirtRoadLayer != null) newLayers.Add(dirtRoadLayer);
            // 🛣️ [표준 5슬롯 지형 레이어 순서 보존]: [0]주 잔디, [1]절벽 암반, [2]흙길/도로, [3]숲 바닥, [4+]수변 자갈/사용자 슬롯
            // 기존 4슬롯 재배열로 [1]암반/[2]흙길/[3]숲/[4]자갈 스플랫맵 및 도로 채널이 뒤바뀌거나 소실되는 문제 방지
            TerrainLayer[] existingLayers = tData.terrainLayers;
            int targetCount = Mathf.Max(4, existingLayers != null ? existingLayers.Length : 0);
            List<TerrainLayer> newLayers = new List<TerrainLayer>(targetCount);

            TerrainLayer slot0 = primaryGrass ?? (existingLayers != null && existingLayers.Length > 0 ? existingLayers[0] : null);
            TerrainLayer slot1 = cliffRockLayer ?? (existingLayers != null && existingLayers.Length > 1 ? existingLayers[1] : null);
            TerrainLayer slot2 = dirtRoadLayer ?? (existingLayers != null && existingLayers.Length > 2 ? existingLayers[2] : null);
            TerrainLayer slot3 = forestFloorLayer ?? (existingLayers != null && existingLayers.Length > 3 ? existingLayers[3] : null);

            if (slot0 != null) newLayers.Add(slot0);
            if (slot1 != null) newLayers.Add(slot1);
            if (slot2 != null) newLayers.Add(slot2);
            if (slot3 != null) newLayers.Add(slot3);

            // 5번째(Slot #4 자갈/수변 등) 이후 추가 레이어는 유실 없이 그대로 보존
            if (existingLayers != null && existingLayers.Length > 4)
            {
                for (int i = 4; i < existingLayers.Length; i++)
                {
                    if (existingLayers[i] != null)
                    {
                        newLayers.Add(existingLayers[i]);
                    }
                }
            }

            if (newLayers.Count > 0)
            {
                tData.terrainLayers = newLayers.ToArray();
            }

            // 2. 디테일 잔디 프로토타입 갱신
            // 💡 [3D 보호]: 이미 삼탈워 공식 3D 풀포기(usePrototypeMesh)가 장착되어 있으면 2D 빌보드로 덮어쓰지 않고 보존합니다.
            bool alreadyHas3DGrass = false;
            if (tData.detailPrototypes != null && tData.detailPrototypes.Length > 0)
            {
                foreach (var dp in tData.detailPrototypes)
                {
                    if (dp.usePrototypeMesh && dp.prototype != null)
                    {
                        alreadyHas3DGrass = true;
                        break;
                    }
                }
            }

            if (!alreadyHas3DGrass && detailGrassConfigs != null && detailGrassConfigs.Count > 0)
            {
                DetailPrototype[] prototypes = new DetailPrototype[detailGrassConfigs.Count];
                bool isAutumnOrWinter = (season == TacticalForestZone.Season.Autumn || season == TacticalForestZone.Season.Winter);

                // 삼탈워 공식 잔디 텍스처 풀 사전 로드
                Texture2D[] grassTextures = LoadGrassTexturesPool(season);

                for (int i = 0; i < detailGrassConfigs.Count; i++)
                {
                    var cfg = detailGrassConfigs[i];
                    DetailPrototype dp = new DetailPrototype();

                    // 가을/겨울에 대체 마른 잔디가 있으면 교체
                    GameObject targetPrefab = (isAutumnOrWinter && cfg.dryGrassPrefab != null) ? cfg.dryGrassPrefab : cfg.grassPrefab;

                    // 1. 삼탈워 공식 잔디 텍스처 매핑 (Detail Texture is missing 원천 차단)
                    Texture2D assignedTex = null;
                    if (grassTextures != null && grassTextures.Length > 0)
                    {
                        assignedTex = grassTextures[i % grassTextures.Length];
                    }

                    dp.usePrototypeMesh = false;
                    dp.renderMode = DetailRenderMode.GrassBillboard;
                    dp.prototypeTexture = assignedTex;

                    // 전장에서 자연스럽고 사실적인 잔디 크기 (0.5m ~ 1.0m)
                    bool isFlower = (assignedTex != null && assignedTex.name.Contains("flower"));
                    if (isFlower)
                    {
                        dp.minWidth = 0.4f;
                        dp.maxWidth = 0.7f;
                        dp.minHeight = 0.35f;
                        dp.maxHeight = 0.65f;
                    }
                    else
                    {
                        dp.minWidth = 0.6f;
                        dp.maxWidth = 1.0f;
                        dp.minHeight = 0.5f;
                        dp.maxHeight = 0.9f;
                    }
                    dp.alignToGround = cfg.alignToGround;

                    // 원본 삼탈워 고해상도 텍스처 색상을 100% 그대로 살리는 자연스러운 틴트
                    if (season == TacticalForestZone.Season.Winter && snowAmount > 0.3f)
                    {
                        Color snowTint = Color.Lerp(Color.white, new Color(0.92f, 0.95f, 1.0f), snowAmount);
                        dp.dryColor = snowTint;
                        dp.healthyColor = snowTint;
                    }
                    else if (season == TacticalForestZone.Season.Autumn)
                    {
                        dp.dryColor = new Color(0.95f, 0.85f, 0.65f);
                        dp.healthyColor = new Color(1.0f, 0.92f, 0.75f);
                    }
                    else
                    {
                        // 봄/여름: 원본 텍스처 색상을 순수하게 보존
                        dp.dryColor = new Color(0.92f, 0.95f, 0.92f);
                        dp.healthyColor = Color.white;
                    }

                    prototypes[i] = dp;
                }

                tData.detailPrototypes = prototypes;
                terrain.detailObjectDistance = Mathf.Max(detailDistance, 250f); // 최대 가시거리 보장
                terrain.detailObjectDensity = 1.0f; // 최대 밀도 보장

                // 2D 잔디 비틀림 왜곡 원천 차단 (미풍 또는 고정)
                tData.wavingGrassStrength = 0.04f;
                tData.wavingGrassAmount = 0.05f;
                tData.wavingGrassSpeed = 0.08f;
            }

            // 전역 셰이더 프로퍼티 동기화
            Shader.SetGlobalFloat("_TotalWarTerrainSnowAmount", snowAmount);
            Shader.SetGlobalFloat("_TotalWarTerrainAutumnTint", season == TacticalForestZone.Season.Autumn ? 1.0f : 0.0f);
        }

        private static Texture2D[] cachedGrassTextures;

        /// <summary>
        /// 삼탈워 공식 순수 잔디 풀포기 및 야생화 텍스처 풀을 계절에 맞춰 로드합니다. (거대한 고사리/연잎 아틀라스 제외)
        /// </summary>
        private static Texture2D[] LoadGrassTexturesPool(TacticalForestZone.Season season)
        {
#if UNITY_EDITOR
            string baseDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures";
            string[] texNames;

            if (season == TacticalForestZone.Season.Autumn)
            {
                texNames = new[] {
                    "shrub_grassclump_au_base_colour.dds",
                    "shrub_grassclump_dry_a_billboard_au_base_colour.dds",
                    "shrub_grassclump_dry_seedheads_a_billboard_au_base_colour.dds",
                    "shrub_grassclump_tall_a_billboard_au_base_colour.dds",
                    "shrub_flower_b_billboard_base_colour.dds"
                };
            }
            else if (season == TacticalForestZone.Season.Winter)
            {
                texNames = new[] {
                    "shrub_grassclump_wi_base_colour.dds",
                    "shrub_grassclump_dry_a_billboard_wi_base_colour.dds",
                    "shrub_grassclump_tall_a_billboard_wi_base_colour.dds",
                    "shrub_grassclump_base_colour.dds"
                };
            }
            else
            {
                // 봄/여름: 싱그러운 녹색 야생 잔디 4종 + 노란 야생화 포인트 1종 (검게 타는 마른풀 원천 배제)
                texNames = new[] {
                    "shrub_grassclump_su_base_colour.dds",
                    "shrub_grassclump_base_colour.dds",
                    "sbt_grass_base_colour.dds",
                    "shrub_grassclump_tall_a_billboard_base_colour.dds",
                    "shrub_flower_a_billboard_base_colour.dds"
                };
            }

            List<Texture2D> list = new List<Texture2D>();
            for (int i = 0; i < texNames.Length; i++)
            {
                string p = $"{baseDir}/{texNames[i]}";
                Texture2D t = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) list.Add(t);
            }

            // 폴백: 아무것도 없으면 디렉터리 내 첫 번째 텍스처
            if (list.Count == 0 && System.IO.Directory.Exists(baseDir))
            {
                string[] allDds = System.IO.Directory.GetFiles(baseDir, "*grass*base_colour*.dds");
                for (int i = 0; i < Mathf.Min(6, allDds.Length); i++)
                {
                    string rel = allDds[i].Replace('\\', '/');
                    Texture2D fb = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
                    if (fb != null) list.Add(fb);
                }
            }

            return list.ToArray();
#else
            return Array.Empty<Texture2D>();
#endif
        }

        private static readonly Dictionary<string, Material> detailMatCache = new Dictionary<string, Material>();

        /// <summary>
        /// 디테일 잔디용 URP FoliageWind 머티리얼 캐싱 및 생성
        /// </summary>
        private static Material GetOrCreateDetailMaterial(string prefabName, Texture2D tex)
        {
#if UNITY_EDITOR
            if (tex == null) return null;
            string key = $"{prefabName}_{tex.name}";
            if (detailMatCache.TryGetValue(key, out Material existing) && existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/TotalWarFoliageWind");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            Material mat = new Material(shader);
            mat.name = $"Mat_{key}";
            mat.mainTexture = tex;

            // FoliageWind 셰이더 프로퍼티 설정
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);
            if (mat.HasProperty("_WindStrength")) mat.SetFloat("_WindStrength", 0.5f);
            if (mat.HasProperty("_IsShrubGrass")) mat.SetFloat("_IsShrubGrass", 1.0f);

            // 알파 테스트 및 양면 렌더링 활성화
            mat.SetFloat("_Surface", 0f); // Opaque / Alpha Cutout
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cull", 0f); // Two-Sided
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            detailMatCache[key] = mat;
            return mat;
#else
            return null;
#endif
        }
    }
}
