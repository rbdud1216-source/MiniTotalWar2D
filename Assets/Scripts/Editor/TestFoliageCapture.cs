using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🧪 [삼탈워 식생 전수 렌더링 캡처 실험 엔진]
    /// - 유니티 에디터 내부에서 실제 카메라와 라이트를 띄워 꽃, 융단, 이삭 등의 모델을 1:1로 렌더링합니다.
    /// - 지표면(Y=0) 기준면 디스크와 함께 1024x1024 고화질 PNG 이미지로 캡처하여 저장합니다.
    /// - 머티리얼, 텍스처, 알파 클리핑, 지면 밀착 여부를 시각적 실물로 전수 검증합니다.
    /// </summary>
    public static class TestFoliageCapture
    {
        private const string ARTIFACT_DIR = @"C:\Users\PC\.gemini\antigravity-ide\brain\27c3bc13-c555-4e54-8fe3-64dcb683a499";
        private const string PROJECT_CAPTURE_DIR = "Assets/Settings/Vegetation/Captures";

        [MenuItem("TotalWar/🧪 식생 전수 렌더링 캡처 실험 실행", false, 2)]
        public static void RunCaptureTestMenu()
        {
            ExecuteFullCaptureTest();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ExecuteFullCaptureTest();
        }

        public static void ExecuteFullCaptureTest()
        {
            Debug.Log("<color=cyan><b>[TestFoliageCapture] 🌱 삼탈워 식생 전수 렌더링 캡처 실험 시작...</b></color>");

            Directory.CreateDirectory(PROJECT_CAPTURE_DIR);
            if (!Directory.Exists(ARTIFACT_DIR)) Directory.CreateDirectory(ARTIFACT_DIR);

            // 테스트 대상 식생 목록 (주요 꽃, 관목, 융단, 이삭, 고사리 등)
            string[] testModels = new string[]
            {
                "Shrub_Flower_A.obj",
                "Shrub_Flower_B.obj",
                "Shrub_Convallaria_A.obj",
                "Shrub_Rhododendron_A.obj",
                "Shrub_Magnolia_G.obj",
                "Shrub_Lotus_A.obj",
                "Shrub_Grassclump_Tall_Seedhead_A.obj",
                "Shrub_Flat_A.obj",
                "Fern_Royal_A.obj"
            };

            // 가상 스튜디오 환경 구축
            GameObject studioRoot = new GameObject("__FoliageCaptureStudio__");
            studioRoot.hideFlags = HideFlags.DontSave;

            Camera cam = null;
            Light dirLight = null;
            RenderTexture rt = null;

            try
            {
                // 1. 카메라 생성
                GameObject camObj = new GameObject("StudioCamera");
                camObj.transform.SetParent(studioRoot.transform);
                cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.18f, 0.20f, 0.23f, 1.0f); // 짙은 스튜디오 그레이
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 200f;
                cam.fieldOfView = 40f;

                // 2. 조명 생성 (태양광 시뮬레이션)
                GameObject lightObj = new GameObject("StudioLight");
                lightObj.transform.SetParent(studioRoot.transform);
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(1.0f, 0.96f, 0.88f);
                dirLight.intensity = 1.3f;
                lightObj.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

                // 보조 역광 (Rim Light)
                GameObject rimObj = new GameObject("StudioRimLight");
                rimObj.transform.SetParent(studioRoot.transform);
                Light rimLight = rimObj.AddComponent<Light>();
                rimLight.type = LightType.Directional;
                rimLight.color = new Color(0.6f, 0.75f, 0.9f);
                rimLight.intensity = 0.45f;
                rimObj.transform.rotation = Quaternion.Euler(-30f, 145f, 0f);

                // 3. 지표면 기준면 디스크 메쉬 생성 (Y=0 지표면 확인용)
                GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                groundObj.transform.SetParent(studioRoot.transform);
                groundObj.name = "GroundPlane";
                groundObj.transform.position = new Vector3(0, -0.015f, 0); // 지표면 바로 아래
                groundObj.transform.localScale = new Vector3(5.0f, 0.01f, 5.0f);
                var groundRend = groundObj.GetComponent<Renderer>();
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Lit");
                Material groundMat = new Material(unlitShader);
                groundMat.color = new Color(0.22f, 0.38f, 0.18f, 1.0f); // 자연스러운 짙은 초록 잔디색
                groundRend.sharedMaterial = groundMat;

                // 4. 렌더 텍스처 (1024x1024 고해상도)
                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;
                cam.targetTexture = rt;

                // 리포트 작성용 스트링빌더
                var reportLines = new List<string>();
                reportLines.Add("# 🧪 삼탈워 식생 전수 렌더링 캡처 실험 리포트");
                reportLines.Add($"실행 일시: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                reportLines.Add("| 번호 | 모델 파일명 | 카테고리 | 크기(폭x높이) | 지면 띄움(Offset) | 머티리얼 / 텍스처 | 결과 이미지 |");
                reportLines.Add("| :---: | :--- | :--- | :---: | :---: | :--- | :--- |");

                int index = 0;
                foreach (string modelName in testModels)
                {
                    index++;
                    string modelPath = FindModelPath(modelName);
                    if (string.IsNullOrEmpty(modelPath))
                    {
                        Debug.LogWarning($"[TestFoliageCapture] 모델을 찾을 수 없음: {modelName}");
                        continue;
                    }

                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(modelPath);
                    if (mesh == null)
                    {
                        Debug.LogWarning($"[TestFoliageCapture] 메쉬 로드 실패: {modelPath}");
                        continue;
                    }

                    // 카탈로그 정보 또는 추정 스펙
                    float width = mesh.bounds.size.x;
                    float height = mesh.bounds.size.y;
                    float minY = mesh.bounds.min.y;
                    float offset = -minY; // 기본 자동 밀착 오프셋

                    // 특정 모델 특수 스케일 및 오프셋 조정
                    float renderScale = 1.0f;
                    if (modelName.Contains("Magnolia"))
                    {
                        renderScale = 0.5f; // 거대한 목련(13m)을 스튜디오 화각에 맞춤
                    }
                    else if (modelName.Contains("Lotus"))
                    {
                        // 연꽃은 수심 4.5m 특수 피벗이므로 Y=0에 줄기 밑동이 닿도록 보정
                        offset = 4.47f;
                    }

                    // 식생 인스턴스 오브젝트 생성
                    GameObject plantInst = new GameObject("Plant_" + modelName);
                    plantInst.transform.SetParent(studioRoot.transform);
                    var mf = plantInst.AddComponent<MeshFilter>();
                    var mr = plantInst.AddComponent<MeshRenderer>();
                    mf.sharedMesh = mesh;

                    // 머티리얼 바인딩
                    Material mat = ResolveMaterialForModel(modelName);
                    mr.sharedMaterial = mat;

                    // 위치 및 스케일 설정
                    Vector3 plantPos = new Vector3(0, offset * renderScale, 0);
                    plantInst.transform.position = plantPos;
                    plantInst.transform.localScale = Vector3.one * renderScale;

                    // 카메라 위치 자동 프레이밍
                    Bounds b = mesh.bounds;
                    float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z) * renderScale;
                    float camDist = Mathf.Clamp(maxDim * 1.8f, 1.8f, 15f);
                    Vector3 center = (b.center * renderScale) + plantPos;

                    camObj.transform.position = center + new Vector3(0, maxDim * 0.25f, -camDist);
                    camObj.transform.LookAt(center);

                    // 렌더링 실행
                    cam.Render();

                    // Texture2D로 복사 및 PNG 인코딩
                    RenderTexture.active = rt;
                    Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    byte[] pngBytes = tex.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(tex);

                    string cleanName = Path.GetFileNameWithoutExtension(modelName);
                    string fileName = $"foliage_test_{index:D2}_{cleanName}.png";
                    string projSavePath = Path.Combine(PROJECT_CAPTURE_DIR, fileName).Replace("\\", "/");
                    string artifactSavePath = Path.Combine(ARTIFACT_DIR, fileName);

                    File.WriteAllBytes(projSavePath, pngBytes);
                    File.WriteAllBytes(artifactSavePath, pngBytes);

                    Debug.Log($"<color=green>[TestFoliageCapture] ✅ #{index} '{cleanName}' 캡처 완료 -> {artifactSavePath}</color>");

                    string matName = mat != null ? mat.name : "None";
                    string texName = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "None";
                    string cat = CategorizeModel(modelName);

                    reportLines.Add($"| {index} | `{modelName}` | {cat} | {width:F2}m x {height:F2}m | {offset:+#0.00;-#0.00;0.00}m | `{matName}`<br>({texName}) | ![{cleanName}]({artifactSavePath.Replace("\\", "/")}) |");

                    // 식생 인스턴스 정리
                    UnityEngine.Object.DestroyImmediate(plantInst);
                }

                // 리포트 MD 파일 저장
                string reportPath = Path.Combine(ARTIFACT_DIR, "foliage_capture_experiment_report.md");
                File.WriteAllLines(reportPath, reportLines);
                Debug.Log($"<color=cyan><b>[TestFoliageCapture] 🏁 식생 전수 렌더링 실험 완료! 리포트: {reportPath}</b></color>");

                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestFoliageCapture] 렌더링 캡처 실험 중 오류 발생: {ex}");
            }
            finally
            {
                if (rt != null)
                {
                    cam.targetTexture = null;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (studioRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(studioRoot);
                }
            }
        }

        private static string FindModelPath(string modelName)
        {
            string shrubPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/" + modelName;
            if (File.Exists(shrubPath)) return shrubPath;

            string[] detailFolders = new[] { "Climate_Temperate", "Climate_Arid", "Climate_Cold", "Climate_Subtropical" };
            foreach (var fold in detailFolders)
            {
                string p = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/{fold}/{modelName}";
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static Material ResolveMaterialForModel(string modelName)
        {
            string baseName = Path.GetFileNameWithoutExtension(modelName);
            string lower = modelName.ToLower();

            // 목표 대상 텍스처 키워드 결정
            string targetTexKeyword = "shrub_grassclump_su";
            if (lower.Contains("flower") || lower.Contains("convallaria"))
                targetTexKeyword = "shrub_flower_a_billboard";
            else if (lower.Contains("rhododendron"))
                targetTexKeyword = "shrub_rhododendron";
            else if (lower.Contains("magnolia"))
                targetTexKeyword = "shrub_magnolia";
            else if (lower.Contains("lotus") || lower.Contains("tropical"))
                targetTexKeyword = "3k_tropical_shrubs";
            else if (lower.Contains("fern"))
                targetTexKeyword = "shrubs_ferns";
            else if (lower.Contains("seedhead"))
                targetTexKeyword = "shrub_grassclump";
            else if (lower.Contains("flat"))
                targetTexKeyword = "shrub_grassclump";

            // 1. RadialMaterials 폴더에서 모델명 + 목표 텍스처가 모두 포함된 머티리얼 1순위 탐색
            string[] guids = AssetDatabase.FindAssets(baseName + " t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
            if (guids != null && guids.Length > 0)
            {
                // 1순위: 목표 텍스처 키워드를 포함하는 머티리얼 탐색
                foreach (var g in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.ToLower().Contains(targetTexKeyword.ToLower()))
                    {
                        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                        if (m != null) return m;
                    }
                }

                // 2순위: 첫 번째 일치 머티리얼
                string p0 = AssetDatabase.GUIDToAssetPath(guids[0]);
                var m0 = AssetDatabase.LoadAssetAtPath<Material>(p0);
                if (m0 != null) return m0;
            }

            // 2. 텍스처명 기반 머티리얼 탐색
            string[] texGuids = AssetDatabase.FindAssets(targetTexKeyword + " t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
            if (texGuids != null && texGuids.Length > 0)
            {
                string p = AssetDatabase.GUIDToAssetPath(texGuids[0]);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) return m;
            }

            // 3. 없으면 텍스처를 직접 로드하여 URP 식생 셰이더 머티리얼 즉석 생성
            Shader s = Shader.Find("Universal Render Pipeline/TotalWarRadialGrassLOD");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");

            Material dynMat = new Material(s);
            dynMat.hideFlags = HideFlags.DontSave;
            dynMat.SetFloat("_Cull", 0); // 양면 렌더링
            dynMat.SetFloat("_Cutoff", 0.3f); // 알파 컷오프

            string pngPath = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/{targetTexKeyword}_base_colour.png";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (albedo != null)
            {
                if (dynMat.HasProperty("_BaseMap")) dynMat.SetTexture("_BaseMap", albedo);
                if (dynMat.HasProperty("_MainTex")) dynMat.SetTexture("_MainTex", albedo);
            }

            return dynMat;
        }

        private static string CategorizeModel(string modelName)
        {
            string lower = modelName.ToLower();
            if (lower.Contains("flower")) return "🌼 야생화";
            if (lower.Contains("convallaria")) return "🌼 은방울꽃";
            if (lower.Contains("rhododendron")) return "🌸 철쭉/진달래";
            if (lower.Contains("magnolia")) return "🌳 목련 꽃나무";
            if (lower.Contains("lotus")) return "🪷 수생 연꽃";
            if (lower.Contains("seedhead")) return "🌾 붉은 이삭 풀";
            if (lower.Contains("flat")) return "🌱 대형 지피 융단";
            if (lower.Contains("fern")) return "🌲 고사리";
            return "🌿 기타 식생";
        }
    }
}
