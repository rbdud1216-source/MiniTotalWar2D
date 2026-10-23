#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class CheckSnowStateDebug
{
    static CheckSnowStateDebug()
    {
        EditorApplication.delayCall += RunCheck;
    }

    [MenuItem("TotalWar/🔍 현재 씬 초목 적설 상태 진단")]
    public static void RunCheck()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        Debug.Log($"<color=#00FFFF><b>[CheckSnowStateDebug] 씬 내 TacticalForestZone 총 {zones.Length}개 발견</b></color>");

        foreach (var z in zones)
        {
            Debug.Log($"Zone: {z.name} | Season: {z.currentSeason} | enableWinterSnow: {z.enableWinterSnow} | winterSnowAmount: {z.winterSnowAmount}");
            z.RefreshVegetationMaterials();
            EditorUtility.SetDirty(z);
            var renderers = z.GetComponentsInChildren<MeshRenderer>(true);
            int matCount = 0;
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null)
                    {
                        float snow = m.HasProperty("_SnowAmount") ? m.GetFloat("_SnowAmount") : -1f;
                        float burn = m.HasProperty("_BurnProgress") ? m.GetFloat("_BurnProgress") : -1f;
                        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        string texName = tex != null ? tex.name : "NULL";
                        Debug.Log($"   Renderer: {r.name} | Mat: {m.name} | Shader: {m.shader.name} | _SnowAmount: {snow} | _BurnProgress: {burn} | Tex: {texName}");
                        matCount++;
                        if (matCount >= 4) goto ZoneDone;
                    }
                }
            }
        ZoneDone:;
        }
    }
}
#endif
