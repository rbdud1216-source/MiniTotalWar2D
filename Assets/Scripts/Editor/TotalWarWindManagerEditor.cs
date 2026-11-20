using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🍃 [TotalWarWindManagerEditor]
    /// 전장 바람 매니저(TotalWarWindManager)의 100% 한국어 커스텀 인스펙터
    /// 원클릭 바람 세기 프리셋(무풍~태풍/광풍), 8방위 풍향 표시, 실시간 돌풍 계기판을 제공합니다.
    /// </summary>
    [CustomEditor(typeof(TotalWarWindManager))]
    public class TotalWarWindManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _windAngleProp;
        private SerializedProperty _windSpeedProp;
        private SerializedProperty _windStrengthProp;
        private SerializedProperty _enableGustProp;
        private SerializedProperty _gustFrequencyProp;
        private SerializedProperty _gustMultiplierProp;

        private void OnEnable()
        {
            _windAngleProp = serializedObject.FindProperty("windAngle");
            _windSpeedProp = serializedObject.FindProperty("windSpeed");
            _windStrengthProp = serializedObject.FindProperty("windStrength");
            _enableGustProp = serializedObject.FindProperty("enableGust");
            _gustFrequencyProp = serializedObject.FindProperty("gustFrequency");
            _gustMultiplierProp = serializedObject.FindProperty("gustMultiplier");
        }

        public override void OnInspectorGUI()
        {
            TotalWarWindManager mgr = (TotalWarWindManager)target;
            if (mgr == null) return;

            serializedObject.Update();

            // 1. 상단 타이틀 배너
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            GUI.color = new Color(0.45f, 0.92f, 1.0f, 1.0f);
            EditorGUILayout.LabelField("🍃 삼국지 토탈워 전장 바람 매니저 (Wind Manager)", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.LabelField("전장 전체의 풀밭·수목·화재 연기에 실시간 풍향과 강풍·돌풍을 동기화합니다.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // 2. 실시간 바람 상태 계기판
            mgr.UpdateWindShaderGlobals();
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 실시간 전장 바람 상태", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• 진행 방향: {mgr.GetCompassDirectionKorean()} ({mgr.windAngle:F0}°)");
            EditorGUILayout.LabelField($"• 현재 실효 강도: {mgr.CurrentEffectiveStrength:F2}  (기본 {mgr.windStrength:F2} x 돌풍 {mgr.CurrentGustFactor:F2}배)");
            EditorGUILayout.LabelField($"• 물결 속도: {mgr.windSpeed:F2}");
            EditorGUILayout.EndVertical();

            // 3. 원클릭 바람 세기 프리셋
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ 원클릭 바람 세기 프리셋", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawPresetButton(mgr, "무풍", TotalWarWindManager.WindPreset.Calm, new Color(0.85f, 0.85f, 0.85f));
            DrawPresetButton(mgr, "산들바람", TotalWarWindManager.WindPreset.GentleBreeze, new Color(0.65f, 1.0f, 0.75f));
            DrawPresetButton(mgr, "선선한 바람", TotalWarWindManager.WindPreset.FreshWind, new Color(0.60f, 0.92f, 1.0f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawPresetButton(mgr, "강풍", TotalWarWindManager.WindPreset.StrongGale, new Color(1.0f, 0.88f, 0.45f));
            DrawPresetButton(mgr, "폭풍", TotalWarWindManager.WindPreset.Storm, new Color(1.0f, 0.65f, 0.35f));
            DrawPresetButton(mgr, "태풍 · 광풍", TotalWarWindManager.WindPreset.Typhoon, new Color(1.0f, 0.45f, 0.45f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // 4. 풍향 및 풍속 세부 설정
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌬️ 풍향 및 풍속 설정", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Slider(
                _windAngleProp, 0f, 360f,
                new GUIContent("풍향 (불어가는 각도 °)", "Wind Angle: 바람이 불어가는 나침반 각도 (0°=북, 90°=동, 180°=남, 270°=서)")
            );

            // 빠른 4방위 선택 버튼
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("빠른 방향 선택");
            if (GUILayout.Button("북 (0°)")) SetWindAngle(mgr, 0f);
            if (GUILayout.Button("동 (90°)")) SetWindAngle(mgr, 90f);
            if (GUILayout.Button("남 (180°)")) SetWindAngle(mgr, 180f);
            if (GUILayout.Button("서 (270°)")) SetWindAngle(mgr, 270f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Slider(
                _windSpeedProp, 0.0f, 12.0f,
                new GUIContent("바람 속도 (물결 주기)", "Wind Speed: 풀밭 바람 물결과 나뭇가지 흔들림의 이동 속도 (0=정지 ~ 12=광풍)")
            );

            EditorGUILayout.Slider(
                _windStrengthProp, 0.0f, 10.0f,
                new GUIContent("바람 강도 (휘어짐 세기)", "Wind Strength: 풀과 나무가 바람 방향으로 눕고 휘어지는 물리적 강도 (0=무풍, 0.45=산들바람, 3.5=강풍, 9.0=태풍)")
            );

            EditorGUILayout.EndVertical();

            // 5. 돌풍 시뮬레이션 세부 설정
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💨 돌풍 시뮬레이션", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                _enableGustProp,
                new GUIContent("돌풍 활성화", "Enable Gust: 펄린 노이즈 기반으로 강약이 불규칙하게 몰아치는 돌풍 효과 사용 여부")
            );

            using (new EditorGUI.DisabledScope(!_enableGustProp.boolValue))
            {
                EditorGUILayout.Slider(
                    _gustFrequencyProp, 0.05f, 4.0f,
                    new GUIContent("돌풍 변화 빈도", "Gust Frequency: 돌풍이 강해졌다 약해지는 변화 주기 속도")
                );

                EditorGUILayout.Slider(
                    _gustMultiplierProp, 1.0f, 5.0f,
                    new GUIContent("돌풍 최대 배율", "Gust Multiplier: 돌풍이 정점에 달했을 때 기본 바람 강도에 곱해지는 최대 배율")
                );
            }

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck() || serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                mgr.UpdateWindShaderGlobals();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            // 돌풍 실시간 계기판 갱신
            if (mgr.enableGust && mgr.windStrength > 0.001f)
            {
                Repaint();
            }
        }

        private void DrawPresetButton(TotalWarWindManager mgr, string label, TotalWarWindManager.WindPreset preset, Color tint)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                Undo.RecordObject(mgr, $"바람 프리셋 적용: {label}");
                mgr.ApplyWindPreset(preset);
                serializedObject.Update();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
        }

        private void SetWindAngle(TotalWarWindManager mgr, float angle)
        {
            Undo.RecordObject(mgr, $"풍향 변경: {angle}°");
            _windAngleProp.floatValue = angle;
            serializedObject.ApplyModifiedProperties();
            mgr.UpdateWindShaderGlobals();
            EditorUtility.SetDirty(mgr);
            SceneView.RepaintAll();
        }
    }
}
