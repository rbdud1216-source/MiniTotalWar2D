using UnityEngine;
using UnityEditor;

/// <summary>
/// 🌲 TacticalForestZone 커스텀 인스펙터 에디터
/// 인스펙터에서 수종 토글, 테마, 계절 변경 시 즉각 씬 뷰에 반영하고
/// 원클릭 초목 재생성, 계절 전환, 화재 시뮬레이션 제어 버튼을 제공합니다.
/// </summary>
[CustomEditor(typeof(TacticalForestZone))]
[CanEditMultipleObjects]
public class TacticalForestZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TacticalForestZone zone = (TacticalForestZone)target;

        // 상단 실시간 제어 버튼 바
        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
        if (GUILayout.Button("🌲 초목 즉시 재생성 (Regenerate Trees Now)", GUILayout.Height(34)))
        {
            Undo.RecordObject(zone, "Regenerate Vegetation");
            zone.GenerateVegetation();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌸 봄 (Spring)"))
        {
            Undo.RecordObject(zone, "Set Season Spring");
            zone.SetSeason(TacticalForestZone.Season.Spring);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("☀️ 여름 (Summer)"))
        {
            Undo.RecordObject(zone, "Set Season Summer");
            zone.SetSeason(TacticalForestZone.Season.Summer);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("🍁 가을 (Autumn)"))
        {
            Undo.RecordObject(zone, "Set Season Autumn");
            zone.SetSeason(TacticalForestZone.Season.Autumn);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("❄️ 겨울 (Winter)"))
        {
            Undo.RecordObject(zone, "Set Season Winter");
            zone.SetSeason(TacticalForestZone.Season.Winter);
            EditorUtility.SetDirty(zone);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1.0f, 0.4f, 0.2f);
        if (GUILayout.Button("🔥 화재 연출 (Ignite Fire)", GUILayout.Height(24)))
        {
            Undo.RecordObject(zone, "Ignite Fire");
            zone.IgniteForestFire();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1.0f);
        if (GUILayout.Button("🧯 화재 진압 (Extinguish)", GUILayout.Height(24)))
        {
            Undo.RecordObject(zone, "Extinguish Fire");
            zone.ExtinguishFire();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 기본 프로퍼티 렌더링
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(zone);
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("🧹 초목 모두 제거 (Clear All)", GUILayout.Height(24)))
        {
            Undo.RecordObject(zone, "Clear Vegetation");
            zone.ClearVegetation();
            EditorUtility.SetDirty(zone);
        }
    }
}
