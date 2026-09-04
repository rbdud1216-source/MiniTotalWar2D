using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유니티 에디터 상에서 씬 하이어라키의 Canvas 하위에 'TacticalMinimap' UI 오브젝트를 영구적으로 생성하고 프리팹화하는 에디터 도구입니다.
/// </summary>
[InitializeOnLoad]
public static class MinimapHierarchyBuilder
{
    static MinimapHierarchyBuilder()
    {
        EditorApplication.delayCall += AutoBuildIfMissing;
    }

    [MenuItem("Tools/Build Tactical Minimap in Scene")]
    [MenuItem("GameObject/UI/Tactical Minimap", false, 10)]
    public static void BuildMinimapInScene()
    {
        CreateOrUpdateMinimapInScene(forceRebuild: true);
    }

    private static void AutoBuildIfMissing()
    {
        CreateOrUpdateMinimapInScene(forceRebuild: false);
    }

    public static GameObject CreateOrUpdateMinimapInScene(bool forceRebuild = false)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            if (forceRebuild) Debug.LogWarning("[MinimapHierarchyBuilder] 씬에 Canvas가 존재하지 않아 미니맵을 생성할 수 없습니다.");
            return null;
        }

        Transform existingMinimap = canvas.transform.Find("TacticalMinimap");
        if (existingMinimap != null && !forceRebuild)
        {
            return existingMinimap.gameObject;
        }

        if (existingMinimap != null && forceRebuild)
        {
            Undo.DestroyObjectImmediate(existingMinimap.gameObject);
        }

        // 1. 루트 미니맵 오브젝트 생성
        GameObject minimapObj = new GameObject("TacticalMinimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(MinimapManager));
        minimapObj.transform.SetParent(canvas.transform, false);
        Undo.RegisterCreatedObjectUndo(minimapObj, "Create Tactical Minimap");

        RectTransform rootRect = minimapObj.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(18f, 18f); // 화면 좌측 하단 여백 18px
        rootRect.sizeDelta = new Vector2(210f, 210f);      // 210x210 정사각형

        Image bgImage = minimapObj.GetComponent<Image>();
        bgImage.color = new Color(0.08f, 0.11f, 0.15f, 0.94f); // 짙은 다크 슬레이트 톤
        bgImage.raycastTarget = true;

        // 2. 카메라 Frustum 시야각 레이어 생성
        GameObject frustumObj = new GameObject("CameraFrustum", typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapCameraFrustum));
        frustumObj.transform.SetParent(minimapObj.transform, false);
        RectTransform frustumRect = frustumObj.GetComponent<RectTransform>();
        frustumRect.anchorMin = Vector2.zero;
        frustumRect.anchorMax = Vector2.one;
        frustumRect.pivot = new Vector2(0.5f, 0.5f);
        frustumRect.sizeDelta = Vector2.zero;
        frustumRect.anchoredPosition = Vector2.zero;
        MinimapCameraFrustum frustumGraphic = frustumObj.GetComponent<MinimapCameraFrustum>();

        // 3. 부대 마커 컨테이너 생성
        GameObject markerObj = new GameObject("MarkerContainer", typeof(RectTransform));
        markerObj.transform.SetParent(minimapObj.transform, false);
        RectTransform markerRect = markerObj.GetComponent<RectTransform>();
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = Vector2.one;
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = Vector2.zero;
        markerRect.anchoredPosition = Vector2.zero;

        // 4. 외곽 테두리 프레임 생성
        GameObject borderObj = new GameObject("BorderFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderObj.transform.SetParent(minimapObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(4f, 4f); // 2px 테두리 두께
        borderRect.anchoredPosition = Vector2.zero;

        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(0.42f, 0.48f, 0.55f, 0.95f); // 청회색 황동 프레임
        borderImage.raycastTarget = false;

        // 5. MinimapManager 직렬화 필드 연결
        MinimapManager manager = minimapObj.GetComponent<MinimapManager>();
        SerializedObject serObj = new SerializedObject(manager);
        serObj.FindProperty("minimapRect").objectReferenceValue = rootRect;
        serObj.FindProperty("markerContainer").objectReferenceValue = markerRect;
        serObj.FindProperty("cameraFrustum").objectReferenceValue = frustumGraphic;
        serObj.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        serObj.FindProperty("borderFrameImage").objectReferenceValue = borderImage;
        serObj.ApplyModifiedProperties();

        manager.AutoDetectTerrainBounds();

        // 6. 프리팹으로도 안전하게 저장
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        PrefabUtility.SaveAsPrefabAsset(minimapObj, "Assets/Prefabs/TacticalMinimap.prefab");

        // 7. 씬 더티 마킹 및 저장
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("[MinimapHierarchyBuilder] 하이어라키 Canvas 하위에 'TacticalMinimap' 오브젝트를 성공적으로 생성하고 씬에 영구 등록했습니다.");
        return minimapObj;
    }
}
