using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌊 [TotalWarWaterShaderGUI]
    /// 실사 물리 기반 삼국지 토탈워 URP 수면 셰이더(TotalWarWater.shader) 전용 100% 한국어 인스펙터
    /// 원클릭 수계 프리셋(실사 바다/호수, 맑은 중원 강물, 탁한 남만 늪지, 겨울 빙해)과 상세 물리 슬라이더 제공
    /// </summary>
    public class TotalWarWaterShaderGUI : ShaderGUI
    {
        private static bool _showColor = true;
        private static bool _showWaves = true;
        private static bool _showReflection = true;
        private static bool _showFoam = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material mat = materialEditor.target as Material;
            if (mat == null) return;

            EditorGUI.BeginChangeCheck();

            // 상단 배너
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            GUI.color = new Color(0.35f, 0.85f, 1.0f, 1.0f);
            EditorGUILayout.LabelField("🌊 삼국지 토탈워 실사 물리 수면 셰이더 (TotalWar Water PBR)", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.LabelField("Beer-Lambert 수심 흡수, 화면 굴절, Gerstner 파도, 커스틱스, 바람 연동 및 연안 포말 지원", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // 원클릭 실사 프리셋
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ 원클릭 실사 수계 프리셋", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌊 실사 바다·호수", GUILayout.Height(24))) ApplySeaPreset(mat);
            if (GUILayout.Button("🏞️ 맑은 강물 (유속)", GUILayout.Height(24))) ApplyRiverPreset(mat);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌿 탁한 늪지·연못", GUILayout.Height(24))) ApplySwampPreset(mat);
            if (GUILayout.Button("❄️ 차가운 겨울 빙해", GUILayout.Height(24))) ApplyWinterSeaPreset(mat);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // 1. 수계 모드 및 색상·굴절·투명도
            DrawHeader(ref _showColor, "🎨 수계 유형 · 수심 광흡수 및 수중 굴절", new Color(0.45f, 0.90f, 1.0f));
            if (_showColor)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProp(materialEditor, properties, "_FlowMode", "수계 모드 (0:바다, 1:강물, 2:늪지)", "Flow Mode: 0=바다/호수 파도, 1=강줄기 하류 유속, 2=탁한 늪지");
                DrawProp(materialEditor, properties, "_FlowSpeed", "강물 유속 속도", "Flow Speed: 강물 모드일 때 상류에서 하류로 흐르는 물살 속도");
                DrawProp(materialEditor, properties, "_ShallowColor", "얕은 물 산란색", "Shallow Color: 수심이 얕은 연안/여울목의 산란 색감");
                DrawProp(materialEditor, properties, "_DeepColor", "깊은 물 심해색", "Deep Color: 깊은 바다나 심연의 중후한 색상");
                DrawProp(materialEditor, properties, "_DepthDistance", "수심 색상 전환 거리 (m)", "Depth Fade Distance: 얕은 물에서 깊은 물 색으로 전환되는 수심 기준");
                DrawProp(materialEditor, properties, "_Clarity", "수질 투명도 (높을수록 맑음)", "Clarity: Beer-Lambert 광흡수 기준 투명도 (바다 3~5, 늪지 0.3~0.8)");
                DrawProp(materialEditor, properties, "_RefractionStrength", "수중 굴절 왜곡 강도", "Refraction Strength: 물결에 의해 물속 바닥이 일렁이며 굴절되는 강도");
                DrawProp(materialEditor, properties, "_WetBankDarken", "물가 젖은 바닥 어두움", "Wet Bank Darkening: 얕게 잠긴 모래/자갈이 젖어 어두워지는 강도");
                EditorGUILayout.EndVertical();
            }

            // 2. 파도 및 물결 노멀
            DrawHeader(ref _showWaves, "🌊 파도 기복(Gerstner) 및 다중 잔물결", new Color(0.4f, 0.75f, 1.0f));
            if (_showWaves)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProp(materialEditor, properties, "_GerstnerAmp", "입체 파도 기복 높이", "Gerstner Amplitude: 수면 메쉬 자체가 출렁이는 입체 파도 진폭 (바람 매니저 자동 연동)");
                DrawProp(materialEditor, properties, "_WaveChoppiness", "파도 거칠기 (뾰족함)", "Wave Choppiness: 파도 능선이 뾰족하게 솟구치는 정도");
                DrawProp(materialEditor, properties, "_WaveScale", "잔물결 밀도 스케일", "Wave Scale: 3중 절차적 물결 노멀의 크기 배율");
                DrawProp(materialEditor, properties, "_WaveStrength", "물결 굴곡 강도", "Wave Normal Strength: 표면 잔물결의 입체 굴곡 세기");
                DrawProp(materialEditor, properties, "_BumpMap", "추가 노멀맵 (선택 사항)", "Normal Map: 비워두어도 3중 절차적 실사 잔물결이 기본 작동합니다");
                EditorGUILayout.EndVertical();
            }

            // 3. 하늘 반사 · 프레넬 · 태양 반짝임 · 투과광
            DrawHeader(ref _showReflection, "☀️ 하늘 반사 · 태양 반짝임 및 파도 투과광(SSS)", new Color(1.0f, 0.85f, 0.45f));
            if (_showReflection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProp(materialEditor, properties, "_ReflectionIntensity", "하늘·환경 반사 강도", "Reflection Intensity: 스카이박스 및 대기 반사 광도");
                DrawProp(materialEditor, properties, "_SkyReflectionTint", "하늘 반사 기본 색조", "Sky Reflection Tint: 대기/하늘 반사의 기본 틴트");
                DrawProp(materialEditor, properties, "_FresnelPower", "프레넬 반사 지수", "Fresnel Power: 높을수록 비스듬한 각도에서만 거울처럼 반사되고 위에서는 맑게 투과");
                DrawProp(materialEditor, properties, "_Smoothness", "표면 매끄러움 (광택)", "Smoothness: 수면 반사의 선명도");
                DrawProp(materialEditor, properties, "_SunSpecularPower", "태양 반짝임 선명도", "Sun Specular Power: 수면 위 햇빛 반짝임 점의 예리함");
                DrawProp(materialEditor, properties, "_SunSpecularIntensity", "태양 반짝임 강도", "Sun Specular Intensity: 태양 직사광 하이라이트 밝기");
                DrawProp(materialEditor, properties, "_SubsurfaceColor", "파도 햇살 투과색 (SSS)", "Subsurface Color: 파도 능선을 역광으로 볼 때 비치는 청록빛");
                DrawProp(materialEditor, properties, "_SubsurfaceStrength", "파도 햇살 투과 세기", "Subsurface Strength: 파도 역광 산란 강도");
                EditorGUILayout.EndVertical();
            }

            // 4. 커스틱스 빛망울 및 연안 파도 거품
            DrawHeader(ref _showFoam, "🫧 수중 빛망울(커스틱스) 및 연안 포말", new Color(0.75f, 0.98f, 0.90f));
            if (_showFoam)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawProp(materialEditor, properties, "_CausticsStrength", "수중 커스틱스 빛망울 세기", "Caustics Strength: 얕은 물속 바닥에 일렁이는 햇살 그물 무늬 강도");
                DrawProp(materialEditor, properties, "_CausticsScale", "커스틱스 무늬 크기", "Caustics Scale: 수중 빛망울 패턴 밀도");
                DrawProp(materialEditor, properties, "_FoamColor", "파도 거품 색상", "Foam Color: 연안 포말과 흰파도의 기본 색");
                DrawProp(materialEditor, properties, "_FoamDistance", "연안 포말 폭 (m)", "Foam Distance: 물가에서 포말이 밀려드는 수심 폭");
                DrawProp(materialEditor, properties, "_FoamIntensity", "연안 포말 강도", "Foam Intensity: 해안선 파도 거품의 진하기");
                DrawProp(materialEditor, properties, "_FoamNoiseScale", "포말 노이즈 밀도", "Foam Noise Scale: 거품이 부서지는 패턴의 세밀함");
                DrawProp(materialEditor, properties, "_WhitecapThreshold", "강풍 흰파도 임계값", "Whitecap Threshold: 낮을수록 강풍 시 파도 마루에 흰 거품이 쉽게 발생");
                DrawProp(materialEditor, properties, "_EdgeSoftness", "물가 경계 부드러움 (m)", "Edge Softness: 물과 땅의 접합면이 칼자국 없이 스며드는 폭");
                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(mat);
                SceneView.RepaintAll();
            }
        }

        private static void ApplySeaPreset(Material mat)
        {
            Undo.RecordObject(mat, "실사 대양·호수 프리셋 적용");
            mat.SetFloat("_FlowMode", 0f);
            mat.SetColor("_ShallowColor", new Color(0.22f, 0.50f, 0.58f, 0.35f));
            mat.SetColor("_DeepColor", new Color(0.04f, 0.22f, 0.42f, 1.0f));
            mat.SetColor("_SkyReflectionTint", new Color(0.48f, 0.72f, 0.95f, 1.0f));
            mat.SetFloat("_DepthDistance", 5.5f);
            mat.SetFloat("_Clarity", 5.5f);
            mat.SetFloat("_RefractionStrength", 0.014f);
            mat.SetFloat("_GerstnerAmp", 0.09f);
            mat.SetFloat("_WaveStrength", 0.28f);
            mat.SetFloat("_ReflectionIntensity", 0.95f);
            mat.SetFloat("_CausticsStrength", 0.0f);
            mat.SetFloat("_FoamDistance", 0.14f);
            mat.SetFloat("_FoamIntensity", 0.22f);
            EditorUtility.SetDirty(mat);
            SceneView.RepaintAll();
        }

        private static void ApplyRiverPreset(Material mat)
        {
            Undo.RecordObject(mat, "맑은 강물 프리셋 적용");
            mat.SetFloat("_FlowMode", 1f);
            mat.SetFloat("_FlowSpeed", 0.55f);
            mat.SetColor("_ShallowColor", new Color(0.20f, 0.42f, 0.38f, 0.28f));
            mat.SetColor("_DeepColor", new Color(0.06f, 0.22f, 0.26f, 0.94f));
            mat.SetColor("_SkyReflectionTint", new Color(0.46f, 0.68f, 0.85f, 1.0f));
            mat.SetFloat("_DepthDistance", 3.8f);
            mat.SetFloat("_Clarity", 7.5f);
            mat.SetFloat("_RefractionStrength", 0.012f);
            mat.SetFloat("_GerstnerAmp", 0.01f);
            mat.SetFloat("_WaveStrength", 0.20f);
            mat.SetFloat("_ReflectionIntensity", 0.65f);
            mat.SetFloat("_CausticsStrength", 0.12f);
            mat.SetFloat("_FoamDistance", 0.08f);
            mat.SetFloat("_FoamIntensity", 0.12f);
            EditorUtility.SetDirty(mat);
            SceneView.RepaintAll();
        }

        private static void ApplySwampPreset(Material mat)
        {
            Undo.RecordObject(mat, "탁한 늪지 프리셋 적용");
            mat.SetFloat("_FlowMode", 2f);
            mat.SetColor("_ShallowColor", new Color(0.18f, 0.22f, 0.12f, 0.75f));
            mat.SetColor("_DeepColor", new Color(0.06f, 0.09f, 0.05f, 0.98f));
            mat.SetFloat("_DepthDistance", 2.0f);
            mat.SetFloat("_Clarity", 0.55f);
            mat.SetFloat("_RefractionStrength", 0.012f);
            mat.SetFloat("_GerstnerAmp", 0.0f);
            mat.SetFloat("_WaveStrength", 0.35f);
            mat.SetFloat("_ReflectionIntensity", 0.45f);
            mat.SetFloat("_CausticsStrength", 0.08f);
            mat.SetFloat("_FoamDistance", 0.25f);
            mat.SetFloat("_FoamIntensity", 0.20f);
            EditorUtility.SetDirty(mat);
            SceneView.RepaintAll();
        }

        private static void ApplyWinterSeaPreset(Material mat)
        {
            Undo.RecordObject(mat, "겨울 빙해 프리셋 적용");
            mat.SetFloat("_FlowMode", 0f);
            mat.SetColor("_ShallowColor", new Color(0.22f, 0.36f, 0.42f, 0.55f));
            mat.SetColor("_DeepColor", new Color(0.02f, 0.07f, 0.13f, 0.98f));
            mat.SetFloat("_DepthDistance", 5.5f);
            mat.SetFloat("_Clarity", 2.6f);
            mat.SetFloat("_RefractionStrength", 0.028f);
            mat.SetFloat("_GerstnerAmp", 0.16f);
            mat.SetFloat("_WaveStrength", 0.80f);
            mat.SetFloat("_ReflectionIntensity", 1.05f);
            mat.SetFloat("_CausticsStrength", 0.30f);
            mat.SetFloat("_FoamDistance", 0.80f);
            mat.SetFloat("_FoamIntensity", 0.80f);
            EditorUtility.SetDirty(mat);
            SceneView.RepaintAll();
        }

        private static void DrawHeader(ref bool foldout, string label, Color color)
        {
            EditorGUILayout.Space(2);
            GUI.color = color;
            foldout = EditorGUILayout.Foldout(foldout, label, true, EditorStyles.foldoutHeader);
            GUI.color = Color.white;
        }

        private static void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name, string labelKr, string tooltip)
        {
            MaterialProperty p = FindProperty(name, props, false);
            if (p != null)
            {
                editor.ShaderProperty(p, new GUIContent(labelKr, tooltip));
            }
        }
    }
}
