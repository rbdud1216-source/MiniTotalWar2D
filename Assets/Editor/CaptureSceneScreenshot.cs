using UnityEngine;
using UnityEditor;
using System.IO;

public static class CaptureSceneScreenshot
{
    [MenuItem("Tools/TotalWar/부러진 소나무 캡처 (Capture Broken Pine)")]
    public static void CaptureBrokenPine()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        Transform target = null;
        foreach (var z in zones)
        {
            var renderers = z.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r.transform.parent != null && r.transform.parent.name.Contains("Pine_Broken"))
                {
                    target = r.transform.parent;
                    break;
                }
            }
            if (target != null) break;
        }

        if (target == null) return;

        GameObject camObj = new GameObject("TempCaptureCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 50f;

        Vector3 treePos = target.position;
        // 나무 전체가 보이도록 뒤로 12m, 높이 7m
        camObj.transform.position = treePos + new Vector3(3f, 7f, -12f);
        camObj.transform.LookAt(treePos + Vector3.up * 6.0f);

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObj);

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\e78a3f80-35f1-4ace-88fc-390b6b492ca6\scratch";
        string savePath = Path.Combine(saveDir, "pine_broken_full_view.png");
        File.WriteAllBytes(savePath, bytes);

        Debug.Log($"<color=#00FF88><b>[CaptureSceneScreenshot] ✅ 부러진 소나무 전신 캡처 완료: {savePath}</b></color>");
    }

    [MenuItem("Tools/TotalWar/그루터기 캡처 (Capture Stump)")]
    public static void CaptureStump()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        Transform target = null;
        foreach (var z in zones)
        {
            var renderers = z.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string rName = r.gameObject.name.ToLower();
                string pName = (r.transform.parent != null) ? r.transform.parent.name.ToLower() : "";
                if (rName.Contains("stump") || pName.Contains("stump"))
                {
                    target = r.transform;
                    break;
                }
            }
            if (target != null) break;
        }

        if (target == null)
        {
            Debug.LogWarning("[CaptureSceneScreenshot] Stump를 씬에서 찾을 수 없습니다.");
            return;
        }

        GameObject camObj = new GameObject("TempStumpCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.fieldOfView = 45f;

        Vector3 pos = target.position;
        // 그루터기 정면 및 단면이 선명하게 보이도록 대각선 위 3m, 거리 4m
        camObj.transform.position = pos + new Vector3(2.5f, 2.5f, -3.5f);
        camObj.transform.LookAt(pos + Vector3.up * 0.8f);

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObj);

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\e78a3f80-35f1-4ace-88fc-390b6b492ca6\scratch";
        string savePath = Path.Combine(saveDir, "stump_render_capture.png");
        File.WriteAllBytes(savePath, bytes);

        Debug.Log($"<color=#00FF88><b>[CaptureSceneScreenshot] ✅ 그루터기 렌더링 캡처 완료: {savePath}</b></color>");
    }

    [InitializeOnLoadMethod]
    private static void CheckAutoCapture()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        string flagPath = @"c:\unityProject\MiniTotalWar2D\scratch\trigger_capture.flag";
        if (File.Exists(flagPath))
        {
            try { File.Delete(flagPath); } catch { }
            Debug.Log("[CaptureSceneScreenshot] 🚩 트리거 플래그 감지 -> 캡처 실행");
            CaptureWinterSnowAndCharredTreeScreenshot();
        }
    }

    [MenuItem("Tools/TotalWar/🔥 화재 및 연기 렌더링 캡처 (Capture Fire & Smoke)")]
    public static void CaptureFireAndSmokeScreenshot()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        TacticalForestZone zone = null;
        foreach (var z in zones)
        {
            var vr = z.transform.Find("Vegetation_Container");
            if (vr != null && vr.childCount > 0)
            {
                zone = z;
                break;
            }
        }
        if (zone == null) zone = zones[0];

        Transform vegRoot = zone.transform.Find("Vegetation_Container");
        if (vegRoot == null || vegRoot.childCount == 0)
        {
            zone.GenerateVegetation();
        }
        zone.InitTreeCombustionNodes();

        zone.ClearFireVFX();
        zone.SetSeason(TacticalForestZone.Season.Summer);
        zone.enableWinterSnow = false;
        zone.winterSnowAmount = 0f;
        zone.burningTreesCount = 1;
        zone.enableFireVFX = true;
        zone.smokeVisualType = TacticalForestZone.SmokeVisualType.TotalWarWarmCharcoal;
        zone.smokeDensity = 0.75f;
        zone.smokeScaleMultiplier = 1.0f;
        zone.smokeStartColor = new Color(0.62f, 0.56f, 0.50f, 1.0f);
        zone.smokeEndColor = new Color(0.75f, 0.70f, 0.65f, 1.0f);
        zone.SpawnFireVFX();

        // 파티클 1.5초 시뮬레이션으로 나뭇잎 표면에서 화염과 훈연이 신선하게 솟아오르는 상태 생성
        var allPS = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in allPS)
        {
            ps.Simulate(1.5f, true, true);
        }

        Transform targetTree = null;
        if (zone.CombustionNodes != null && zone.CombustionNodes.Count > 0)
        {
            for (int i = 0; i < zone.CombustionNodes.Count; i++)
            {
                var n = zone.CombustionNodes[i];
                if (n != null && n.treeObject != null && (n.treeObject.name.Contains("Pine_A") || n.treeObject.name.Contains("Huangshan")))
                {
                    targetTree = n.treeObject.transform;
                    break;
                }
            }
            if (targetTree == null)
            {
                targetTree = zone.CombustionNodes[0].treeObject.transform;
            }
        }

        if (targetTree == null)
        {
            Debug.LogWarning("[CaptureSceneScreenshot] 화재 대상을 찾을 수 없습니다.");
            return;
        }

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\3cc4d2a0-2845-4b27-b568-d5053c4f194b\scratch";
        Directory.CreateDirectory(saveDir);

        // 2. 소나무 숲과 나뭇잎 화염/연기 기둥을 한눈에 조망하는 원경 뷰 캡처
        GameObject camObj = new GameObject("TempFireCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 45f;

        Vector3 pos = targetTree.position;
        // 소나무 전체와 상공으로 피어오르는 아담한 훈연을 한눈에 담는 전경 배치 (거리 22m, 높이 9m)
        camObj.transform.position = pos + new Vector3(3.0f, 9.0f, -22.0f);
        camObj.transform.LookAt(pos + Vector3.up * 4.5f);

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObj);

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string savePath = Path.Combine(saveDir, "fire_and_smoke_rendered.png");
        File.WriteAllBytes(savePath, bytes);

        // 프로젝트 루트 scratch에도 복사
        string rootScratch = @"c:\unityProject\MiniTotalWar2D\scratch\fire_and_smoke_rendered.png";
        File.WriteAllBytes(rootScratch, bytes);

        Debug.Log($"<color=#00FF88><b>[CaptureSceneScreenshot] 🔥 화재 및 연기 렌더링 캡처 완료: {savePath}</b></color>");
    }

    [MenuItem("Tools/TotalWar/🪵 전소된 숯 나무 렌더링 캡처 (Capture Charred Tree)")]
    public static void CaptureCharredTreeScreenshot()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        TacticalForestZone zone = zones[0];
        Transform vegRoot = zone.transform.Find("Vegetation_Container");
        if (vegRoot == null || vegRoot.childCount == 0)
        {
            zone.GenerateVegetation();
        }
        zone.InitTreeCombustionNodes();

        // 화재 파티클 소멸 후 전소 완료 상태 시뮬레이션
        zone.ClearFireVFX();

        Transform targetTree = null;
        if (zone.CombustionNodes != null && zone.CombustionNodes.Count > 0)
        {
            for (int i = 0; i < zone.CombustionNodes.Count; i++)
            {
                var n = zone.CombustionNodes[i];
                if (n != null && n.treeObject != null && (n.treeObject.name.Contains("Pine_A") || n.treeObject.name.Contains("Huangshan")))
                {
                    targetTree = n.treeObject.transform;
                    break;
                }
            }
            if (targetTree == null)
            {
                targetTree = zone.CombustionNodes[0].treeObject.transform;
            }
        }

        if (targetTree == null) return;

        // 타겟 나무 및 주변 나무들에 전소 진행도 1.0 (BurnProgress = 1.0) 즉시 주입
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetFloat(Shader.PropertyToID("_BurnProgress"), 1.0f);

        var allRenderers = targetTree.GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null) r.SetPropertyBlock(mpb);
        }

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\3cc4d2a0-2845-4b27-b568-d5053c4f194b\scratch";
        Directory.CreateDirectory(saveDir);

        GameObject camObj = new GameObject("TempCharredCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 45f;

        Vector3 pos = targetTree.position;
        // 숯으로 변한 줄기와 앙상한 가지 골격을 근접에서 선명하게 포착 (거리 12m, 높이 5m)
        camObj.transform.position = pos + new Vector3(2.5f, 5.0f, -12.0f);
        camObj.transform.LookAt(pos + Vector3.up * 4.0f);

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObj);

        byte[] bytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        string savePath = Path.Combine(saveDir, "charred_tree_rendered.png");
        File.WriteAllBytes(savePath, bytes);

        string rootScratch = @"c:\unityProject\MiniTotalWar2D\scratch\charred_tree_rendered.png";
        File.WriteAllBytes(rootScratch, bytes);

        Debug.Log($"<color=#00FF88><b>[CaptureSceneScreenshot] 🪵 전소된 숯 나무 렌더링 캡처 완료: {savePath}</b></color>");
    }

    [MenuItem("Tools/TotalWar/❄️ 겨울 설경 및 겨울 숯 나무 렌더링 캡처 (Capture Winter Snow & Charred)")]
    public static void CaptureWinterSnowAndCharredTreeScreenshot()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return;

        TacticalForestZone zone = zones[0];
        Transform vegRoot = zone.transform.Find("Vegetation_Container");
        if (vegRoot == null || vegRoot.childCount == 0)
        {
            zone.GenerateVegetation();
        }
        zone.InitTreeCombustionNodes();

        // 1. 겨울 설경 모드 설정 및 머티리얼 강제 동기화
        zone.ClearFireVFX();
        zone.SetSeason(TacticalForestZone.Season.Winter);
        zone.enableWinterSnow = true;
        zone.winterSnowAmount = 0.85f;
        zone.RefreshVegetationMaterials();

        Transform targetTree = null;
        if (zone.CombustionNodes != null && zone.CombustionNodes.Count > 0)
        {
            for (int i = 0; i < zone.CombustionNodes.Count; i++)
            {
                var n = zone.CombustionNodes[i];
                if (n != null && n.treeObject != null && (n.treeObject.name.Contains("Pine_A") || n.treeObject.name.Contains("Huangshan")))
                {
                    targetTree = n.treeObject.transform;
                    break;
                }
            }
            if (targetTree == null)
            {
                targetTree = zone.CombustionNodes[0].treeObject.transform;
            }
        }

        if (targetTree == null) return;

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\3cc4d2a0-2845-4b27-b568-d5053c4f194b\scratch";
        Directory.CreateDirectory(saveDir);

        GameObject camObj = new GameObject("TempWinterCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 45f;

        Vector3 pos = targetTree.position;
        camObj.transform.position = pos + new Vector3(3.0f, 6.0f, -14.0f);
        camObj.transform.LookAt(pos + Vector3.up * 4.5f);

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        // 1. 겨울 설경 상태의 정상 나무 캡처 (기둥은 어두운 수피 100% 보존, 상단 잎과 가지에만 서리/설화)
        MaterialPropertyBlock normalMpb = new MaterialPropertyBlock();
        normalMpb.SetFloat(Shader.PropertyToID("_BurnProgress"), 0.0f);
        var targetRenderers = targetTree.GetComponentsInChildren<Renderer>(true);
        foreach (var r in targetRenderers)
        {
            if (r != null) r.SetPropertyBlock(normalMpb);
        }

        cam.Render();
        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();
        byte[] snowBytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        File.WriteAllBytes(Path.Combine(saveDir, "tw_style_winter_snow.png"), snowBytes);
        File.WriteAllBytes(@"c:\unityProject\MiniTotalWar2D\scratch\tw_style_winter_snow.png", snowBytes);

        // 2. 겨울 지역에서 불타서 전소된 숯 나무 캡처 (화염 열기로 눈이 융해되고 칠흑 같은 숯으로 완벽 탄화)
        MaterialPropertyBlock charredMpb = new MaterialPropertyBlock();
        charredMpb.SetFloat(Shader.PropertyToID("_BurnProgress"), 1.0f);
        foreach (var r in targetRenderers)
        {
            if (r != null) r.SetPropertyBlock(charredMpb);
        }

        cam.Render();
        screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();
        byte[] charredBytes = screenShot.EncodeToPNG();
        Object.DestroyImmediate(screenShot);

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObj);

        File.WriteAllBytes(Path.Combine(saveDir, "tw_style_winter_charred.png"), charredBytes);
        File.WriteAllBytes(@"c:\unityProject\MiniTotalWar2D\scratch\tw_style_winter_charred.png", charredBytes);

        Debug.Log($"<color=#00FF88><b>[CaptureSceneScreenshot] ❄️ 삼탈워 스타일 겨울 설경 및 겨울 숯 나무 캡처 완료!</b></color>");
    }
}
// Trigger capture update: 2026-10-23 13:02:00


// Trigger recompile for winter capture: 2026-10-23 16:10:01
