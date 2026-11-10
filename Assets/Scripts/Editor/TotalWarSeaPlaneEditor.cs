using UnityEngine;
using UnityEditor;

/// <summary>
/// TotalWarSeaPlane 커스텀 에디터 인스펙터
/// - [🌊 전역 바다·수면 평면 생성/동기화] 버튼
/// - [📐 터레인 최저 고도 자동 감지] 버튼
/// - 수위 슬라이더 실시간 프리뷰
/// </summary>
[CustomEditor(typeof(TotalWarSeaPlane))]
public class TotalWarSeaPlaneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TotalWarSeaPlane sea = (TotalWarSeaPlane)target;

        EditorGUILayout.Space(8);
        LabelOfField("🌊 삼국지 토탈워 전역 바다 & 수면 평면");

        DrawDefaultInspector();

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
        if (GUILayout.Button("🌊 전역 바다·수면 평면 생성 / 메쉬 동기화", GUILayout.Height(36)))
        {
            Undo.RecordObject(sea, "Generate Sea Plane");
            sea.GenerateSeaPlane();
            EditorUtility.SetDirty(sea);
        }

        GUI.backgroundColor = new Color(0.5f, 0.85f, 0.6f);
        if (GUILayout.Button("📐 터레인 고도 스캔 및 최저 수위 자동 산출", GUILayout.Height(28)))
        {
            Undo.RecordObject(sea, "Auto Detect Water Level");
            sea.AutoDetectWaterLevelFromTerrain();
            EditorUtility.SetDirty(sea);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "💡 팁: 터레인 브러시로 지형을 파내어 고도가 Water Level Y(현재: " + sea.waterLevelY.ToString("F1") + "m)보다 낮아지면 " +
            "즉시 물이 차오르며 바다, 호수, 만(Bay)이 형성됩니다.\n" +
            "CreateTotalWarTerrainLayers에서 '최저점 수변 자갈 자동 도색'을 실행하면 파여진 물밑 바닥이 자동으로 자갈/진흙으로 칠해집니다.",
            MessageType.Info);
    }

    private static void LabelOfField(string label)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.2f, 0.7f, 1.0f) }
        };
        EditorGUILayout.LabelField(label, style);
    }
}
