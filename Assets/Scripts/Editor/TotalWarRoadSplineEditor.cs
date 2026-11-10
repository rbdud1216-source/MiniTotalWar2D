using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 🛣️ TotalWarRoadSpline 커스텀 에디터
/// - [✏️ 마우스 클릭 도로 펜 모드]: 씬 뷰 지형을 클릭하여 마우스 궤적으로 도로 잇기 + 실시간 3D 메쉬 생성
/// - 씬 뷰 개별 노드 인터랙티브 클릭 선택 및 3축 PositionHandle 연동
/// - [🛣️ 도로 지형 자동 평탄화 (Flatten) + 황토길 도색 + 잔디 배제] 원클릭 0.05초 초고속 실행
/// - [⏪ 평탄화 전 이전 지형으로 되돌리기 (Undo)] 1초 만에 100% 원복
/// </summary>
[CustomEditor(typeof(TotalWarRoadSpline))]
public class TotalWarRoadSplineEditor : Editor
{
    private static bool _isPenMode = false;
    private static int _selectedNodeIndex = -1;
    private bool _showRawWaypoints = false;

    public override void OnInspectorGUI()
    {
        TotalWarRoadSpline road = (TotalWarRoadSpline)target;

        EditorGUILayout.Space(6);
        DrawHeaderLabel("🛣️ 삼국지 토탈워 군사 도로망 시스템 (Road Spline)");

        // 1. 도로 요약 대시보드
        float totalLength = CalculateRoadLength(road);
        EditorGUILayout.HelpBox($"총 {road.waypoints.Count}개 도로 노드 | 도로 총 연장: {totalLength:F1} m | 도로 폭: {road.roadWidth:F1} m | 갓길: {road.shoulderWidth:F1} m", MessageType.Info);

        EditorGUILayout.Space(6);

        // 🏛️ [도로 표면 스타일 원클릭 토글 툴바]
        EditorGUILayout.LabelField("🛣️ 도로 표면 스타일 선택 (삼탈워 야전 고증 원클릭 토글)", EditorStyles.boldLabel);
        string[] styleLabels = new string[]
        {
            "🌿 기후 테마 연동\n(기본 권장)",
            "🪨 군용 자갈길\n(Gravel)",
            "🌾 다져진 흙길\n(Dirt)"
        };

        int currentStyle = (int)road.surfaceType;
        int selectedStyle = GUILayout.Toolbar(currentStyle, styleLabels, GUILayout.Height(38));
        if (selectedStyle != currentStyle)
        {
            Undo.RecordObject(road, "Change Road Surface Type");
            road.SetSurfaceType((RoadSurfaceType)selectedStyle);
            EditorUtility.SetDirty(road);
        }

        // 현재 기후 연동 상세 상태 표시
        if (road.surfaceType == RoadSurfaceType.ClimateSync)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"🌿 [기후 테마 연동 중] {road.GetCurrentClimateInfo()}\n터레인 기후 테마가 바뀌면 도로 Albedo/Normal/도색 레이어가 자동으로 즉각 동기화됩니다.", MessageType.Info);
            if (GUILayout.Button("🔄 재동기화", GUILayout.Width(75), GUILayout.Height(38)))
            {
                road.SyncWithTerrainClimate();
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("🌿 터레인 기후 테마 동기화 완료!"));
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox($"고정 스타일 적용 중: {road.surfaceType} (도색 레이어 Slot #{road.roadLayerIndex})\n언제든 첫 번째 '🌿 기후 테마 연동' 버튼을 누르면 터레인 기후와 일체화됩니다.", MessageType.None);
        }

        EditorGUILayout.Space(6);

        // 2. [마우스 도로 펜 모드] 토글 버튼
        GUI.backgroundColor = _isPenMode ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.9f, 0.9f);
        string penText = _isPenMode ? "✏️ [마우스 펜 모드 활성 중!] 지형을 클릭하여 도로를 이어가세요" : "✏️ [마우스 펜 모드] 씬 뷰를 클릭하여 도로 잇기 (추천!)";
        if (GUILayout.Button(penText, GUILayout.Height(34)))
        {
            _isPenMode = !_isPenMode;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;

        // 수평 보정 버튼
        Vector3 rotAngles = road.transform.rotation.eulerAngles;
        bool isTilted = Mathf.Abs(Mathf.DeltaAngle(rotAngles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(rotAngles.z, 0f)) > 0.05f;
        if (isTilted)
        {
            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.3f);
            if (GUILayout.Button("⚖️ 수면 완전 수평 맞추기 (Zero Tilt - 기울어짐 제거)", GUILayout.Height(30)))
            {
                Undo.RecordObject(road.transform, "Flatten Road Transform");
                Undo.RecordObject(road, "Flatten Road Spline");
                road.EnforceLevelTransform();
                road.GenerateRoadMesh();
                EditorUtility.SetDirty(road);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);
        }

        // 3. 퀵 노드 편집 버튼 툴바
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎯 지형 표면에 착지", GUILayout.Height(28)))
        {
            Undo.RecordObject(road, "Snap Waypoints to Terrain");
            SnapWaypointsToTerrain(road);
            road.GenerateRoadMesh();
            EditorUtility.SetDirty(road);
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("🎯 모든 도로 노드가 지형 표면에 밀착되었습니다!"));
        }

        if (GUILayout.Button("+ 끝에 연장", GUILayout.Height(28)))
        {
            Undo.RecordObject(road, "Add Road Waypoint");
            Vector3 lastPt = road.waypoints.Count > 0 ? road.waypoints[road.waypoints.Count - 1] : Vector3.zero;
            road.waypoints.Add(lastPt + Vector3.forward * 20f);
            road.GenerateRoadMesh();
            EditorUtility.SetDirty(road);
        }

        if (GUILayout.Button("- 마지막 삭제", GUILayout.Height(28)))
        {
            if (road.waypoints.Count > 2)
            {
                Undo.RecordObject(road, "Remove Road Waypoint");
                road.waypoints.RemoveAt(road.waypoints.Count - 1);
                road.GenerateRoadMesh();
                EditorUtility.SetDirty(road);
            }
        }

        if (_selectedNodeIndex >= 0 && _selectedNodeIndex < road.waypoints.Count && road.waypoints.Count > 2)
        {
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button($"🗑️ 노드 #{_selectedNodeIndex + 1} 삭제", GUILayout.Height(28)))
            {
                Undo.RecordObject(road, "Delete Selected Node");
                road.waypoints.RemoveAt(_selectedNodeIndex);
                _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, road.waypoints.Count - 1);
                road.GenerateRoadMesh();
                EditorUtility.SetDirty(road);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // 4. 인스펙터 기본 프로퍼티
        EditorGUI.BeginChangeCheck();
        SerializedProperty propRoadWidth = serializedObject.FindProperty("roadWidth");
        SerializedProperty propShoulderWidth = serializedObject.FindProperty("shoulderWidth");
        SerializedProperty propFlattenStrength = serializedObject.FindProperty("flattenStrength");
        SerializedProperty propRoadLayer = serializedObject.FindProperty("roadLayerIndex");
        SerializedProperty propRes = serializedObject.FindProperty("resolutionPerSegment");

        EditorGUILayout.LabelField("🛣️ 도로 규격 및 평탄화 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(propRoadWidth, new GUIContent("도로 폭 (너비, m)"));
        EditorGUILayout.PropertyField(propShoulderWidth, new GUIContent("갓길 경사 폭 (m)"));
        EditorGUILayout.PropertyField(propFlattenStrength, new GUIContent("평탄화 강도"));
        EditorGUILayout.PropertyField(propRoadLayer, new GUIContent("도색 레이어 (Layer 2)"));
        EditorGUILayout.PropertyField(propRes, new GUIContent("스플라인 곡선 해상도"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎨 3D 도로 메쉬 설정 (시각적 리본 메쉬)", EditorStyles.boldLabel);
        SerializedProperty propShowMesh = serializedObject.FindProperty("showRoadMesh");
        SerializedProperty propYOffset = serializedObject.FindProperty("surfaceYOffset");
        SerializedProperty propUVTiling = serializedObject.FindProperty("uvTilingLength");
        SerializedProperty propRoadMat = serializedObject.FindProperty("roadMaterial");

        EditorGUILayout.PropertyField(propShowMesh, new GUIContent("3D 도로 메쉬 표시"));
        if (road.showRoadMesh)
        {
            EditorGUILayout.PropertyField(propYOffset, new GUIContent("지형 위 띄움 높이 (m)"));
            EditorGUILayout.PropertyField(propUVTiling, new GUIContent("UV 반복 길이 (m)"));
            EditorGUILayout.PropertyField(propRoadMat, new GUIContent("도로 머티리얼"));
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            road.GenerateRoadMesh();
            EditorUtility.SetDirty(road);
        }

        EditorGUILayout.Space(10);

        // 5. 핵심 지형 평탄화 및 황토길 도색 버튼 (원클릭 즉시 실행!)
        GUI.backgroundColor = new Color(0.15f, 0.65f, 0.95f);
        if (GUILayout.Button("🛣️ 도로 지형 자동 평탄화 (Flatten) + 황토길 도색 + 잔디 배제", GUILayout.Height(38)))
        {
            Undo.RecordObject(road, "Carve And Paint Road");
            road.CarveAndPaintRoad();
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("🛣️ 도로 평탄화 및 황토길 도색 완료!"));
        }

        // 6. 원클릭 되돌리기 버튼
        if (road.HasBackup)
        {
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.15f);
            if (GUILayout.Button("⏪ 평탄화 전 이전 지형으로 1초 만에 되돌리기 (Undo)", GUILayout.Height(32)))
            {
                road.RestoreTerrainBackup();
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("⏪ 이전 지형으로 원상복구 완료!"));
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        // 7. 상세 숫자 좌표 목록 (접기)
        _showRawWaypoints = EditorGUILayout.Foldout(_showRawWaypoints, "⚙️ 상세 숫자 좌표 목록 (필요시 열기)");
        if (_showRawWaypoints)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < road.waypoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"노드 #{i + 1}", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                Vector3 newPt = EditorGUILayout.Vector3Field("", road.waypoints[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(road, "Edit Road Waypoint");
                    road.waypoints[i] = newPt;
                    road.GenerateRoadMesh();
                    EditorUtility.SetDirty(road);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
    }

    private void OnSceneGUI()
    {
        TotalWarRoadSpline road = (TotalWarRoadSpline)target;
        Event currentEvent = Event.current;

        // ✏️ [마우스 도로 펜 모드]
        if (_isPenMode)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && !currentEvent.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 5000f))
                {
                    Undo.RecordObject(road, "Pen Click Add Road Node");
                    Vector3 localPos = road.transform.InverseTransformPoint(hit.point);
                    road.waypoints.Add(localPos);
                    _selectedNodeIndex = road.waypoints.Count - 1;
                    road.GenerateRoadMesh();
                    EditorUtility.SetDirty(road);
                    currentEvent.Use();
                }
            }
        }

        if (road.waypoints == null || road.waypoints.Count == 0) return;

        // 씬 뷰 제어점 기즈모 및 개별 노드 클릭 선택
        for (int i = 0; i < road.waypoints.Count; i++)
        {
            Vector3 worldPos = road.transform.TransformPoint(road.waypoints[i]);
            float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.16f;

            bool isSelected = (_selectedNodeIndex == i);
            Handles.color = isSelected ? Color.yellow : new Color(0.95f, 0.7f, 0.2f, 0.85f);

            if (Handles.Button(worldPos, Quaternion.identity, handleSize, handleSize * 1.3f, Handles.SphereHandleCap))
            {
                _selectedNodeIndex = i;
                Repaint();
            }

            Handles.Label(worldPos + Vector3.up * handleSize * 1.4f, $"#{i + 1}");

            // 선택된 노드에만 3축 이동 핸들 표시
            if (isSelected && !_isPenMode)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(road, "Move Road Waypoint");
                    road.waypoints[i] = road.transform.InverseTransformPoint(newWorldPos);
                    road.GenerateRoadMesh();
                    EditorUtility.SetDirty(road);
                }
            }
        }
    }

    private void SnapWaypointsToTerrain(TotalWarRoadSpline road)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) return;

        for (int i = 0; i < road.waypoints.Count; i++)
        {
            Vector3 worldPos = road.transform.TransformPoint(road.waypoints[i]);
            float h = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
            worldPos.y = h;
            road.waypoints[i] = road.transform.InverseTransformPoint(worldPos);
        }
    }

    private float CalculateRoadLength(TotalWarRoadSpline road)
    {
        List<Vector3> pts = road.SampleSplinePointsWorld();
        if (pts.Count < 2) return 0f;

        float len = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            len += Vector3.Distance(pts[i], pts[i + 1]);
        }
        return len;
    }

    private void DrawHeaderLabel(string text)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.9f, 0.75f, 0.35f) }
        };
        EditorGUILayout.LabelField(text, style);
    }
}
