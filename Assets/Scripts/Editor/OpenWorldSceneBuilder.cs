using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniTotalWar.OpenWorld.Editor
{
    /// <summary>
    /// 기존 실험 씬(Game2.unity)을 일절 건드리지 않고,
    /// 오픈월드 캠페인(MainMap), 로딩(Loading), 심리스 3x3 전투(MainBattle) 3대 씬을 자동 생성·연결하고
    /// Build Settings에 등록하는 에디터 씬 빌더입니다.
    /// </summary>
    public static class OpenWorldSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string MainMapPath = "Assets/Scenes/MainMap.unity";
        private const string LoadingPath = "Assets/Scenes/Loading.unity";
        private const string MainBattlePath = "Assets/Scenes/MainBattle.unity";

        private const string WaterMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Materials/TotalWarWater.mat";
        private const string PebblesLayerPath = "Assets/TerrainSampleAssets/TerrainLayers/Pebbles_A_TerrainLayer.terrainlayer";
        private const string FallbackStoneLayerPath = "Assets/Settings/TerrainLayers/TotalWar_Stone_Layer.terrainlayer";

        [MenuItem("TotalWar/🎬 오픈월드 3대 씬 일괄 생성 (MainMap · Loading · MainBattle)")]
        public static void BuildAllOpenWorldScenesMenu()
        {
            BuildAllOpenWorldScenes(interactive: true);
        }

        public static void BuildAllOpenWorldScenes(bool interactive = true)
        {
            // 현재 열려 있는 실험 씬(예: Game2.unity)에 미저장 수정 사항이 있다면 사용자에게 안전하게 저장 여부 확인
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
            }

            BuildMainMapScene();
            BuildLoadingScene();
            BuildMainBattleScene();
            RegisterScenesInBuildSettings();

            // 생성 완료 후 플레이 시작점인 MainMap 씬을 열어둠 (Game2.unity는 원본 그대로 무수정 보존)
            EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg =
                "오픈월드 3대 씬 생성 및 Build Settings 등록이 완료되었습니다!\n" +
                $"- 전략 지도 씬: {MainMapPath}\n" +
                $"- 비동기 로딩 씬: {LoadingPath}\n" +
                $"- 3x3 심리스 전장 씬: {MainBattlePath}\n\n" +
                "MainMap 씬에서 Play 버튼을 누르면 대륙 지도 -> 적 교전/전장 진입 -> Loading -> MainBattle 순으로 바로 테스트할 수 있습니다.";

            Debug.Log($"<color=#00FF88><b>[OpenWorldSceneBuilder]</b></color> {msg}");
            if (interactive)
            {
                EditorUtility.DisplayDialog("오픈월드 3대 씬 생성 완료", msg, "확인");
            }
        }

        private static void BuildMainMapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. 카메라
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 4000f;
            cam.fieldOfView = 45f;
            camObj.AddComponent<AudioListener>();
            camObj.transform.position = new Vector3(320f, 180f, 140f);
            camObj.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

            // 2. 조명 및 삼탈워 대기 스카이박스
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(42f, 235f, 0f);
            RenderSettings.sun = dirLight;

            GameObject envObj = new GameObject("TotalWar_EnvironmentLighting");
            envObj.AddComponent<TotalWarEnvironmentLighting>();

            // 3. 캠페인 컨트롤러 및 3D 지도 에셋 자동 바인딩
            GameObject ctrlObj = new GameObject("MainMap_Controller");
            MainMapController ctrl = ctrlObj.AddComponent<MainMapController>();
            ctrl.continentId = OpenWorldSceneLoader.ActiveContinentId;
            ctrl.mapCamera = cam;

            string worldFolder = SectorBinaryIO.GetWorldFolder(OpenWorldSceneLoader.WorldRootFolder, ctrl.continentId);
            ctrl.campaignMapMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{worldFolder}/CampaignMapMesh.asset");
            ctrl.campaignMapMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{worldFolder}/CampaignMapMaterial.mat");
            ctrl.seaMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MainMapPath);
        }

        private static void BuildLoadingScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
            camObj.AddComponent<AudioListener>();

            // UI Canvas 구축
            GameObject canvasObj = new GameObject("LoadingCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 배경 패널
            GameObject bgObj = CreateUIPanel("Backdrop", canvasObj.transform, new Color(0.07f, 0.09f, 0.12f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 중앙 카드 패널
            GameObject cardObj = CreateUIPanel("CardPanel", bgObj.transform, new Color(0.11f, 0.14f, 0.19f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(920f, 340f), Vector2.zero);

            Text titleTxt = CreateUIText("TitleText", cardObj.transform, uiFont, 34, TextAnchor.MiddleCenter,
                new Color(0.96f, 0.88f, 0.65f, 1f), new Vector2(0.5f, 0.78f), new Vector2(860f, 60f), "전장 지형 데이터를 불러오는 중...");

            Text statusTxt = CreateUIText("StatusText", cardObj.transform, uiFont, 20, TextAnchor.MiddleCenter,
                new Color(0.82f, 0.88f, 0.94f, 1f), new Vector2(0.5f, 0.58f), new Vector2(860f, 40f), "3x3 오픈월드 섹터 합성 대기 중...");

            // 진행 바 트랙 & 채우기
            GameObject barBg = CreateUIPanel("ProgressBarBg", cardObj.transform, new Color(0.04f, 0.05f, 0.07f, 1f),
                new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(760f, 28f), Vector2.zero);

            GameObject barFillObj = new GameObject("ProgressBarFill");
            barFillObj.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = barFillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            Image fillImg = barFillObj.AddComponent<Image>();
            fillImg.color = new Color(0.24f, 0.72f, 0.48f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            Text percentTxt = CreateUIText("PercentText", cardObj.transform, uiFont, 22, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0.5f, 0.30f), new Vector2(200f, 36f), "0%");

            Text tipTxt = CreateUIText("TipText", cardObj.transform, uiFont, 18, TextAnchor.MiddleCenter,
                new Color(0.68f, 0.74f, 0.82f, 1f), new Vector2(0.5f, 0.14f), new Vector2(860f, 48f), "전술 팁을 불러오는 중...");

            GameObject ctrlObj = new GameObject("LoadingScreen_Controller");
            LoadingScreenController loader = ctrlObj.AddComponent<LoadingScreenController>();
            loader.titleText = titleTxt;
            loader.statusText = statusTxt;
            loader.tipText = tipTxt;
            loader.progressFillImage = fillImg;
            loader.progressPercentText = percentTxt;

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, LoadingPath);
        }

        private static void BuildMainBattleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. 메인 카메라 & RTS 카메라 컨트롤러
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 50f;
            camObj.AddComponent<AudioListener>();
            CameraController camCtrl = camObj.AddComponent<CameraController>();
            camCtrl.SetRuntimeBounds(new Vector2(-99999f, -99999f), new Vector2(99999f, 99999f), 5f, 450f, enableBounds: false);

            // 2. 조명 · 바람 · 바다
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(38f, 245f, 0f);
            RenderSettings.sun = dirLight;

            GameObject envObj = new GameObject("TotalWar_EnvironmentLighting");
            envObj.AddComponent<TotalWarEnvironmentLighting>();

            GameObject windObj = new GameObject("TotalWar_WindManager");
            windObj.AddComponent<TotalWarWindManager>();

            GameObject seaObj = new GameObject("TotalWar_SeaPlane");
            TotalWarSeaPlane sea = seaObj.AddComponent<TotalWarSeaPlane>();
            sea.followTargetCamera = true;
            sea.waterLevelY = 20f;
            sea.waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);

            // 3. 멀티 터레인 · Floating Origin · 스트리머
            GameObject hmObj = new GameObject("TerrainHeightManager");
            hmObj.AddComponent<TerrainHeightManager>();

            GameObject originObj = new GameObject("FloatingOriginManager");
            originObj.AddComponent<FloatingOriginManager>();

            GameObject streamerObj = new GameObject("SeamlessBattleWorldStreamer");
            SeamlessBattleWorldStreamer streamer = streamerObj.AddComponent<SeamlessBattleWorldStreamer>();
            streamer.continentId = OpenWorldSceneLoader.ActiveContinentId;
            streamer.enableContinuousStreaming = true;
            streamer.spawnTacticalForestZones = true;
            streamer.climateThemes = new MiniTotalWar.TotalWarTerrainTheme[]
            {
                AssetDatabase.LoadAssetAtPath<MiniTotalWar.TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Temperate_Theme.asset"),
                AssetDatabase.LoadAssetAtPath<MiniTotalWar.TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Nanman_Theme.asset"),
                AssetDatabase.LoadAssetAtPath<MiniTotalWar.TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Arid_Theme.asset"),
                AssetDatabase.LoadAssetAtPath<MiniTotalWar.TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Subtropical_Theme.asset"),
                AssetDatabase.LoadAssetAtPath<MiniTotalWar.TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Cold_Theme.asset")
            };
            streamer.shoreGravelLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(PebblesLayerPath)
                ?? AssetDatabase.LoadAssetAtPath<TerrainLayer>(FallbackStoneLayerPath);

            // 4. 전투 지휘 파이프라인 (CommandQueue, Previewer, BattleManager, PlayerController)
            GameObject cmdObj = new GameObject("[BattleCommandQueue]");
            cmdObj.AddComponent<BattleCommandQueue>();

            GameObject previewObj = new GameObject("FormationPreviewer");
            FormationPreviewer previewer = previewObj.AddComponent<FormationPreviewer>();
            BindSerializedObjectRef(previewer, "previewDotPrefab", "Assets/Prefabs/PreviewDot.prefab");

            GameObject battleMgrObj = new GameObject("BattleManager");
            BattleManager battleMgr = battleMgrObj.AddComponent<BattleManager>();
            battleMgr.deferAutoSpawnToExternalLoader = true;
            BindSerializedObjectRef(battleMgr, "playerUnitPrefab", "Assets/Prefabs/PlayerUnit_melee.prefab");
            BindSerializedObjectRef(battleMgr, "playerMissilePrefab", "Assets/Prefabs/PlayerUnit_missile.prefab");
            BindSerializedObjectRef(battleMgr, "playerGunnerPrefab", "Assets/Prefabs/PlayerUnit_gunner.prefab");
            BindSerializedObjectRef(battleMgr, "enemyUnitPrefab", "Assets/Prefabs/EnemyUnit_melee.prefab");
            BindSerializedObjectRef(battleMgr, "enemyMissilePrefab", "Assets/Prefabs/EnemyUnit_missile.prefab");
            BindSerializedObjectRef(battleMgr, "enemyGunnerPrefab", "Assets/Prefabs/EnemyUnit_gunner.prefab");
            BindSerializedObjectRef(battleMgr, "squadPrefab", "Assets/Prefabs/Squad.prefab");

            GameObject playerCtrlObj = new GameObject("PlayerController");
            PlayerController playerCtrl = playerCtrlObj.AddComponent<PlayerController>();
            BindPlayerControllerFields(playerCtrl, previewer);

            // 5. 전장 부트스트래퍼
            GameObject bootObj = new GameObject("BattleLoadingSequence");
            BattleLoadingSequence boot = bootObj.AddComponent<BattleLoadingSequence>();
            boot.worldStreamer = streamer;
            boot.battleManager = battleMgr;
            boot.cameraController = camCtrl;

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MainBattlePath);
        }

        private static void RegisterScenesInBuildSettings()
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string[] desired = { MainMapPath, LoadingPath, MainBattlePath };

            for (int i = 0; i < desired.Length; i++)
            {
                string path = desired[i];
                bool exists = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].path == path)
                    {
                        list[j].enabled = true;
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void BindSerializedObjectRef(Object target, string propertyName, string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (target == null || prefab == null) return;

            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BindPlayerControllerFields(PlayerController ctrl, FormationPreviewer previewer)
        {
            if (ctrl == null) return;

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 8;
            int playerUnitLayer = LayerMask.NameToLayer("PlayerUnit");
            if (playerUnitLayer < 0) playerUnitLayer = 6;
            int enemyUnitLayer = LayerMask.NameToLayer("EnemyUnit");
            if (enemyUnitLayer < 0) enemyUnitLayer = 7;

            SerializedObject so = new SerializedObject(ctrl);
            SerializedProperty gProp = so.FindProperty("groundLayer");
            if (gProp != null) gProp.intValue = 1 << groundLayer;

            SerializedProperty uProp = so.FindProperty("unitLayer");
            if (uProp != null) uProp.intValue = (1 << playerUnitLayer) | (1 << enemyUnitLayer);

            SerializedProperty pProp = so.FindProperty("previewer");
            if (pProp != null) pProp.objectReferenceValue = previewer;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateUIPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            Image img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static Text CreateUIText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color, Vector2 anchorCenter, Vector2 sizeDelta, string initialText)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;

            Text txt = go.AddComponent<Text>();
            if (font != null) txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.text = initialText;
            return txt;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
