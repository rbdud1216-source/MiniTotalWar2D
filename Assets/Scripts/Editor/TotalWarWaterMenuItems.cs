using UnityEngine;
using UnityEditor;

/// <summary>
/// 삼국지 토탈워 삼위일체 수계 시스템 메뉴 아이템
/// - GameObject > Total War Water 메뉴를 통해 씬에 즉시 오브젝트 생성
/// </summary>
public static class TotalWarWaterMenuItems
{
    [MenuItem("GameObject/Total War Water/🌊 전역 바다·대형 호수 수면 평면 (Sea Plane)", false, 10)]
    public static void CreateSeaPlane(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("TotalWarSeaPlane");
        TotalWarSeaPlane sea = go.AddComponent<TotalWarSeaPlane>();

        // 씬 뷰 카메라 앞 또는 터레인 중심에 배치
        PositionInFrontOfSceneCamera(go);

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create TotalWarSeaPlane");
        Selection.activeObject = go;

        sea.GenerateSeaPlane();
        Debug.Log("[TotalWarWater] 전역 바다 수면 평면이 씬에 생성되었습니다!");
    }

    [MenuItem("GameObject/Total War Water/🌊 굽이치는 스플라인 하천 (River Spline)", false, 11)]
    public static void CreateRiverSpline(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("TotalWarRiverSpline");
        TotalWarRiverSpline river = go.AddComponent<TotalWarRiverSpline>();

        PositionInFrontOfSceneCamera(go);

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create TotalWarRiverSpline");
        Selection.activeObject = go;

        river.InitializeDefaultWaypoints();
        river.GenerateRiverMesh();
        Debug.Log("[TotalWarWater] 굽이치는 스플라인 하천이 씬에 생성되었습니다! 씬 뷰에서 노드를 드래그하여 강줄기를 만들어보세요.");
    }

    [MenuItem("GameObject/Total War Water/🌊 전술 수계 구역 - 얕은 여울목 (Shallow Ford)", false, 20)]
    public static void CreateShallowFordZone(MenuCommand menuCommand)
    {
        CreateTacticalWaterZoneInternal(menuCommand, TacticalWaterZone.WaterZoneType.ShallowFord, "TacticalWaterZone_Ford");
    }

    [MenuItem("GameObject/Total War Water/🌊 전술 수계 구역 - 남만·강남 늪지 (Swamp)", false, 21)]
    public static void CreateSwampZone(MenuCommand menuCommand)
    {
        CreateTacticalWaterZoneInternal(menuCommand, TacticalWaterZone.WaterZoneType.Swamp, "TacticalWaterZone_Swamp");
    }

    [MenuItem("GameObject/Total War Water/🌊 전술 수계 구역 - 깊은 연못 (Pond)", false, 22)]
    public static void CreatePondZone(MenuCommand menuCommand)
    {
        CreateTacticalWaterZoneInternal(menuCommand, TacticalWaterZone.WaterZoneType.Pond, "TacticalWaterZone_Pond");
    }

    private static void CreateTacticalWaterZoneInternal(MenuCommand menuCommand, TacticalWaterZone.WaterZoneType type, string name)
    {
        GameObject go = new GameObject(name);
        TacticalWaterZone zone = go.AddComponent<TacticalWaterZone>();
        zone.zoneType = type;
        zone.UpdatePresetProperties();

        PositionInFrontOfSceneCamera(go);

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        Selection.activeObject = go;

        zone.SyncLocalWaterMesh();
        Debug.Log($"[TotalWarWater] 전술 수계 구역({type})이 생성되었습니다.");
    }

    private static void PositionInFrontOfSceneCamera(GameObject go)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 center = terrain.transform.position + terrain.terrainData.size * 0.5f;
            center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
            go.transform.position = center;
            return;
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null && sceneView.camera != null)
        {
            Ray ray = new Ray(sceneView.camera.transform.position, sceneView.camera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                go.transform.position = hit.point;
                return;
            }
            go.transform.position = ray.GetPoint(30f);
        }
        else
        {
            go.transform.position = Vector3.zero;
        }
    }
}
