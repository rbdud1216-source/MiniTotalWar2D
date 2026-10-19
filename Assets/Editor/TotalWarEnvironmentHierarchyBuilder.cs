using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ☀️ [삼국지 토탈워 전장 환경 & 숲 하이어라키 빌더 (Total War Environment Hierarchy Builder)]
/// 게임을 실행(Play)하기 전 에디터 상태에서도 하이어라키 창에
/// 'TotalWar_EnvironmentLighting', 'TotalWar_WindManager', 'Forest_West_HuangshanPine', 'Forest_East_BambooGrove'가
/// 항상 존재하도록 자동 생성 및 배치하여, 개발자가 에디터에서 자유롭게 인스펙터 값을 조절할 수 있도록 보장합니다.
/// </summary>
[InitializeOnLoad]
public static class TotalWarEnvironmentHierarchyBuilder
{
    static TotalWarEnvironmentHierarchyBuilder()
    {
        EditorApplication.delayCall += () => EnsureEnvironmentInScene(forceRebuild: false);
        EditorSceneManager.sceneOpened += (scene, mode) => EnsureEnvironmentInScene(forceRebuild: false);
    }

    [MenuItem("MiniTotalWar/☀️ 전장 환경 & 숲 하이어라키에 배치 (Bake Scene Environment)", false, 50)]
    public static void ManualBakeEnvironment()
    {
        AssetDatabase.Refresh();
        EnsureEnvironmentInScene(forceRebuild: true);
    }

    [MenuItem("MiniTotalWar/🔥 서쪽 숲 바람결 연쇄 화재 전파 개시 (Start Wildfire Propagation)", false, 70)]
    public static void StartWildfirePropagationWestForest()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z.name.Contains("West") || z.theme == TacticalForestZone.ForestTheme.HuangshanPine)
            {
                z.StartWildfirePropagation();
                Debug.Log("<color=#FF5500><b>[MiniTotalWar] 🔥 서쪽 황산 소나무림 풍상측 나무에 착화! 바람을 타고 숲 전체로 불길이 전파됩니다.</b></color>");
            }
        }
    }

    [MenuItem("MiniTotalWar/🔥 서쪽 숲 전면 화재 즉시 점화 (Ignite West Forest Instant)", false, 71)]
    public static void IgniteWestForest()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z.name.Contains("West") || z.theme == TacticalForestZone.ForestTheme.HuangshanPine)
            {
                z.enableFireVFX = true;
                z.burningTreesCount = 8;
                z.GenerateVegetation();
                z.SpawnFireVFX();
                Debug.Log("<color=#FF5500><b>[MiniTotalWar] 🔥 서쪽 황산 소나무림에 삼탈워 화재 파티클이 점화되었습니다!</b></color>");
            }
        }
    }

    [MenuItem("MiniTotalWar/🧯 전장 화재 전면 진압 (Extinguish All Fires)", false, 72)]
    public static void ExtinguishAllFires()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            z.ExtinguishFire();
        }
        Debug.Log("<color=#00D4FF><b>[MiniTotalWar] 🧯 전장의 모든 숲 화재가 진압되었습니다.</b></color>");
    }

    /// <summary>
    /// 현재 열린 씬의 하이어라키에 환경 조명, 바람 매니저, 전술 숲 오브젝트가 존재하는지 확인하고 없으면 자동 생성합니다.
    /// </summary>
    public static void EnsureEnvironmentInScene(bool forceRebuild = false)
    {
        if (Application.isPlaying) return;

        bool sceneDirty = false;

        // 1. 삼탈워 공식 환경 조명 및 스카이박스 시스템 (TotalWar_EnvironmentLighting)
        TotalWarEnvironmentLighting lighting = Object.FindFirstObjectByType<TotalWarEnvironmentLighting>();
        if (lighting == null || forceRebuild)
        {
            if (lighting != null && forceRebuild)
            {
                Undo.DestroyObjectImmediate(lighting.gameObject);
            }

            GameObject lightingGo = new GameObject("TotalWar_EnvironmentLighting");
            lighting = lightingGo.AddComponent<TotalWarEnvironmentLighting>();
            Undo.RegisterCreatedObjectUndo(lightingGo, "Create TotalWar_EnvironmentLighting");

            lighting.ApplyLightingAndSkybox();
            sceneDirty = true;
            Debug.Log("<color=#FFCC00><b>[TotalWarEnvironmentHierarchyBuilder] ☀️ 하이어라키에 'TotalWar_EnvironmentLighting'이 성공적으로 배치되었습니다.</b></color>");
        }

        // 2. 삼탈워 전장 바람 시뮬레이션 매니저 (TotalWar_WindManager)
        TotalWarWindManager wind = Object.FindFirstObjectByType<TotalWarWindManager>();
        if (wind == null || forceRebuild)
        {
            if (wind != null && forceRebuild)
            {
                Undo.DestroyObjectImmediate(wind.gameObject);
            }

            GameObject windGo = new GameObject("TotalWar_WindManager");
            wind = windGo.AddComponent<TotalWarWindManager>();
            Undo.RegisterCreatedObjectUndo(windGo, "Create TotalWar_WindManager");

            wind.UpdateWindShaderGlobals();
            sceneDirty = true;
            Debug.Log("<color=#00D4FF><b>[TotalWarEnvironmentHierarchyBuilder] 🍃 하이어라키에 'TotalWar_WindManager'가 성공적으로 배치되었습니다.</b></color>");
        }

        // 3. 삼탈워 3D 전술 숲 군락지 2개소 (TacticalForestZone)
        TacticalForestZone existingZone = Object.FindFirstObjectByType<TacticalForestZone>();
        if (existingZone == null || forceRebuild)
        {
            if (forceRebuild)
            {
                TacticalForestZone[] allZones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
                foreach (var z in allZones)
                {
                    Undo.DestroyObjectImmediate(z.gameObject);
                }
            }

            // 서쪽 황산 소나무림 & 동쪽 대나무 군락지 자동 생성
            TacticalForestZone.AutoCreateDefaultForests();
            sceneDirty = true;
            Debug.Log("<color=#00FF88><b>[TotalWarEnvironmentHierarchyBuilder] 🌲 하이어라키에 삼탈워 전술 숲 2개소(소나무림, 대나무숲)가 성공적으로 배치되었습니다.</b></color>");
        }

        if (sceneDirty && !Application.isPlaying)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }
}
