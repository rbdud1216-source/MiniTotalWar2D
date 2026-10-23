using UnityEngine;
using UnityEditor;

public static class DebugCurrentForestSceneMaterials
{
    [MenuItem("Tools/TotalWar/머티리얼 진단 (Debug Scene Materials)")]
    public static void RunDebug()
    {
        var zones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            Debug.Log($"=== Zone: {z.gameObject.name} (Theme: {z.theme}, Season: {z.currentSeason}) ===");
            var renderers = z.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r.transform.parent == null) continue;
                string objName = r.gameObject.name;
                string parentName = r.transform.parent.name;
                string matsStr = "";
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    var m = r.sharedMaterials[i];
                    if (m != null)
                    {
                        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                        string texName = tex != null ? tex.name : "NULL_TEX";
                        matsStr += $"[Submesh {i}: Mat={m.name}, Shader={m.shader.name}, Cutoff={m.GetFloat("_Cutoff")}, Tex={texName}] ";
                    }
                    else
                    {
                        matsStr += $"[Submesh {i}: NULL_MAT] ";
                    }
                }
                Debug.Log($"  Tree: {objName} (Parent: {parentName}) -> {matsStr}");
            }
        }
    }
}
