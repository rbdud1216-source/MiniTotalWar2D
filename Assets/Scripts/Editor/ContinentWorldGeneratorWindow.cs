using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniTotalWar.OpenWorld.Editor
{
    /// <summary>
    /// 삼국지 토탈워 대륙 오픈월드 자동 생성기 에디터 창
    /// 전투 중인 3x3 구역만 런타임 즉석 생성하는 착시 구조로 114km 원작 풀 스케일 이상을 수 MB만으로 구축합니다.
    /// </summary>
    public class ContinentWorldGeneratorWindow : EditorWindow
    {
        private ContinentGenerationSettings settings = new ContinentGenerationSettings();
        private Texture2D previewTexture;
        private bool showAdvanced = false;
        private string lastReport = "";
        private Vector2 scrollPos;

        [MenuItem("TotalWar/🗺️ 대륙 오픈월드 자동 생성기 (Continent Generator)")]
        public static void OpenWindow()
        {
            var win = GetWindow<ContinentWorldGeneratorWindow>("대륙 오픈월드 생성기");
            win.minSize = new Vector2(490f, 670f);
            win.Show();
        }

        private void OnDisable()
        {
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🗺️ 대륙 오픈월드 자동 생성기 (가상 온디맨드 SSOT)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "전투가 벌어지는 3x3 구역(1.54km x 1.54km)만 런타임에 밀리초 단위로 즉석 합성하는 구조를 사용해, " +
                "수 테라바이트 없이 수 MB의 마스터 캐시(WorldSourceCache.bin)만으로 114km 삼국지 풀 스케일 이상을 구동합니다.",
                MessageType.Info);

            // 1. 추출 에셋 탐지 배지
            bool hasXml = File.Exists(settings.battleLocationsXmlPath);
            bool hasSea = File.Exists(settings.seaHeightMapPath);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📡 원본 토탈워 추출 에셋 탐지 상태", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("전장/해안 그리드 XML (892x703)", hasXml ? "✅ 감지됨" : "⚠️ 미발견 (절차적 폴백 사용)");
            EditorGUILayout.LabelField("황하·장강·주강 수계 PNG (3568x2810)", hasSea ? "✅ 감지됨" : "⚠️ 미발견 (기본 수계 사용)");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 2. 대륙 규모 프리셋 버튼 3종
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📐 대륙 규모 프리셋 (섹터 1개 = 512m x 512m)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("소형 테스트\n(18x14 · 9.2km)"))
            {
                settings.sectorCountX = 18;
                settings.sectorCountZ = 14;
                settings.useOnDemandSynthesis = true;
            }
            if (GUILayout.Button("대형 전선\n(60x47 · 30.7km)"))
            {
                settings.sectorCountX = 60;
                settings.sectorCountZ = 47;
                settings.useOnDemandSynthesis = true;
                settings.bakeAllSectorsToDisk = false;
            }
            GUI.backgroundColor = new Color(0.65f, 0.90f, 1.0f);
            if (GUILayout.Button("토탈워 풀 스케일 (권장)\n(223x175 · 114.2km x 89.6km)"))
            {
                settings.sectorCountX = 223;
                settings.sectorCountZ = 175;
                settings.useOnDemandSynthesis = true;
                settings.bakeAllSectorsToDisk = false;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            int totalSec = settings.sectorCountX * settings.sectorCountZ;
            float totalKmX = (settings.sectorCountX * settings.sectorSizeMeters) / 1000f;
            float totalKmZ = (settings.sectorCountZ * settings.sectorSizeMeters) / 1000f;
            EditorGUILayout.LabelField(
                $"현재 대륙 규격: {settings.sectorCountX} x {settings.sectorCountZ} = {totalSec:N0}섹터 ({totalKmX:F1}km x {totalKmZ:F1}km)",
                EditorStyles.miniBoldLabel);

            settings.useOnDemandSynthesis = EditorGUILayout.ToggleLeft(
                "⚡ 전투 구역만 런타임 즉석 생성 (On-the-Fly — 약 5MB로 수 초 생성)",
                settings.useOnDemandSynthesis);

            using (new EditorGUI.DisabledScope(totalSec > 1000))
            {
                settings.bakeAllSectorsToDisk = EditorGUILayout.ToggleLeft(
                    totalSec > 1000
                        ? "💾 전 섹터 디스크 사전 굽기 (1,000섹터 초과는 용량 보호를 위해 비활성화됨)"
                        : "💾 전 섹터 Sector_X_Z.bin 디스크 사전 굽기 (소규모 맵 전용)",
                    totalSec <= 1000 && settings.bakeAllSectorsToDisk);
            }
            if (totalSec > 1000) settings.bakeAllSectorsToDisk = false;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 3. 상세 파라미터
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "⚙️ 상세 생성 파라미터 및 경로 설정", true);
            if (showAdvanced)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                settings.continentId = EditorGUILayout.TextField("대륙 식별 ID", settings.continentId);
                settings.displayName = EditorGUILayout.TextField("대륙 표시 명칭", settings.displayName);
                settings.outputRootFolder = EditorGUILayout.TextField("출력 루트 폴더", settings.outputRootFolder);
                settings.seed = EditorGUILayout.IntField("난수 시드 (Seed)", settings.seed);
                settings.sectorCountX = EditorGUILayout.IntSlider("동서 섹터 수 (X)", settings.sectorCountX, 8, 500);
                settings.sectorCountZ = EditorGUILayout.IntSlider("남북 섹터 수 (Z)", settings.sectorCountZ, 8, 400);
                settings.sectorSizeMeters = EditorGUILayout.FloatField("섹터 크기 (m)", settings.sectorSizeMeters);
                settings.campaignMapWidthMeters = EditorGUILayout.FloatField("캠페인 3D 지도 테이블 가로(m)", settings.campaignMapWidthMeters);
                settings.heightmapResolution = EditorGUILayout.IntPopup("높이맵 해상도", settings.heightmapResolution,
                    new[] { "65", "129", "257" }, new[] { 65, 129, 257 });
                settings.alphamapResolution = EditorGUILayout.IntPopup("스플랫맵 해상도", settings.alphamapResolution,
                    new[] { "64", "128", "256" }, new[] { 64, 128, 256 });
                settings.seaLevelY = EditorGUILayout.Slider("전역 해수면 Y (m)", settings.seaLevelY, 5f, 40f);
                settings.maxTerrainHeight = EditorGUILayout.Slider("최대 고도 (m)", settings.maxTerrainHeight, 80f, 300f);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            // 4. 미리보기 및 실행 버튼
            if (GUILayout.Button("🔍 대륙 지형·기후·황하/장강·도로 미리보기 갱신", GUILayout.Height(30f)))
            {
                if (previewTexture != null) DestroyImmediate(previewTexture);
                previewTexture = ContinentWorldGenerator.GeneratePreviewTexture(settings, 420, 330);
            }

            if (previewTexture != null)
            {
                EditorGUILayout.Space(6);
                Rect rect = GUILayoutUtility.GetRect(420f, 310f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField("범례: 남색=대양 / 청록=황하·장강·해안 / 황토=도로 / 진녹=숲 / 회백=한랭·설산 / 갈색=서량", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button("🗺️ 대륙 오픈월드 일괄 생성 (WorldSourceCache + 3D 캠페인 지도)", GUILayout.Height(40f)))
            {
                RunFullGeneration();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            if (GUILayout.Button("🧪 생성된 대륙 섹터 경계 이음새(Seam) 0% 무결성 검증", GUILayout.Height(28f)))
            {
                string worldFolder = SectorBinaryIO.GetWorldFolder(settings.outputRootFolder, settings.continentId);
                ContinentWorldGenerator.ValidateContinentSeams(worldFolder, out int borders, out int mismatches, out int maxDelta);
                lastReport = $"경계 무결성 검증 결과 -> 검사 경계 수: {borders}개 | 오차 픽셀: {mismatches}개 | 최대 오차: {maxDelta}";
                Debug.Log($"[ContinentWorldGenerator] {lastReport}");
            }

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.55f, 0.80f, 1.0f);
            if (GUILayout.Button("🎬 오픈월드 3대 씬 일괄 생성 (MainMap · Loading · MainBattle)", GUILayout.Height(34f)))
            {
                OpenWorldSceneBuilder.BuildAllOpenWorldScenes(interactive: true);
            }
            GUI.backgroundColor = Color.white;

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(lastReport, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunFullGeneration()
        {
            try
            {
                var summary = ContinentWorldGenerator.GenerateContinent(settings, (msg, progress) =>
                {
                    EditorUtility.DisplayProgressBar("대륙 오픈월드 자동 생성 중...", msg, progress);
                });

                lastReport =
                    $"✅ 생성 완료: {summary.worldFolder}\n" +
                    $"- 대륙 크기: {summary.totalWidthKm:F1}km x {summary.totalDepthKm:F1}km (총 {summary.totalSectors:N0}개 가상 섹터)\n" +
                    $"- 생성 모드: {(summary.onDemandMode ? "런타임 즉석 생성 (WorldSourceCache.bin + On-the-Fly)" : "디스크 사전 굽기")} | 사전 굽기 파일 {summary.bakedSectorFilesCount}개\n" +
                    $"- 거점 {summary.strategicNodeCount}개 · 도로 {summary.roadCount}노선 · 대하천 {summary.riverCount}개\n" +
                    $"- 경계 무결성: {summary.checkedBorders}개 경계 검사, 불일치 {summary.seamMismatchCount}건 (최대 오차 {summary.maxSeamDelta})";

                Debug.Log($"<color=#00FF88><b>[ContinentWorldGenerator]</b></color>\n{lastReport}");
                EditorUtility.DisplayDialog("대륙 오픈월드 생성 완료", lastReport, "확인");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
