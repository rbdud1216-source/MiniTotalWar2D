using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌾 [TotalWarRadialGrassShaderGUI]
    /// 삼국지 토탈워 3D 방사형 잔디 셰이더 전용 머티리얼 인스펙터 GUI
    /// 모든 프로퍼티를 100% 명확한 한국어로 표기하고, 마우스 호버 시 영문 전문용어 및 상세 튜닝 가이드 툴팁 제공
    /// </summary>
    public class TotalWarRadialGrassShaderGUI : ShaderGUI
    {
        private static bool _showBase = true;
        private static bool _showAlpha = true;
        private static bool _showLOD = true;
        private static bool _showWind = true;
        private static bool _showSurface = true;
        private static bool _showFeather = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material targetMat = materialEditor.target as Material;
            if (targetMat == null) return;

            EditorGUI.BeginChangeCheck();

            // 💡 상단 타이틀 배너
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            GUI.color = new Color(0.3f, 1.0f, 0.5f, 1.0f);
            EditorGUILayout.LabelField("🌾 삼탈워 3D 방사형 잔디 셰이더 (TotalWar Radial Grass LOD)", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.LabelField("마우스를 각 항목 위에 올리면 영문 전문용어와 상세 튜닝 안내 툴팁이 표시됩니다.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);

            // 1. 기본 텍스처 및 색조
            DrawCategoryHeader(ref _showBase, "🎨 기본 텍스처 및 색조 (Base Texture & Tint)", new Color(0.4f, 0.85f, 1.0f));
            if (_showBase)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProperty(materialEditor, properties, "_BaseMap", "기본 알베도 텍스처", "Albedo Map: 풀잎의 고해상도 색상 아틀라스 텍스처");
                DrawProperty(materialEditor, properties, "_BumpMap", "법선 굴곡 텍스처 (노멀)", "Normal Map: 잎사귀 표면의 미세 입체 굴곡을 표현하는 노멀 맵");
                DrawProperty(materialEditor, properties, "_BaseColor", "기본 색상 틴트", "Base Color Tint: 기본 텍스처 색상과 실시간 곱해지는 색조 조절");
                EditorGUILayout.EndVertical();
            }

            // 2. 투명도 컷아웃 및 렌더링 면
            DrawCategoryHeader(ref _showAlpha, "✂️ 투명도 컷아웃 및 단면/양면 (Alpha Cutout & Culling)", new Color(1.0f, 0.75f, 0.3f));
            if (_showAlpha)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProperty(materialEditor, properties, "_Cutoff", "알파 컷오프 임계값", "Alpha Cutoff: 잎사귀 윤곽선을 오려내는 투명도 기준값 (기본 0.25 ~ 0.35)");
                DrawProperty(materialEditor, properties, "_Cull", "단면 / 양면 렌더링", "Culling Mode: 0 = 앞뒤 양면 모두 렌더링(Both), 2 = 뒷면 제거 단면 렌더링(Back)");
                EditorGUILayout.EndVertical();
            }

            // 3. 풀잎 끝 칼자국 방지 소프트 테이퍼링
            DrawCategoryHeader(ref _showFeather, "🌿 풀잎 끝 칼자국 방지 소프트 테이퍼링 (Tip & Edge Feathering)", new Color(0.4f, 1.0f, 0.6f));
            if (_showFeather)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("텍스처 윗줄 모서리에 닿아 칼로 자른 듯 수평으로 잘리는 현상과 공중 파편을 100% 제거합니다.", MessageType.Info);
                DrawProperty(materialEditor, properties, "_TipFadeStart", "풀잎 끝 감쇄 시작점 (V)", "Tip Fade Start: 텍스처 V축에서 잎끝이 뾰족하게 오므라들기 시작하는 지점 (권장: 0.930 ~ 0.950)");
                DrawProperty(materialEditor, properties, "_TipFadeEnd", "풀잎 끝 완전 소멸점 (V)", "Tip Fade End: 텍스처 상단 테두리에 닿기 전 잎끝이 완전히 닫히는 지점 (권장: 0.980 ~ 0.985)");
                DrawProperty(materialEditor, properties, "_EdgeBleedCrop", "외곽 파편 차단 여백 (U)", "Edge Bleed Margin: 텍스처 아틀라스 좌우 경계 누수로 인해 공중에 둥둥 떠다니는 파편 조각 차단 마진 (권장: 0.006)");
                EditorGUILayout.EndVertical();
            }

            // 4. 거리별 LOD 페이드
            DrawCategoryHeader(ref _showLOD, "👁️ 거리별 디더링 LOD 페이드 (Distance LOD Fadeout)", new Color(0.9f, 0.6f, 1.0f));
            if (_showLOD)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProperty(materialEditor, properties, "_FadeStartDistance", "페이드 시작 거리 (미터)", "Fade Start Distance: 화면 디더링 페이드아웃이 시작되는 카메라 거리 (m)");
                DrawProperty(materialEditor, properties, "_FadeEndDistance", "완전 소멸 거리 (미터)", "Fade End Distance: 3D 풀이 100% 소멸하고 지형 PBR 텍스처로 완전히 전환되는 거리 (m)");
                EditorGUILayout.EndVertical();
            }

            // 5. 바람 물결 애니메이션
            DrawCategoryHeader(ref _showWind, "💨 바람 물결 애니메이션 (Wind Sway & Flutter)", new Color(0.4f, 0.9f, 1.0f));
            if (_showWind)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProperty(materialEditor, properties, "_WindMultiplier", "바람 흔들림 강도 배율", "Wind Sway Multiplier: 전역 바람에 의해 풀포기 전체가 흔들리는 배율");
                DrawProperty(materialEditor, properties, "_WindFrequency", "바람 파동 주기 주파수", "Wind Frequency: 바람 파동 물결이 지나가는 주기 및 진동수");
                DrawProperty(materialEditor, properties, "_LeafFlutter", "풀잎 끝자락 펄럭임 세기", "Leaf Flutter Strength: 개별 잎끝의 고주파 잔물결 진동 강도");
                EditorGUILayout.EndVertical();
            }

            // 6. 표면 물리 광택
            DrawCategoryHeader(ref _showSurface, "✨ 표면 광택 및 금속성 (Surface Smoothness & Metallic)", new Color(0.8f, 0.8f, 0.8f));
            if (_showSurface)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProperty(materialEditor, properties, "_Smoothness", "표면 매끄러움 (광택)", "Smoothness: 표면 반사광 하이라이트 강도 (비올 때 윤기)");
                DrawProperty(materialEditor, properties, "_Metallic", "금속성 (메탈릭)", "Metallic: 금속성 반사도 (식물은 0.0 권장)");
                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetMat);
            }
        }

        private static void DrawCategoryHeader(ref bool foldout, string label, Color headerCol)
        {
            EditorGUILayout.Space(2);
            GUI.color = headerCol;
            foldout = EditorGUILayout.Foldout(foldout, label, true, EditorStyles.foldoutHeader);
            GUI.color = Color.white;
        }

        private static void DrawProperty(MaterialEditor editor, MaterialProperty[] properties, string propName, string labelKr, string tooltipEn)
        {
            MaterialProperty prop = FindProperty(propName, properties, false);
            if (prop != null)
            {
                GUIContent content = new GUIContent(labelKr, tooltipEn);
                editor.ShaderProperty(prop, content);
            }
        }
    }
}

