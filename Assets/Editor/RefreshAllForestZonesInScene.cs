using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RefreshAllForestZonesInScene
{
    [MenuItem("Tools/TotalWar/초목 전체 새로고침 (Refresh All Forests)")]
    public static void RefreshAll()
    {
        AssetDatabase.Refresh();
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        Debug.Log($"<color=#00FFFF><b>[RefreshAllForestZones] 씬 내 총 {zones.Length}개의 숲 구역을 재생성합니다...</b></color>");

        for (int i = 0; i < zones.Length; i++)
        {
            var zone = zones[i];
            if (zone == null) continue;
            zone.ClearVegetation();
            zone.SnapToTerrain();
            zone.GenerateVegetation();
            EditorUtility.SetDirty(zone);
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        Debug.Log($"<color=#00FF88><b>[RefreshAllForestZones] ✅ 씬 내 {zones.Length}개 숲 구역 초목 재생성 및 머티리얼 바인딩 완료!</b></color>");
        DebugCurrentForestSceneMaterials.RunDebug();
        CaptureSceneScreenshot.CaptureBrokenPine();
        CaptureSceneScreenshot.CaptureStump();
    }
}
