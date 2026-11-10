using UnityEngine;
using UnityEditor;

public static class CheckHaTexture
{
    [MenuItem("TotalWar/🧪 [진단] Seedhead 모델 진단", false, 50)]
    public static void Run()
    {
        string modelPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_Seedhead_A.obj";
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (go == null)
        {
            Debug.LogError("모델 로드 실패: " + modelPath);
            return;
        }

        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("메쉬 없음");
            return;
        }

        Mesh m = mf.sharedMesh;
        Debug.Log($"<color=yellow>[진단] Tall_Seedhead_A bounds: min={m.bounds.min}, max={m.bounds.max}, size={m.bounds.size}</color>");

        string tallDPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj";
        GameObject goD = AssetDatabase.LoadAssetAtPath<GameObject>(tallDPath);
        Mesh mD = goD.GetComponentInChildren<MeshFilter>().sharedMesh;
        Debug.Log($"<color=yellow>[진단] Tall_D bounds: min={mD.bounds.min}, max={mD.bounds.max}, size={mD.bounds.size}</color>");
    }
}
