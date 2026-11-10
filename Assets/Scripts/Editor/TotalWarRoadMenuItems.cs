using UnityEngine;
using UnityEditor;

/// <summary>
/// 에디터 상단 메뉴 GameObject > Total War Roads 원클릭 스폰 도구
/// </summary>
public static class TotalWarRoadMenuItems
{
    [MenuItem("GameObject/Total War Roads/🛣️ 굽이치는 군사 도로 (Road Spline)", false, 10)]
    public static void CreateRoadSpline(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("TotalWarRoadSpline");
        TotalWarRoadSpline road = go.AddComponent<TotalWarRoadSpline>();

        PositionInFrontOfSceneCamera(go);

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create TotalWarRoadSpline");
        Selection.activeObject = go;

        road.InitializeDefaultWaypoints();
        Debug.Log("[TotalWarRoad] 굽이치는 군사 도로가 씬에 생성되었습니다! [마우스 펜 모드]를 켜고 씬 뷰 지형을 클릭하여 도로를 이어보세요.");
    }

    private static void PositionInFrontOfSceneCamera(GameObject go)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null && view.camera != null)
        {
            Ray ray = new Ray(view.camera.transform.position, view.camera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f))
            {
                go.transform.position = hit.point;
                return;
            }

            Vector3 spawnPos = view.camera.transform.position + view.camera.transform.forward * 40f;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                spawnPos.y = terrain.SampleHeight(spawnPos) + terrain.transform.position.y;
            }
            go.transform.position = spawnPos;
        }
    }
}
