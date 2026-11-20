using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 캠페인 대륙 전략 지도(MainMap.unity) 컨트롤러입니다.
    /// 114.2km x 89.6km 중국 대륙 축소 3D 지도 위에서 전략 거점 표시, 아군 군단 행군 지휘,
    /// 적군 조우 시 자동전투 vs 직접 지휘(Loading -> MainBattle) 분기를 처리합니다.
    /// </summary>
    public class MainMapController : MonoBehaviour
    {
        [Serializable]
        public class CampaignArmyRuntime
        {
            public string armyId;
            public string displayName;
            public bool isPlayer;
            public byte ownerPlayerId;
            public WorldCoord worldCoord;
            public WorldCoord moveTargetCoord;
            public bool hasMoveOrder;
            public int strengthSoldiers;
            public int maxSoldiers;
            public float mapMoveSpeed = 28f;
            public GameObject visualRoot;
        }

        [Header("대륙 데이터 및 3D 지도 참조")]
        public string continentId = "ThreeKingdoms_China";
        public Mesh campaignMapMesh;
        public Material campaignMapMaterial;
        public Material seaMaterial;
        public Camera mapCamera;

        [Header("카메라 이동 및 줌 설정")]
        public float panSpeed = 160f;
        public float zoomSpeed = 240f;
        public float minCamHeight = 55f;
        public float maxCamHeight = 620f;
        public float encounterRadiusMapMeters = 14f;

        private WorldMetaData meta;
        private WorldSourceCacheData sourceCache;
        private MasterNetworkData networks;

        private MeshCollider terrainCollider;
        private float mapWidthMeters = 640f;
        private float mapDepthMeters = 504f;
        private float mapHeightScale = 0.2f;

        private readonly List<CampaignArmyRuntime> armies = new List<CampaignArmyRuntime>();
        private CampaignArmyRuntime selectedArmy;
        private CampaignArmyRuntime pendingPlayerEncounterArmy;
        private CampaignArmyRuntime pendingEnemyEncounterArmy;
        private bool showEncounterPopup = false;
        private string statusBanner = "좌클릭: 군단 선택 · 우클릭: 행군 / 적 군단 공격 · WASD/휠: 지도 탐색";

        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private Vector3 lastRotateMousePos;

        // 캠페인 ↔ 전투 왕복 시 군단 위치·병력 보존용 정적 스냅샷
        private static readonly List<CampaignArmyRuntime> SavedSessionArmies = new List<CampaignArmyRuntime>();

        private void Start()
        {
            if (mapCamera == null) mapCamera = Camera.main;

            if (!OpenWorldSceneLoader.EnsureContinentLoaded(continentId))
            {
                statusBanner = "경고: WorldMeta.json이 없습니다. 메뉴 TotalWar > 대륙 오픈월드 자동 생성기를 먼저 실행하세요.";
                return;
            }

            meta = OpenWorldSceneLoader.CachedMeta;
            sourceCache = OpenWorldSceneLoader.CachedSourceCache;
            networks = OpenWorldSceneLoader.CachedNetworks;

            ComputeMapDimensions();
            BuildCampaignTerrainSurface();
            SpawnStrategicNodeMarkers();
            InitializeArmies();

            if (selectedArmy != null)
            {
                FocusCameraOnCoord(selectedArmy.worldCoord);
            }
        }

        private void ComputeMapDimensions()
        {
            mapWidthMeters = Mathf.Max(128f, meta.campaignMapWidthMeters);
            float aspect = meta.TotalDepthMeters / Mathf.Max(1f, meta.TotalWidthMeters);
            mapDepthMeters = mapWidthMeters * aspect;
            mapHeightScale = Mathf.Clamp((mapWidthMeters / Mathf.Max(1f, meta.TotalWidthMeters)) * 16.0f, 0.16f, 0.35f);
        }

        private void BuildCampaignTerrainSurface()
        {
            if (campaignMapMesh == null)
            {
                Debug.LogWarning("[MainMapController] campaignMapMesh가 할당되지 않았습니다.");
                return;
            }

            GameObject surfaceObj = new GameObject("CampaignContinentSurface");
            surfaceObj.transform.SetParent(transform, false);
            surfaceObj.transform.position = Vector3.zero;

            MeshFilter mf = surfaceObj.AddComponent<MeshFilter>();
            mf.sharedMesh = campaignMapMesh;

            MeshRenderer mr = surfaceObj.AddComponent<MeshRenderer>();
            if (campaignMapMaterial != null)
            {
                mr.sharedMaterial = campaignMapMaterial;
            }

            terrainCollider = surfaceObj.AddComponent<MeshCollider>();
            terrainCollider.sharedMesh = campaignMapMesh;

            // 대륙 외곽 광활한 해수면 배경 평면 추가
            GameObject seaObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seaObj.name = "CampaignSeaBackdrop";
            seaObj.transform.SetParent(transform, false);
            seaObj.transform.position = new Vector3(mapWidthMeters * 0.5f, (meta.seaLevelY - 0.8f) * mapHeightScale, mapDepthMeters * 0.5f);
            seaObj.transform.localScale = new Vector3(mapWidthMeters * 0.25f, 1f, mapDepthMeters * 0.25f);
            Destroy(seaObj.GetComponent<Collider>());

            MeshRenderer seaRenderer = seaObj.GetComponent<MeshRenderer>();
            if (seaMaterial != null)
            {
                seaRenderer.sharedMaterial = seaMaterial;
            }
            else if (campaignMapMaterial != null)
            {
                seaRenderer.sharedMaterial = campaignMapMaterial;
            }
        }

        private void SpawnStrategicNodeMarkers()
        {
            if (networks == null || networks.nodes == null) return;

            GameObject markerRoot = new GameObject("StrategicNodes_Container");
            markerRoot.transform.SetParent(transform, false);

            for (int i = 0; i < networks.nodes.Count; i++)
            {
                StrategicNodeData node = networks.nodes[i];
                if (node == null) continue;

                Vector3 pos = WorldCoordToMapPos(node.coord);
                GameObject marker = GameObject.CreatePrimitive(
                    node.nodeType == StrategicNodeType.MountainGate ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                marker.name = $"Node_{node.nodeId}_{node.displayName}";
                marker.transform.SetParent(markerRoot.transform, false);

                float scale = node.nodeType == StrategicNodeType.CapitalCity ? 5.2f :
                              node.nodeType == StrategicNodeType.WalledCity ? 3.8f : 2.6f;
                marker.transform.position = pos + Vector3.up * (scale * 0.35f);
                marker.transform.localScale = new Vector3(scale, scale * 0.4f, scale);

                Renderer r = marker.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material mat = new Material(lit);
                    Color c = node.nodeType == StrategicNodeType.CapitalCity ? new Color(0.95f, 0.78f, 0.22f) :
                              node.nodeType == StrategicNodeType.Port ? new Color(0.25f, 0.80f, 0.92f) :
                              node.nodeType == StrategicNodeType.MountainGate ? new Color(0.92f, 0.45f, 0.18f) :
                              node.nodeType == StrategicNodeType.RiverFord ? new Color(0.45f, 0.75f, 0.95f) :
                              new Color(0.88f, 0.88f, 0.84f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    r.sharedMaterial = mat;
                }
            }
        }

        private void InitializeArmies()
        {
            armies.Clear();

            if (SavedSessionArmies.Count > 0)
            {
                for (int i = 0; i < SavedSessionArmies.Count; i++)
                {
                    var saved = SavedSessionArmies[i];
                    if (saved.strengthSoldiers <= 0) continue;
                    armies.Add(CloneArmyState(saved));
                }
            }
            else
            {
                // 기본 초기 5개 군단 배치 (낙양·적벽 아군 vs 장안·업성·건업 적군)
                AddDefaultArmy("army_player_1", "제1 중앙군 (총사령관)", true, 0, FindAnchorCoord("luoyang", 0.52f, 0.56f), 260, 12f, 0f);
                AddDefaultArmy("army_player_2", "제2 강동 원정대 (제1장군)", true, 1, FindAnchorCoord("chibi", 0.55f, 0.37f), 220, -10f, 8f);

                AddDefaultArmy("army_enemy_1", "서량·동탁 선봉군", false, 255, FindAnchorCoord("changan", 0.41f, 0.55f), 240, 10f, 0f);
                AddDefaultArmy("army_enemy_2", "하북 원소 주력군", false, 255, FindAnchorCoord("ye", 0.58f, 0.66f), 260, 0f, -12f);
                AddDefaultArmy("army_enemy_3", "장강 수비대", false, 255, FindAnchorCoord("jianye", 0.69f, 0.43f), 220, -10f, -6f);
            }

            for (int i = 0; i < armies.Count; i++)
            {
                SpawnArmyVisual(armies[i]);
                if (selectedArmy == null && armies[i].isPlayer)
                {
                    selectedArmy = armies[i];
                }
            }
        }

        private WorldCoord FindAnchorCoord(string nodeId, float fallbackU, float fallbackV)
        {
            if (networks != null && networks.nodes != null)
            {
                for (int i = 0; i < networks.nodes.Count; i++)
                {
                    if (networks.nodes[i].nodeId == nodeId)
                    {
                        return networks.nodes[i].coord;
                    }
                }
            }

            return WorldCoord.FromDouble2(
                new double2(fallbackU * meta.TotalWidthMeters, fallbackV * meta.TotalDepthMeters),
                meta.sectorSizeMeters);
        }

        private void AddDefaultArmy(string id, string name, bool isPlayer, byte ownerId, WorldCoord anchorCoord, int soldiers, float offsetSectorX, float offsetSectorZ)
        {
            WorldCoord c = new WorldCoord(
                Mathf.Clamp(anchorCoord.sectorX + Mathf.RoundToInt(offsetSectorX * 0.25f), 1, meta.sectorCountX - 2),
                Mathf.Clamp(anchorCoord.sectorZ + Mathf.RoundToInt(offsetSectorZ * 0.25f), 1, meta.sectorCountZ - 2),
                anchorCoord.localX,
                anchorCoord.localZ);

            armies.Add(new CampaignArmyRuntime
            {
                armyId = id,
                displayName = name,
                isPlayer = isPlayer,
                ownerPlayerId = ownerId,
                worldCoord = c,
                moveTargetCoord = c,
                hasMoveOrder = false,
                strengthSoldiers = soldiers,
                maxSoldiers = soldiers
            });
        }

        private void SpawnArmyVisual(CampaignArmyRuntime army)
        {
            GameObject root = new GameObject($"CampaignArmy_{army.armyId}");
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoordToMapPos(army.worldCoord);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            body.transform.localScale = new Vector3(3.4f, 3.2f, 3.4f);

            GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.transform.SetParent(root.transform, false);
            banner.transform.localPosition = new Vector3(0f, 8.2f, 0f);
            banner.transform.localScale = new Vector3(4.5f, 2.8f, 0.6f);

            Color teamColor = army.isPlayer ? new Color(0.18f, 0.58f, 0.98f) : new Color(0.90f, 0.22f, 0.20f);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", teamColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", teamColor);

            body.GetComponent<Renderer>().sharedMaterial = mat;
            banner.GetComponent<Renderer>().sharedMaterial = mat;

            army.visualRoot = root;
        }

        private void Update()
        {
            if (meta == null) return;

            HandleCameraControls();

            if (!showEncounterPopup)
            {
                HandleMapSelectionAndMoveInput();
                UpdateArmiesMovement();
            }
        }

        private void HandleCameraControls()
        {
            if (mapCamera == null) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            float mult = Input.GetKey(KeyCode.LeftShift) ? 2.2f : 1.0f;

            Vector3 forwardXZ = Vector3.ProjectOnPlane(mapCamera.transform.forward, Vector3.up).normalized;
            Vector3 rightXZ = Vector3.ProjectOnPlane(mapCamera.transform.right, Vector3.up).normalized;
            Vector3 move = (rightXZ * h + forwardXZ * v) * (panSpeed * mult * Time.deltaTime);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            Vector3 pos = mapCamera.transform.position + move + mapCamera.transform.forward * (scroll * zoomSpeed);

            if (Input.GetMouseButtonDown(2))
            {
                lastRotateMousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - lastRotateMousePos;
                lastRotateMousePos = Input.mousePosition;
                mapCamera.transform.RotateAround(pos, Vector3.up, delta.x * 0.25f);
            }

            pos.x = Mathf.Clamp(pos.x, -40f, mapWidthMeters + 40f);
            pos.z = Mathf.Clamp(pos.z, -120f, mapDepthMeters + 40f);
            pos.y = Mathf.Clamp(pos.y, minCamHeight, maxCamHeight);
            mapCamera.transform.position = pos;
        }

        private void HandleMapSelectionAndMoveInput()
        {
            if (mapCamera == null || terrainCollider == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // 좌클릭: 군단 선택
            if (Input.GetMouseButtonDown(0))
            {
                if (TryRaycastMap(Input.mousePosition, out Vector3 hitPoint))
                {
                    CampaignArmyRuntime clicked = FindNearestArmyAtMapPos(hitPoint, 10f, playerOnly: true);
                    if (clicked != null)
                    {
                        selectedArmy = clicked;
                        statusBanner = $"선택된 군단: {clicked.displayName} ({clicked.strengthSoldiers}명) · 위치 {clicked.worldCoord}";
                    }
                }
            }

            // 우클릭: 이동 및 적 군단 공격 지시
            if (Input.GetMouseButtonDown(1) && selectedArmy != null)
            {
                if (TryRaycastMap(Input.mousePosition, out Vector3 hitPoint))
                {
                    CampaignArmyRuntime targetEnemy = FindNearestArmyAtMapPos(hitPoint, 11f, playerOnly: false);
                    WorldCoord destCoord = MapPosToWorldCoord(targetEnemy != null ? WorldCoordToMapPos(targetEnemy.worldCoord) : hitPoint);

                    float u = (float)(destCoord.ToDouble2(meta.sectorSizeMeters).x / meta.TotalWidthMeters);
                    float v = (float)(destCoord.ToDouble2(meta.sectorSizeMeters).y / meta.TotalDepthMeters);
                    GlobalSurfaceSample sample = ContinentSectorSynthesizer.EvaluateGlobalSample(u, v, meta, sourceCache, includeTacticalDetail: false);
                    if (sample.isOcean && targetEnemy == null)
                    {
                        statusBanner = "해상 영역은 육상 군단으로 행군할 수 없습니다.";
                        return;
                    }

                    selectedArmy.moveTargetCoord = destCoord;
                    selectedArmy.hasMoveOrder = true;
                    statusBanner = targetEnemy != null
                        ? $"{selectedArmy.displayName}이(가) {targetEnemy.displayName}을(를) 향해 추격 개시!"
                        : $"{selectedArmy.displayName} 이동 목표 설정: {destCoord}";
                }
            }
        }

        private void UpdateArmiesMovement()
        {
            for (int i = 0; i < armies.Count; i++)
            {
                CampaignArmyRuntime army = armies[i];
                if (!army.hasMoveOrder) continue;

                Vector3 curPos = WorldCoordToMapPos(army.worldCoord);
                Vector3 dstPos = WorldCoordToMapPos(army.moveTargetCoord);
                Vector3 nextPos = Vector3.MoveTowards(curPos, dstPos, army.mapMoveSpeed * Time.deltaTime);

                army.worldCoord = MapPosToWorldCoord(nextPos);
                if (army.visualRoot != null)
                {
                    army.visualRoot.transform.position = WorldCoordToMapPos(army.worldCoord);
                }

                if (Vector2.Distance(new Vector2(nextPos.x, nextPos.z), new Vector2(dstPos.x, dstPos.z)) < 0.25f)
                {
                    army.hasMoveOrder = false;
                }
            }

            // 아군-적군 교전 거리 판정
            for (int i = 0; i < armies.Count; i++)
            {
                CampaignArmyRuntime a = armies[i];
                if (!a.isPlayer || a.strengthSoldiers <= 0) continue;
                Vector3 pa = WorldCoordToMapPos(a.worldCoord);

                for (int j = 0; j < armies.Count; j++)
                {
                    CampaignArmyRuntime e = armies[j];
                    if (e.isPlayer || e.strengthSoldiers <= 0) continue;

                    Vector3 pe = WorldCoordToMapPos(e.worldCoord);
                    if (Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pe.x, pe.z)) <= encounterRadiusMapMeters)
                    {
                        a.hasMoveOrder = false;
                        e.hasMoveOrder = false;
                        pendingPlayerEncounterArmy = a;
                        pendingEnemyEncounterArmy = e;
                        showEncounterPopup = true;
                        return;
                    }
                }
            }
        }

        private bool TryRaycastMap(Vector3 screenPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            Ray ray = mapCamera.ScreenPointToRay(screenPos);
            if (terrainCollider != null && terrainCollider.Raycast(ray, out RaycastHit hit, 5000f))
            {
                hitPoint = hit.point;
                return true;
            }

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        private CampaignArmyRuntime FindNearestArmyAtMapPos(Vector3 mapPos, float maxRadius, bool playerOnly)
        {
            CampaignArmyRuntime best = null;
            float bestDist = maxRadius;

            for (int i = 0; i < armies.Count; i++)
            {
                CampaignArmyRuntime a = armies[i];
                if (a.strengthSoldiers <= 0) continue;
                if (playerOnly && !a.isPlayer) continue;
                if (!playerOnly && a.isPlayer) continue;

                Vector3 p = WorldCoordToMapPos(a.worldCoord);
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(mapPos.x, mapPos.z));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = a;
                }
            }
            return best;
        }

        public Vector3 WorldCoordToMapPos(WorldCoord coord)
        {
            double2 d = coord.ToDouble2(meta.sectorSizeMeters);
            float u = Mathf.Clamp01((float)(d.x / Mathf.Max(1f, meta.TotalWidthMeters)));
            float v = Mathf.Clamp01((float)(d.y / Mathf.Max(1f, meta.TotalDepthMeters)));

            float x = u * mapWidthMeters;
            float z = v * mapDepthMeters;
            float y = meta.seaLevelY * mapHeightScale;

            if (terrainCollider != null && terrainCollider.Raycast(new Ray(new Vector3(x, 300f, z), Vector3.down), out RaycastHit hit, 600f))
            {
                y = hit.point.y;
            }

            return new Vector3(x, y, z);
        }

        public WorldCoord MapPosToWorldCoord(Vector3 mapPos)
        {
            float u = Mathf.Clamp01(mapPos.x / Mathf.Max(1f, mapWidthMeters));
            float v = Mathf.Clamp01(mapPos.z / Mathf.Max(1f, mapDepthMeters));

            return WorldCoord.FromDouble2(
                new double2(u * meta.TotalWidthMeters, v * meta.TotalDepthMeters),
                meta.sectorSizeMeters);
        }

        public void FocusCameraOnCoord(WorldCoord coord)
        {
            if (mapCamera == null) return;
            Vector3 p = WorldCoordToMapPos(coord);
            mapCamera.transform.position = new Vector3(p.x, Mathf.Clamp(mapCamera.transform.position.y, 90f, 220f), p.z - 85f);
            mapCamera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        }

        private void EnterBattleSceneAt(CampaignArmyRuntime playerArmy, CampaignArmyRuntime enemyArmy)
        {
            SaveSessionArmies();

            WorldCoord encounter = playerArmy.worldCoord;
            float angle = enemyArmy != null
                ? WorldCoord.BearingDegrees(playerArmy.worldCoord, enemyArmy.worldCoord, meta.sectorSizeMeters)
                : 0f;

            double2 d = encounter.ToDouble2(meta.sectorSizeMeters);
            float u = Mathf.Clamp01((float)(d.x / meta.TotalWidthMeters));
            float v = Mathf.Clamp01((float)(d.y / meta.TotalDepthMeters));
            GlobalSurfaceSample sample = ContinentSectorSynthesizer.EvaluateGlobalSample(u, v, meta, sourceCache);

            var ctx = new BattleTransitionContext
            {
                continentId = meta.continentId,
                encounterCoord = encounter,
                approachAngleDegrees = angle,
                season = sample.climate == ContinentClimateId.Cold ? TacticalForestZone.Season.Winter : TacticalForestZone.Season.Summer,
                windAngle = 45f,
                windStrength = 0.65f,
                snowAmount = sample.climate == ContinentClimateId.Cold ? 0.75f : 0f,
                returnToCampaignOnFinish = true
            };

            string label = enemyArmy != null
                ? $"{playerArmy.displayName} vs {enemyArmy.displayName} — 전장 전개 중"
                : $"{playerArmy.displayName} 위치 전술 정찰 진입 중";
            OpenWorldSceneLoader.LoadBattleViaLoadingScene(ctx, label);
        }

        private void ResolveAutoBattle(CampaignArmyRuntime playerArmy, CampaignArmyRuntime enemyArmy)
        {
            // 간단한 란체스터 2차 법칙 교환비 자동전투
            float pPower = playerArmy.strengthSoldiers * playerArmy.strengthSoldiers;
            float ePower = enemyArmy.strengthSoldiers * enemyArmy.strengthSoldiers;
            if (pPower >= ePower)
            {
                int remaining = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, pPower - ePower)));
                playerArmy.strengthSoldiers = Mathf.Max(20, remaining);
                enemyArmy.strengthSoldiers = 0;
                if (enemyArmy.visualRoot != null) enemyArmy.visualRoot.SetActive(false);
                statusBanner = $"자동전투 승리! {playerArmy.displayName} 잔존 병력 {playerArmy.strengthSoldiers}명";
            }
            else
            {
                int remaining = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, ePower - pPower)));
                enemyArmy.strengthSoldiers = Mathf.Max(20, remaining);
                playerArmy.strengthSoldiers = 0;
                if (playerArmy.visualRoot != null) playerArmy.visualRoot.SetActive(false);
                statusBanner = $"자동전투 패배! 적 {enemyArmy.displayName} 잔존 {enemyArmy.strengthSoldiers}명";
            }
            SaveSessionArmies();
        }

        private void SaveSessionArmies()
        {
            SavedSessionArmies.Clear();
            for (int i = 0; i < armies.Count; i++)
            {
                SavedSessionArmies.Add(CloneArmyState(armies[i]));
            }
        }

        private static CampaignArmyRuntime CloneArmyState(CampaignArmyRuntime src)
        {
            return new CampaignArmyRuntime
            {
                armyId = src.armyId,
                displayName = src.displayName,
                isPlayer = src.isPlayer,
                ownerPlayerId = src.ownerPlayerId,
                worldCoord = src.worldCoord,
                moveTargetCoord = src.moveTargetCoord,
                hasMoveOrder = src.hasMoveOrder,
                strengthSoldiers = src.strengthSoldiers,
                maxSoldiers = src.maxSoldiers,
                mapMoveSpeed = src.mapMoveSpeed
            };
        }

        private void OnGUI()
        {
            if (meta == null) return;
            EnsureGuiStyles();

            // 상단 전략 정보 바
            GUI.Box(new Rect(12, 12, Screen.width - 24, 44), GUIContent.none, boxStyle);
            string info = $"대륙: {meta.displayName} ({meta.TotalWidthMeters / 1000f:F1}km x {meta.TotalDepthMeters / 1000f:F1}km)  |  {statusBanner}";
            GUI.Label(new Rect(24, 22, Screen.width - 48, 28), info, headerStyle);

            // 좌측 주요 거점 이동 및 즉시 전장 진입 패널
            GUI.Box(new Rect(12, 66, 250, 320), GUIContent.none, boxStyle);
            GUI.Label(new Rect(22, 74, 230, 22), "주요 거점 빠른 이동 & 전장 진입", headerStyle);

            if (networks != null && networks.nodes != null)
            {
                int shown = 0;
                for (int i = 0; i < networks.nodes.Count && shown < 6; i++)
                {
                    StrategicNodeData n = networks.nodes[i];
                    if (GUI.Button(new Rect(22, 102 + shown * 28, 230, 24), n.displayName))
                    {
                        FocusCameraOnCoord(n.coord);
                    }
                    shown++;
                }
            }

            if (selectedArmy != null && GUI.Button(new Rect(22, 282, 230, 36), "⚔️ 현재 선택 군단 위치로 전장 진입"))
            {
                EnterBattleSceneAt(selectedArmy, null);
            }

            if (selectedArmy != null)
            {
                double2 d = selectedArmy.worldCoord.ToDouble2(meta.sectorSizeMeters);
                GUI.Label(new Rect(22, 324, 230, 52),
                    $"선택: {selectedArmy.displayName}\n절대좌표: ({d.x / 1000.0:F2}km, {d.y / 1000.0:F2}km)");
            }

            // 적군 조우 교전 팝업
            if (showEncounterPopup && pendingPlayerEncounterArmy != null && pendingEnemyEncounterArmy != null)
            {
                float pw = 380f;
                float ph = 185f;
                Rect popup = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
                GUI.Box(popup, GUIContent.none, boxStyle);

                GUI.Label(new Rect(popup.x + 18, popup.y + 16, pw - 36, 28), "⚔️ 적 군단과 조우했습니다!", headerStyle);
                GUI.Label(new Rect(popup.x + 18, popup.y + 48, pw - 36, 52),
                    $"아군: {pendingPlayerEncounterArmy.displayName} ({pendingPlayerEncounterArmy.strengthSoldiers}명)\n" +
                    $"적군: {pendingEnemyEncounterArmy.displayName} ({pendingEnemyEncounterArmy.strengthSoldiers}명)\n" +
                    $"교전 위치: {pendingPlayerEncounterArmy.worldCoord}");

                if (GUI.Button(new Rect(popup.x + 18, popup.y + 122, 118, 40), "⚔️ 직접 지휘"))
                {
                    showEncounterPopup = false;
                    EnterBattleSceneAt(pendingPlayerEncounterArmy, pendingEnemyEncounterArmy);
                }
                if (GUI.Button(new Rect(popup.x + 144, popup.y + 122, 118, 40), "🎲 자동전투"))
                {
                    showEncounterPopup = false;
                    ResolveAutoBattle(pendingPlayerEncounterArmy, pendingEnemyEncounterArmy);
                }
                if (GUI.Button(new Rect(popup.x + 270, popup.y + 122, 92, 40), "후퇴"))
                {
                    showEncounterPopup = false;
                }
            }
        }

        private void EnsureGuiStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                headerStyle.normal.textColor = new Color(0.95f, 0.90f, 0.72f);
            }
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
            }
        }
    }
}
