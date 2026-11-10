using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌿 [식생 3대 결함 전면 일괄 교정 & 씬 정화 원스톱 실행기]
    /// 1. 씬 내 핑크색(Missing) 데모 잔여 오브젝트 일괄 영구 파괴
    /// 2. 마스터 빌더 전수 실행 (왕고사리 머티리얼 복구, 수림 잔디 반전 교정, 연꽃 규격 정상화)
    /// 3. 쇼케이스 창 자동 새로고침 및 전장 터레인 영구 베이크 동기화
    /// </summary>
    [InitializeOnLoad]
    public static class CleanAndRebuildFoliage
    {
        private const string FlagFile = "Assets/Settings/Vegetation/clean_rebuild_v1.flag";

        static CleanAndRebuildFoliage()
        {
            EditorApplication.delayCall += RunOnce;
        }

        [MenuItem("TotalWar/🧹 [원스톱 전면 교정] 식생 3대 결함 수정 & 씬 핑크색 오브젝트 청소 & 재빌드", false, 0)]
        public static void RunManual()
        {
            if (File.Exists(FlagFile)) File.Delete(FlagFile);
            RunOnce();
        }

        private static void RunOnce()
        {
            if (Application.isPlaying) return;
            if (File.Exists(FlagFile)) return;

            Debug.Log("<color=#00FFFF><b>[CleanAndRebuildFoliage] 🚀 식생 3대 결함 전면 교정 & 씬 정화 작업 시작...</b></color>");

            try
            {
                // 1. 씬 내 잔여 화재/불티/파티클 오브젝트 전수 소거 (에디터 씬 화재 진화)
                CleanResidualFireObjects.CleanAll();

                // 2. 씬 내 분홍색(Missing Shader) 및 잔여 데모 오브젝트 전수 정화
                int cleanedCount = CleanSceneGarbageObjects();
                Debug.Log($"<color=#00FF88><b>[CleanAndRebuildFoliage] 🧹 씬 내 불필요/에러 오브젝트 {cleanedCount}개 영구 정리 완료!</b></color>");

                // 3. 마스터 빌더 전수 실행 (식생 94종 + 수목 46종 + Game2 영구 베이크)
                TotalWarMasterVegetationBuilder.ExecuteMasterBuildAndBake();
                Debug.Log("<color=#00FF88><b>[CleanAndRebuildFoliage] ✅ 마스터 빌더 전수 에셋 및 Game2 터레인 베이크 완료!</b></color>");

                // 3. 쇼케이스 창 새로고침
                var showcaseWindows = Resources.FindObjectsOfTypeAll<TotalWarFoliageShowcaseWindow>();
                foreach (var win in showcaseWindows)
                {
                    win.ScanAllModels();
                    win.Repaint();
                }

                // 4. 완료 플래그 기록
                Directory.CreateDirectory("Assets/Settings/Vegetation");
                File.WriteAllText(FlagFile, $"RebuildCompleted_{DateTime.Now}");

                // 5. 씬 변경사항 저장
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isDirty)
                {
                    EditorSceneManager.SaveScene(activeScene);
                }

                Debug.Log("<color=#00FFFF><b>[CleanAndRebuildFoliage] 🎉 모든 교정 및 씬 정화가 완벽하게 완료되었습니다!</b></color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CleanAndRebuildFoliage] ❌ 오류 발생: {ex}");
            }
        }

        private static int CleanSceneGarbageObjects()
        {
            int count = 0;
            var allGOs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (go == null) continue;
                string n = go.name;

                // 데모 및 임시 프리뷰 오브젝트 대상
                bool isTarget = n.StartsWith("[DEMO]") ||
                                n.StartsWith("Instance_") ||
                                n.StartsWith("__Foliage_Showcase_Preview__") ||
                                n.Contains("Photo1") || n.Contains("Photo2") || n.Contains("Photo3") ||
                                n.Contains("PinkBuds") || n.Contains("PurplePetals") || n.Contains("OrangeSpikes");

                // 또는 렌더러에 Missing Material/Shader(핑크색)가 걸린 오브젝트
                if (!isTarget)
                {
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                            {
                                isTarget = true;
                                break;
                            }
                        }
                        if (isTarget) break;
                    }
                }

                if (isTarget)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    count++;
                }
            }
            return count;
        }
    }
}

