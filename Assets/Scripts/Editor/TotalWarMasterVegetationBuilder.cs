using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MiniTotalWar;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌿 [토탈워: 삼국지 전장 식생 4대 유형 전수(94종) & 수목 46종 마스터 빌더 + 구버전 클리너]
    /// - 추출된 삼탈워 식생 텍스처 전체를 단 하나도 빠짐없이 정제된 절차적 기하학으로 100% 전수 생성합니다.
    /// - 난잡한 구버전 프리팹/메쉬/머티리얼을 디스크에서 깨끗하게 정리하고 TotalWar_* 표준 에셋만 유지합니다.
    /// </summary>
    public static class TotalWarMasterVegetationBuilder
    {
        public const string MeshDir = "Assets/Settings/Vegetation/RadialMeshes";
        public const string MatDir = "Assets/Settings/Vegetation/RadialMaterials";
        public const string TreePrefabDir = "Assets/Settings/Vegetation/TreePrefabs";
        public const string TexRootDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/";

        [InitializeOnLoadMethod]
        private static void AutoRunOnceOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                // 전수 생성 대표 에셋(원작 정품 3D 수생 연꽃, 보리밭, 차나무, 열대 양치류) 존재 여부와 구버전 프리팹 잔존 여부 체크
                bool hasAllNew = File.Exists($"{MeshDir}/TotalWar_LotusCluster_Mesh.asset") &&
                                 File.Exists($"{MeshDir}/TotalWar_RadialFan3D_Wheat_Gold_Mesh.asset") &&
                                 File.Exists($"{MeshDir}/TotalWar_RadialFan3D_TeaBush_Spring_Mesh.asset") &&
                                 File.Exists($"{MeshDir}/TotalWar_ForestFern_TropicalStrap_Summer_Mesh.asset");
                bool hasLegacy = File.Exists($"{MeshDir}/Ard_Grass0_1_shrub_grassclump_base_colour_Prefab.prefab") ||
                                 File.Exists($"{MeshDir}/TotalWar_TallReed3D_Lotus_Summer_Mesh.asset");

                if (!hasAllNew || hasLegacy)
                {
                    ExecuteMasterBuildAndBake();
                }
            };
        }

        [MenuItem("TotalWar/🚀 [마스터 빌더] 전수 식생(94종) + 46종 수목 생성 & 구버전 클린업 & Game2 베이크", false, 0)]
        public static void ExecuteMasterBuildAndBake()
        {
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(TreePrefabDir);

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🧹 0단계: 구버전 난잡한 프리팹/메쉬/머티리얼 영구 삭제 중...</b></color>");
            CleanUpLegacyVegetationAssets();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🚀 1단계: 모든 식생 텍스처 임포트 설정(Alpha/Normal) 최적화 중...</b></color>");
            EnsureCoreTextureImportSettings();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🌾 2단계: 유형 ① [1자 잔디 띠 & 야생화 10열 교차 군락 (41종)] 전수 생성 중...</b></color>");
            BuildType1_RadialStripClusters();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🌿 3단계: 유형 ② [3D 입체 풀포기 & 농작물 & 화초 관목 (20종)] 전수 생성 중...</b></color>");
            BuildType2_RadialFan3DClumps();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🎋 4단계: 유형 ③ [키 큰 갈대·억새·곡식 이삭·수생 연꽃 (15종)] 전수 생성 중...</b></color>");
            BuildType3_TallReedsAndSeedheads();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🌲 5단계: 유형 ④ [음지 고사리·열대 양치류·바닥 화초 (18종)] 전수 생성 중...</b></color>");
            BuildType4_ForestFerns();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🌳 6단계: 원작 46종 수목(Trees) 프리팹 조립 및 TacticalForestZone 연동 중...</b></color>");
            int treeCount = BuildAll46TreePrefabsAndRefreshForests();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFFF><b>[MasterVegetationBuilder] 🌍 7단계: TotalWarTerrainManager 연동 및 Game2 터레인 영구 베이크 중...</b></color>");
            BakeCurrentSceneTerrainFoliage();

            // 쇼케이스 창이 열려 있다면 즉시 목록 갱신
            var openShowcases = Resources.FindObjectsOfTypeAll<TotalWarFoliageShowcaseWindow>();
            if (openShowcases != null)
            {
                foreach (var win in openShowcases)
                {
                    if (win != null)
                    {
                        win.ScanAllModels();
                        win.Repaint();
                    }
                }
            }

            Debug.Log($"<color=#00FF88><b>[MasterVegetationBuilder] ✅ 구버전 에셋 완전 삭제 + 전장 식생 94종 전수 생성 + 수목 {treeCount}종 프리팹 + Game2 베이크 완료!</b></color>");
        }

        // =========================================================================================
        // 0. 구버전 난잡한 프리팹, 원시 정렬 메쉬, 구형 머티리얼 완전 삭제
        // =========================================================================================
        public static void CleanUpLegacyVegetationAssets()
        {
            int removedFiles = 0;

            if (Directory.Exists(MeshDir))
            {
                string[] allMeshDirFiles = Directory.GetFiles(MeshDir);
                foreach (string filePath in allMeshDirFiles)
                {
                    if (filePath.EndsWith(".meta")) continue;
                    string fileName = Path.GetFileName(filePath);

                    bool isKeep =
                        fileName.StartsWith("TotalWar_MultiDirectionalGrassCluster_") ||
                        fileName.StartsWith("TotalWar_FlowerCluster_") ||
                        fileName.StartsWith("TotalWar_GrassCluster_") ||
                        fileName.StartsWith("TotalWar_RadialFan3D_") ||
                        fileName.StartsWith("TotalWar_TallReed3D_") ||
                        fileName.StartsWith("TotalWar_ForestFern_") ||
                        fileName.StartsWith("TotalWar_LotusCluster_");

                    // 1) 갈대로 오분류되었던 구버전 가짜 2D 카드 연꽃 파일 삭제
                    if (fileName.StartsWith("TotalWar_TallReed3D_Lotus_"))
                    {
                        isKeep = false;
                    }

                    // 2) 임시 스와스 테스트 메쉬 삭제
                    if (fileName.StartsWith("TotalWar_FlowerSwath_"))
                    {
                        isKeep = false;
                    }

                    // 3) 마스터 메쉬가 이미 존재하는데 이름만 바꿔 복제된 낭비 메쉬 파일 정리
                    if (fileName.EndsWith("_Mesh.asset"))
                    {
                        // 3D 풀포기 중복 메쉬 (마스터: TotalWar_RadialFan3D_Clump_Mesh)
                        if (fileName.StartsWith("TotalWar_RadialFan3D_Spring_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_Summer_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_Autumn_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_Winter_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_Harvest_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_Arid_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_RicePaddy_Gold_Mesh") ||
                            fileName.StartsWith("TotalWar_RadialFan3D_RicePaddy_Green_Mesh"))
                        {
                            isKeep = false;
                        }

                        // 키 큰 갈대 중복 메쉬 (마스터: TotalWar_TallReed3D_Plume_Mesh)
                        if (fileName.StartsWith("TotalWar_TallReed3D_Spring_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_Summer_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_Autumn_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_Winter_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_Harvest_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_Arid_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_RiceEar_Gold_Mesh") ||
                            fileName.StartsWith("TotalWar_TallReed3D_RiceEar_Green_Mesh"))
                        {
                            isKeep = false;
                        }

                        // 고사리 사계절 중복 메쉬 (마스터: Sword_Mesh, Royal_Mesh, Wood_Mesh)
                        if (fileName.StartsWith("TotalWar_ForestFern_Sword_Spring_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Sword_Summer_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Sword_Autumn_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Sword_Winter_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Royal_Spring_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Royal_Summer_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Royal_Autumn_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Royal_Winter_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Wood_Spring_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Wood_Summer_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Wood_Autumn_Mesh") ||
                            fileName.StartsWith("TotalWar_ForestFern_Wood_Winter_Mesh"))
                        {
                            isKeep = false;
                        }
                    }

                    if (!isKeep)
                    {
                        DeleteFileAndMeta(filePath);
                        removedFiles++;
                    }
                }
            }

            if (Directory.Exists(MatDir))
            {
                string[] allMatDirFiles = Directory.GetFiles(MatDir);
                foreach (string filePath in allMatDirFiles)
                {
                    if (filePath.EndsWith(".meta")) continue;
                    string fileName = Path.GetFileName(filePath);

                    bool isKeep = fileName.StartsWith("TotalWar_");
                    // 갈대로 오분류되었던 구버전 연꽃 머티리얼 삭제
                    if (fileName.StartsWith("TotalWar_TallReed3D_Lotus_"))
                    {
                        isKeep = false;
                    }

                    if (!isKeep)
                    {
                        DeleteFileAndMeta(filePath);
                        removedFiles++;
                    }
                }
            }

            if (removedFiles > 0)
            {
                Debug.Log($"<color=#FFCC00>[MasterVegetationBuilder] 🧹 구버전 프리팹/메쉬/머티리얼 {removedFiles}개 삭제 완료!</color>");
            }
        }

        private static void DeleteFileAndMeta(string path)
        {
            try
            {
                string normalized = path.Replace("\\", "/");
                if (File.Exists(normalized)) File.Delete(normalized);
                if (File.Exists(normalized + ".meta")) File.Delete(normalized + ".meta");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MasterVegetationBuilder] 파일 삭제 실패 ({path}): {ex.Message}");
            }
        }

        // =========================================================================================
        // 1. 텍스처 임포트 설정 보장 (전수 PNG 알파 컷아웃 & 노멀맵)
        // =========================================================================================
        private static void EnsureCoreTextureImportSettings()
        {
            string[] coreAlbedos = new[]
            {
                "tem_grass_sp_base_colour.png", "tem_grass_base_colour.png", "tem_grass_au_base_colour.png", "tem_grass_wi_base_colour.png",
                "tem_forest_grass_sp_base_colour.png", "tem_forest_grass_base_colour.png", "tem_forest_grass_au_base_colour.png", "tem_forest_grass_wi_base_colour.png",
                "sbt_grass0_sp_base_colour.png", "sbt_grass0_base_colour.png", "sbt_grass0_au_base_colour.png", "sbt_grass_base_colour.png",
                "sbt_forest_grass0_base_colour.png", "sbt_forest_grass0_au_base_colour.png",
                "dyn_sbt_grass0_base_colour.png", "dyn_sbt_forest_grass0_base_colour.png",
                "tro_grass_base_colour.png", "tro_forest_grass_base_colour.png",
                "ard_grass0_base_colour.png", "ard_forest_grass0_au_base_colour.png",
                "cld_grass0_sp_base_colour.png", "cld_grass0_su_base_colour.png", "cld_grass0_base_colour.png", "cld_grass0_au_base_colour.png", "cld_grass0_ha_base_colour.png", "cld_grass0_wi_base_colour.png",
                "cld_forest_grass0_sp_base_colour.png", "cld_forest_grass0_su_base_colour.png", "cld_forest_grass0_au_base_colour.png", "cld_forest_grass0_wi_base_colour.png",
                "gen_rice_paddy0_base_colour.png", "gen_rice_paddy0_au_base_colour.png", "sbt_agri1_base_colour.png",
                "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_au_base_colour.png", "shrub_grassclump_wi_base_colour.png",
                "shrub_grassclump_ha_base_colour.png", "shrub_grassclump_base_colour.png",
                "shrubs_ferns_base_colour.png", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_au_base_colour.png", "shrubs_ferns_wi_base_colour.png",
                "3k_tropical_shrubs_base_colour.png",
                "shrub_rhododendron_sp_base_colour.png", "shrub_rhododendron_base_colour.png",
                "shrub_tea_sp_base_colour.png", "shrub_tea_base_colour.png",
                "shrub_magnolia_sp_base_colour.png", "shrub_magnolia_au_base_colour.png", "shrub_magnolia_wi_base_colour.png", "shrub_magnolia_base_colour.png"
            };

            foreach (string file in coreAlbedos)
            {
                string path = ResolveTextureAssetPath(file);
                if (string.IsNullOrEmpty(path)) continue;
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null)
                {
                    bool changed = false;
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                    if (!ti.mipMapsPreserveCoverage) { ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.18f; changed = true; }
                    if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
                    if (changed) ti.SaveAndReimport();
                }
            }

            string[] coreNormals = new[]
            {
                "tem_grass_normal.png", "tem_forest_grass_normal.png",
                "sbt_grass0_normal.png", "sbt_forest_grass0_normal.png",
                "ard_grass0_normal.png", "ard_forest_grass0_normal.png",
                "cld_grass0_normal.png", "cld_forest_grass0_normal.png",
                "tro_grass_normal.png", "tro_forest_grass_normal.png",
                "gen_rice_paddy0_normal.png", "3k_tropical_shrubs_normal.png",
                "shrub_grassclump_normal.png", "shrubs_ferns_normal.png",
                "shrub_rhododendron_normal.png", "shrub_tea_normal.png", "shrub_magnolia_normal.png"
            };

            foreach (string file in coreNormals)
            {
                string path = ResolveTextureAssetPath(file);
                if (string.IsNullOrEmpty(path)) continue;
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
        }

        private static string ResolveTextureAssetPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string[] dirs = new[]
            {
                TexRootDir,
                TexRootDir + "Seasons/Spring/",
                TexRootDir + "Seasons/Summer/",
                TexRootDir + "Seasons/Autumn/",
                TexRootDir + "Seasons/Winter/",
                TexRootDir + "Branches_Leaves/",
                TexRootDir + "Trunks/"
            };
            foreach (var d in dirs)
            {
                string p = d + fileName;
                if (File.Exists(p)) return p;
            }
            return null;
        }

        public static Material GetOrCreateRadialFoliageMaterial(string matName, string albedoFile, string normalFile, float cutoff = 0.18f)
        {
            string matPath = $"{MatDir}/{matName}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/TotalWarRadialGrassLOD");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/TotalWarFoliageWind");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = false;
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                isNew = true;
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            mat.SetFloat("_Cull", 0f); // 양면 렌더링
            mat.SetFloat("_Cutoff", cutoff);
            if (mat.HasProperty("_TipFadeStart")) mat.SetFloat("_TipFadeStart", 0.992f);
            if (mat.HasProperty("_TipFadeEnd")) mat.SetFloat("_TipFadeEnd", 0.999f);
            if (mat.HasProperty("_EdgeBleedCrop")) mat.SetFloat("_EdgeBleedCrop", 0.002f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1.04f, 1.06f, 1.02f, 1.0f));
            mat.enableInstancing = true;

            string albedoPath = ResolveTextureAssetPath(albedoFile);
            Texture2D albedo = !string.IsNullOrEmpty(albedoPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath) : null;
            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }

            string normalPath = ResolveTextureAssetPath(normalFile);
            if (string.IsNullOrEmpty(normalPath)) normalPath = ResolveTextureAssetPath("shrub_grassclump_normal.png");
            if (!string.IsNullOrEmpty(normalPath))
            {
                Texture2D bump = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (bump != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", bump);
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void SaveMeshAndPrefab(Mesh mesh, string meshAssetName, Material mat, string prefabAssetName = null)
        {
            string meshPath = $"{MeshDir}/{meshAssetName}.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.vertices = mesh.vertices;
                existingMesh.uv = mesh.uv;
                existingMesh.triangles = mesh.triangles;
                existingMesh.normals = mesh.normals;
                existingMesh.colors = mesh.colors;
                existingMesh.tangents = mesh.tangents;
                existingMesh.RecalculateBounds();
                EditorUtility.SetDirty(existingMesh);
            }
            else
            {
                mesh.name = meshAssetName;
                AssetDatabase.CreateAsset(mesh, meshPath);
                existingMesh = mesh;
            }

            if (!string.IsNullOrEmpty(prefabAssetName) && mat != null)
            {
                string prefabPath = $"{MeshDir}/{prefabAssetName}.prefab";
                GameObject tempGO = new GameObject(prefabAssetName);
                MeshFilter mf = tempGO.AddComponent<MeshFilter>();
                mf.sharedMesh = existingMesh;
                MeshRenderer mr = tempGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
                UnityEngine.Object.DestroyImmediate(tempGO);
            }
        }

        private static void ApplyDomeNormalsAndRootAO(Mesh mesh, float darkRootBrightness = 0.10f)
        {
            mesh.RecalculateBounds();
            Vector3[] verts = mesh.vertices;
            Color[] colors = new Color[verts.Length];
            Vector3[] normals = new Vector3[verts.Length];
            Vector3 center = mesh.bounds.center;
            float maxY = Mathf.Max(0.01f, mesh.bounds.size.y);
            float minY = mesh.bounds.min.y;

            for (int i = 0; i < verts.Length; i++)
            {
                float hNorm = Mathf.Clamp01((verts[i].y - minY) / maxY);
                colors[i] = Color.Lerp(new Color(darkRootBrightness, darkRootBrightness, darkRootBrightness, 1f), Color.white, hNorm);

                Vector3 n = (verts[i] - center).normalized;
                n.y = Mathf.Max(n.y, 0.55f);
                normals[i] = n.normalized;
            }
            mesh.colors = colors;
            mesh.normals = normals;
        }

        // =========================================================================================
        // 2. 유형 ① [1자 잔디 띠 & 야생화]: 오각형 컷아웃 + 10장 360도 방사형 교차 단일 군락 (41종 전수)
        // =========================================================================================
        private static Mesh CreateSinglePentagonStripMesh(
            float uLeft, float uLeftShoulder, float uCenter, float uRightShoulder, float uRight,
            float vBottom, float vLeftShoulder, float vPeak, float vRightShoulder,
            float width, float height, float xShift, float zShift)
        {
            float uSpan = Mathf.Max(0.001f, uRight - uLeft);
            float vDiff = vPeak - vBottom;
            float vMid = (vBottom + vPeak) * 0.5f;

            Vector2[] uvs = new Vector2[7]
            {
                new Vector2(uLeft, vBottom),
                new Vector2(uCenter, vBottom),
                new Vector2(uRight, vBottom),
                new Vector2(uLeftShoulder, vLeftShoulder),
                new Vector2(uCenter, vPeak),
                new Vector2(uRightShoulder, vRightShoulder),
                new Vector2(uCenter, vMid)
            };

            Vector3[] verts = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                // uLeft~uRight 범위를 [-0.5, +0.5]로 정규화하여 아틀라스 부분 영역도 항상 중심(0,0)에 오도록 보장
                float normX = ((uvs[i].x - uLeft) / uSpan) - 0.5f;
                float vx = normX * width + xShift;
                // vBottom이 항상 지면(vy=0), vPeak가 항상 하늘 꼭대기(vy=height)가 되도록 방향에 무관하게 정규화
                float vy = Mathf.Abs(vDiff) > 0.0001f ? Mathf.Clamp01((uvs[i].y - vBottom) / vDiff) * height : 0f;
                verts[i] = new Vector3(vx, vy, zShift);
            }

            int[] tris = new int[]
            {
                0, 3, 1,
                1, 3, 6,
                3, 4, 6,
                6, 4, 5,
                1, 6, 5,
                1, 5, 2
            };

            Mesh m = new Mesh();
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildPentagon10StripRadialClusterMesh(
            string meshName,
            float uLeft, float uRight,
            float rowA_vBot, float rowA_vSh, float rowA_vPeak,
            float rowB_vBot, float rowB_vSh, float rowB_vPeak,
            float stripWidth = 1.06f, float stripHeight = 0.28f)
        {
            float uCenter = (uLeft + uRight) * 0.5f;
            float uLeftSh = Mathf.Lerp(uLeft, uCenter, 0.48f);
            float uRightSh = Mathf.Lerp(uCenter, uRight, 0.52f);

            Mesh frontStrip = CreateSinglePentagonStripMesh(
                uLeft, uLeftSh, uCenter, uRightSh, uRight,
                rowA_vBot, rowA_vSh, rowA_vPeak, rowA_vSh,
                stripWidth, stripHeight, 0f, 0.003f);

            Mesh backStrip = CreateSinglePentagonStripMesh(
                uLeft, uLeftSh, uCenter, uRightSh, uRight,
                rowB_vBot, rowB_vSh, rowB_vPeak, rowB_vSh,
                stripWidth * 0.96f, stripHeight * 0.93f, -0.042f, -0.003f);

            CombineInstance[] swathComb = new CombineInstance[2];
            swathComb[0] = new CombineInstance { mesh = frontStrip, transform = Matrix4x4.identity };
            swathComb[1] = new CombineInstance { mesh = backStrip, transform = Matrix4x4.identity };

            Mesh swathMesh = new Mesh();
            swathMesh.CombineMeshes(swathComb, true, true);

            int stripCount = 10;
            CombineInstance[] clusterComb = new CombineInstance[stripCount];
            for (int s = 0; s < stripCount; s++)
            {
                float baseAngle = (s / (float)stripCount) * 180f + ((s % 2 == 1) ? 180f : 0f);
                float angleJitter = (s % 2 == 0) ? 4.5f : -4.5f;
                float yaw = baseAngle + angleJitter;

                float dist = (s == 0) ? 0f : (0.05f + (s % 4) * 0.035f);
                float offsetDir = (s * 137.5f) * Mathf.Deg2Rad;
                float stripX = Mathf.Cos(offsetDir) * dist;
                float stripZ = Mathf.Sin(offsetDir) * dist;
                float stripScale = Mathf.Lerp(1.08f, 0.94f, dist / 0.16f);

                clusterComb[s].mesh = swathMesh;
                clusterComb[s].transform = Matrix4x4.TRS(
                    new Vector3(stripX, 0f, stripZ),
                    Quaternion.Euler(0f, yaw, 0f),
                    Vector3.one * stripScale
                );
            }

            Mesh clusterMesh = new Mesh();
            clusterMesh.name = meshName;
            clusterMesh.CombineMeshes(clusterComb, true, true);
            ApplyDomeNormalsAndRootAO(clusterMesh, 0.10f);
            return clusterMesh;
        }

        public static void BuildType1_RadialStripClusters()
        {
            // 1. 기존 검증된 표준 1~4번 머티리얼 및 메쉬 보장
            Material pureGrassSuMat = GetOrCreateRadialFoliageMaterial("TotalWar_GrassCluster_PureSu_Mat", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png");
            GetOrCreateRadialFoliageMaterial("TotalWar_Spring_CarpetGrass_Mat", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png");
            Material temSpFlowerMat = GetOrCreateRadialFoliageMaterial("TotalWar_TemGrassSp_Flower_Mat", "tem_grass_sp_base_colour.png", "tem_grass_normal.png");
            Material sbtSpFlowerMat = GetOrCreateRadialFoliageMaterial("TotalWar_SbtGrass0Sp_Flower_Mat", "sbt_grass0_sp_base_colour.png", "sbt_grass0_normal.png");

            TotalWarProceduralFoliageDemo.BuildPentagonFlowerClusterMeshAndPrefab(TotalWarProceduralFoliageDemo.TotalWarFlowerGrassVariant.PinkBuds_Photo1, temSpFlowerMat);
            TotalWarProceduralFoliageDemo.BuildPentagonFlowerClusterMeshAndPrefab(TotalWarProceduralFoliageDemo.TotalWarFlowerGrassVariant.PurplePetals_Photo2, temSpFlowerMat);
            TotalWarProceduralFoliageDemo.BuildPentagonFlowerClusterMeshAndPrefab(TotalWarProceduralFoliageDemo.TotalWarFlowerGrassVariant.OrangeSpikes_Photo3, sbtSpFlowerMat);

            // 2. 야생화 다방향 군락 추가 8종 (아열대 청보라꽃, 수레국화, 노란들국화, 구절초, 수림 보라꽃, 수림 분홍꽃, 한랭 보라꽃, 열대 토끼풀)
            var flowerDefs = new (string key, string tex, string norm, float uL, float uR, float rA_bot, float rA_sh, float rA_peak, float rB_bot, float rB_sh, float rB_peak, float w, float h)[]
            {
                ("BluePetals_Sbt",     "sbt_grass0_sp_base_colour.png",        "sbt_grass0_normal.png",       0.004f, 0.996f, 0.503f, 0.640f, 0.785f, 0.303f, 0.400f, 0.495f, 1.06f, 0.29f),
                ("Cornflower_Sbt",     "sbt_grass_base_colour.png",            "sbt_grass0_normal.png",       0.180f, 0.880f, 0.004f, 0.140f, 0.195f, 0.004f, 0.140f, 0.195f, 1.02f, 0.30f),
                ("YellowDaisy_Sbt",    "sbt_grass_base_colour.png",            "sbt_grass0_normal.png",       0.545f, 0.765f, 0.240f, 0.480f, 0.590f, 0.180f, 0.140f, 0.195f, 0.96f, 0.32f),
                ("WhiteAster_Sbt",     "sbt_grass_base_colour.png",            "sbt_grass0_normal.png",       0.610f, 0.930f, 0.045f, 0.220f, 0.320f, 0.045f, 0.220f, 0.320f, 0.98f, 0.30f),
                ("ForestViolet_Tem",   "tem_forest_grass_sp_base_colour.png",  "tem_forest_grass_normal.png", 0.660f, 0.985f, 0.055f, 0.260f, 0.365f, 0.055f, 0.260f, 0.365f, 0.98f, 0.31f),
                ("ForestPink_Tem",     "tem_forest_grass_sp_base_colour.png",  "tem_forest_grass_normal.png", 0.540f, 0.985f, 0.640f, 0.820f, 0.925f, 0.640f, 0.820f, 0.925f, 1.02f, 0.32f),
                ("ColdViolet_Cld",     "cld_forest_grass0_sp_base_colour.png", "cld_forest_grass0_normal.png",0.660f, 0.985f, 0.055f, 0.260f, 0.365f, 0.055f, 0.260f, 0.365f, 0.96f, 0.28f),
                ("TropicalClover_Tro", "tro_grass_base_colour.png",            "tro_grass_normal.png",        0.004f, 0.996f, 0.802f, 0.920f, 0.996f, 0.303f, 0.400f, 0.495f, 1.06f, 0.30f)
            };

            foreach (var fd in flowerDefs)
            {
                string matName = $"TotalWar_FlowerCluster_{fd.key}_Mat";
                string meshName = $"TotalWar_FlowerCluster_{fd.key}_Mesh";
                string prefabName = $"TotalWar_FlowerCluster_{fd.key}_Prefab";

                Material mat = GetOrCreateRadialFoliageMaterial(matName, fd.tex, fd.norm, 0.18f);
                Mesh mesh = BuildPentagon10StripRadialClusterMesh(
                    meshName, fd.uL, fd.uR,
                    fd.rA_bot, fd.rA_sh, fd.rA_peak,
                    fd.rB_bot, fd.rB_sh, fd.rB_peak,
                    fd.w, fd.h);
                SaveMeshAndPrefab(mesh, meshName, mat, prefabName);
            }

            // 3. 전 기후(온대, 아열대, 열대, 수변, 건조, 한랭) & 초원/수림 & 사계절 지피 잔디 군락 29종 전수 생성
            // 💡 주의: 원작 수림 잔디(Forest Grass) 텍스처는 윗부분(v=0.500)이 줄기 밑동(뿌리)이고 아랫부분(v=0.320)이 잎끝이므로
            //          rA_bot=0.500f, rA_peak=0.320f 로 매핑하여 잎끝이 하늘로 곧게 솟도록 상하 반전을 완벽하게 교정합니다.
            var grassDefs = new (string key, string tex, string norm, float uL, float uR, float rA_bot, float rA_sh, float rA_peak, float rB_bot, float rB_sh, float rB_peak, float h)[]
            {
                // ① 온대 초원 (Temperate Plains) 4계절
                ("Tem_Spring",       "tem_grass_sp_base_colour.png",          "tem_grass_normal.png",          0.004f, 0.996f, 0.004f, 0.150f, 0.296f, 0.303f, 0.400f, 0.497f, 0.27f),
                ("Tem_Summer",       "tem_grass_base_colour.png",             "tem_grass_normal.png",          0.004f, 0.996f, 0.004f, 0.150f, 0.296f, 0.303f, 0.400f, 0.497f, 0.28f),
                ("Tem_Autumn",       "tem_grass_au_base_colour.png",          "tem_grass_normal.png",          0.004f, 0.996f, 0.004f, 0.150f, 0.296f, 0.303f, 0.400f, 0.497f, 0.28f),
                ("Tem_Winter",       "tem_grass_wi_base_colour.png",          "tem_grass_normal.png",          0.004f, 0.996f, 0.004f, 0.150f, 0.296f, 0.303f, 0.400f, 0.497f, 0.25f),

                // ② 온대 수림 (Temperate Forest) 4계절 (수림 밑동 v:0.500 -> 잎끝 v:0.320 직립 정규화)
                ("TemForest_Spring", "tem_forest_grass_sp_base_colour.png",   "tem_forest_grass_normal.png",   0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.26f),
                ("TemForest_Summer", "tem_forest_grass_base_colour.png",      "tem_forest_grass_normal.png",   0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.27f),
                ("TemForest_Autumn", "tem_forest_grass_au_base_colour.png",   "tem_forest_grass_normal.png",   0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.26f),
                ("TemForest_Winter", "tem_forest_grass_wi_base_colour.png",   "tem_forest_grass_normal.png",   0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.24f),

                // ③ 아열대 초원 & 수변 (Subtropical Plains & Wetland) 6종
                ("Sbt_Spring",       "sbt_grass0_sp_base_colour.png",         "sbt_grass0_normal.png",         0.004f, 0.996f, 0.303f, 0.400f, 0.495f, 0.803f, 0.900f, 0.996f, 0.29f),
                ("Sbt_Summer",       "sbt_grass0_base_colour.png",            "sbt_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.30f),
                ("Sbt_Autumn",       "sbt_grass0_au_base_colour.png",         "sbt_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.29f),
                ("Sbt_Meadow",       "sbt_grass_base_colour.png",             "sbt_grass0_normal.png",         0.080f, 0.540f, 0.480f, 0.680f, 0.820f, 0.480f, 0.680f, 0.820f, 0.31f),
                ("Sbt_Wetland",      "dyn_sbt_grass0_base_colour.png",        "sbt_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.30f),
                ("SbtForest_Wetland","dyn_sbt_forest_grass0_base_colour.png", "sbt_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.28f),

                // ④ 아열대 수림 (Subtropical Forest) 2종 (수림 밑동 v:0.500 -> 잎끝 v:0.320 직립 정규화)
                ("SbtForest_Summer", "sbt_forest_grass0_base_colour.png",     "sbt_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.28f),
                ("SbtForest_Autumn", "sbt_forest_grass0_au_base_colour.png",  "sbt_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.27f),

                // ⑤ 남만 열대 초원 & 정글 수림 (Tropical) 2종
                ("Tro_Summer",       "tro_grass_base_colour.png",             "tro_grass_normal.png",          0.004f, 0.996f, 0.004f, 0.140f, 0.275f, 0.303f, 0.400f, 0.495f, 0.32f),
                ("TroForest_Summer", "tro_forest_grass_base_colour.png",      "tro_forest_grass_normal.png",   0.150f, 0.860f, 0.505f, 0.420f, 0.320f, 0.505f, 0.420f, 0.320f, 0.29f),

                // ⑥ 서북 건조 황무지 (Arid) 2종
                ("Ard_Summer",       "ard_grass0_base_colour.png",            "ard_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.26f),
                ("Ard_Autumn",       "ard_forest_grass0_au_base_colour.png",  "ard_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.25f),

                // ⑦ 북방 한랭 초원 (Cold Plains) 5종
                ("Cld_Spring",       "cld_grass0_sp_base_colour.png",         "cld_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.25f),
                ("Cld_Summer",       "cld_grass0_su_base_colour.png",         "cld_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.26f),
                ("Cld_Autumn",       "cld_grass0_au_base_colour.png",         "cld_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.25f),
                ("Cld_Harvest",      "cld_grass0_ha_base_colour.png",         "cld_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.25f),
                ("Cld_Winter",       "cld_grass0_wi_base_colour.png",         "cld_grass0_normal.png",         0.004f, 0.996f, 0.004f, 0.140f, 0.295f, 0.303f, 0.400f, 0.495f, 0.24f),

                // ⑧ 북방 한랭 수림 (Cold Forest) 4종 (수림 밑동 v:0.500 -> 잎끝 v:0.320 직립 정규화)
                ("CldForest_Spring", "cld_forest_grass0_sp_base_colour.png",  "cld_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.25f),
                ("CldForest_Summer", "cld_forest_grass0_su_base_colour.png",  "cld_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.26f),
                ("CldForest_Autumn", "cld_forest_grass0_au_base_colour.png",  "cld_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.25f),
                ("CldForest_Winter", "cld_forest_grass0_wi_base_colour.png",  "cld_forest_grass0_normal.png",  0.160f, 0.850f, 0.500f, 0.420f, 0.320f, 0.500f, 0.420f, 0.320f, 0.23f)
            };

            foreach (var def in grassDefs)
            {
                string matName = $"TotalWar_GrassCluster_{def.key}_Mat";
                string meshName = $"TotalWar_GrassCluster_{def.key}_Mesh";
                string prefabName = $"TotalWar_GrassCluster_{def.key}_Prefab";

                Material mat = GetOrCreateRadialFoliageMaterial(matName, def.tex, def.norm, 0.18f);
                Mesh mesh = BuildPentagon10StripRadialClusterMesh(
                    meshName, def.uL, def.uR,
                    def.rA_bot, def.rA_sh, def.rA_peak,
                    def.rB_bot, def.rB_sh, def.rB_peak,
                    1.06f, def.h);

                SaveMeshAndPrefab(mesh, meshName, mat, prefabName);
            }
        }

        // =========================================================================================
        // 3. 유형 ② [3D 입체 풀포기 & 농작물 & 화초 관목]: 중심 피벗 방사형 부채꼴 (20종 전수)
        // =========================================================================================
        /// <summary>
        /// 아틀라스 내 특정 줄기/포기 축(uvBase -> uvTip)을 따라 3단 방사형 부채꼴(Radial Fan 3D) 메쉬를 생성합니다.
        /// </summary>
        private static Mesh BuildCustomAxisRadialFan3DMesh(
            string meshName,
            Vector2 uvBase, Vector2 uvTip, float uvHalfWidth,
            float bladeHeight, float bladeWidth,
            int innerCount = 6, int midCount = 8, int outerCount = 10,
            float archAmount = 0.14f)
        {
            Mesh singleCard = CreateArchingCardMesh(uvBase, uvTip, uvHalfWidth, bladeHeight, bladeWidth, archAmount);

            int total = innerCount + midCount + outerCount;
            CombineInstance[] combines = new CombineInstance[total];
            int idx = 0;

            for (int i = 0; i < innerCount; i++)
            {
                float angleRad = (i / (float)innerCount) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.02f, 0f, Mathf.Sin(angleRad) * 0.02f);
                float yaw = (i / (float)innerCount) * 360f + ((i % 2 == 0) ? 5f : -5f);
                float pitch = -14f + (i % 3) * 3f; // 직립 코어
                combines[idx++] = new CombineInstance
                {
                    mesh = singleCard,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), new Vector3(0.92f, 1.08f, 0.92f))
                };
            }

            for (int i = 0; i < midCount; i++)
            {
                float angleRad = ((i + 0.5f) / midCount) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.045f, 0f, Mathf.Sin(angleRad) * 0.045f);
                float yaw = ((i + 0.5f) / midCount) * 360f + ((i % 2 == 0) ? -6f : 6f);
                float pitch = -4f + (i % 3) * 3f; // 중측 부채꼴
                combines[idx++] = new CombineInstance
                {
                    mesh = singleCard,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * 0.98f)
                };
            }

            for (int i = 0; i < outerCount; i++)
            {
                float angleRad = (i / (float)outerCount) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.07f, 0f, Mathf.Sin(angleRad) * 0.07f);
                float yaw = (i / (float)outerCount) * 360f + ((i % 2 == 0) ? 7f : -7f);
                float pitch = 6f + (i % 3) * 3f; // 외측 부채꼴
                combines[idx++] = new CombineInstance
                {
                    mesh = singleCard,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), new Vector3(1.04f, 0.90f, 1.04f))
                };
            }

            Mesh clump = new Mesh();
            clump.name = meshName;
            clump.CombineMeshes(combines, true, true);
            ApplyDomeNormalsAndRootAO(clump, 0.10f);
            return clump;
        }

        private static Mesh CreateArchingCardMesh(Vector2 uvBase, Vector2 uvTip, float uvHalfWidth, float height, float width, float archAmount)
        {
            int segs = 4;
            int vertCount = (segs + 1) * 2;
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[segs * 6];

            Vector2 axis = uvTip - uvBase;
            Vector2 perp = new Vector2(-axis.y, axis.x).normalized;

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float y = t * height;
                float z = (t * t) * (height * archAmount);

                float halfW3D = width * 0.5f;
                Vector2 uvCenter = Vector2.Lerp(uvBase, uvTip, t);
                Vector2 uvLeft = uvCenter - perp * uvHalfWidth;
                Vector2 uvRight = uvCenter + perp * uvHalfWidth;

                uvLeft.x = Mathf.Clamp(uvLeft.x, 0.004f, 0.996f);
                uvLeft.y = Mathf.Clamp(uvLeft.y, 0.004f, 0.996f);
                uvRight.x = Mathf.Clamp(uvRight.x, 0.004f, 0.996f);
                uvRight.y = Mathf.Clamp(uvRight.y, 0.004f, 0.996f);

                verts[i * 2 + 0] = new Vector3(-halfW3D, y, z);
                verts[i * 2 + 1] = new Vector3(+halfW3D, y, z);
                uvs[i * 2 + 0] = uvLeft;
                uvs[i * 2 + 1] = uvRight;
            }

            int tIdx = 0;
            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 2; int b1 = i * 2 + 1;
                int t0 = (i + 1) * 2; int t1 = (i + 1) * 2 + 1;
                tris[tIdx++] = b0; tris[tIdx++] = t0; tris[tIdx++] = b1;
                tris[tIdx++] = b1; tris[tIdx++] = t0; tris[tIdx++] = t1;
            }

            Mesh m = new Mesh();
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            m.RecalculateNormals();
            return m;
        }

        public static void BuildType2_RadialFan3DClumps()
        {
            List<Mesh> cleanBlades = TotalWarProceduralFoliageDemo.ExtractCardsFromObj("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj");
            if (cleanBlades != null && cleanBlades.Count > 0)
            {
                int innerCount = 8;
                int midCount = 10;
                int outerCount = 12;
                int totalCards = innerCount + midCount + outerCount;

                CombineInstance[] combines = new CombineInstance[totalCards];
                int idx = 0;

                for (int i = 0; i < innerCount; i++)
                {
                    float angleRad = (i / (float)innerCount) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.025f, 0f, Mathf.Sin(angleRad) * 0.025f);
                    float yaw = -angleRad * Mathf.Rad2Deg + 90f + ((i % 2 == 0) ? 5f : -5f);
                    float pitch = 15f + (i % 3) * 2.5f;
                    Mesh card = cleanBlades[(cleanBlades.Count - 1) - (i % Mathf.Min(4, cleanBlades.Count))];
                    float s = 0.34f;
                    combines[idx++] = new CombineInstance
                    {
                        mesh = card,
                        transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), new Vector3(s, s * 1.12f, s))
                    };
                }

                for (int i = 0; i < midCount; i++)
                {
                    float angleRad = ((i + 0.5f) / midCount) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.055f, 0f, Mathf.Sin(angleRad) * 0.055f);
                    float yaw = -angleRad * Mathf.Rad2Deg + 90f + ((i % 2 == 0) ? -6f : 6f);
                    float pitch = 28f + (i % 3) * 3.0f;
                    Mesh card = cleanBlades[(i + 2) % cleanBlades.Count];
                    float s = 0.30f;
                    combines[idx++] = new CombineInstance
                    {
                        mesh = card,
                        transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * s)
                    };
                }

                for (int i = 0; i < outerCount; i++)
                {
                    float angleRad = (i / (float)outerCount) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angleRad) * 0.085f, 0f, Mathf.Sin(angleRad) * 0.085f);
                    float yaw = -angleRad * Mathf.Rad2Deg + 90f + ((i % 2 == 0) ? 7f : -7f);
                    float pitch = 40f + (i % 3) * 3.0f;
                    Mesh card = cleanBlades[i % Mathf.Min(5, cleanBlades.Count)];
                    float s = 0.25f;
                    combines[idx++] = new CombineInstance
                    {
                        mesh = card,
                        transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * s)
                    };
                }

                Mesh clump3DMesh = new Mesh();
                clump3DMesh.name = "TotalWar_RadialFan3D_Clump_Mesh";
                clump3DMesh.CombineMeshes(combines, true, true);
                ApplyDomeNormalsAndRootAO(clump3DMesh, 0.09f);

                // 마스터 메쉬 1개만 디스크에 저장
                SaveMeshAndPrefab(clump3DMesh, "TotalWar_RadialFan3D_Clump_Mesh", null, null);

                // 사계절 3D 풀포기 6종 + 수전 벼 포기 2종 (동일 UV 레이아웃, 마스터 메쉬 공유)
                var variants = new (string key, string tex, string norm)[]
                {
                    ("Spring",          "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Summer",          "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Autumn",          "shrub_grassclump_au_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Winter",          "shrub_grassclump_wi_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Harvest",         "shrub_grassclump_ha_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Arid",            "shrub_grassclump_base_colour.png",    "shrub_grassclump_normal.png"),
                    ("RicePaddy_Green", "gen_rice_paddy0_base_colour.png",     "gen_rice_paddy0_normal.png"),
                    ("RicePaddy_Gold",  "gen_rice_paddy0_au_base_colour.png",  "gen_rice_paddy0_normal.png")
                };

                foreach (var v in variants)
                {
                    Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_RadialFan3D_{v.key}_Mat", v.tex, v.norm, 0.22f);
                    // 메쉬를 중복 복제하지 않고 마스터 메쉬를 공유하여 프리팹 생성
                    SaveMeshAndPrefab(clump3DMesh, "TotalWar_RadialFan3D_Clump_Mesh", mat, $"TotalWar_RadialFan3D_{v.key}_Prefab");
                }
            }

            // 특수 작물(보리/밀), 열대 식물(코끼리풀/활엽초/열대꽃), 수림 화초(은방울꽃), 관목(철쭉/차나무/목련) 12종 전수 생성
            var customClumps = new (string key, string tex, string norm, Vector2 uvBase, Vector2 uvTip, float uvHalfW, float h, float w, float arch)[]
            {
                // 농경지 황금 보리/밀 포기 & 건초 보리 포기 (sbt_agri1)
                ("Wheat_Gold",          "sbt_agri1_base_colour.png",            "gen_rice_paddy0_normal.png",   new Vector2(0.475f, 0.030f), new Vector2(0.240f, 0.650f), 0.145f, 0.52f, 0.34f, 0.16f),
                ("Wheat_Dry",           "sbt_agri1_base_colour.png",            "gen_rice_paddy0_normal.png",   new Vector2(0.975f, 0.030f), new Vector2(0.740f, 0.650f), 0.145f, 0.50f, 0.34f, 0.16f),

                // 남만 열대 코끼리풀 포기, 토란 활엽초 포기, 열대 야생화 포기 (3k_tropical_shrubs)
                ("ElephantGrass",       "3k_tropical_shrubs_base_colour.png",   "3k_tropical_shrubs_normal.png", new Vector2(0.595f, 0.100f), new Vector2(0.585f, 0.960f), 0.165f, 0.62f, 0.42f, 0.18f),
                ("TropicalBigleaf",     "3k_tropical_shrubs_base_colour.png",   "3k_tropical_shrubs_normal.png", new Vector2(0.215f, 0.445f), new Vector2(0.230f, 0.610f), 0.072f, 0.44f, 0.32f, 0.25f),
                ("TropicalFlower",      "3k_tropical_shrubs_base_colour.png",   "3k_tropical_shrubs_normal.png", new Vector2(0.310f, 0.940f), new Vector2(0.275f, 0.720f), 0.065f, 0.45f, 0.28f, 0.15f),

                // 수림 은방울꽃 (Convallaria - shrubs_ferns_su 좌하단)
                ("Convallaria",         "shrubs_ferns_su_base_colour.png",      "shrubs_ferns_normal.png",       new Vector2(0.015f, 0.015f), new Vector2(0.120f, 0.220f), 0.100f, 0.38f, 0.28f, 0.20f),

                // 진달래/철쭉 꽃덤불 (봄/여름 - shrub_rhododendron)
                ("Rhododendron_Spring", "shrub_rhododendron_sp_base_colour.png","shrub_rhododendron_normal.png", new Vector2(0.295f, 0.665f), new Vector2(0.180f, 0.965f), 0.160f, 0.54f, 0.40f, 0.22f),
                ("Rhododendron_Summer", "shrub_rhododendron_base_colour.png",   "shrub_rhododendron_normal.png", new Vector2(0.295f, 0.665f), new Vector2(0.180f, 0.965f), 0.160f, 0.54f, 0.40f, 0.22f),

                // 강남 차나무 꽃덤불 (봄/여름 - shrub_tea)
                ("TeaBush_Spring",      "shrub_tea_sp_base_colour.png",         "shrub_tea_normal.png",          new Vector2(0.920f, 0.230f), new Vector2(0.840f, 0.950f), 0.135f, 0.50f, 0.38f, 0.20f),
                ("TeaBush_Summer",      "shrub_tea_base_colour.png",            "shrub_tea_normal.png",          new Vector2(0.920f, 0.230f), new Vector2(0.840f, 0.950f), 0.135f, 0.50f, 0.38f, 0.20f),

                // 목련 꽃덤불 (봄/가을 - shrub_magnolia)
                ("Magnolia_Spring",     "shrub_magnolia_sp_base_colour.png",    "shrub_magnolia_normal.png",     new Vector2(0.350f, 0.040f), new Vector2(0.820f, 0.360f), 0.160f, 0.56f, 0.42f, 0.22f),
                ("Magnolia_Autumn",     "shrub_magnolia_au_base_colour.png",    "shrub_magnolia_normal.png",     new Vector2(0.350f, 0.040f), new Vector2(0.820f, 0.360f), 0.160f, 0.56f, 0.42f, 0.22f)
            };

            foreach (var cc in customClumps)
            {
                Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_RadialFan3D_{cc.key}_Mat", cc.tex, cc.norm, 0.22f);
                Mesh mesh = BuildCustomAxisRadialFan3DMesh(
                    $"TotalWar_RadialFan3D_{cc.key}_Mesh",
                    cc.uvBase, cc.uvTip, cc.uvHalfW,
                    cc.h, cc.w, 6, 8, 10, cc.arch);
                SaveMeshAndPrefab(mesh, $"TotalWar_RadialFan3D_{cc.key}_Mesh", mat, $"TotalWar_RadialFan3D_{cc.key}_Prefab");
            }
        }

        // =========================================================================================
        // 4. 유형 ③ [키 큰 갈대 / 물억새 / 곡식 이삭 / 수생 연꽃]: 0.8m~1.2m 직립 포인트 (15종 전수)
        // =========================================================================================
        /// <summary>
        /// 🪷 [원작 1:1 정품 3D 연꽃 메쉬] Shrub_Lotus_A.obj의 꽃봉오리 12면 + 연잎 18면을 정밀 추출하여 단일 군락으로 정제합니다.
        /// </summary>
        private static Mesh BuildAuthenticLotusClusterMesh(string meshName)
        {
            string objPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Lotus_A.obj";
            if (!File.Exists(objPath))
            {
                return BuildAquaticLotusClusterMesh(meshName);
            }

            try
            {
                List<Vector3> rawVerts = new List<Vector3>();
                List<Vector2> rawUVs = new List<Vector2>();
                List<List<int>> faceVerts = new List<List<int>>();
                List<List<int>> faceUVs = new List<List<int>>();

                string[] lines = File.ReadAllLines(objPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("v "))
                    {
                        string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        rawVerts.Add(new Vector3(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                                                 float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                                                 float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (line.StartsWith("vt "))
                    {
                        string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        rawUVs.Add(new Vector2(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                                               float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (line.StartsWith("f "))
                    {
                        string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        List<int> fv = new List<int>();
                        List<int> ft = new List<int>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string[] sub = parts[i].Split('/');
                            fv.Add(int.Parse(sub[0]) - 1);
                            ft.Add(int.Parse(sub[1]) - 1);
                        }
                        faceVerts.Add(fv);
                        faceUVs.Add(ft);
                    }
                }

                // 🪷 [수생 연꽃 군락 정밀 추출]
                // 물속 깊은 줄기(vy < 0.25f)가 포함된 수직 줄기 폴리곤을 배제하고, 수면 연잎과 분홍 꽃봉오리만 정밀 추출
                // 중심 (c0x = 0.73f, c0z = 0.20f) 기준 반경 1.55m 내 수면 위 면 선택
                float c0x = 0.73f, c0z = 0.20f;
                List<int> selFaceIndices = new List<int>();
                for (int fi = 0; fi < faceVerts.Count; fi++)
                {
                    var fv = faceVerts[fi];
                    bool hasDeepVert = false;
                    float cx = 0f, cy = 0f, cz = 0f;
                    for (int i = 0; i < fv.Count; i++)
                    {
                        float vy = rawVerts[fv[i]].y;
                        if (vy < 0.25f) { hasDeepVert = true; break; }
                        cx += rawVerts[fv[i]].x;
                        cy += vy;
                        cz += rawVerts[fv[i]].z;
                    }
                    if (hasDeepVert) continue; // 물속 깊이 연결된 수직 줄기 정점 배제

                    cx /= fv.Count; cy /= fv.Count; cz /= fv.Count;
                    float dx = cx - c0x, dz = cz - c0z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < 1.55f)
                    {
                        selFaceIndices.Add(fi);
                    }
                }

                if (selFaceIndices.Count == 0) return BuildAquaticLotusClusterMesh(meshName);

                // 최저점 Y 계산 (수면/지면 Y=0 바닥 정렬)
                float minY = 999f;
                foreach (int fi in selFaceIndices)
                {
                    foreach (int vi in faceVerts[fi])
                    {
                        if (rawVerts[vi].y < minY) minY = rawVerts[vi].y;
                    }
                }

                Dictionary<string, int> vMap = new Dictionary<string, int>();
                List<Vector3> newVerts = new List<Vector3>();
                List<Vector2> newUVs = new List<Vector2>();
                List<int> newTris = new List<int>();

                // 연꽃 군락 적정 스케일 (가로 1.6m x 폭 1.4m, 높이 0.50m 내외의 수생 군락)
                float scaleXZ = 0.55f;
                float scaleY = 1.0f;

                foreach (int fi in selFaceIndices)
                {
                    var fv = faceVerts[fi];
                    var ft = faceUVs[fi];
                    List<int> poly = new List<int>();
                    for (int i = 0; i < fv.Count; i++)
                    {
                        int vi = fv[i];
                        int ti = ft[i];
                        string key = vi + "_" + ti;
                        if (!vMap.TryGetValue(key, out int idx))
                        {
                            idx = newVerts.Count;
                            vMap[key] = idx;
                            Vector3 orig = rawVerts[vi];
                            Vector2 uv = rawUVs[ti];

                            float vx = (orig.x - c0x) * scaleXZ;
                            float vz = (orig.z - c0z) * scaleXZ;
                            float vy = (orig.y - minY) * scaleY;

                            // 꽃봉오리(UV u>0.82, v>0.65)는 연잎 위로 우아하게 솟아오르도록 보정 (+0.12m)
                            if (uv.x > 0.82f && uv.y > 0.65f)
                            {
                                vy += 0.12f;
                            }

                            newVerts.Add(new Vector3(vx, vy, vz));
                            newUVs.Add(uv);
                        }
                        poly.Add(idx);
                    }

                    for (int i = 1; i < poly.Count - 1; i++)
                    {
                        newTris.Add(poly[0]);
                        newTris.Add(poly[i]);
                        newTris.Add(poly[i + 1]);
                    }
                }

                Mesh m = new Mesh();
                m.name = meshName;
                m.SetVertices(newVerts);
                m.SetUVs(0, newUVs);
                m.SetTriangles(newTris, 0);
                m.RecalculateBounds();
                m.RecalculateNormals();
                ApplyDomeNormalsAndRootAO(m, 0.12f);
                return m;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MasterVegetationBuilder] Shrub_Lotus_A.obj 로드 실패, 절차적 메쉬로 폴백: {ex.Message}");
                return BuildAquaticLotusClusterMesh(meshName);
            }
        }

        private static Mesh BuildAquaticLotusClusterMesh(string meshName)
        {
            // 폴백용 절차적 카드 메쉬
            Mesh padCard = CreateArchingCardMesh(
                new Vector2(0.732f, 0.328f),
                new Vector2(0.732f, 0.558f),
                0.115f,
                0.36f, 0.36f, 0.05f);

            Mesh flowerStalkCard = CreateArchingCardMesh(
                new Vector2(0.990f, 0.630f),
                new Vector2(0.915f, 0.855f),
                0.062f,
                0.68f, 0.32f, 0.06f);

            int padCount = 6;
            int stalkCount = 6;
            CombineInstance[] combines = new CombineInstance[padCount + stalkCount];
            int idx = 0;

            for (int i = 0; i < padCount; i++)
            {
                float yaw = (i / (float)padCount) * 360f + ((i % 2 == 0) ? 8f : -8f);
                float pitch = 72f + (i % 3) * 4f;
                float sc = 0.92f + (i % 3) * 0.08f;
                combines[idx++] = new CombineInstance
                {
                    mesh = padCard,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.03f, 0f), Quaternion.Euler(pitch, yaw, 0f), Vector3.one * sc)
                };
            }

            for (int i = 0; i < stalkCount; i++)
            {
                float angleRad = (i / (float)stalkCount) * Mathf.PI * 2f;
                float r = (i < 2) ? 0.02f : 0.06f;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * r, 0f, Mathf.Sin(angleRad) * r);
                float yaw = (i / (float)stalkCount) * 360f + 30f;
                float pitch = (i < 2) ? -4f : 8f;
                float sc = (i < 2) ? 1.05f : 0.90f;
                combines[idx++] = new CombineInstance
                {
                    mesh = flowerStalkCard,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * sc)
                };
            }

            Mesh lotusMesh = new Mesh();
            lotusMesh.name = meshName;
            lotusMesh.CombineMeshes(combines, true, true);
            ApplyDomeNormalsAndRootAO(lotusMesh, 0.14f);
            return lotusMesh;
        }

        public static void BuildType3_TallReedsAndSeedheads()
        {
            List<Mesh> seedheadCards = TotalWarProceduralFoliageDemo.ExtractCardsFromObj("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_Seedhead_A.obj");
            List<Mesh> tallBlades = TotalWarProceduralFoliageDemo.ExtractCardsFromObj("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj");

            if (seedheadCards != null && seedheadCards.Count > 0)
            {
                int coreReeds = 10;
                int baseBlades = (tallBlades != null && tallBlades.Count > 0) ? 10 : 0;
                CombineInstance[] combines = new CombineInstance[coreReeds + baseBlades];
                int idx = 0;

                for (int i = 0; i < coreReeds; i++)
                {
                    float angleRad = (i / (float)coreReeds) * Mathf.PI * 2f;
                    float r = (i < 4) ? 0.018f : 0.048f;
                    Vector3 pos = new Vector3(Mathf.Cos(angleRad) * r, 0f, Mathf.Sin(angleRad) * r);
                    float yaw = -angleRad * Mathf.Rad2Deg + 90f + ((i % 2 == 0) ? 12f : -12f);
                    float pitch = (i < 4) ? (6f + (i % 2) * 3f) : (14f + (i % 3) * 3f);

                    Mesh card = seedheadCards[i % seedheadCards.Count];
                    float cardH = Mathf.Max(0.1f, card.bounds.size.y);
                    float targetH = (i < 4) ? 1.08f : 0.92f;
                    float scaleFactor = targetH / cardH;

                    combines[idx++] = new CombineInstance
                    {
                        mesh = card,
                        transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), new Vector3(scaleFactor * 0.85f, scaleFactor, scaleFactor * 0.85f))
                    };
                }

                for (int i = 0; i < baseBlades; i++)
                {
                    float angleRad = ((i + 0.5f) / baseBlades) * Mathf.PI * 2f;
                    float r = 0.065f;
                    Vector3 pos = new Vector3(Mathf.Cos(angleRad) * r, 0f, Mathf.Sin(angleRad) * r);
                    float yaw = -angleRad * Mathf.Rad2Deg + 90f;
                    float pitch = 24f + (i % 3) * 4f;

                    Mesh card = tallBlades[(tallBlades.Count - 1) - (i % Mathf.Min(4, tallBlades.Count))];
                    float cardH = Mathf.Max(0.1f, card.bounds.size.y);
                    float scaleFactor = 0.58f / cardH;

                    combines[idx++] = new CombineInstance
                    {
                        mesh = card,
                        transform = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * scaleFactor)
                    };
                }

                Mesh plumeReedMesh = new Mesh();
                plumeReedMesh.name = "TotalWar_TallReed3D_Plume_Mesh";
                plumeReedMesh.CombineMeshes(combines, true, true);
                ApplyDomeNormalsAndRootAO(plumeReedMesh, 0.10f);

                // 마스터 메쉬 1개만 디스크에 저장
                SaveMeshAndPrefab(plumeReedMesh, "TotalWar_TallReed3D_Plume_Mesh", null, null);

                // 사계절 깃털 갈대 6종 + 수전 벼/기장 이삭 갈대 2종 (동일 UV 아틀라스, 마스터 메쉬 공유)
                var reedVariants = new (string key, string tex, string norm)[]
                {
                    ("Spring",        "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Summer",        "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Autumn",        "shrub_grassclump_au_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Winter",        "shrub_grassclump_wi_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Harvest",       "shrub_grassclump_ha_base_colour.png", "shrub_grassclump_normal.png"),
                    ("Arid",          "shrub_grassclump_base_colour.png",    "shrub_grassclump_normal.png"),
                    ("RiceEar_Green", "gen_rice_paddy0_base_colour.png",     "gen_rice_paddy0_normal.png"),
                    ("RiceEar_Gold",  "gen_rice_paddy0_au_base_colour.png",  "gen_rice_paddy0_normal.png")
                };

                foreach (var rv in reedVariants)
                {
                    Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_TallReed3D_{rv.key}_Mat", rv.tex, rv.norm, 0.22f);
                    // 메쉬 복제 없이 마스터 메쉬를 공유하여 프리팹 생성!
                    SaveMeshAndPrefab(plumeReedMesh, "TotalWar_TallReed3D_Plume_Mesh", mat, $"TotalWar_TallReed3D_{rv.key}_Prefab");
                }
            }

            // 2) 온대 Row 2 직립 이삭줄기 6장 별 모양 교차 갈대 (0.92m)
            Mesh spikeStrip = CreateSinglePentagonStripMesh(
                0.004f, 0.250f, 0.500f, 0.750f, 0.996f,
                0.503f, 0.640f, 0.795f, 0.640f,
                0.88f, 0.92f, 0f, 0f);

            int spikePlanes = 6;
            CombineInstance[] spikeCombs = new CombineInstance[spikePlanes];
            for (int s = 0; s < spikePlanes; s++)
            {
                float yaw = (s / (float)spikePlanes) * 180f + ((s % 2 == 1) ? 180f : 0f);
                float dist = (s % 2 == 0) ? 0.02f : 0.05f;
                float rad = s * 137.5f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
                float sc = Mathf.Lerp(1.04f, 0.92f, (float)s / spikePlanes);
                spikeCombs[s] = new CombineInstance
                {
                    mesh = spikeStrip,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * sc)
                };
            }

            Mesh spikeReedMesh = new Mesh();
            spikeReedMesh.name = "TotalWar_TallReed3D_Spike_Mesh";
            spikeReedMesh.CombineMeshes(spikeCombs, true, true);
            ApplyDomeNormalsAndRootAO(spikeReedMesh, 0.10f);

            Material temSpikeMat = GetOrCreateRadialFoliageMaterial("TotalWar_TallReed3D_Spike_Mat", "tem_grass_sp_base_colour.png", "tem_grass_normal.png", 0.18f);
            SaveMeshAndPrefab(spikeReedMesh, "TotalWar_TallReed3D_Spike_Mesh", temSpikeMat, "TotalWar_TallReed3D_Spike_Prefab");

            // 3) 특수 키 큰 갈대/곡식이삭 4종 (남만 물억새, 황금 보리이삭, 건조 보리이삭, 대왕 코끼리풀)
            var customReeds = new (string key, string tex, string norm, Vector2 uvBase, Vector2 uvTip, float uvHalfW, float h, float w)[]
            {
                ("RiverPlume",     "sbt_grass_base_colour.png",          "sbt_grass0_normal.png",        new Vector2(0.035f, 0.485f), new Vector2(0.185f, 0.890f), 0.095f, 1.02f, 0.36f),
                ("BarleyEar_Gold", "sbt_agri1_base_colour.png",          "gen_rice_paddy0_normal.png",   new Vector2(0.265f, 0.720f), new Vector2(0.145f, 0.975f), 0.130f, 0.88f, 0.38f),
                ("BarleyEar_Dry",  "sbt_agri1_base_colour.png",          "gen_rice_paddy0_normal.png",   new Vector2(0.765f, 0.720f), new Vector2(0.645f, 0.975f), 0.130f, 0.86f, 0.38f),
                ("ElephantTall",   "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", new Vector2(0.595f, 0.080f), new Vector2(0.585f, 0.980f), 0.175f, 1.15f, 0.48f)
            };

            foreach (var cr in customReeds)
            {
                Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_TallReed3D_{cr.key}_Mat", cr.tex, cr.norm, 0.20f);
                Mesh mesh = BuildCustomAxisRadialFan3DMesh(
                    $"TotalWar_TallReed3D_{cr.key}_Mesh",
                    cr.uvBase, cr.uvTip, cr.uvHalfW,
                    cr.h, cr.w, 5, 6, 6, 0.08f);
                SaveMeshAndPrefab(mesh, $"TotalWar_TallReed3D_{cr.key}_Mesh", mat, $"TotalWar_TallReed3D_{cr.key}_Prefab");
            }

            // 4) 🪷 [원작 1:1 정품 3D 연꽃] 강남 수생 연꽃 군락 2종 (여름/가을, Shrub_Lotus_A.obj 기반)
            Mesh authenticLotusMesh = BuildAuthenticLotusClusterMesh("TotalWar_LotusCluster_Mesh");
            SaveMeshAndPrefab(authenticLotusMesh, "TotalWar_LotusCluster_Mesh", null, null);

            var lotusSeasons = new (string key, string tex)[]
            {
                ("Summer", "shrubs_ferns_su_base_colour.png"),
                ("Autumn", "shrubs_ferns_au_base_colour.png")
            };
            foreach (var ls in lotusSeasons)
            {
                Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_LotusCluster_{ls.key}_Mat", ls.tex, "shrubs_ferns_normal.png", 0.22f);
                SaveMeshAndPrefab(authenticLotusMesh, "TotalWar_LotusCluster_Mesh", mat, $"TotalWar_LotusCluster_{ls.key}_Prefab");
            }
        }

        // =========================================================================================
        // 5. 유형 ④ [음지 고사리 / 열대 양치류 / 바닥덮개]: 지면 밀착 부채꼴 (18종 전수)
        // =========================================================================================
        private static Mesh CreateArchingFernFrondMesh(Vector2 uvBase, Vector2 uvTip, float uvHalfWidth, float length = 0.52f, float width = 0.22f)
        {
            int segs = 5;
            int vertCount = (segs + 1) * 2;
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[segs * 6];

            Vector2 axis = uvTip - uvBase;
            Vector2 perp = new Vector2(-axis.y, axis.x).normalized;

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float z = t * length;
                float y = Mathf.Sin(t * Mathf.PI * 0.82f) * (length * 0.34f) + (t * 0.04f);

                float widthProfile = Mathf.Lerp(0.75f, 1.0f, Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI * 0.5f));
                if (t > 0.85f) widthProfile = Mathf.Lerp(1.0f, 0.65f, (t - 0.85f) / 0.15f);

                float halfW3D = (width * 0.5f) * widthProfile;
                float halfWUV = uvHalfWidth * widthProfile;

                Vector2 uvCenter = Vector2.Lerp(uvBase, uvTip, t);
                Vector2 uvLeft = uvCenter - perp * halfWUV;
                Vector2 uvRight = uvCenter + perp * halfWUV;

                uvLeft.x = Mathf.Clamp(uvLeft.x, 0.004f, 0.996f);
                uvLeft.y = Mathf.Clamp(uvLeft.y, 0.004f, 0.996f);
                uvRight.x = Mathf.Clamp(uvRight.x, 0.004f, 0.996f);
                uvRight.y = Mathf.Clamp(uvRight.y, 0.004f, 0.996f);

                verts[i * 2 + 0] = new Vector3(-halfW3D, y, z);
                verts[i * 2 + 1] = new Vector3(+halfW3D, y, z);

                uvs[i * 2 + 0] = uvLeft;
                uvs[i * 2 + 1] = uvRight;
            }

            int tIdx = 0;
            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 2;
                int b1 = i * 2 + 1;
                int t0 = (i + 1) * 2;
                int t1 = (i + 1) * 2 + 1;

                tris[tIdx++] = b0; tris[tIdx++] = t0; tris[tIdx++] = b1;
                tris[tIdx++] = b1; tris[tIdx++] = t0; tris[tIdx++] = t1;
            }

            Mesh m = new Mesh();
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            m.RecalculateNormals();
            return m;
        }

        private static Mesh BuildRadialFernRosetteMesh(string meshName, Mesh frondMesh, int innerCount, int outerCount)
        {
            int total = innerCount + outerCount;
            CombineInstance[] combines = new CombineInstance[total];
            int idx = 0;

            for (int i = 0; i < innerCount; i++)
            {
                float yaw = (i / (float)innerCount) * 360f + ((i % 2 == 0) ? 6f : -6f);
                float pitch = -12f + (i % 3) * 3f;
                float sc = 0.88f + (i % 2) * 0.08f;

                combines[idx++] = new CombineInstance
                {
                    mesh = frondMesh,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.01f, 0f), Quaternion.Euler(pitch, yaw, 0f), Vector3.one * sc)
                };
            }

            for (int i = 0; i < outerCount; i++)
            {
                float yaw = ((i + 0.5f) / outerCount) * 360f + ((i % 2 == 0) ? -5f : 5f);
                float pitch = 6f + (i % 3) * 3f;
                float sc = 1.02f + (i % 3) * 0.05f;

                combines[idx++] = new CombineInstance
                {
                    mesh = frondMesh,
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(pitch, yaw, 0f), Vector3.one * sc)
                };
            }

            Mesh rosette = new Mesh();
            rosette.name = meshName;
            rosette.CombineMeshes(combines, true, true);
            ApplyDomeNormalsAndRootAO(rosette, 0.12f);
            return rosette;
        }

        public static void BuildType4_ForestFerns()
        {
            // 1) 검고사리(Sword Fern): 좌측 수직/사선 외줄기 잎 (u=0.198, v=0.115 -> u=0.028, v=0.805)
            Mesh swordFrond = CreateArchingFernFrondMesh(
                new Vector2(0.198f, 0.115f),
                new Vector2(0.028f, 0.805f),
                0.052f,
                0.48f, 0.18f);
            Mesh swordRosette = BuildRadialFernRosetteMesh("TotalWar_ForestFern_Sword_Mesh", swordFrond, 6, 9);

            // 2) 우드고사리(Wood/Fishbone Fern): 중앙 대각선 겹잎 (u=0.565, v=0.030 -> u=0.062, v=0.900)
            Mesh woodFrond = CreateArchingFernFrondMesh(
                new Vector2(0.565f, 0.030f),
                new Vector2(0.062f, 0.900f),
                0.115f,
                0.52f, 0.30f);
            Mesh woodRosette = BuildRadialFernRosetteMesh("TotalWar_ForestFern_Wood_Mesh", woodFrond, 5, 7);

            // 3) 왕고사리(Royal Broad Fern): 상단 대각선 넓은 양치잎 (u=0.950, v=0.640 -> u=0.290, v=0.940)
            Mesh royalFrond = CreateArchingFernFrondMesh(
                new Vector2(0.950f, 0.640f),
                new Vector2(0.290f, 0.940f),
                0.135f,
                0.54f, 0.32f);
            Mesh royalRosette = BuildRadialFernRosetteMesh("TotalWar_ForestFern_Royal_Mesh", royalFrond, 5, 7);

            var fernSeasons = new (string key, string tex)[]
            {
                ("Spring", "shrubs_ferns_base_colour.png"), // 원작 고유 1:1 봄/기본 텍스처 (shrubs_ferns_base_colour)
                ("Summer", "shrubs_ferns_su_base_colour.png"),
                ("Autumn", "shrubs_ferns_au_base_colour.png"),
                ("Winter", "shrubs_ferns_wi_base_colour.png")
            };

            // 3대 고사리 마스터 메쉬 디스크 저장
            SaveMeshAndPrefab(swordRosette, "TotalWar_ForestFern_Sword_Mesh", null, null);
            SaveMeshAndPrefab(royalRosette, "TotalWar_ForestFern_Royal_Mesh", null, null);
            SaveMeshAndPrefab(woodRosette, "TotalWar_ForestFern_Wood_Mesh", null, null);

            foreach (var fs in fernSeasons)
            {
                Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_ForestFern_{fs.key}_Mat", fs.tex, "shrubs_ferns_normal.png", 0.24f);

                SaveMeshAndPrefab(swordRosette, "TotalWar_ForestFern_Sword_Mesh", mat, $"TotalWar_ForestFern_Sword_{fs.key}_Prefab");
                SaveMeshAndPrefab(royalRosette, "TotalWar_ForestFern_Royal_Mesh", mat, $"TotalWar_ForestFern_Royal_{fs.key}_Prefab");
                SaveMeshAndPrefab(woodRosette, "TotalWar_ForestFern_Wood_Mesh", mat, $"TotalWar_ForestFern_Wood_{fs.key}_Prefab");
            }

            // 4) 특수 양치류 및 바닥 화초 6종 (공작고사리 여름/가을, 온대/아열대 3갈래 고사리, 열대 야자고사리, 열대 파초일엽)
            var specialFerns = new (string key, string tex, string norm, Vector2 uvBase, Vector2 uvTip, float uvHalfW, float len, float wid)[]
            {
                ("Maidenhair_Summer",    "shrubs_ferns_su_base_colour.png",      "shrubs_ferns_normal.png",       new Vector2(0.848f, 0.045f), new Vector2(0.750f, 0.440f), 0.115f, 0.44f, 0.28f),
                ("Maidenhair_Autumn",    "shrubs_ferns_au_base_colour.png",      "shrubs_ferns_normal.png",       new Vector2(0.848f, 0.045f), new Vector2(0.750f, 0.440f), 0.115f, 0.44f, 0.28f),
                ("ForestTriple_Spring",  "tem_forest_grass_sp_base_colour.png",  "tem_forest_grass_normal.png",   new Vector2(0.515f, 0.500f), new Vector2(0.505f, 0.795f), 0.115f, 0.48f, 0.28f),
                ("ForestTriple_Summer",  "sbt_forest_grass0_base_colour.png",    "sbt_forest_grass0_normal.png",  new Vector2(0.515f, 0.500f), new Vector2(0.505f, 0.795f), 0.115f, 0.50f, 0.30f),
                ("TropicalPalm_Summer",  "3k_tropical_shrubs_base_colour.png",   "3k_tropical_shrubs_normal.png", new Vector2(0.055f, 0.580f), new Vector2(0.245f, 0.965f), 0.085f, 0.56f, 0.28f),
                ("TropicalStrap_Summer", "tro_forest_grass_base_colour.png",     "tro_forest_grass_normal.png",   new Vector2(0.015f, 0.240f), new Vector2(0.325f, 0.355f), 0.120f, 0.52f, 0.30f)
            };

            foreach (var sf in specialFerns)
            {
                Material mat = GetOrCreateRadialFoliageMaterial($"TotalWar_ForestFern_{sf.key}_Mat", sf.tex, sf.norm, 0.22f);
                Mesh frond = CreateArchingFernFrondMesh(sf.uvBase, sf.uvTip, sf.uvHalfW, sf.len, sf.wid);
                Mesh rosette = BuildRadialFernRosetteMesh($"TotalWar_ForestFern_{sf.key}_Mesh", frond, 5, 8);
                SaveMeshAndPrefab(rosette, $"TotalWar_ForestFern_{sf.key}_Mesh", mat, $"TotalWar_ForestFern_{sf.key}_Prefab");
            }
        }

        // =========================================================================================
        // 6. 수목(Trees) 46종 원작 모델 기후별 프리팹 조립 및 TacticalForestZone 연동
        // =========================================================================================
        public static int BuildAll46TreePrefabsAndRefreshForests()
        {
            string treesSourceDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees";
            if (!Directory.Exists(treesSourceDir)) return 0;

            Directory.CreateDirectory(TreePrefabDir);

            GameObject helperGo = new GameObject("__TempForestMaterialHelper__");
            helperGo.hideFlags = HideFlags.HideAndDontSave;
            TacticalForestZone helperZone = helperGo.AddComponent<TacticalForestZone>();
            helperZone.currentSeason = TacticalForestZone.Season.Summer;
            helperZone.enableWinterSnow = false;

            string[] objFiles = Directory.GetFiles(treesSourceDir, "*.obj");
            int builtCount = 0;

            try
            {
                foreach (string objPath in objFiles)
                {
                    string assetPath = objPath.Replace("\\", "/");
                    string baseName = Path.GetFileNameWithoutExtension(assetPath);
                    GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (modelAsset == null) continue;

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    if (instance == null) instance = UnityEngine.Object.Instantiate(modelAsset);
                    instance.name = baseName + "_Prefab";

                    helperZone.ApplyTreeMaterialsPublic(instance, baseName);

                    string outPrefabPath = $"{TreePrefabDir}/{baseName}_Prefab.prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, outPrefabPath);
                    UnityEngine.Object.DestroyImmediate(instance);
                    builtCount++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(helperGo);
            }

            AssetDatabase.SaveAssets();

            var activeZones = UnityEngine.Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
            foreach (var zone in activeZones)
            {
                if (zone == null) continue;
                zone.ClearVegetation();
                zone.SnapToTerrain();
                zone.GenerateVegetation();
                EditorUtility.SetDirty(zone);
            }

            return builtCount;
        }

        // =========================================================================================
        // 7. Game2 전장 터레인 영구 베이크 및 디스크 저장
        // =========================================================================================
        public static void BakeCurrentSceneTerrainFoliage()
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<TotalWarTerrainManager>();
            if (manager != null)
            {
                manager.customGrassList = RadialGrassConfig.GetBiomeFoliagePack(manager.grassBiomeTheme);
                manager.BakeChunkFoliage();
            }
            else
            {
                var renderer = UnityEngine.Object.FindAnyObjectByType<TacticalGrassRenderer>();
                if (renderer != null)
                {
                    renderer.SyncPrototypesFromConfigs(RadialGrassConfig.GetBiomeFoliagePack(GrassBiomeTheme.CentralPlains20));
                    renderer.BakeFoliageData();
                    renderer.SaveFoliageDataToDisk();
                }
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }
        }
    }
}
