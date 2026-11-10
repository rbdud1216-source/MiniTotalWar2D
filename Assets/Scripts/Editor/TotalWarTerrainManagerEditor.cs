using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MiniTotalWar.Editor
{
    [CustomEditor(typeof(TotalWarTerrainManager))]
    public class TotalWarTerrainManagerEditor : UnityEditor.Editor
    {
        [MenuItem("TotalWar/🌾 터레인 매니저 청크별 지피식생 즉시 빌드 및 저장", false, 3)]
        public static void BakeChunkFoliageMenu()
        {
            var mgr = Object.FindAnyObjectByType<TotalWarTerrainManager>();
            if (mgr == null)
            {
                Terrain t = Terrain.activeTerrain != null ? Terrain.activeTerrain : Object.FindAnyObjectByType<Terrain>();
                if (t != null)
                {
                    mgr = t.GetComponent<TotalWarTerrainManager>();
                    if (mgr == null) mgr = t.gameObject.AddComponent<TotalWarTerrainManager>();
                }
            }

            if (mgr == null)
            {
                EditorUtility.DisplayDialog("경고", "씬에 Terrain 또는 TotalWarTerrainManager가 없습니다.", "확인");
                return;
            }

            mgr.BakeChunkFoliage();
            EditorUtility.DisplayDialog("완료", "🌾 [터레인 매니저] 청크별 지피잔디 데이터 빌드 및 영구 저장이 완료되었습니다!\n\n• 5.3m/8m 본편 납작 잔디 융단이 바닥에 착 달라붙게 장착되었습니다.", "확인");
        }

        private SerializedProperty targetTerrainProp;
        private SerializedProperty currentThemeProp;
        private SerializedProperty seasonProp;
        private SerializedProperty globalSnowAmountProp;
        private SerializedProperty globalAutumnTintProp;
        private SerializedProperty grassWavingStrengthProp;
        private SerializedProperty grassWavingSpeedProp;

        private bool showGrassListFoldout = true;
        private bool isCompactView = false; // 📋 20~30종 일괄 등록 시 가독성을 위한 간략 테이블 뷰 토글

        // 🎯 청크 좌표 간편 추출 도우미 상태 변수
        private Vector3 _helperWorldPos = Vector3.zero;
        private GameObject _helperTargetObj = null;
        private FoliageBiomeType _helperBiome = FoliageBiomeType.PlainsCarpet;
        private float _helperDensity = 1.0f;
        private string _helperAllowedModelsText = ""; // 쉼표 구분 풀 모델 번호 (예: "0, 1")

        private void OnEnable()
        {
            targetTerrainProp = serializedObject.FindProperty("targetTerrain");
            currentThemeProp = serializedObject.FindProperty("currentTheme");
            seasonProp = serializedObject.FindProperty("season");
            globalSnowAmountProp = serializedObject.FindProperty("globalSnowAmount");
            globalAutumnTintProp = serializedObject.FindProperty("globalAutumnTint");
            grassWavingStrengthProp = serializedObject.FindProperty("grassWavingStrength");
            grassWavingSpeedProp = serializedObject.FindProperty("grassWavingSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TotalWarTerrainManager mgr = (TotalWarTerrainManager)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚔️ 삼국지 토탈워 전장 터레인 및 사계절 환경 관리자", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("선택한 기후 테마(TotalWarTerrainTheme)에 맞춰 지형 레이어 텍스처와 3D 디테일 식생을 자동 세팅하고, 사계절 텍스처 및 전역 적설 셰이더를 실시간 동기화합니다.", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🏔️ 1. 터레인 및 기후 테마", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetTerrainProp, new GUIContent("대상 터레인 (Terrain)"));
            EditorGUILayout.PropertyField(currentThemeProp, new GUIContent("기후 테마 프리셋 (Theme)"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🍂 2. 사계절 및 권역 환경", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(seasonProp, new GUIContent("사계절 모드 (Season)"));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(globalSnowAmountProp, new GUIContent("전역 눈 덮임 비율 (Snow Amount)"));
            EditorGUILayout.PropertyField(globalAutumnTintProp, new GUIContent("전역 가을 단풍 틴트 (Autumn Tint)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🌬️ 잔디 바람 흔들림 (Waving) 제어", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(grassWavingStrengthProp, new GUIContent("잔디 바람 흔들림 강도 (0: 고정)"));
            EditorGUILayout.PropertyField(grassWavingSpeedProp, new GUIContent("잔디 바람 흔들림 속도"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                mgr.UpdateGlobalShaderParameters();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🎨 3. 삼탈워 5대 기후 다채널 복합 지형 페인팅 (Multi-Layer Splatmap)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("단순 3색을 넘어, 평지 잔디 + 절벽 암벽 + 황토 흙길 + 숲속 부엽토 + 수변 자갈의 5개 레이어를 경사도, 고도, 숲 구역(TacticalForestZone)과 결합하여 한 번에 자동 도색합니다.", MessageType.Info);

            // 1행: 온대 5색, 서량 사막 5색, 남만 정글 5색
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("🌾 중원 온대 5색 페인팅", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Temperate5);
            }
            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button("🏜️ 서량 사막 5색 페인팅", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Arid5);
            }
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.6f);
            if (GUILayout.Button("🌴 남만 정글 5색 페인팅", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Nanman5);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // 2행: 하북 설원 5색, 강남 아열대 5색, 숲 바닥 부엽토 재동기화
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            if (GUILayout.Button("❄️ 하북 한랭 5색 페인팅", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Cold5);
            }
            GUI.backgroundColor = new Color(0.5f, 0.95f, 0.8f);
            if (GUILayout.Button("🌾 강남 아열대 5색 페인팅", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Subtropical5);
            }
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.4f);
            if (GUILayout.Button("🌲 숲 바닥 부엽토 재동기화", GUILayout.Height(30)))
            {
                CreateTotalWarTerrainLayers.ApplyAndPaintPaletteToAllTerrains(ClimateTerrainPalette.Temperate5, false);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌍 기후 테마 즉시 적용", GUILayout.Height(28)))
            {
                mgr.ApplyCurrentTheme();
            }
            if (GUILayout.Button("❄️ 사계절/적설 셰이더 갱신", GUILayout.Height(28)))
            {
                mgr.UpdateGlobalShaderParameters();
            }
            EditorGUILayout.EndHorizontal();

            // ======================================================================================
            // 🌾 [삼탈워 원작 1:1 공식] 3D 풀포기 & 서식 지형(스플랫맵) 커스텀 편집기
            // ======================================================================================
            EditorGUILayout.Space(10);
            DrawCustomGrassManagerSection(mgr);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("⚡ 5. 원클릭 계절 전환 퀵 액션", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌸 봄 (Spring)")) mgr.SetSeason(TacticalForestZone.Season.Spring);
            if (GUILayout.Button("🌿 여름 (Summer)")) mgr.SetSeason(TacticalForestZone.Season.Summer);
            if (GUILayout.Button("🍁 가을 (Autumn)")) mgr.SetSeason(TacticalForestZone.Season.Autumn);
            if (GUILayout.Button("❄️ 겨울 (Winter)")) mgr.SetSeason(TacticalForestZone.Season.Winter);
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 3D 풀포기 목록 편집 및 스플랫맵 서식 지형 선택 GUI
        /// </summary>
        private void DrawCustomGrassManagerSection(TotalWarTerrainManager mgr)
        {
            if (mgr.customGrassList == null)
            {
                mgr.customGrassList = RadialGrassConfig.GetDefaultFoliageList();
            }

            GUI.backgroundColor = new Color(0.2f, 0.95f, 0.4f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            showGrassListFoldout = EditorGUILayout.Foldout(showGrassListFoldout, $"🌾 4. 데이터 기반 청크별 식생 관리 시스템 (식생 팔레트 {mgr.customGrassList.Count}종 & 청크별 모델/밀도 할당)", true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (showGrassListFoldout)
            {
                EditorGUILayout.HelpBox("💡 [삼탈워 고증 청크 식생 완전체 시스템]\n• 1단계 [식생 팔레트]: 아래 10대 생태계 테마 또는 364종 브라우저에서 사용할 3D 풀 모델(스크린샷 붉은 이삭 풀 등)을 팔레트에 등록합니다.\n• 2단계 [청크별 할당]: 32m x 32m 청크별로 원하는 모델 번호(예: 0, 1)나 바이옴(평원/숲그늘/공터)을 콕 집어 오버라이드합니다.\n• 3단계 [영구 저장]: [Bake & Save]를 누르면 파일로 압축 저장되어 씬 뷰/인게임에서 착 달라붙는 잔디로 GPU 렌더링됩니다.", MessageType.Info);

                // 🌍 1. 식생 환경 테마 (Biome Theme) 드롭다운 & 토글 버튼 그룹
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("🌍 환경 생태계 식생 테마 (Biome Theme)", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                GrassBiomeTheme selectedTheme = (GrassBiomeTheme)EditorGUILayout.EnumPopup("현재 생태계 테마", mgr.grassBiomeTheme, GUILayout.Height(24));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Grass Biome Theme");
                    mgr.SetGrassBiomeTheme(selectedTheme, true);
                    EditorUtility.SetDirty(mgr);
                    Selection.activeGameObject = mgr.gameObject;
                    SceneView.RepaintAll();
                }

                // 2. ⚡ 10대 전장 생태계 바이옴 퀵 토글 버튼 그룹
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("⚡ 10대 전장 생태계 바이옴 메가팩 (원클릭 18~30종 대량 세팅)", EditorStyles.miniBoldLabel);

                // 1행: 온대 평원, 수림 고사리, 남만 정글, 강남 습지, 서량 황무지
                EditorGUILayout.BeginHorizontal();
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.CentralPlains20, "🌾 중원 평원 20종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.DeepForest20, "🌲 수림 고사리 20종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.NanmanJungle22, "🌴 남만 정글 22종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.JiangnanWetland20, "🌾 강남 습지 20종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.AridWasteland18, "🏜️ 서량 황무지 18종");
                EditorGUILayout.EndHorizontal();

                // 2행: 한랭 설원, 봄꽃 만개, 황금 농경지, 산악 암반, 올인원 30종
                EditorGUILayout.BeginHorizontal();
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.ColdSteppe18, "❄️ 한랭 설원 18종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.BloomingMeadows22, "🌸 봄꽃 만개 22종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.HarvestFarmlands20, "🌾 황금 농경지 20종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.RockyHighlands18, "⛰️ 산악 암반 18종");
                DrawBiomeThemeButton(mgr, GrassBiomeTheme.AllInclusive30, "🌟 올인원 30종");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // 3. 브라우저, 쇼케이스 캘리브레이터, 봄 잔디 리셋, 밀도 보정, 간략/상세 뷰 전환 툴바
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.1f, 0.95f, 0.4f);
                if (GUILayout.Button("🌱 364종 즉석 쇼케이스 & 지면 띄움 캘리브레이터", GUILayout.Height(28)))
                {
                    TotalWarFoliageShowcaseWindow.OpenWindow();
                }
                GUI.backgroundColor = Color.white;
                
                GUI.backgroundColor = new Color(0.2f, 1f, 0.5f);
                if (GUILayout.Button("🌸 삼탈워 봄 잔디 20종 로드 & 베이크", GUILayout.Height(28)))
                {
                    Undo.RecordObject(mgr, "Reset to Spring Central Plains");
                    mgr.grassBiomeTheme = GrassBiomeTheme.CentralPlains20;
                    mgr.defaultGrassSpacing = 2.2f; // 삼탈워 본편 장사 고증 촘촘한 융단 간격
                    mgr.customGrassList = RadialGrassConfig.GetBiomeFoliagePack(GrassBiomeTheme.CentralPlains20);
                    EditorUtility.SetDirty(mgr);
                    mgr.BakeChunkFoliage();
                    Selection.activeGameObject = mgr.gameObject; // 포커스 튕김 방지 (터레인 매니저에 고정)
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
                if (GUILayout.Button("🔍 364종 브라우저", GUILayout.Height(28)))
                {
                    TotalWarFoliageBrowserWindow.OpenWindow();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
                if (GUILayout.Button("✨ 풀 밀도 풍성하게 자동 보정", GUILayout.Height(24)))
                {
                    Undo.RecordObject(mgr, "Auto Fix Grass Density");
                    foreach (var g in mgr.customGrassList)
                    {
                        if (g == null) continue;
                        g.growOnGrass = true;

                        if (g.noiseThreshold > 0.40f)
                        {
                            g.noiseThreshold = g.displayName.Contains("Seedhead") || g.displayName.Contains("Flower") || g.displayName.Contains("Fern") ? 0.35f : 0.15f;
                        }
                        if (g.minDensity < 30) g.minDensity = 40;
                        if (g.maxDensity < 80) g.maxDensity = 95;
                    }
                    EditorUtility.SetDirty(mgr);
                }
                GUI.backgroundColor = Color.white;

                string viewToggleLabel = isCompactView ? "🎛️ 슬라이더 상세 뷰로 전환" : "📋 간략 테이블 뷰로 전환";
                GUI.backgroundColor = isCompactView ? new Color(0.85f, 0.75f, 1f) : new Color(0.75f, 0.95f, 1f);
                if (GUILayout.Button(viewToggleLabel, GUILayout.Height(24)))
                {
                    isCompactView = !isCompactView;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // 4. 풀 리스트 렌더링 (간략 테이블 뷰 vs 상세 카드 뷰)
                int deleteIdx = -1;

                if (isCompactView)
                {
                    // 📋 [간략 테이블 뷰] 20~30종이 등록되어도 한눈에 보고 체크/수정하는 초경량 뷰
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField("활성", GUILayout.Width(28));
                    EditorGUILayout.LabelField("식생 명칭 / 3D 모델", EditorStyles.boldLabel, GUILayout.Width(220));
                    EditorGUILayout.LabelField("서식 지형 (스플랫맵)", GUILayout.Width(200));
                    EditorGUILayout.LabelField("규격 / 밀도 요약", GUILayout.Width(130));
                    EditorGUILayout.LabelField("삭제", GUILayout.Width(35));
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < mgr.customGrassList.Count; i++)
                    {
                        var cfg = mgr.customGrassList[i];
                        if (cfg == null) continue;

                        GUI.backgroundColor = cfg.isEnabled ? ((i % 2 == 0) ? new Color(0.95f, 1f, 0.95f) : Color.white) : new Color(0.85f, 0.85f, 0.85f);
                        EditorGUILayout.BeginHorizontal("box");
                        GUI.backgroundColor = Color.white;

                        cfg.isEnabled = EditorGUILayout.Toggle(cfg.isEnabled, GUILayout.Width(24));
                        EditorGUILayout.LabelField($"#{i + 1} {cfg.displayName}", GUILayout.Width(220));

                        cfg.growOnGrass = EditorGUILayout.ToggleLeft("🌱잔디", cfg.growOnGrass, GUILayout.Width(60));
                        cfg.growOnDirt = EditorGUILayout.ToggleLeft("🪨흙길", cfg.growOnDirt, GUILayout.Width(60));
                        cfg.growOnRock = EditorGUILayout.ToggleLeft("⛰️바위", cfg.growOnRock, GUILayout.Width(60));

                        EditorGUILayout.LabelField($"크기 {cfg.minWidth:F1}~{cfg.maxWidth:F1}m | 밀도 {cfg.minDensity}~{cfg.maxDensity}", EditorStyles.miniLabel, GUILayout.Width(130));

                        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                        if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(18)))
                        {
                            deleteIdx = i;
                        }
                        GUI.backgroundColor = Color.white;

                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    // 🎛️ [슬라이더 상세 카드 뷰] 각 풀의 모델, 텍스처, 크기, 밀도 슬라이더를 정밀 조작하는 뷰
                    for (int i = 0; i < mgr.customGrassList.Count; i++)
                    {
                        var cfg = mgr.customGrassList[i];
                        if (cfg == null) continue;

                        GUI.backgroundColor = cfg.isEnabled ? new Color(0.92f, 0.98f, 0.92f) : new Color(0.85f, 0.85f, 0.85f);
                        EditorGUILayout.BeginVertical("box");
                        GUI.backgroundColor = Color.white;

                        // 카드 헤더
                        EditorGUILayout.BeginHorizontal();
                        cfg.isEnabled = EditorGUILayout.Toggle(cfg.isEnabled, GUILayout.Width(20));
                        EditorGUILayout.LabelField($"#{i + 1} {cfg.displayName}", EditorStyles.boldLabel);

                        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                        if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                        {
                            deleteIdx = i;
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();

                        if (cfg.isEnabled)
                        {
                            EditorGUI.indentLevel++;

                            // 3D 파일 및 텍스처
                            EditorGUILayout.BeginHorizontal();
                            cfg.modelFile = EditorGUILayout.TextField("3D 모델", cfg.modelFile);
                            cfg.albedoFile = EditorGUILayout.TextField("텍스처", cfg.albedoFile);
                            EditorGUILayout.EndHorizontal();

                            // 규격 슬라이더
                            EditorGUILayout.BeginHorizontal();
                            cfg.minWidth = EditorGUILayout.FloatField("너비 Min", cfg.minWidth);
                            cfg.maxWidth = EditorGUILayout.FloatField("Max", cfg.maxWidth);
                            cfg.minHeight = EditorGUILayout.FloatField("높이 Min", cfg.minHeight);
                            cfg.maxHeight = EditorGUILayout.FloatField("Max", cfg.maxHeight);
                            EditorGUILayout.EndHorizontal();

                            // 밀도 및 노이즈
                            EditorGUILayout.BeginHorizontal();
                            cfg.minDensity = EditorGUILayout.IntSlider("최소 밀도", cfg.minDensity, 1, 100);
                            cfg.maxDensity = EditorGUILayout.IntSlider("최대 밀도", cfg.maxDensity, 1, 150);
                            EditorGUILayout.EndHorizontal();
                            cfg.noiseThreshold = EditorGUILayout.Slider("군락 희소도 (Noise)", cfg.noiseThreshold, 0.05f, 0.90f);

                            // ⛰️ 지형 경사 밀착도 및 지면 높이 띄움 오프셋
                            EditorGUILayout.BeginHorizontal();
                            cfg.alignToGround = EditorGUILayout.Slider(new GUIContent("경사 밀착도", "1: 지형 경사면에 100% 밀착(지피 잔디), 0.4: 수직 직립성 유지(수풀/꽃대)"), cfg.alignToGround, 0f, 1f);
                            cfg.groundOffset = EditorGUILayout.Slider(new GUIContent("지면 띄움", "바닥 파묻힘 방지용 높이 보정 (-0.5m ~ +1.5m)"), cfg.groundOffset, -0.5f, 1.5f);
                            EditorGUILayout.EndHorizontal();

                            // 🌍 서식 지형 레이어 선택 (스플랫맵 연동)
                            EditorGUILayout.Space(2);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("🌍 서식 지형:", GUILayout.Width(75));
                            cfg.growOnGrass = EditorGUILayout.ToggleLeft("🌱 푸른 잔디", cfg.growOnGrass, GUILayout.Width(95));
                            cfg.growOnDirt = EditorGUILayout.ToggleLeft("🪨 흙/자갈길", cfg.growOnDirt, GUILayout.Width(95));
                            cfg.growOnRock = EditorGUILayout.ToggleLeft("⛰️ 절벽/바위", cfg.growOnRock, GUILayout.Width(95));
                            EditorGUILayout.EndHorizontal();

                            // ⚠️ 서식지 상태 피드백
                            if (!cfg.growOnGrass && (cfg.growOnDirt || cfg.growOnRock))
                            {
                                EditorGUILayout.HelpBox("⚠️ '푸른 잔디'가 꺼져 있어 현재 전장의 평지(잔디밭)에는 자라지 않습니다! 평지에도 피어나게 하려면 [🌱 푸른 잔디]를 체크하세요.", MessageType.Warning);
                            }
                            else if (!cfg.growOnGrass && !cfg.growOnDirt && !cfg.growOnRock)
                            {
                                EditorGUILayout.HelpBox("❌ 모든 서식 지형이 꺼져 있어 어디에도 자라지 않습니다! [🌱 푸른 잔디]를 체크하세요.", MessageType.Error);
                            }

                            EditorGUI.indentLevel--;
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }
                }

                if (deleteIdx >= 0)
                {
                    Undo.RecordObject(mgr, "Delete Grass Item");
                    mgr.customGrassList.RemoveAt(deleteIdx);
                    EditorUtility.SetDirty(mgr);
                }

                EditorGUILayout.Space(8);
                // ======================================================================================
                // 🎯 파트 2: 청크 좌표 추출기 & 청크별 모델/바이옴 할당
                // ======================================================================================
                EditorGUILayout.LabelField("🎯 파트 2: 청크 좌표 추출기 및 청크별 식생 모델 할당", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("32m x 32m 공간 청크별로 지피잔디의 바이옴 및 특정 모델 번호(예: 0, 1)를 지정하여, 해당 청크에만 원하는 풀이 자라나게 합니다.", MessageType.None);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("grassRenderer"), new GUIContent("전술 잔디 렌더러 (TacticalGrassRenderer)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultGrassSpacing"), new GUIContent("기본 잔디 간격 (Spacing, 기본: 3.8m)"));

                // 청크 픽커 박스
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical("helpbox");
                _helperTargetObj = (GameObject)EditorGUILayout.ObjectField("기준 오브젝트 (선택 시 자동 좌표)", _helperTargetObj, typeof(GameObject), true);
                if (_helperTargetObj != null)
                {
                    _helperWorldPos = _helperTargetObj.transform.position;
                }
                _helperWorldPos = EditorGUILayout.Vector3Field("월드 좌표 (World X, Y, Z)", _helperWorldPos);

                Terrain activeTerrain = mgr.grassRenderer != null && mgr.grassRenderer.targetTerrain != null ? mgr.grassRenderer.targetTerrain : Terrain.activeTerrain;
                Vector3 tPos = activeTerrain != null ? activeTerrain.transform.position : Vector3.zero;
                float cSize = mgr.grassRenderer != null ? mgr.grassRenderer.chunkSize : 32f;
                int calcChunkX = Mathf.FloorToInt((_helperWorldPos.x - tPos.x) / cSize);
                int calcChunkZ = Mathf.FloorToInt((_helperWorldPos.z - tPos.z) / cSize);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"👉 계산된 청크: X = {calcChunkX}, Z = {calcChunkZ}", EditorStyles.boldLabel, GUILayout.Width(190));
                
                GUI.backgroundColor = new Color(0.3f, 1f, 0.6f);
                if (GUILayout.Button("🚀 씬 카메라를 이 청크로 이동", GUILayout.Height(22)))
                {
                    float cCenterX = tPos.x + (calcChunkX + 0.5f) * cSize;
                    float cCenterZ = tPos.z + (calcChunkZ + 0.5f) * cSize;
                    float cCenterY = (activeTerrain != null) ? activeTerrain.SampleHeight(new Vector3(cCenterX, 0, cCenterZ)) + tPos.y : 0f;
                    Vector3 targetPos = new Vector3(cCenterX, cCenterY, cCenterZ);
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.LookAt(targetPos, Quaternion.Euler(38f, 0f, 0f), 35f);
                        SceneView.lastActiveSceneView.Repaint();
                    }
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🎥 카메라 좌표 따오기", GUILayout.Width(140), GUILayout.Height(22)))
                {
                    if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                    {
                        _helperWorldPos = SceneView.lastActiveSceneView.camera.transform.position;
                        if (activeTerrain != null)
                        {
                            _helperWorldPos.y = activeTerrain.SampleHeight(_helperWorldPos) + tPos.y;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(0.2f, 0.95f, 0.4f);
                if (GUILayout.Button($"🌱 [청크 X: {calcChunkX}, Z: {calcChunkZ}]에서 즉석 식생 쇼케이스 & 캘리브레이터 열기", GUILayout.Height(26)))
                {
                    TotalWarFoliageShowcaseWindow.OpenWindowForChunk(calcChunkX, calcChunkZ);
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.BeginHorizontal();
                _helperBiome = (FoliageBiomeType)EditorGUILayout.EnumPopup("적용할 바이옴", _helperBiome);
                _helperDensity = EditorGUILayout.Slider("밀도 배율", _helperDensity, 0.0f, 2.0f);
                EditorGUILayout.EndHorizontal();

                _helperAllowedModelsText = EditorGUILayout.TextField("청크에 배치할 특정 모델 번호 (쉼표 구분, 예: 0, 1 / 빈칸: 전체)", _helperAllowedModelsText);

                GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
                if (GUILayout.Button($"➕ [청크 X: {calcChunkX}, Z: {calcChunkZ}]를 오버라이드 목록에 즉시 등록", GUILayout.Height(28)))
                {
                    Undo.RecordObject(mgr, "Add Chunk Foliage Override");
                    if (mgr.chunkFoliageOverrides == null) mgr.chunkFoliageOverrides = new List<ChunkFoliageOverride>();

                    List<int> parsedIndices = new List<int>();
                    if (!string.IsNullOrEmpty(_helperAllowedModelsText))
                    {
                        string[] tokens = _helperAllowedModelsText.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in tokens)
                        {
                            if (int.TryParse(t, out int mIdx)) parsedIndices.Add(mIdx);
                        }
                    }

                    var existing = mgr.chunkFoliageOverrides.Find(o => o.chunkCoord.x == calcChunkX && o.chunkCoord.y == calcChunkZ);
                    if (existing != null)
                    {
                        existing.biomeType = _helperBiome;
                        existing.densityMultiplier = _helperDensity;
                        existing.allowedModelIndices = parsedIndices;
                    }
                    else
                    {
                        mgr.chunkFoliageOverrides.Add(new ChunkFoliageOverride
                        {
                            chunkCoord = new Vector2Int(calcChunkX, calcChunkZ),
                            biomeType = _helperBiome,
                            densityMultiplier = _helperDensity,
                            allowedModelIndices = parsedIndices
                        });
                    }
                    EditorUtility.SetDirty(mgr);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chunkFoliageOverrides"), new GUIContent("청크별 커스텀 식생 오버라이드 목록"), true);

                // ======================================================================================
                // 💾 파트 3: 원클릭 청크 식생 빌드 & 디스크 영구 저장
                // ======================================================================================
                EditorGUILayout.Space(8);
                GUI.backgroundColor = new Color(0.1f, 0.85f, 0.35f);
                if (GUILayout.Button("🌾 청크별 식생 데이터 즉시 빌드 및 영구 저장 (Bake & Save)", GUILayout.Height(42)))
                {
                    mgr.BakeChunkFoliage();
                    EditorUtility.SetDirty(mgr);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;

                if (mgr.grassRenderer != null)
                {
                    EditorGUILayout.HelpBox($"• 보관 중인 잔디: {mgr.grassRenderer.TotalInstanceCount:N0}개 풀포기\n• 저장 경로: {mgr.grassRenderer.GetTargetDataDirectory()}", MessageType.None);
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 식생 환경 테마 토글 버튼 (선택된 테마는 시각적 강조 표시)
        /// </summary>
        private void DrawBiomeThemeButton(TotalWarTerrainManager mgr, GrassBiomeTheme theme, string label)
        {
            bool isCurrent = (mgr.grassBiomeTheme == theme);
            if (isCurrent)
            {
                GUI.backgroundColor = new Color(0.3f, 0.95f, 0.5f);
            }

            if (GUILayout.Button(label, GUILayout.Height(26)))
            {
                Undo.RecordObject(mgr, $"Switch Biome to {theme}");
                mgr.SetGrassBiomeTheme(theme, true);
                EditorUtility.SetDirty(mgr);
                Selection.activeGameObject = mgr.gameObject;
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = Color.white;
        }

        private void AddPreset(TotalWarTerrainManager mgr, RadialGrassConfig preset)
        {
            Undo.RecordObject(mgr, "Add Grass Preset");
            mgr.customGrassList.Add(preset);
            EditorUtility.SetDirty(mgr);
        }
    }
}
