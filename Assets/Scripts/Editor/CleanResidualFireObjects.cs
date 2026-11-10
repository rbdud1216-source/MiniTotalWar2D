#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MiniTotalWar;

public static class CleanResidualFireObjects
{
    [MenuItem("TotalWar/🧯 씬 내 모든 잔여 화재 오브젝트 전수 소거 (Clean All Fire Objects)")]
    public static void CleanAll()
    {
        int deletedCount = 0;

        // 1. TacticalGrassRenderer의 잔여 Foliage_FireVFX_Root 삭제
        var grassRenderer = Object.FindFirstObjectByType<TacticalGrassRenderer>();
        if (grassRenderer != null)
        {
            grassRenderer.CleanupResidualFireVFX();
        }

        // 2. 씬 전체에서 Fire_VFX_Container, Foliage_FireVFX_Root, TotalWar_TreeFire 전수 검색 및 삭제
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null) continue;

            string n = t.gameObject.name;
            bool isFireTarget = n == "Foliage_FireVFX_Root" ||
                                n == "Fire_VFX_Container" ||
                                n == "Fire_PointLight" ||
                                n == "Burning_Plant_Mesh" ||
                                n.StartsWith("Fire_") ||
                                n.StartsWith("TotalWar_TreeFire") ||
                                n.StartsWith("Ash_Plant_") ||
                                n == "Rising_Embers_TotalWar" ||
                                n == "Embers_Sparks";

            if (isFireTarget)
            {
                // 파티클 시스템 정지
                var psList = t.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in psList)
                {
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                Object.DestroyImmediate(t.gameObject);
                deletedCount++;
            }
        }

        // 3. 씬 더티 마킹 및 자동 저장
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log($"<color=#00FF88><b>[CleanResidualFireObjects] 🧯 총 {deletedCount}개의 잔여 화재/불티 오브젝트를 완전히 소거하고 씬을 저장했습니다!</b></color>");
    }
}
#endif
