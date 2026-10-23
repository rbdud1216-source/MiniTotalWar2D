#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 삼국지 토탈워 식생 머티리얼 및 셰이더 일괄 복구 & 자동 연결 툴
/// - 컴파일 에러가 발생했던 TotalWarFoliageWind 셰이더 재적용
/// - 나뭇잎(Cutout) 및 기둥(Opaque) 머티리얼의 누락된 노멀맵 및 탄화(Charred) 텍스처 자동 탐색 및 바인딩
/// - 보라색(Magenta) 깨짐 현상 원천 해결
/// </summary>
[InitializeOnLoad]
public static class FixVegetationMaterials
{
    private const string CharredTexPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/3k_cityprop_mines_wood_ashes_base_colour.png";
    private const string FoliageShaderName = "Universal Render Pipeline/TotalWarFoliageWind";
    private const string TrunkShaderName = "Universal Render Pipeline/Lit";

    private static bool hasExecutedOnce = false;

    static FixVegetationMaterials()
    {
        EditorApplication.delayCall += () =>
        {
            if (!hasExecutedOnce)
            {
                hasExecutedOnce = true;
                FixAllVegetationMaterials();
            }
        };
    }

    [MenuItem("TotalWar/🌲 식생 머티리얼 일괄 복구 및 연결 (Fix & Connect Vegetation Materials)")]
    public static void FixAllVegetationMaterials()
    {
        Debug.Log("<color=#00FF88><b>[FixVegetationMaterials] 식생 머티리얼 및 셰이더 정밀 진단 및 자동 복구를 시작합니다...</b></color>");

        // 1. 필수 셰이더 로드 확인
        Shader foliageShader = Shader.Find(FoliageShaderName);
        Shader trunkShader = Shader.Find(TrunkShaderName);

        if (foliageShader == null)
        {
            Debug.LogError($"[FixVegetationMaterials] 필수 셰이더 '{FoliageShaderName}'를 찾을 수 없습니다!");
            return;
        }

        // 2. 탄화 텍스처 로드
        Texture2D charredTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CharredTexPath);
        if (charredTex != null)
        {
            Debug.Log($"[FixVegetationMaterials] 삼탈워 공식 탄화(Charred) 텍스처 로드 성공: {charredTex.name}");
        }

        // 3. 씬 내 모든 MeshRenderer 순회 수집
        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;
        HashSet<Material> processedMaterials = new HashSet<Material>();

        foreach (var mr in allRenderers)
        {
            if (mr == null) continue;

            // 식생 관련 오브젝트 판별 (Vegetation_Container 하위 또는 이름에 Pine, Tree, Bush, Shrub, Bamboo 포함)
            bool isVegetation = false;
            Transform cur = mr.transform;
            while (cur != null)
            {
                if (cur.name.Contains("Vegetation") || cur.name.Contains("Forest") || cur.name.Contains("Pine") || cur.name.Contains("Tree") || cur.name.Contains("Bamboo"))
                {
                    isVegetation = true;
                    break;
                }
                cur = cur.parent;
            }

            if (!isVegetation) continue;

            Material[] mats = mr.sharedMaterials;
            bool rendererChanged = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                if (processedMaterials.Add(m))
                {
                    bool matChanged = FixSingleMaterial(m, foliageShader, trunkShader, charredTex);
                    if (matChanged)
                    {
                        EditorUtility.SetDirty(m);
                        fixedCount++;
                        rendererChanged = true;
                    }
                }
            }

