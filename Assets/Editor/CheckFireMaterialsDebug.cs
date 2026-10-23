using UnityEngine;
using UnityEditor;

public static class CheckFireMaterialsDebug
{
    [MenuItem("Tools/TotalWar/화재 머티리얼 정밀 진단 (Check Fire Materials)")]
    public static void RunCheck()
    {
        string[] matPaths = new string[]
        {
            "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/M_TotalWar_CompositeFlames.mat",
            "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/M_TotalWar_WispySmoke.mat",
            "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/M_TotalWar_Embers.mat",
            "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/M_TotalWar_CharredWood.mat",
            "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/M_TotalWar_RisingFlames.mat"
        };

        foreach (var p in matPaths)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                Debug.LogError($"[VFX Check] ❌ 파일 없음: {p}");
                continue;
            }

            Shader s = m.shader;
            bool shaderValid = (s != null && s.isSupported && s.name != "Hidden/InternalErrorShader");
            string status = shaderValid ? $"<color=#00FF88>정상 (Shader: {s.name})</color>" : $"<color=#FF0055>❌ 셰이더 깨짐! (Shader: {(s != null ? s.name : "null")}, isSupported: {(s != null ? s.isSupported.ToString() : "false")})</color>";
            Debug.Log($"[VFX Check] 머티리얼: {m.name} -> {status}");
        }

        // Resources.Load 체크
        Material rFlames = Resources.Load<Material>("VFX/M_TotalWar_CompositeFlames");
        Material rSmoke = Resources.Load<Material>("VFX/M_TotalWar_WispySmoke");
        Material rEmbers = Resources.Load<Material>("VFX/M_TotalWar_Embers");
        Debug.Log($"[VFX Check] Resources.Load 결과: Flames={(rFlames != null)}, Smoke={(rSmoke != null)}, Embers={(rEmbers != null)}");
    }
}
