using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌍 [삼국지 토탈워 5대 기후 지형 레이어 팔레트 분류]
    /// </summary>
    public enum ClimateTerrainPalette
    {
        [InspectorName("🌾 1. 중원 온대 5색 (Temperate Plains)")]
        Temperate5,
        [InspectorName("🏜️ 2. 서량 사막 5색 (Arid Wasteland)")]
        Arid5,
        [InspectorName("🌴 3. 남만 정글 5색 (Nanman Jungle)")]
        Nanman5,
        [InspectorName("❄️ 4. 하북 한랭 5색 (Cold Steppe)")]
        Cold5,
        [InspectorName("🌾 5. 강남 아열대 5색 (Subtropical Wetlands)")]
        Subtropical5
    }

    /// <summary>
    /// 🌍 [삼국지 토탈워 원작 1:1 고증 다채널 복합 지형 레이어 생성 및 절차적 자동 페인팅 엔진]
    /// 단순 3색(잔디/흙/바위)을 넘어, 5대 기후별 5개 지형 텍스처(평지 잔디, 절벽 암벽, 황토 흙길, 숲속 부엽토, 수변 자갈/진흙)를
    /// 경사도 + 고도 + 숲 구역(TacticalForestZone) 마스크 + 펄린 노이즈와 결합하여 전장에 자동 도색합니다.
    /// </summary>
    public static class CreateTotalWarTerrainLayers
    {
        private const string LayerFolder = "Assets/Settings/TerrainLayers";
        private const string SampleLayerDir = "Assets/TerrainSampleAssets/TerrainLayers";
        private const string ExtractedTexDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Environment/Textures";

        [InitializeOnLoadMethod]
        private static void AutoInitLayersOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureLayerDirectories();
            };
        }

        private static void EnsureLayerDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
            if (!AssetDatabase.IsValidFolder(LayerFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "TerrainLayers");
            }
        }

        /// <summary>
        /// 지정한 기후 팔레트에 맞춰 5대 PBR 터레인 레이어 세트를 로드하거나 자동 빌드합니다.
        /// [0]: 평지 잔디, [1]: 절벽 바위, [2]: 황토 흙길/비탈, [3]: 숲속 부엽토, [4]: 수변 자갈/진흙
        /// </summary>
        public static TerrainLayer[] GetOrCreatePaletteLayers(ClimateTerrainPalette palette)
        {
            EnsureLayerDirectories();

            TerrainLayer lGrass = null;
            TerrainLayer lRock = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Rock_TerrainLayer.terrainlayer");
            TerrainLayer lDirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Soil_Rocks_TerrainLayer.terrainlayer");
            TerrainLayer lForest = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Muddy_TerrainLayer.terrainlayer");
            TerrainLayer lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Pebbles_A_TerrainLayer.terrainlayer");

            switch (palette)
            {
                case ClimateTerrainPalette.Temperate5:
                    lGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_A_TerrainLayer.terrainlayer");
                    lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Pebbles_A_TerrainLayer.terrainlayer");
                    break;

                case ClimateTerrainPalette.Arid5:
                    // 서량 사막: 마른 잔디 + 사암 절벽 + 모래 자갈 + 건조 잡초 + 고운 모래
                    Texture2D texArdGrass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExtractedTexDir}/ard_grass0_base_colour.png");
                    Texture2D texArdRock = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExtractedTexDir}/ard_rock0_base_colour.png");
                    Texture2D texArdStones = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExtractedTexDir}/ard_stones0_base_colour.png");

                    lGrass = texArdGrass != null ? GetOrCreateLayer("TW_Arid_Grass0", texArdGrass, new Vector2(4, 4)) : AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_Dry_TerrainLayer.terrainlayer");
                    if (texArdRock != null) lRock = GetOrCreateLayer("TW_Arid_Rock0", texArdRock, new Vector2(8, 8));
                    if (texArdStones != null) lDirt = GetOrCreateLayer("TW_Arid_Stones0", texArdStones, new Vector2(5, 5));
                    lForest = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_Dry_TerrainLayer.terrainlayer");
                    lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Sand_TerrainLayer.terrainlayer");
                    break;

                case ClimateTerrainPalette.Nanman5:
                    // 남만 정글: 이끼 덩굴풀 + 이끼 암벽 + 붉은 흙길 + 정글 부엽토 + 정글 습지
                    lGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_Moss_TerrainLayer.terrainlayer");
                    lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Tidal_Pools_TerrainLayer.terrainlayer") 
                              ?? AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Pebbles_B_TerrainLayer.terrainlayer");
                    break;

                case ClimateTerrainPalette.Cold5:
                    // 하북 설원: 한랭 동토 잔디 + 어두운 암벽 + 얼어붙은 흙길 + 침엽수 부엽토 + 눈밭
                    Texture2D texCldGrass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExtractedTexDir}/cld_grass0_base_colour.png");
                    lGrass = texCldGrass != null ? GetOrCreateLayer("TW_Cold_Grass0", texCldGrass, new Vector2(4, 4)) : AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_Dry_TerrainLayer.terrainlayer");
                    lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Snow_TerrainLayer.terrainlayer");
                    break;

                case ClimateTerrainPalette.Subtropical5:
                    // 강남 아열대: 연둣빛 윤기 잔디 + 습윤 암벽 + 비옥한 흙길 + 수변 부엽토 + 강가 조약돌
                    lGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_B_TerrainLayer.terrainlayer");
                    lForest = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_Soil_TerrainLayer.terrainlayer");
                    lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Pebbles_C_TerrainLayer.terrainlayer");
                    break;
            }

            // 폴백 처리
            if (lGrass == null) lGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Grass_A_TerrainLayer.terrainlayer");
            if (lRock == null) lRock = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Rock_TerrainLayer.terrainlayer");
            if (lDirt == null) lDirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Soil_Rocks_TerrainLayer.terrainlayer");
            if (lForest == null) lForest = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Muddy_TerrainLayer.terrainlayer");
            if (lStones == null) lStones = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SampleLayerDir}/Pebbles_A_TerrainLayer.terrainlayer");

            // UV 타일링 크기 최적화
            if (lGrass != null) lGrass.tileSize = new Vector2(4, 4);
            if (lRock != null) lRock.tileSize = new Vector2(9, 9);
            if (lDirt != null) lDirt.tileSize = new Vector2(5, 5);
            if (lForest != null) lForest.tileSize = new Vector2(4.5f, 4.5f);
            if (lStones != null) lStones.tileSize = new Vector2(5, 5);

            AssetDatabase.SaveAssets();

            return new[] { lGrass, lRock, lDirt, lForest, lStones };
        }

        public static TerrainLayer GetOrCreateLayer(string name, Texture2D diffuseTex, Vector2 tileSize)
        {
            EnsureLayerDirectories();
            string path = $"{LayerFolder}/{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                layer.name = name;
                AssetDatabase.CreateAsset(layer, path);
            }

            if (diffuseTex != null)
            {
                layer.diffuseTexture = diffuseTex;
            }
            layer.tileSize = tileSize;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>
        /// 씬 내 모든 활성 터레인에 지정한 기후의 5색 지형 레이어를 적용하고,
        /// 경사도 + 숲 마스크 + 고도/수변 + 펄린 노이즈를 결합한 삼탈워식 다요소 복합 자동 페인팅을 실행합니다.
        /// </summary>
        public static void ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette palette, bool showDialog = true)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                Terrain t = Terrain.activeTerrain;
                if (t != null) terrains = new[] { t };
            }

            if (terrains == null || terrains.Length == 0)
            {
                Debug.LogWarning("[TotalWarTerrainLayers] 씬에 활성화된 Terrain이 없습니다.");
                return;
            }

            TerrainLayer[] layers = GetOrCreatePaletteLayers(palette);

            // 씬 내 숲 구역(TacticalForestZone) 전수 수집
            TacticalForestZone[] forestZones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);

            foreach (var ter in terrains)
            {
                if (ter == null || ter.terrainData == null) continue;
                TerrainData tData = ter.terrainData;

                Undo.RecordObject(tData, $"Apply {palette} Terrain Layers");
                tData.terrainLayers = layers;

                // 삼탈워식 다요소 복합 페인팅 실행
                AutoPaintTotalWarTerrainMultiFactor(ter, tData, forestZones);

                ter.Flush();
                EditorUtility.SetDirty(tData);
                EditorUtility.SetDirty(ter);
            }

            SceneView.RepaintAll();
            Debug.Log($"<color=#00FF88><b>[TotalWarTerrainLayers] 🌍 삼탈워 {palette} 5색 지형 레이어 장착 및 복합 절차적 페인팅 완료!</b></color>");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("완료", $"🌍 [삼국지 토탈워 {palette} 지형 페인팅 완료]\n\n" +
                    "1. 평지(0~20도): 주 기후 잔디 (Grass)\n" +
                    "2. 완경사(20~36도): 황토 흙길 & 비탈길 (Dirt & Soil)\n" +
                    "3. 급경사(36도 이상): 절벽 암벽 바위 (Cliff Rock)\n" +
                    "4. 숲 구역 내부: 숲속 그늘 낙엽 부엽토 (Forest Undergrowth)\n" +
                    "5. 저지대/수변: 강가 자갈 & 모래 진흙 (Riverbed / Mud)\n\n" +
                    "🌿 이제 숲속 바닥과 황토길, 절벽 바위가 삼탈워 원작처럼 유기적으로 도색되었습니다!", "확인");
            }
        }

        /// <summary>
        /// 4대 복합 요소 (경사도 + 숲 마스크 + 고도/수변 + 펄린 프랙탈 노이즈)를 융합하여 5색 스플랫맵을 절차적으로 구워냅니다.
        /// </summary>
        public static void AutoPaintTotalWarTerrainMultiFactor(Terrain ter, TerrainData tData, TacticalForestZone[] forestZones, float stoneSlope = 20f, float cliffSlope = 36f)
        {
            int alphaRes = tData.alphamapResolution;
            int layerCount = tData.terrainLayers != null ? tData.terrainLayers.Length : 0;
            if (layerCount < 5) return;

            float[,,] splat = new float[alphaRes, alphaRes, layerCount];
            Vector3 terrainPos = ter.transform.position;
            Vector3 terrainSize = tData.size;

            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            // 터레인 고도 범위 스캔
            for (int y = 0; y < alphaRes; y += 16)
            {
                float normY = (float)y / alphaRes;
                for (int x = 0; x < alphaRes; x += 16)
                {
                    float normX = (float)x / alphaRes;
                    float h = tData.GetInterpolatedHeight(normX, normY);
                    if (h < minHeight) minHeight = h;
                    if (h > maxHeight) maxHeight = h;
                }
            }

            float riverThreshold = minHeight + Mathf.Min(8f, (maxHeight - minHeight) * 0.15f);

            // 전역 수면 평면(TotalWarSeaPlane)이 씬에 존재할 경우 수위 높이에 동기화 (+1m 연안 완충)
            if (TotalWarSeaPlane.Instance != null)
            {
                float seaWaterLocalH = TotalWarSeaPlane.Instance.waterLevelY - terrainPos.y;
                riverThreshold = Mathf.Max(riverThreshold, seaWaterLocalH + 1.0f);
            }

            for (int y = 0; y < alphaRes; y++)
            {
                float normY = (float)y / alphaRes;
                float worldZ = terrainPos.z + normY * terrainSize.z;

                for (int x = 0; x < alphaRes; x++)
                {
                    float normX = (float)x / alphaRes;
                    float worldX = terrainPos.x + normX * terrainSize.x;
                    float worldY = terrainPos.y + tData.GetInterpolatedHeight(normX, normY);
                    Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

                    float slope = tData.GetSteepness(normX, normY);
                    float h = tData.GetInterpolatedHeight(normX, normY);

                    // 1. 미세 지형 노이즈 (경계면이 인위적인 직선으로 잘리지 않도록 유기적 프랙탈 노이즈 결합)
                    float nSlope = (Mathf.PerlinNoise(normX * 18f, normY * 18f) - 0.5f) * 6f;
                    float effectiveSlope = Mathf.Clamp(slope + nSlope, 0f, 90f);

                    // 2. 숲 구역(TacticalForestZone) 내부 판별
                    float forestFactor = 0f;
                    if (forestZones != null && forestZones.Length > 0)
                    {
                        foreach (var fz in forestZones)
                        {
                            if (fz == null) continue;
                            Vector3 fzPos = fz.transform.position;
                            float distXZ = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(fzPos.x, fzPos.z));

                            // 원형/직사각형 숲 구역 판별
                            float radius = (fz.shape == TacticalForestZone.ZoneShape.Circle) ? fz.radius : Mathf.Max(fz.boxSize.x, fz.boxSize.y) * 0.5f;
                            if (radius <= 0.1f) radius = 35f;

                            if (distXZ < radius)
                            {
                                float edgeT = Mathf.Clamp01((radius - distXZ) / 5f); // 5m 부드러운 페이드
                                if (edgeT > forestFactor) forestFactor = edgeT;
                            }
                        }
                    }

                    // 5개 레이어 가중치 초기화
                    // 0: Grass, 1: Rock, 2: Dirt, 3: Forest, 4: Stones/River
                    float wGrass = 0f;
                    float wRock = 0f;
                    float wDirt = 0f;
                    float wForest = 0f;
                    float wStones = 0f;

                    // 3. 경사도 및 고도 기반 기본 도색
                    if (effectiveSlope >= cliffSlope)
                    {
                        // 급경사 (36도 이상): 거친 암벽 바위 100%
                        float t = Mathf.Clamp01((effectiveSlope - cliffSlope) / 10f);
                        wRock = 0.8f + 0.2f * t;
                        wDirt = 0.2f * (1f - t);
                    }
                    else if (effectiveSlope >= stoneSlope)
                    {
                        // 완경사 (20~36도): 황토 흙길 & 자갈 비탈길
                        float t = (effectiveSlope - stoneSlope) / (cliffSlope - stoneSlope);
                        wDirt = 1f - t * 0.5f;
                        wRock = t * 0.5f;
                        wStones = 0.2f * (1f - t);
                    }
                    else
                    {
                        // 완만한 평지 (0~20도)
                        if (forestFactor > 0.05f)
                        {
                            // 🌲 숲속 내부: 잔디 대신 어두운 낙엽 부엽토(Forest Floor) 우선 도색!
                            wForest = forestFactor;
                            wGrass = (1f - forestFactor);
                        }
                        else if (h <= riverThreshold)
                        {
                            // 💧 최저 계곡/하천변: 자갈과 축축한 진흙으로 도색
                            float riverT = 1f - Mathf.Clamp01((h - minHeight) / Mathf.Max(0.1f, riverThreshold - minHeight));
                            wStones = riverT * 0.85f;
                            wGrass = 1f - wStones;
                        }
                        else
                        {
                            // 🌿 일반 양지 평야: 푸른 주 잔디 100%
                            wGrass = 1.0f;
                        }
                    }

                    // 정규화 (가중치 합 = 1.0f)
                    float sum = wGrass + wRock + wDirt + wForest + wStones;
                    if (sum > 0.0001f)
                    {
                        splat[y, x, 0] = wGrass / sum;
                        splat[y, x, 1] = wRock / sum;
                        splat[y, x, 2] = wDirt / sum;
                        splat[y, x, 3] = wForest / sum;
                        splat[y, x, 4] = wStones / sum;
                    }
                    else
                    {
                        splat[y, x, 0] = 1.0f;
                    }
                }
            }

            tData.SetAlphamaps(0, 0, splat);
        }

        /// <summary>
        /// 레거시 호환용 메서드
        /// </summary>
        public static void GenerateAndApplyLayers()
        {
            ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Temperate5, false);
        }
    }
}