            if (rendererChanged)
            {
                EditorUtility.SetDirty(mr);
            }
        }

        // 4. 씬 저장 마크
        if (fixedCount > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FF88><b>[FixVegetationMaterials] 총 {fixedCount}개의 머티리얼 복구 및 연결이 완료되었습니다!</b></color>");
        }
        else
        {
            Debug.Log("[FixVegetationMaterials] 이미 모든 머티리얼이 정상 연결되어 있습니다.");
        }
    }

    [MenuItem("TotalWar/❄️ 나뭇잎 설경(적설) 즉시 반영 및 갱신 (Refresh Foliage Snow)")]
    public static void RefreshFoliageSnowAll()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z != null)
            {
                z.RefreshVegetationMaterials();
                EditorUtility.SetDirty(z);
            }
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("<color=#00FFFF><b>[FixVegetationMaterials] ❄️ 씬 내 모든 숲 구역의 나뭇잎 설경(적설)이 최신 셰이더 설정으로 동기화되었습니다!</b></color>");
    }

    [MenuItem("TotalWar/🔥 화재 VFX 즉시 재스폰 및 최신화 (Refresh Fire VFX)")]
    public static void RefreshFireVFXAll()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z != null && z.enableFireVFX)
            {
                z.SpawnFireVFX();
                EditorUtility.SetDirty(z);
            }
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("<color=#FFAA00><b>[FixVegetationMaterials] 🔥 씬 내 화재 중인 모든 숲의 화염(플립북) 및 연기(잿빛 훈연) 파티클이 최신 설정으로 즉시 재스폰되었습니다!</b></color>");
    }

    [MenuItem("TotalWar/🧯 씬 잔여 화재 완전 청소 및 나뭇잎 복구 (Purge Fire & Restore Leaves)")]
    public static void PurgeAllFireVFX()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z != null)
            {
                z.enableFireVFX = false;
                z.ClearFireVFX();
                z.ExtinguishFire();
                EditorUtility.SetDirty(z);
            }
        }

        // 혹시라도 남아있는 파티클 오브젝트 전수 강제 삭제
        string[] fireNames = new string[] {
            "CompositeFlames_TotalWar", "VolumetricSmoke_TotalWar", "Rising_Embers_TotalWar",
            "Fire_PointLight", "TotalWar_TreeFire"
        };
        foreach (var name in fireNames)
        {
            var gos = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in gos)
            {
                if (go != null && go.name.Contains(name))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("<color=#00FF88><b>[FixVegetationMaterials] 🧯 씬 내 모든 잔여 화재 파티클/연기가 완전 삭제되었으며, 모든 나무의 나뭇잎이 깨끗하게 원상 복구되었습니다!</b></color>");
    }

    private static bool FixSingleMaterial(Material mat, Shader foliageShader, Shader trunkShader, Texture2D charredTex)
    {
        bool changed = false;
        string matName = mat.name.ToLower();

        bool isLeaf = matName.Contains("branch") || matName.Contains("leaf") || matName.Contains("cutout") || 
                      matName.Contains("shrub") || matName.Contains("fern") || matName.Contains("tea");
        bool isTrunk = matName.Contains("trunk") || matName.Contains("bark") || matName.Contains("opaque");

        // A. 셰이더 연결 확인 및 복구
        if (isLeaf)
        {
            if (mat.shader != foliageShader)
            {
                mat.shader = foliageShader;
                changed = true;
                Debug.Log($"[FixVegetationMaterials] 나뭇잎 머티리얼 '{mat.name}'에 TotalWarFoliageWind 셰이더 연결");
            }

            // Alpha Cutout 기본 키워드 활성화
            if (!mat.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                changed = true;
            }
            if (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") < 0.5f)
            {
                mat.SetFloat("_AlphaClip", 1.0f);
                changed = true;
            }
            if (mat.HasProperty("_Cutoff") && mat.GetFloat("_Cutoff") <= 0.01f)
            {
                mat.SetFloat("_Cutoff", 0.35f);
                changed = true;
            }
            if (mat.HasProperty("_BurnProgress") && mat.GetFloat("_BurnProgress") > 0.001f)
            {
                mat.SetFloat("_BurnProgress", 0.0f);
                changed = true;
            }
            mat.renderQueue = 2450;

            // 탄화 텍스처 연결
            if (charredTex != null && mat.HasProperty("_CharredMap"))
            {
                if (mat.GetTexture("_CharredMap") == null)
                {
                    mat.SetTexture("_CharredMap", charredTex);
                    mat.SetFloat("_CharredTile", 3.0f);
                    changed = true;
                    Debug.Log($"[FixVegetationMaterials] '{mat.name}'에 탄화(Charred) 텍스처 연결 완료");
                }
            }

            // 적설 임계값 최신화 (-0.15f)
            if (mat.HasProperty("_SnowThreshold"))
            {
                if (Mathf.Abs(mat.GetFloat("_SnowThreshold") - (-0.15f)) > 0.01f)
                {
                    mat.SetFloat("_SnowThreshold", -0.15f);
                    changed = true;
                }
            }
        }
        else if (isTrunk)
        {
            if (trunkShader != null && mat.shader != trunkShader && mat.shader.name.Contains("Error"))
            {
                mat.shader = trunkShader;
                changed = true;
                Debug.Log($"[FixVegetationMaterials] 기둥 머티리얼 '{mat.name}'에 URP Lit 셰이더 연결");
            }
        }

        // B. 누락된 노멀맵 자동 탐색 및 바인딩
        if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") == null)
        {
            Texture2D foundNormal = FindMatchingNormalTexture(mat.name);
            if (foundNormal != null)
            {
                mat.SetTexture("_BumpMap", foundNormal);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_EnableNormalMap")) mat.SetFloat("_EnableNormalMap", 1.0f);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.2f);
                changed = true;
                Debug.Log($"[FixVegetationMaterials] '{mat.name}'에 누락된 노멀맵 '{foundNormal.name}' 자동 바인딩 완료");
            }
        }

        return changed;
    }

    /// <summary>
    /// 머티리얼 이름을 기반으로 Assets 내에서 가장 적합한 노멀맵 텍스처 자동 검색
    /// </summary>
    private static Texture2D FindMatchingNormalTexture(string matName)
    {
        string lower = matName.ToLower().Replace("_cutout", "").Replace("_opaque", "").Replace("_base_colour", "");

        // 1. 핵심 키워드 매핑 테이블
        Dictionary<string, string> normalGuids = new Dictionary<string, string>()
        {
            { "huangshan_pine_branch", "941185fc80812b64a98d15436ffd432c" },
            { "huangshan_pine_trunk",  "b209f27314789ee4a8718a1213917ef4" },
            { "pine_q_shrub",          "827416fdfb5db534fa5707a58a98811b" },
            { "pine_r_shrub",          "pine_r_shrub_billboard_normal" },
            { "pine_bark",             "cadffe06c854b1440a66c15b2aefc592" },
            { "pine_trunk",            "3f508a6df33ce6246a2db50c21aec045" },
            { "pine",                  "82fb4c9b5ed303e4ebba600220f481c5" },
            { "bamboo",                "6fee2be3ef051894d89af5fbe11e6c9a" },
            { "fern",                  "d5d5c3fdf19298a488fe2f0d851d6eb2" },
            { "rhododendron",          "8ef55a9b25c0c20458239d0035763b12" },
            { "tea",                   "9ae59c379c6082548b7517dbbaefe049" },
        };

        foreach (var kvp in normalGuids)
        {
            if (lower.Contains(kvp.Key))
            {
                string path = AssetDatabase.GUIDToAssetPath(kvp.Value);
                if (!string.IsNullOrEmpty(path))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
        }

        return null;
    }
}
#endif
