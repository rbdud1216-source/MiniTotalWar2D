using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using MiniTotalWar;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌍 [삼국지 토탈워 5대 기후 테마 프리셋 자동 생성 에디터 유틸리티]
    /// 온대(Temperate), 남만(Nanman), 건조(Arid), 한랭(Cold), 아열대(Subtropical) 5종의
    /// TotalWarTerrainTheme ScriptableObject 에셋을 자동 생성하고
    /// 238종 디테일 잔디와 터레인 레이어를 1:1 매핑합니다.
    /// </summary>
    public static class CreateDefaultTerrainThemes
    {
        private const string ThemeFolder = "Assets/Settings/TerrainThemes";
        private const string LayerFolder = "Assets/TerrainSampleAssets/TerrainLayers";
        private const string GrassFolder = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass";

        [InitializeOnLoadMethod]
        private static void AutoInitThemesOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                string temperatePath = $"{ThemeFolder}/Temperate_Theme.asset";
                if (!File.Exists(temperatePath))
                {
                    GenerateAllThemes();
                }
            };
        }

        [MenuItem("TotalWar/🌍 기후 테마 5종 자동 생성 (Create 5 Climate Themes)", false, 10)]
        public static void GenerateAllThemes()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
            if (!AssetDatabase.IsValidFolder(ThemeFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "TerrainThemes");
            }

            // 5대 기후 테마 생성
            var temperate = CreateOrUpdateTheme("Temperate_Theme", TotalWarTerrainTheme.ClimateType.Temperate,
                "Grass_A_TerrainLayer", "Grass_Dry_TerrainLayer", "Snow_TerrainLayer",
                "Grass_B_TerrainLayer", "Rock_TerrainLayer", "Pebbles_A_TerrainLayer",
                "Climate_Temperate");

            var nanman = CreateOrUpdateTheme("Nanman_Theme", TotalWarTerrainTheme.ClimateType.Nanman,
                "Grass_Moss_TerrainLayer", "Muddy_TerrainLayer", "Grass_Soil_TerrainLayer",
                "Muddy_TerrainLayer", "Rock_TerrainLayer", "Soil_Rocks_TerrainLayer",
                "Climate_Subtropical");

            var arid = CreateOrUpdateTheme("Arid_Theme", TotalWarTerrainTheme.ClimateType.Arid,
                "Grass_Dry_TerrainLayer", "Sand_TerrainLayer", "Black_Sand_TerrainLayer",
                "Sand_TerrainLayer", "Rock_TerrainLayer", "Pebbles_C_TerrainLayer",
                "Climate_Arid");

            var cold = CreateOrUpdateTheme("Cold_Theme", TotalWarTerrainTheme.ClimateType.Cold,
                "Grass_Dry_TerrainLayer", "Heather_TerrainLayer", "Snow_TerrainLayer",
                "Soil_Rocks_TerrainLayer", "Rock_TerrainLayer", "Pebbles_B_TerrainLayer",
                "Climate_Cold");

            var subtropical = CreateOrUpdateTheme("Subtropical_Theme", TotalWarTerrainTheme.ClimateType.Subtropical,
                "Grass_A_TerrainLayer", "Heather_TerrainLayer", "Tidal_Pools_TerrainLayer",
                "Grass_Moss_TerrainLayer", "Rock_TerrainLayer", "Muddy_TerrainLayer",
                "Climate_Subtropical");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 씬 내 TotalWarTerrainManager에 기본 테마 자동 연결
            var mgr = Object.FindAnyObjectByType<TotalWarTerrainManager>();
            if (mgr != null)
            {
                if (mgr.currentTheme == null)
                {
                    mgr.currentTheme = temperate;
                    EditorUtility.SetDirty(mgr);
                    mgr.ApplyCurrentTheme();
                    Debug.Log("<color=#00FF88><b>[CreateDefaultTerrainThemes] 씬의 TotalWarTerrainManager에 'Temperate_Theme'이 기본값으로 연결되고 적용되었습니다!</b></color>");
                }
            }

            Debug.Log("<color=#00FF88><b>[CreateDefaultTerrainThemes] 🌍 5대 기후 테마 에셋(.asset) 생성이 성공적으로 완료되었습니다!</b></color>");
        }

        private static TotalWarTerrainTheme CreateOrUpdateTheme(
            string assetName,
            TotalWarTerrainTheme.ClimateType climate,
            string suLayer, string auLayer, string wiLayer,
            string forestLayer, string rockLayer, string roadLayer,
            string grassSubFolder)
        {
            string path = $"{ThemeFolder}/{assetName}.asset";
            TotalWarTerrainTheme theme = AssetDatabase.LoadAssetAtPath<TotalWarTerrainTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<TotalWarTerrainTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }

            theme.climate = climate;
            theme.summerGrassLayer = LoadLayer(suLayer);
            theme.autumnGrassLayer = LoadLayer(auLayer);
            theme.winterGrassLayer = LoadLayer(wiLayer);
            theme.forestFloorLayer = LoadLayer(forestLayer);
            theme.cliffRockLayer = LoadLayer(rockLayer);
            theme.dirtRoadLayer = LoadLayer(roadLayer);

            theme.detailDistance = 250f;
            theme.detailDensity = 1.0f;

            // 디테일 잔디 모델 로드 (최대 16종 바인딩)
            theme.detailGrassConfigs = LoadDetailGrass(grassSubFolder, 16);

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static TerrainLayer LoadLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return null;
            string path = $"{LayerFolder}/{layerName}.terrainlayer";
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }

        private static List<TotalWarTerrainTheme.DetailGrassConfig> LoadDetailGrass(string subFolder, int maxCount)
        {
            List<TotalWarTerrainTheme.DetailGrassConfig> list = new List<TotalWarTerrainTheme.DetailGrassConfig>();
            string dirPath = $"{GrassFolder}/{subFolder}";

            if (!Directory.Exists(dirPath)) return list;

            string[] objFiles = Directory.GetFiles(dirPath, "*.obj");
            int step = Mathf.Max(1, objFiles.Length / maxCount);

            for (int i = 0; i < objFiles.Length && list.Count < maxCount; i += step)
            {
                string relPath = objFiles[i].Replace('\\', '/');
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relPath);
                if (prefab != null)
                {
                    var cfg = new TotalWarTerrainTheme.DetailGrassConfig
                    {
                        grassPrefab = prefab,
                        dryGrassPrefab = null,
                        densityFactor = 1.0f,
                        widthRange = new Vector2(1.1f, 1.8f),
                        heightRange = new Vector2(1.0f, 1.6f),
                        alignToGround = 0.5f
                    };
                    list.Add(cfg);
                }
            }

            return list;
        }
    }
}
