using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MiniTotalWar;

public static class CaptureTerrainSceneView
{
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += ExecuteCapture;
    }

    [MenuItem("Tools/MiniTotalWar/Capture Terrain Vegetation Scene")]
    public static void ExecuteCapture()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Game2")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game2.unity", OpenSceneMode.Single);
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        TacticalGrassRenderer renderer = terrain.GetComponent<TacticalGrassRenderer>();
        if (renderer == null) return;

        // 1. 전장 중심부 (0,0 부근 또는 플레이어 스폰 위치) 탐색
        Vector3 focusPoint = Vector3.zero;
        GameObject playerSpawn = GameObject.Find("PlayerSpawnPoint");
        if (playerSpawn != null)
        {
            focusPoint = playerSpawn.transform.position;
        }

        // 지표면 실제 고도 샘플링
        float groundY = terrain.SampleHeight(focusPoint) + terrain.transform.position.y;
        focusPoint.y = groundY;

        // 2. 고품질 캡처용 가상 카메라 생성
        GameObject camGo = new GameObject("__TerrainCaptureCamera__");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.fieldOfView = 48f;

        // 카메라를 지상 0.75m 높이, 전방 2.8m 지면을 살짝 내려다보는 최적 화각으로 배치
        Vector3 camPos = focusPoint + new Vector3(0.8f, 0.75f, -2.5f);
        camPos.y = terrain.SampleHeight(camPos) + terrain.transform.position.y + 0.65f;
        camGo.transform.position = camPos;
        camGo.transform.LookAt(focusPoint + Vector3.up * 0.15f);

        int w = 1280;
        int h = 720;
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // 3. TacticalGrassRenderer 강제 드로우콜 발행 & 렌더링
        renderer.ForceRenderCamera(cam);
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);

        byte[] jpgBytes = tex.EncodeToJPG(92);
        Object.DestroyImmediate(tex);

        string savePath = "C:/Users/PC/.gemini/antigravity/brain/e54e15ce-c4f8-4f5a-bade-59595f8ab819/scratch/SceneCapture_TerrainFoliage.jpg";
        File.WriteAllBytes(savePath, jpgBytes);
        Debug.Log($"<color=#00FF88><b>[CaptureTerrain] 전장 터레인 표준 식생 캡처 저장 완료: {savePath} (포커스: {focusPoint})</b></color>");
    }
}