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
}
