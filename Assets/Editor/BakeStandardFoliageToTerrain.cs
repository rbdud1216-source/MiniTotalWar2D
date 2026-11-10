using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MiniTotalWar;

public class BakeStandardFoliageToTerrain
{
    [MenuItem("Tools/MiniTotalWar/Bake Standard Foliage to Terrain")]
    public static void ExecuteBake()
    {
        // 1. Game2 씬 확인 및 로드
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.name != "Game2")
        {
            Debug.Log("[BakeFoliage] Game2 씬을 로드합니다...");
            EditorSceneManager.OpenScene("Assets/Scenes/Game2.unity", OpenSceneMode.Single);
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("[BakeFoliage] 전장에 활성화된 Terrain이 없습니다!");
            return;
        }

        // 2. TacticalGrassRenderer 및 TotalWarTerrainManager 탐색
        TacticalGrassRenderer grassRenderer = terrain.GetComponent<TacticalGrassRenderer>();
        if (grassRenderer == null)
        {
            grassRenderer = terrain.gameObject.AddComponent<TacticalGrassRenderer>();
        }

        TotalWarTerrainManager terrainManager = Object.FindFirstObjectByType<TotalWarTerrainManager>();

        // 3. 규격 및 표준 간격 설정 (군락 지름 0.9m에 맞춘 1.6m 간격)
        grassRenderer.targetTerrain = terrain;
        grassRenderer.sampleSpacing = 1.6f;
        grassRenderer.enableMultiSpeciesBlending = true;
        grassRenderer.tallGrassChance = 0.15f;
        grassRenderer.wildflowerPatchChance = 0.15f;
        grassRenderer.maxRenderDistance = 120f;

        if (terrainManager != null)
        {
            terrainManager.defaultGrassSpacing = 1.6f;
            terrainManager.grassBiomeTheme = GrassBiomeTheme.CentralPlains20;
            terrainManager.customGrassList = RadialGrassConfig.GetDefaultFoliageList();
            EditorUtility.SetDirty(terrainManager);
        }

        // 4. 레거시 메쉬 전면 배제 및 표준 8대 프로토타입 완전 바인딩
        grassRenderer.EnsureDefaultPrototypes(true);
        grassRenderer.EnsureAllPrototypesGrounded(true);

        Debug.Log($"[BakeFoliage] 표준 프로토타입 바인딩 완료 (총 {grassRenderer.prototypes.Count}종):");
        for (int i = 0; i < grassRenderer.prototypes.Count; i++)
        {
            var p = grassRenderer.prototypes[i];
            Debug.Log($"  [{i}] {p.protoName} | Mesh: {(p.mesh != null ? p.mesh.name : "NULL")} | Mat: {(p.material != null ? p.material.name : "NULL")}");
        }

        // 5. 전장 식생 데이터 베이크 및 디스크 파일 영구 저장
        grassRenderer.BakeFoliageData();
        grassRenderer.SaveFoliageDataToDisk();

        EditorUtility.SetDirty(grassRenderer);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"<color=#00FF88><b>[BakeFoliage] 전장 터레인 표준 식생 데이터 베이크 완료! (청크 수: {grassRenderer.ActiveChunkCount}, 총 인스턴스: {grassRenderer.TotalInstanceCount:N0}개)</b></color>");

        // 6. 씬 뷰 카메라 포커스 및 캡처
        FocusAndCapture(terrain);
    }

    private static void FocusAndCapture(Terrain terrain)
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            sceneView = EditorWindow.GetWindow<SceneView>();
        }

        if (sceneView != null)
        {
            // 지면 근처 잔디밭 융단과 3D 풀포기를 잘 조망할 수 있는 위치 (평원 지점)
            Vector3 terPos = terrain.transform.position;
            Vector3 viewPos = terPos + new Vector3(256f, 15f, 256f);
            float groundY = terrain.SampleHeight(viewPos) + terPos.y;
            viewPos.y = groundY + 2.2f; // 지상 2.2m 눈높이 뷰

            sceneView.pivot = viewPos + new Vector3(0, -0.8f, 5.0f);
            sceneView.rotation = Quaternion.Euler(18f, 35f, 0f);
            sceneView.size = 6.5f;
            sceneView.Repaint();

            // 지연 캡처 실행
            EditorApplication.delayCall += () =>
            {
                CaptureScene(sceneView);
            };
        }
    }

    private static void CaptureScene(SceneView sceneView)
    {
        if (sceneView == null || sceneView.camera == null) return;

        Camera cam = sceneView.camera;
        int width = 1280;
        int height = 720;

        RenderTexture rt = new RenderTexture(width, height, 24);
        RenderTexture oldRt = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        cam.targetTexture = oldRt;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        byte[] bytes = tex.EncodeToJPG(90);
        Object.DestroyImmediate(tex);

        string savePath = "C:/Users/PC/.gemini/antigravity/brain/e54e15ce-c4f8-4f5a-bade-59595f8ab819/scratch/SceneCapture_TerrainFoliage.jpg";
        File.WriteAllBytes(savePath, bytes);
        Debug.Log($"[BakeFoliage] 씬 뷰 캡처 저장 완료: {savePath}");
    }
}

