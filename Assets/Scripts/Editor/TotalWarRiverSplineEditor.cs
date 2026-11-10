using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// TotalWarRiverSpline 혁신적 에디터 UI 및 마우스 스플라인 펜 툴
/// - [✏️ 마우스 클릭 펜 모드]: 씬 뷰 지형을 콕콕 클릭하기만 하면 실시간으로 S자 강줄기가 이어짐 (숫자 입력 필요 없음!)
/// - [🎯 직관적 노드 선택 & 이동]: 원하는 노드를 클릭하면 노란색으로 강조되며 마우스 핸들로 자유롭게 곡선 절곡
/// - [⏪ 원클릭 지형 되돌리기]: 파내기 직전 지형으로 1초 만에 100% 롤백
/// </summary>
[CustomEditor(typeof(TotalWarRiverSpline))]
public class TotalWarRiverSplineEditor : Editor
{
    private static bool _isPenMode = false;
    private static int _selectedNodeIndex = -1;
    private bool _showRawWaypoints = false;

    public override void OnInspectorGUI()
    {
        TotalWarRiverSpline river = (TotalWarRiverSpline)target;

        EditorGUILayout.Space(6);
        DrawHeaderLabel("🌊 삼국지 토탈워 굽이치는 스플라인 하천 시스템");

        // 1. 하천 현황 대시보드
        float totalLength = CalculateTotalLength(river);
        EditorGUILayout.HelpBox(
            $"📊 하천 제원: 총 {river.waypoints.Count}개 제어 노드 | 총 연장: {totalLength:F1} m | 하천 너비: {river.riverWidth:F1} m | 굴착 수심: {river.riverDepth:F1} m",
            MessageType.None);

        EditorGUILayout.Space(6);

        // 2. ✏️ 마우스 클릭 강줄기 펜 모드 (가장 중요한 혁신 기능!)
        GUI.backgroundColor = _isPenMode ? new Color(1.0f, 0.4f, 0.2f) : new Color(0.2f, 0.85f, 0.4f);
        string penBtnText = _isPenMode 
            ? "🛑 [펜 모드 활성 중] 지형 클릭 시 노드 추가! (종료: 여기 클릭 또는 ESC)" 
            : "✏️ [마우스 펜 모드] 씬 뷰를 클릭하여 강줄기 자유롭게 잇기 (추천!)";

        if (GUILayout.Button(penBtnText, GUILayout.Height(38)))
        {
            _isPenMode = !_isPenMode;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
        // 수면 기울어짐 감지 및 즉시 수평화 버튼
        Vector3 rotAngles = river.transform.rotation.eulerAngles;
        bool isTilted = Mathf.Abs(Mathf.DeltaAngle(rotAngles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(rotAngles.z, 0f)) > 0.05f;
        if (isTilted)
        {
            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.3f);
            if (GUILayout.Button("⚖️ 수면 완전 수평 맞추기 (Zero Tilt - 기울어짐 제거)", GUILayout.Height(30)))
            {
                Undo.RecordObject(river.transform, "Flatten River Transform");
                Undo.RecordObject(river, "Flatten River Spline");
                river.EnforceLevelTransform();
                river.GenerateRiverMesh();
                EditorUtility.SetDirty(river);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);
        }

        // 3. 퀵 노드 편집 버튼 툴바
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎯 지형 표면에 착지", GUILayout.Height(28)))
        {
            Undo.RecordObject(river, "Snap Waypoints to Terrain");
            SnapWaypointsToTerrain(river);
            river.GenerateRiverMesh();
            EditorUtility.SetDirty(river);
        }

        if (GUILayout.Button("+ 끝에 연장", GUILayout.Height(28)))
        {
            Undo.RecordObject(river, "Add River Waypoint");
            Vector3 lastPt = river.waypoints.Count > 0 ? river.waypoints[river.waypoints.Count - 1] : Vector3.zero;
            river.waypoints.Add(lastPt + Vector3.forward * 25f);
            river.GenerateRiverMesh();
            EditorUtility.SetDirty(river);
        }

        if (GUILayout.Button("- 마지막 삭제", GUILayout.Height(28)))
        {
            if (river.waypoints.Count > 2)
            {
                Undo.RecordObject(river, "Remove River Waypoint");
                river.waypoints.RemoveAt(river.waypoints.Count - 1);
                river.GenerateRiverMesh();
                EditorUtility.SetDirty(river);
            }
        }

        if (_selectedNodeIndex >= 0 && _selectedNodeIndex < river.waypoints.Count && river.waypoints.Count > 2)
        {
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button($"🗑️ 노드 #{_selectedNodeIndex + 1} 삭제", GUILayout.Height(28)))
            {
                Undo.RecordObject(river, "Delete Selected Node");
                river.waypoints.RemoveAt(_selectedNodeIndex);
                _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, river.waypoints.Count - 1);
                river.GenerateRiverMesh();
                EditorUtility.SetDirty(river);
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // 4. 하천 규격 슬라이더 (실시간 반응)
        EditorGUI.BeginChangeCheck();
        river.riverWidth = EditorGUILayout.Slider("하천 폭 (너비, m)", river.riverWidth, 2f, 80f);
        river.riverDepth = EditorGUILayout.Slider("굴착 수심 (깊이, m)", river.riverDepth, 0.5f, 10f);
        river.bankWidth = EditorGUILayout.Slider("강둑 경사 폭 (m)", river.bankWidth, 1f, 20f);
        river.surfaceYOffset = EditorGUILayout.Slider("수면 높이 오프셋 (m)", river.surfaceYOffset, 0.01f, 0.5f);
        river.uvTilingLength = EditorGUILayout.FloatField("물결 타일링 주기 (m)", river.uvTilingLength);
        river.riverMaterial = (Material)EditorGUILayout.ObjectField("수면 머티리얼", river.riverMaterial, typeof(Material), false);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(river, "Change River Spline Config");
            river.GenerateRiverMesh();
            EditorUtility.SetDirty(river);
        }

        EditorGUILayout.Space(10);

        // 5. 핵심 액션 버튼
        GUI.backgroundColor = new Color(0.15f, 0.75f, 1.0f);
        if (GUILayout.Button("🌊 강바닥 자동 파내기 (Carve) + 자갈 도색 + 잔디 배제", GUILayout.Height(42)))
        {
            if (EditorUtility.DisplayDialog("강바닥 파내기 확인",
                $"스플라인 궤적을 따라 터레인 고도를 U자형(너비: {river.riverWidth}m, 깊이: {river.riverDepth}m)으로 깎아내고, 바닥에 자갈/진흙 텍스처를 칠하며 수중 잔디를 클리어링합니다.\n(마음에 안 들면 언제든 아래 되돌리기 버튼으로 1초 만에 복원 가능)",
                "실행", "취소"))
            {
                Undo.RecordObject(river, "Carve Riverbed");
                river.CarveTerrainRiverbed();
                EditorUtility.SetDirty(river);
            }
        }

        EditorGUILayout.Space(4);

        // 이전 지형 되돌리기 버튼
        EditorGUI.BeginDisabledGroup(!river.HasBackup);
        GUI.backgroundColor = river.HasBackup ? new Color(1.0f, 0.6f, 0.2f) : Color.gray;
        if (GUILayout.Button(river.HasBackup ? "⏪ 파내기 전 이전 지형으로 1초 만에 되돌리기 (Undo)" : "⏪ 이전 지형 되돌리기 (백업 없음)", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog("지형 되돌리기 확인", "강바닥을 파내기 직전의 원래 지형(고도 + 텍스처 + 잔디)으로 완벽하게 복원하시겠습니까?", "복원 실행", "취소"))
            {
                Undo.RecordObject(river, "Restore Terrain Backup");
                river.RestoreTerrainBackup();
                EditorUtility.SetDirty(river);
            }
        }
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        // 6. 숫자기반 좌표 리스트 (접어두기)
        _showRawWaypoints = EditorGUILayout.Foldout(_showRawWaypoints, "⚙️ 상세 숫자 좌표 목록 (필요시 열기)");
        if (_showRawWaypoints)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < river.waypoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"노드 #{i + 1}", GUILayout.Width(60));
                Vector3 newPt = EditorGUILayout.Vector3Field("", river.waypoints[i]);
                if (newPt != river.waypoints[i])
                {
                    Undo.RecordObject(river, $"Edit Point {i}");
                    river.waypoints[i] = newPt;
                    river.GenerateRiverMesh();
                    EditorUtility.SetDirty(river);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
    }

