using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class InspectSbtGrassUV
{
    static InspectSbtGrassUV()
    {
        EditorApplication.delayCall += Analyze;
    }

    [MenuItem("TotalWar/🔍 Sbt_Grass 메쉬별 UV 및 꽃 영역 전수 분석", false, 99)]
    public static void Analyze()
    {
        string dir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Subtropical";
        if (!Directory.Exists(dir)) return;
        string[] files = Directory.GetFiles(dir, "*.obj");

        Debug.Log("<color=cyan><b>[InspectSbtGrassUV] 🔍 Sbt_Grass 메쉬 전수 분석 시작 (" + files.Length + "개 파일)</b></color>");

        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(file);
            if (mesh == null)
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath(file);
                foreach (var o in objs) { if (o is Mesh m) { mesh = m; break; } }
            }
            if (mesh == null) continue;

            Vector2[] uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) continue;

            float minU = 999, maxU = -999, minV = 999, maxV = -999;
            foreach (var uv in uvs)
            {
                if (uv.x < minU) minU = uv.x;
                if (uv.x > maxU) maxU = uv.x;
                if (uv.y < minV) minV = uv.y;
                if (uv.y > maxV) maxV = uv.y;
            }

            string tag = "일반 잔디";
            if (minU < 0.35f && maxV > 0.6f && mesh.bounds.size.y > 0.4f) tag = "🌾 큰 갈대 (Tall Reed)";
            else if (minU >= 0.30f && maxU <= 0.68f && minV <= 0.55f) tag = "🌸 파란/보라색 들꽃 (Blue/Purple Wildflower)";
            else if (minU >= 0.50f && maxV >= 0.30f) tag = "🌼 주황/노란색 꽃 (Orange/Yellow Flower)";
            else if (maxV <= 0.38f) tag = "🌱 하단 소형 잔디 & 미니 꽃";

            Debug.Log($"[{fileName}] 정점: {mesh.vertexCount}, 크기: {mesh.bounds.size.x:F2}x{mesh.bounds.size.y:F2}m | UV: U[{minU:F2}~{maxU:F2}] V[{minV:F2}~{maxV:F2}] => <color=yellow>{tag}</color>");
        }
    }
}
