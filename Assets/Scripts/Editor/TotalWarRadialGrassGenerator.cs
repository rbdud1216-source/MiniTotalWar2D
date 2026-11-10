using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌾 [삼국지 토탈워 원작 1:1 공식 3D 풀포기 & 롱 글래스 이삭 장착 시스템]
    /// 토탈워 삼국지 공식 추출 OBJ 모델과 1:1 전용 고해상도 아틀라스 텍스처(Albedo + Normal)를 바인딩하여,
    /// 1) 사용자가 인스펙터에서 자유롭게 선택한 3D 풀포기 목록을 실시간 빌드
    /// 2) URP 실사 라이팅 및 70m~220m 거리 기반 LOD 디졸브 구현
    /// 3) 터레인 스플랫맵(잔디, 바위, 흙길) 가중치를 기반으로 지형 레이어별 선택적 서식 완벽 구현
    /// </summary>
    [InitializeOnLoad]
    public static class TotalWarRadialGrassGenerator
    {
        static TotalWarRadialGrassGenerator()
        {
            TotalWarTerrainManager.OnThemeAppliedEditor -= OnThemeAppliedHandler;
            TotalWarTerrainManager.OnThemeAppliedEditor += OnThemeAppliedHandler;
        }

        private static void OnThemeAppliedHandler()
        {
            GenerateAllRadialGrassAndApply(false);
        }

        private const string PrefabDir = "Assets/Settings/Vegetation/RadialMeshes";
        private const string MeshDir = "Assets/Settings/Vegetation/RadialMeshes";
        private const string MaterialDir = "Assets/Settings/Vegetation/RadialMaterials";

        [MenuItem("TotalWar/🌾 삼탈워 원작 1:1 공식 3D 풀포기 & 이삭 전장 장착", false, 1)]
        public static void GenerateAllRadialGrassAndApplyMenu()
        {
            GenerateAllRadialGrassAndApply(true);
        }

        /// <summary>
        /// 설정된 3D 풀포기 목록을 터레인에 장착하고 스플랫맵 가중치에 맞춰 살포합니다.
        /// </summary>
        public static void GenerateAllRadialGrassAndApply(bool showDialog = false, List<RadialGrassConfig> inputConfigs = null)
        {
            EnsureDirectories();

            // 1. 활성화된 식생 설정 수집
            List<RadialGrassConfig> sourceConfigs = inputConfigs;
            if (sourceConfigs == null || sourceConfigs.Count == 0)
            {
                TotalWarTerrainManager mgr = Object.FindAnyObjectByType<TotalWarTerrainManager>();
                if (mgr != null && mgr.customGrassList != null && mgr.customGrassList.Count > 0)
                {
                    sourceConfigs = mgr.customGrassList;
                }
                else
                {
                    sourceConfigs = RadialGrassConfig.GetDefaultFoliageList();
                    if (mgr != null) mgr.customGrassList = sourceConfigs;
                }
            }

            // 활성화(isEnabled)된 항목만 필터링
            List<RadialGrassConfig> activeConfigs = new List<RadialGrassConfig>();
            foreach (var cfg in sourceConfigs)
            {
                if (cfg != null && cfg.isEnabled)
                {
                    activeConfigs.Add(cfg);
                }
            }

            if (activeConfigs.Count == 0)
            {
                Debug.LogWarning("[TotalWarNativeGrass] 활성화된 3D 풀 설정이 없습니다. 기본 5종을 로드합니다.");
                activeConfigs = RadialGrassConfig.GetDefaultFoliageList();
            }

            Shader lodShader = Shader.Find("Universal Render Pipeline/TotalWarRadialGrassLOD");
            if (lodShader == null) lodShader = Shader.Find("Universal Render Pipeline/Lit");

            List<GameObject> generatedPrefabs = new List<GameObject>();
            List<RadialGrassConfig> validConfigs = new List<RadialGrassConfig>();

            for (int i = 0; i < activeConfigs.Count; i++)
            {
                var cfg = activeConfigs[i];

                // 1. 삼탈워 공식 3D OBJ 모델 자동 탐색 및 로드 (Shrubs / DetailGrass 전수 탐색)
                GameObject modelObj = FindModelAsset(cfg.modelFile);
                if (modelObj == null)
                {
                    Debug.LogWarning($"[TotalWarNativeGrass] 모델 파일을 찾을 수 없어 건너뜁니다: {cfg.modelFile}");
                    continue;
                }

                MeshFilter mfInModel = modelObj.GetComponentInChildren<MeshFilter>();
                if (mfInModel == null || mfInModel.sharedMesh == null)
                {
                    Debug.LogWarning($"[TotalWarNativeGrass] 모델 내 메시가 비어있습니다: {cfg.modelFile}");
                    continue;
                }

                Mesh nativeMesh = mfInModel.sharedMesh;

                // 2. 전용 무손실 아틀라스 텍스처 (Albedo + Normal) 스마트 탐색 및 로드
                Texture2D albedoTex = FindTextureAsset(cfg.albedoFile);
                Texture2D normalTex = !string.IsNullOrEmpty(cfg.normalFile) ? FindTextureAsset(cfg.normalFile) : null;

                // 3. 고품질 URP LOD 디졸브 머티리얼 생성 또는 갱신
                string cleanName = Path.GetFileNameWithoutExtension(cfg.modelFile) + "_" + Path.GetFileNameWithoutExtension(cfg.albedoFile);
                string matPath = $"{MaterialDir}/{cleanName}_Mat.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(lodShader);
                    mat.name = $"{cleanName}_Mat";
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.shader = lodShader;
                if (albedoTex != null)
                {
                    mat.SetTexture("_BaseMap", albedoTex);
                    mat.SetTexture("_MainTex", albedoTex);
                    mat.mainTexture = albedoTex;
                }

                if (normalTex != null)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                }

                mat.SetColor("_BaseColor", Color.white);
                mat.color = Color.white;
                mat.SetFloat("_Cutoff", 0.35f);
                mat.SetFloat("_Cull", 0.0f); // 양면 렌더링
                mat.SetFloat("_FadeStartDistance", 70.0f);
                mat.SetFloat("_FadeEndDistance", 220.0f);
                mat.SetFloat("_Smoothness", 0.05f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_WindMultiplier", 0.40f);
                EditorUtility.SetDirty(mat);

                // 4. 프리팹 생성 및 피벗 바닥면 자동 정렬 (Bottom-Zero Ground Clamping & FlipY 보정)
                string alignedMeshPath = $"{MeshDir}/{cleanName}_AlignedMesh.asset";
                Mesh alignedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(alignedMeshPath);
                if (alignedMesh == null)
                {
                    alignedMesh = CreateGroundAlignedMesh(nativeMesh, cfg.groundOffset, cfg.flipY);
                    alignedMesh.name = $"{cleanName}_AlignedMesh";
                    AssetDatabase.CreateAsset(alignedMesh, alignedMeshPath);
                }
                else
                {
                    UpdateGroundAlignedMesh(alignedMesh, nativeMesh, cfg.groundOffset, cfg.flipY);
                    EditorUtility.SetDirty(alignedMesh);
                }

                string prefabPath = $"{PrefabDir}/{cleanName}_Prefab.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    GameObject go = new GameObject(cleanName + "_Foliage");
                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = alignedMesh;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;

                    prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    Object.DestroyImmediate(go);
                }
                else
                {
                    MeshFilter mf = prefab.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = alignedMesh;
                    MeshRenderer mr = prefab.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = mat;
                    EditorUtility.SetDirty(prefab);
                }

                if (prefab != null)
                {
                    generatedPrefabs.Add(prefab);
                    validConfigs.Add(cfg);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FF88><b>[TotalWarNativeGrass] 🌾 총 {generatedPrefabs.Count}종의 삼탈워 공식 3D 풀포기 프리팹 준비 완료!</b></color>");

            // 5. 전장 터레인에 정식 장착 및 스플랫맵 레이어 가중치 기반 살포
            ApplyNativeFoliageToAllTerrains(generatedPrefabs, validConfigs, showDialog);
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Vegetation")) AssetDatabase.CreateFolder("Assets/Settings", "Vegetation");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Settings/Vegetation", "RadialMeshes");
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/Settings/Vegetation", "RadialMaterials");
        }

        /// <summary>
        /// 생성된 3D 프리팹들을 터레인 디테일 프로토타입으로 장착하고 살포합니다.
        /// </summary>
        public static void ApplyNativeFoliageToAllTerrains(List<GameObject> prefabs, List<RadialGrassConfig> configs, bool showDialog = false)
        {
            if (prefabs == null || prefabs.Count == 0) return;

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                Terrain t = Terrain.activeTerrain;
                if (t != null) terrains = new[] { t };
            }

            if (terrains == null || terrains.Length == 0) return;

            DetailPrototype[] prototypes = new DetailPrototype[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                DetailPrototype dp = new DetailPrototype();
                dp.usePrototypeMesh = true;
                dp.prototype = prefabs[i];
                dp.renderMode = DetailRenderMode.Grass;

                var cfg = configs[i];
                dp.minWidth = cfg.minWidth;
                dp.maxWidth = cfg.maxWidth;
                dp.minHeight = cfg.minHeight;
                dp.maxHeight = cfg.maxHeight;

                // 식생별 지형 경사면 순응도 (지피 잔디 1.0 = 100% 밀착, 입체 수풀 0.4 = 수직성 유지)
                dp.alignToGround = Mathf.Clamp01(cfg.alignToGround);
                dp.healthyColor = Color.white;
                dp.dryColor = Color.white;

                prototypes[i] = dp;
            }

            foreach (var terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                TerrainData tData = terrain.terrainData;

                tData.detailPrototypes = prototypes;
                tData.SetDetailScatterMode(DetailScatterMode.CoverageMode);

                terrain.detailObjectDistance = 250f;
                terrain.detailObjectDensity = 1.0f;
                terrain.drawTreesAndFoliage = true;

                // 🌍 지형 스플랫맵 레이어 가중치 결합 살포 실행
                AutoScatterNativeFoliage(tData, configs, 42f);

                terrain.Flush();
                EditorUtility.SetDirty(tData);
                EditorUtility.SetDirty(terrain);
            }

            SceneView.RepaintAll();
            Debug.Log("<color=#00FF88><b>[TotalWarNativeGrass] 🌍 삼탈워 3D 풀포기가 지형 레이어(잔디/바위/흙길) 스플랫맵 필터링과 함께 100% 장착되었습니다!</b></color>");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("완료", "🌾 [삼국지 토탈워 3D 풀포기 & 이삭 장착 완료]\n\n• 지형 스플랫맵(잔디/바위/흙길) 레이어별 선택적 서식이 성공적으로 적용되었습니다.\n• 바위 절벽에는 풀이 자라지 않고, 잔디와 지정된 흙길에만 선명하게 자라납니다.\n• 카메라를 250m 이내로 가까이 이동하면 씬 뷰에서 즉시 감상하실 수 있습니다!", "확인");
            }
        }

        /// <summary>
        /// 지형의 경사도(Slope), 펄린 노이즈(Noise) 및 터레인 스플랫맵(Alphamap) 레이어 가중치를 곱하여 완벽한 서식지별 살포를 수행합니다.
        /// </summary>
        private static void AutoScatterNativeFoliage(TerrainData tData, List<RadialGrassConfig> configs, float maxSlope = 42f)
        {
            int dRes = tData.detailResolution;
            if (dRes <= 0 || configs == null || configs.Count == 0) return;

            // 터레인 스플랫맵 알파맵 로드
            int alphaRes = tData.alphamapResolution;
            int layerCount = tData.terrainLayers != null ? tData.terrainLayers.Length : 0;
            float[,,] splatmap = null;
            if (alphaRes > 0 && layerCount > 0)
            {
                splatmap = tData.GetAlphamaps(0, 0, alphaRes, alphaRes);
            }

            Random.InitState(777);

            for (int protoIdx = 0; protoIdx < configs.Count; protoIdx++)
            {
                var cfg = configs[protoIdx];
                int[,] layerMap = new int[dRes, dRes];

                for (int y = 0; y < dRes; y++)
                {
                    float normY = (float)y / dRes;
                    for (int x = 0; x < dRes; x++)
                    {
                        float normX = (float)x / dRes;
                        float slope = tData.GetSteepness(normX, normY);

                        // 1. 경사도 제한
                        if (slope > maxSlope)
                        {
                            layerMap[y, x] = 0;
                            continue;
                        }

                        // 2. 🌍 지형 스플랫맵(Alphamap) 서식 적합도 동적 계산 (풀밭 vs 황토길 vs 절벽 바위)
                        float habitatWeight = 1.0f;
                        if (splatmap != null && layerCount > 0)
                        {
                            int ax = Mathf.Clamp(Mathf.RoundToInt(normX * (alphaRes - 1)), 0, alphaRes - 1);
                            int ay = Mathf.Clamp(Mathf.RoundToInt(normY * (alphaRes - 1)), 0, alphaRes - 1);

                            float grassW = 0f;
                            float rockW = 0f;
                            float dirtW = 0f;

                            for (int l = 0; l < layerCount; l++)
                            {
                                float w = splatmap[ay, ax, l];
                                if (w <= 0.001f) continue;

                                var layer = tData.terrainLayers[l];
                                string lName = (layer != null && layer.name != null) ? layer.name.ToLower() : "";

                                // 레이어 0번 또는 이름에 grass가 포함된 경우: 순수 푸른 잔디밭
                                if (l == 0 || lName.Contains("grass"))
                                {
                                    grassW += w;
                                }
                                else if (lName.Contains("rock") || lName.Contains("cliff"))
                                {
                                    rockW += w;
                                }
                                else // soil, stone, dirt, road, gravel 등 황토길 / 자갈길 / 마른 흙
                                {
                                    dirtW += w;
                                }
                            }

                            // 설정된 서식 허용 여부(풀밭 / 흙길 / 바위)에 따라 동적으로 적합도 산출
                            float allowedWeight = 0f;
                            if (cfg.growOnGrass) allowedWeight += grassW;
                            if (cfg.growOnDirt) allowedWeight += dirtW;
                            if (cfg.growOnRock) allowedWeight += rockW;

                            habitatWeight = Mathf.Clamp01(allowedWeight);
                        }

                        // 🚫 황토길/바위 등 서식 불허 지형은 풀이 0%로 완벽하게 자라지 않음 (길이 깨끗하게 드러남)
                        if (habitatWeight < 0.03f)
                        {
                            layerMap[y, x] = 0;
                            continue;
                        }

                        // 🌊 수면 아래(바다/하천/호수)는 3D 잔디 0% 완전 배제! (물속에 풀이 자라지 않고 맑은 조약돌 노출)
                        if (TotalWarSeaPlane.Instance != null)
                        {
                            float terY = (Terrain.activeTerrain != null) ? Terrain.activeTerrain.transform.position.y : 0f;
                            float cellWorldY = terY + tData.GetInterpolatedHeight(normX, normY);
                            if (cellWorldY < TotalWarSeaPlane.Instance.waterLevelY)
                            {
                                layerMap[y, x] = 0;
                                continue;
                            }
                        }

                        // 3. 다중 주파수 펄린 노이즈 계산 (군락 형성용)
                        float n1 = Mathf.PerlinNoise(normX * 14f + protoIdx * 11.5f, normY * 14f);
                        float n2 = Mathf.PerlinNoise(normX * 32f + protoIdx * 5.3f, normY * 32f) * 0.5f;
                        float noise = (n1 + n2) / 1.5f;

                        // 4. [풀밭 100% 융단 보장 vs 포인트 수풀 군락 분리]
                        // • 지피 잔디(Tem_Grass0_1 등 5.3m 대형 패치): 풀밭이라면 노이즈에 깎이지 않고 100% 빈틈없이 깔림!
                        // • 포인트 수풀(꽃, 억새, 고사리, 관목): 풀밭 위에서 노이즈가 높은 지점에만 무리 지어 피어남!
                        bool isBaseGrass = cfg.growOnGrass && (cfg.displayName.Contains("지피") || cfg.displayName.Contains("풀포기") || cfg.displayName.Contains("Clump") || cfg.displayName.Contains("Carpet") || cfg.displayName.Contains("Herb") || cfg.displayName.Contains("Grass"));

                        int finalCount = 0;

                        if (isBaseGrass)
                        {
                            // 🌾 [풀밭 100% 융단 잔디]
                            // 잔디밭 영역(habitatWeight)에서는 노이즈 골짜기라도 빈땅 없이 빽빽하게 융단 형성 (셀당 2~3개)
                            float baseDensity = Mathf.Lerp(cfg.minDensity, cfg.maxDensity, noise) / 100f;
                            float densityFactor = Mathf.Max(0.70f, baseDensity) * habitatWeight;

                            int maxPerCell = 3;
                            float scaledCount = densityFactor * maxPerCell;
                            finalCount = Mathf.Clamp(Mathf.RoundToInt(scaledCount), 1, maxPerCell);
                        }
                        else
                        {
                            // 🌸 [포인트 수풀: 꽃, 고사리, 이삭, 관목]
                            // 펄린 노이즈 군락이 형성된 곳에만 무리 지어 솟아남
                            float effectiveThreshold = Mathf.Clamp(cfg.noiseThreshold, 0.20f, 0.65f);
                            if (noise > effectiveThreshold)
                            {
                                float t = (noise - effectiveThreshold) / Mathf.Max(0.01f, 1.0f - effectiveThreshold);
                                float rawPercent = Mathf.Lerp(cfg.minDensity, cfg.maxDensity, t) / 100f;
                                float densityFactor = rawPercent * habitatWeight;

                                int maxPerCell = (cfg.displayName.Contains("고목") || cfg.displayName.Contains("Stump")) ? 1 : 2;
                                float scaledCount = densityFactor * maxPerCell;

                                // 확률적 디더링으로 부드러운 군락 경계면 형성
                                int intCount = Mathf.FloorToInt(scaledCount);
                                float remainder = scaledCount - intCount;
                                float ditherVal = (Mathf.Sin(normX * 12345.67f + normY * 76543.21f) * 43758.5453f) % 1f;
                                if (ditherVal < 0) ditherVal += 1f;
                                if (ditherVal < remainder) intCount++;

                                finalCount = Mathf.Clamp(intCount, 0, maxPerCell);
                            }
                        }

                        layerMap[y, x] = finalCount;
                    }
                }

                tData.SetDetailLayer(0, 0, protoIdx, layerMap);
            }
        }

        public static GameObject FindModelAsset(string modelFile)
        {
            if (string.IsNullOrEmpty(modelFile)) return null;

            if (modelFile.StartsWith("Assets/"))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(modelFile);
            }

            // 1. Shrubs 폴더 탐색
            string p1 = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/{modelFile}";
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(p1);
            if (obj != null) return obj;

            // 2. DetailGrass 기후별 서브폴더 탐색
            string[] subdirs = new[] { "Climate_Temperate", "Climate_Arid", "Climate_Cold", "Climate_Subtropical" };
            foreach (var dir in subdirs)
            {
                string p2 = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/{dir}/{modelFile}";
                obj = AssetDatabase.LoadAssetAtPath<GameObject>(p2);
                if (obj != null) return obj;
            }

            // 3. 파일명 기반 전역 검색
            string cleanName = Path.GetFileNameWithoutExtension(modelFile);
            string[] guids = AssetDatabase.FindAssets($"{cleanName} t:Model");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        public static Texture2D FindTextureAsset(string texFile)
        {
            if (string.IsNullOrEmpty(texFile)) return null;

            if (texFile.StartsWith("Assets/"))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(texFile);
            }

            // 1. Textures 폴더 탐색
            string p1 = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/{texFile}";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p1);
            if (tex != null) return tex;

            // 2. 파일명 기반 전역 검색
            string cleanName = Path.GetFileNameWithoutExtension(texFile);
            string[] guids = AssetDatabase.FindAssets($"{cleanName} t:Texture2D");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return null;
        }

        /// <summary>
        /// 메시의 최하단 버텍스(minY)를 자동으로 감지하고 flipY 여부를 반영하여, 피벗 바닥면이 정확히 Y = 0.0 + groundOffset 에 위치하도록 정렬합니다.
        /// </summary>
        private static Mesh CreateGroundAlignedMesh(Mesh sourceMesh, float groundOffset, bool flipY)
        {
            Mesh newMesh = Object.Instantiate(sourceMesh);
            UpdateGroundAlignedMesh(newMesh, sourceMesh, groundOffset, flipY);
            return newMesh;
        }

        private static void UpdateGroundAlignedMesh(Mesh targetMesh, Mesh sourceMesh, float groundOffset, bool flipY)
        {
            Vector3[] verts = sourceMesh.vertices;
            if (verts == null || verts.Length == 0) return;

            Vector3[] shiftedVerts = new Vector3[verts.Length];
            Vector3[] shiftedNormals = sourceMesh.normals != null && sourceMesh.normals.Length == verts.Length ? new Vector3[verts.Length] : null;

            float minY = float.MaxValue;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = flipY ? -verts[i].y : verts[i].y;
                shiftedVerts[i] = new Vector3(verts[i].x, y, verts[i].z);
                if (y < minY) minY = y;
            }

            // 바닥면(minY)이 Y = 0.0 + groundOffset 에 오도록 모든 정점을 수직 이동
            float yShift = -minY + groundOffset;
            for (int i = 0; i < shiftedVerts.Length; i++)
            {
                shiftedVerts[i].y += yShift;
            }

            targetMesh.vertices = shiftedVerts;

            // flipY인 경우 삼각형 와인딩 순서 반전 및 노멀 Y축 반전 보정
            if (flipY && sourceMesh.triangles != null)
            {
                int[] tris = (int[])sourceMesh.triangles.Clone();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int temp = tris[i];
                    tris[i] = tris[i + 1];
                    tris[i + 1] = temp;
                }
                targetMesh.triangles = tris;

                if (shiftedNormals != null)
                {
                    for (int i = 0; i < sourceMesh.normals.Length; i++)
                    {
                        shiftedNormals[i] = new Vector3(sourceMesh.normals[i].x, -sourceMesh.normals[i].y, sourceMesh.normals[i].z);
                    }
                    targetMesh.normals = shiftedNormals;
                }
            }
            else
            {
                targetMesh.triangles = sourceMesh.triangles;
                targetMesh.normals = sourceMesh.normals;
            }

            targetMesh.uv = sourceMesh.uv;
            targetMesh.tangents = sourceMesh.tangents;
            targetMesh.colors = sourceMesh.colors;
            targetMesh.RecalculateBounds();
        }
    }
}