    private void OnSceneGUI()
    {
        TotalWarRiverSpline river = (TotalWarRiverSpline)target;
        if (river == null || river.waypoints == null) return;

        Event e = Event.current;

        // 1. [✏️ 마우스 펜 모드 처리]
        if (_isPenMode)
        {
            // 씬 뷰 좌측 상단 배너 표시
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(15, 15, 380, 75), EditorStyles.helpBox);
            GUI.color = new Color(1.0f, 0.85f, 0.3f);
            GUILayout.Label("✏️ [스플라인 펜 모드 활성]", EditorStyles.boldLabel);
            GUI.color = Color.white;
            GUILayout.Label("지형 위를 클릭하면 그 위치에 새로운 강 노드가 잇따라 추가됩니다!\n종료: [ESC] 키 또는 우클릭");
            GUILayout.EndArea();
            Handles.EndGUI();

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _isPenMode = false;
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 1) // 우클릭 시 종료
            {
                _isPenMode = false;
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 2000f))
                {
                    Vector3 worldClick = hit.point;
                    Vector3 localClick = river.transform.InverseTransformPoint(worldClick);

                    Undo.RecordObject(river, "Pen Add River Node");
                    river.waypoints.Add(localClick);
                    _selectedNodeIndex = river.waypoints.Count - 1;
                    river.GenerateRiverMesh();
                    EditorUtility.SetDirty(river);
                    e.Use();
                }
            }
        }

        // 2. [노드 인터랙티브 시각화 및 드래그 조작]
        bool changed = false;

        for (int i = 0; i < river.waypoints.Count; i++)
        {
            Vector3 worldPt = river.transform.TransformPoint(river.waypoints[i]);
            bool isSelected = (i == _selectedNodeIndex);

            // 노드 구체 버튼 (마우스로 콕 찍어서 선택)
            float handleSize = HandleUtility.GetHandleSize(worldPt) * 0.22f;
            Handles.color = isSelected ? new Color(1.0f, 0.9f, 0.1f, 0.95f) : new Color(0.2f, 0.85f, 1.0f, 0.75f);

            if (Handles.Button(worldPt, Quaternion.identity, handleSize, handleSize * 1.3f, Handles.SphereHandleCap))
            {
                _selectedNodeIndex = i;
                Repaint();
            }

            // 라벨 표시
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = isSelected ? Color.yellow : Color.cyan }
            };
            Handles.Label(worldPt + Vector3.up * (handleSize * 1.8f), $"#{i + 1}", labelStyle);

            // 선택된 노드에만 3축 이동 핸들 표시 (난잡함 방지 & 초직관적 조작)
            if (isSelected && !_isPenMode)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPt = Handles.PositionHandle(worldPt, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(river, $"Move River Node {i}");
                    river.waypoints[i] = river.transform.InverseTransformPoint(newWorldPt);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            river.GenerateRiverMesh();
            EditorUtility.SetDirty(river);
        }
    }

    private static void SnapWaypointsToTerrain(TotalWarRiverSpline river)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        for (int i = 0; i < river.waypoints.Count; i++)
        {
            Vector3 worldPt = river.transform.TransformPoint(river.waypoints[i]);
            float h = terrain.SampleHeight(worldPt) + terrain.transform.position.y;
            worldPt.y = h;
            river.waypoints[i] = river.transform.InverseTransformPoint(worldPt);
        }
    }

    private static float CalculateTotalLength(TotalWarRiverSpline river)
    {
        if (river == null || river.waypoints == null || river.waypoints.Count < 2) return 0f;
        List<Vector3> pts = river.SampleSplinePointsWorld();
        float dist = 0f;
        for (int i = 0; i < pts.Count - 1; i++) dist += Vector3.Distance(pts[i], pts[i + 1]);
        return dist;
    }

    private static void DrawHeaderLabel(string text)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.15f, 0.75f, 1.0f) }
        };
        EditorGUILayout.LabelField(text, style);
    }
}
