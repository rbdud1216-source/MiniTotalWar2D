using UnityEngine;
using UnityEditor;

/// <summary>
/// TacticalWaterZone 커스텀 에디터 인스펙터 및 씬 뷰 기즈모 핸들
/// - [🎯 현재 지형 표면에 높이 맞추기] 버튼 탑재
/// - [🔄 로컬 수면 메쉬 즉시 동기화 / 재생성]
/// - 씬 뷰 원형 Radius 핸들 드래그 시 실시간 메쉬 반응
/// </summary>
[CustomEditor(typeof(TacticalWaterZone))]
[CanEditMultipleObjects]
public class TacticalWaterZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TacticalWaterZone zone = (TacticalWaterZone)target;

        EditorGUILayout.Space(6);
        DrawHeaderLabel("🌊 삼국지 토탈워 전술 수계·늪지·여울목 구역");

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Change Water Zone Properties");
            zone.UpdatePresetProperties();
            zone.SyncLocalWaterMesh();
            EditorUtility.SetDirty(zone);
        }

        // 수면 기울어짐 감지 및 즉시 수평화 버튼
        Vector3 rotAngles = zone.transform.rotation.eulerAngles;
        bool isTilted = Mathf.Abs(Mathf.DeltaAngle(rotAngles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(rotAngles.z, 0f)) > 0.05f;
        if (isTilted)
        {
            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.3f);
            if (GUILayout.Button("⚖️ 수면 완전 수평 맞추기 (Zero Tilt - 기울어짐 제거)", GUILayout.Height(30)))
            {
                Undo.RecordObject(zone.transform, "Flatten Water Zone Transform");
                zone.EnforceLevelTransform();
                zone.SyncLocalWaterMesh();
                EditorUtility.SetDirty(zone);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
        if (GUILayout.Button("🎯 현재 지형 표면에 높이 맞추기", GUILayout.Height(32)))
        {
            Undo.RecordObject(zone.transform, "Snap Zone To Terrain");
            zone.SnapToTerrainSurface();
            EditorUtility.SetDirty(zone);
        }

        GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
        if (GUILayout.Button("🔄 로컬 수면 메쉬 즉시 갱신", GUILayout.Height(32)))
        {
            Undo.RecordObject(zone, "Sync Local Water Mesh");
            zone.SyncLocalWaterMesh();
            EditorUtility.SetDirty(zone);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.15f, 0.55f, 0.95f);
        if (GUILayout.Button("🌊 연못·웅덩이 지형 파내기 (Carve Basin) + 진흙 도색 + 잔디 배제", GUILayout.Height(36)))
        {
            if (EditorUtility.DisplayDialog("연못/웅덩이 지형 조각",
                "워터존 구역을 오목한 그릇 형태로 파내고 바닥에 진흙/자갈을 도색합니다.\n(파내기 전 지형은 원클릭 Undo로 즉시 되돌릴 수 있습니다)",
                "조각 실행", "취소"))
            {
                zone.CarveTerrainBasin();
            }
        }

        if (zone.HasBackup)
        {
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.15f);
            if (GUILayout.Button("⏪ 파내기 전 이전 지형으로 1초 만에 되돌리기 (Undo)", GUILayout.Height(32)))
            {
                zone.RestoreTerrainBackup();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        string desc = "";
        switch (zone.zoneType)
        {
            case TacticalWaterZone.WaterZoneType.ShallowFord:
                desc = "💡 [얕은 여울목]: 강을 가로지르는 도하 구역입니다. 보병 이동속도가 30% 감속되며, 기병의 돌격 보너스가 무력화됩니다.";
                break;
            case TacticalWaterZone.WaterZoneType.Swamp:
                desc = "💡 [남만·강남 늪지]: 진흙탕 늪지대입니다. 보병 이속 50%, 기병 이속 60%가 격감되며 피로도가 3.5배 빠르게 소모됩니다.";
                break;
            case TacticalWaterZone.WaterZoneType.Pond:
                desc = "💡 [깊은 연못]: 수심이 깊어 군대가 통과할 수 없는 천연 차단벽(Passable = False)입니다.";
                break;
            case TacticalWaterZone.WaterZoneType.Puddle:
                desc = "💡 [물웅덩이]: 얇은 물웅덩이로 경미한 10% 감속과 발자국 물보라를 연출합니다.";
                break;
        }

        EditorGUILayout.HelpBox(desc, MessageType.Info);
    }

    private void OnSceneGUI()
    {
        TacticalWaterZone zone = (TacticalWaterZone)target;
        if (zone == null) return;

        Vector3 center = zone.transform.position + Vector3.up * zone.surfaceYOffset;

        if (zone.shape == TacticalWaterZone.ZoneShape.Circle)
        {
            EditorGUI.BeginChangeCheck();
            float newRadius = Handles.RadiusHandle(Quaternion.identity, center, zone.radius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Change Water Zone Radius");
                zone.radius = Mathf.Max(1.0f, newRadius);
                zone.SyncLocalWaterMesh();
                EditorUtility.SetDirty(zone);
            }
        }
    }

    private static void DrawHeaderLabel(string text)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.2f, 0.75f, 1.0f) }
        };
        EditorGUILayout.LabelField(text, style);
    }
}
