using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 오픈월드 전투 씬(MainBattle.unity)에서 3x3(9개) 섹터 지형을 실시간으로 풀링·스티칭하고,
    /// 부대/카메라가 섹터 경계 밖으로 이동하면 끝없이 다음 지형을 불러오며 Floating Origin을 재정렬합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeamlessBattleWorldStreamer : MonoBehaviour
    {
        public static SeamlessBattleWorldStreamer Instance { get; private set; }

        [Header("대륙 및 지형 테마 설정")]
        public string continentId = "ThreeKingdoms_China";

        [Tooltip("기후 순서: [0]Temperate, [1]Nanman, [2]Arid, [3]Subtropical, [4]Cold")]
        public MiniTotalWar.TotalWarTerrainTheme[] climateThemes = new MiniTotalWar.TotalWarTerrainTheme[5];

        [Tooltip("5번째 스플랫 채널([4] 수변 자갈/모래)용 기본 레이어")]
        public TerrainLayer shoreGravelLayer;

        [Header("심리스 스트리밍 옵션")]
        public bool enableContinuousStreaming = true;
        public bool spawnTacticalForestZones = true;
        public float streamingCheckInterval = 0.25f;
        public Transform streamingFocusTarget;

        private WorldMetaData meta;
        private WorldSourceCacheData sourceCache;
        private MasterNetworkData networks;
        private string worldFolder;

        private readonly Terrain[,] tileGrid = new Terrain[3, 3];
        private readonly (int sx, int sz)[,] slotCoords = new (int, int)[3, 3];
        private readonly Dictionary<(int, int), GameObject> sectorForestContainers = new Dictionary<(int, int), GameObject>();
        private readonly Dictionary<ContinentClimateId, TerrainLayer[]> cachedClimateLayerSets = new Dictionary<ContinentClimateId, TerrainLayer[]>();

        private int centerSectorX;
        private int centerSectorZ;
        private float checkTimer;
        private bool isInitialized;
        private Transform terrainRoot;
        private Transform forestRoot;
        private Material sharedUrpTerrainMaterial;
        private int groundLayerIndex = 8;
        private TacticalForestZone.Season currentSeason = TacticalForestZone.Season.Summer;
        private float currentSnowAmount = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            int resolvedLayer = LayerMask.NameToLayer("Ground");
            groundLayerIndex = (resolvedLayer >= 0) ? resolvedLayer : 8;
        }

        private void OnEnable()
        {
            if (FloatingOriginManager.Instance != null)
            {
                FloatingOriginManager.Instance.OnOriginShifted += HandleOriginShifted;
            }
        }

        private void OnDisable()
        {
            if (FloatingOriginManager.Instance != null)
            {
                FloatingOriginManager.Instance.OnOriginShifted -= HandleOriginShifted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 지정된 대륙 절대 좌표 중심으로 3x3 지형 타일 창을 초기 구축합니다.
        /// </summary>
        public void InitializeAtWorldCoord(WorldCoord centerCoord, TacticalForestZone.Season season = TacticalForestZone.Season.Summer, float snowAmount = 0f)
        {
            currentSeason = season;
            currentSnowAmount = snowAmount;

            if (!OpenWorldSceneLoader.EnsureContinentLoaded(continentId))
            {
                Debug.LogError("[SeamlessBattleWorldStreamer] 대륙 데이터 로드에 실패했습니다.");
                return;
            }

            meta = OpenWorldSceneLoader.CachedMeta;
            sourceCache = OpenWorldSceneLoader.CachedSourceCache;
            networks = OpenWorldSceneLoader.CachedNetworks;
            worldFolder = OpenWorldSceneLoader.GetActiveWorldFolder(meta.continentId);

            centerSectorX = Mathf.Clamp(centerCoord.sectorX, 1, Mathf.Max(1, meta.sectorCountX - 2));
            centerSectorZ = Mathf.Clamp(centerCoord.sectorZ, 1, Mathf.Max(1, meta.sectorCountZ - 2));

            if (FloatingOriginManager.Instance != null)
            {
                FloatingOriginManager.Instance.sectorSize = meta.sectorSizeMeters;
                FloatingOriginManager.Instance.SetOrigin(centerSectorX, centerSectorZ, notify: false);
            }

            EnsureRootContainers();
            EnsureSharedTerrainMaterial();
            CreateOrRefreshAllNineTiles();
            FinalizeTileGridBinding();

            isInitialized = true;
            Debug.Log($"[SeamlessBattleWorldStreamer] 3x3 오픈월드 전장 구축 완료: 중심 Sector({centerSectorX}, {centerSectorZ})");
        }

        private void Update()
        {
            if (!isInitialized || !enableContinuousStreaming) return;

            checkTimer += Time.deltaTime;
            if (checkTimer < streamingCheckInterval) return;
            checkTimer = 0f;

            Transform focus = streamingFocusTarget != null
                ? streamingFocusTarget
                : (Camera.main != null ? Camera.main.transform : null);
            if (focus == null || FloatingOriginManager.Instance == null) return;

            // 부대/카메라가 원점 임계치(1,024m)를 벗어나면 Floating Origin 재정렬
            FloatingOriginManager.Instance.TryShiftOrigin(focus.position);

            WorldCoord focusWorld = FloatingOriginManager.Instance.LocalToWorld(focus.position);
            int targetSX = Mathf.Clamp(focusWorld.sectorX, 1, Mathf.Max(1, meta.sectorCountX - 2));
            int targetSZ = Mathf.Clamp(focusWorld.sectorZ, 1, Mathf.Max(1, meta.sectorCountZ - 2));

            if (targetSX != centerSectorX || targetSZ != centerSectorZ)
            {
                SlideWindowTo(targetSX, targetSZ);
            }
        }

        private void SlideWindowTo(int newCenterX, int newCenterZ)
        {
            int shiftX = newCenterX - centerSectorX;
            int shiftZ = newCenterZ - centerSectorZ;

            centerSectorX = newCenterX;
            centerSectorZ = newCenterZ;

            if (Mathf.Abs(shiftX) >= 3 || Mathf.Abs(shiftZ) >= 3)
            {
                CreateOrRefreshAllNineTiles();
                FinalizeTileGridBinding();
                return;
            }

            Terrain[,] nextGrid = new Terrain[3, 3];
            var recycled = new List<Terrain>(9);
            bool[,] reused = new bool[3, 3];

            // 1. 새 창과 겹치는 기존 타일은 그대로 재사용(높이/스플랫 재생성 0회!)
            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    int oldGX = gx + shiftX;
                    int oldGZ = gz + shiftZ;
                    if (oldGX >= 0 && oldGX < 3 && oldGZ >= 0 && oldGZ < 3 && tileGrid[oldGX, oldGZ] != null)
                    {
                        nextGrid[gx, gz] = tileGrid[oldGX, oldGZ];
                        reused[oldGX, oldGZ] = true;
                    }
                }
            }

            // 2. 창 밖으로 밀려난 타일 수집
            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    if (!reused[gx, gz] && tileGrid[gx, gz] != null)
                    {
                        recycled.Add(tileGrid[gx, gz]);
                        RemoveSectorForests(slotCoords[gx, gz]);
                    }
                }
            }

            // 3. 빈 가장자리 슬롯에 재활용 타일을 배치하고 새 섹터 지형 즉석 합성
            int recycleIdx = 0;
            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    int sx = centerSectorX + (gx - 1);
                    int sz = centerSectorZ + (gz - 1);

                    if (nextGrid[gx, gz] == null)
                    {
                        Terrain tile = (recycleIdx < recycled.Count)
                            ? recycled[recycleIdx++]
                            : CreateTerrainTileObject($"SectorTile_{gx}_{gz}");
                        nextGrid[gx, gz] = tile;
                        ApplySectorToTile(tile, sx, sz);
                    }

                    slotCoords[gx, gz] = (sx, sz);
                }
            }

            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    tileGrid[gx, gz] = nextGrid[gx, gz];
                }
            }

            FinalizeTileGridBinding();
            Debug.Log($"[SeamlessBattleWorldStreamer] 전장 슬라이딩 스트리밍 완료 -> 중심 Sector({centerSectorX}, {centerSectorZ})");
        }

        private void EnsureRootContainers()
        {
            if (terrainRoot == null)
            {
                GameObject go = new GameObject("OpenWorld_TerrainGrid_3x3");
                go.transform.SetParent(transform, false);
                terrainRoot = go.transform;
            }
            if (forestRoot == null)
            {
                GameObject go = new GameObject("OpenWorld_ForestZones");
                go.transform.SetParent(transform, false);
                forestRoot = go.transform;
            }
        }

        private void EnsureSharedTerrainMaterial()
        {
            if (sharedUrpTerrainMaterial != null) return;
            Shader urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard");
            if (urpTerrainShader != null)
            {
                sharedUrpTerrainMaterial = new Material(urpTerrainShader)
                {
                    name = "OpenWorld_URP_Terrain_Mat"
                };
            }
        }

        private void CreateOrRefreshAllNineTiles()
        {
            foreach (var kvp in sectorForestContainers)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            sectorForestContainers.Clear();

            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    if (tileGrid[gx, gz] == null)
                    {
                        tileGrid[gx, gz] = CreateTerrainTileObject($"SectorTile_{gx}_{gz}");
                    }

                    int sx = centerSectorX + (gx - 1);
                    int sz = centerSectorZ + (gz - 1);
                    ApplySectorToTile(tileGrid[gx, gz], sx, sz);
                    slotCoords[gx, gz] = (sx, sz);
                }
            }
        }

        private Terrain CreateTerrainTileObject(string tileName)
        {
            GameObject go = new GameObject(tileName);
            go.transform.SetParent(terrainRoot, false);
            go.layer = groundLayerIndex;

            TerrainData tData = new TerrainData();
            Terrain tile = go.AddComponent<Terrain>();
            tile.terrainData = tData;
            TerrainCollider col = go.AddComponent<TerrainCollider>();
            col.terrainData = tData;

            if (sharedUrpTerrainMaterial != null)
            {
                tile.materialTemplate = sharedUrpTerrainMaterial;
            }
            tile.drawInstanced = true;
            tile.groupingID = 0;
            tile.allowAutoConnect = false;
            return tile;
        }

        private void ApplySectorToTile(Terrain tile, int sx, int sz)
        {
            int clampedX = Mathf.Clamp(sx, 0, Mathf.Max(0, meta.sectorCountX - 1));
            int clampedZ = Mathf.Clamp(sz, 0, Mathf.Max(0, meta.sectorCountZ - 1));

            SectorBinaryData sector = null;
            var key = (clampedX, clampedZ);
            if (OpenWorldSceneLoader.PreSynthesizedSectors.TryGetValue(key, out SectorBinaryData preBuilt))
            {
                sector = preBuilt;
                OpenWorldSceneLoader.PreSynthesizedSectors.Remove(key);
            }
            else
            {
                sector = ContinentSectorSynthesizer.LoadOrSynthesizeSector(worldFolder, meta, sourceCache, networks, clampedX, clampedZ);
            }

            Vector3 localPos = (FloatingOriginManager.Instance != null)
                ? FloatingOriginManager.Instance.WorldToLocal(new WorldCoord(sx, sz, 0f, 0f), 0f)
                : new Vector3(sx * meta.sectorSizeMeters, 0f, sz * meta.sectorSizeMeters);

            tile.transform.position = localPos;
            tile.name = $"Terrain_Sector_{sx}_{sz}";

            TerrainData td = tile.terrainData;
            if (td.heightmapResolution != sector.heightRes)
            {
                td.heightmapResolution = sector.heightRes;
            }
            // 반드시 heightmapResolution 이후에 size 설정
            td.size = new Vector3(meta.sectorSizeMeters, meta.maxTerrainHeight, meta.sectorSizeMeters);
            td.SetHeights(0, 0, sector.ToUnityTerrainHeights());

            TerrainLayer[] layers = ResolveFiveTerrainLayers(sector.dominantClimate);
            if (layers != null && layers.Length > 0)
            {
                td.terrainLayers = layers;
                if (td.alphamapResolution != sector.splatRes)
                {
                    td.alphamapResolution = sector.splatRes;
                }
                if (td.alphamapLayers >= 5)
                {
                    td.SetAlphamaps(0, 0, sector.ToUnityAlphamaps());
                }
            }

            if (spawnTacticalForestZones && sx == clampedX && sz == clampedZ)
            {
                SpawnSectorForests(sx, sz, localPos);
            }
        }

        private TerrainLayer[] ResolveFiveTerrainLayers(ContinentClimateId climate)
        {
            if (cachedClimateLayerSets.TryGetValue(climate, out TerrainLayer[] cached))
            {
                return cached;
            }

            MiniTotalWar.TotalWarTerrainTheme theme = GetThemeForClimate(climate);
            TerrainLayer l0 = null, l1 = null, l2 = null, l3 = null;
            if (theme != null)
            {
                l0 = (currentSeason == TacticalForestZone.Season.Autumn && theme.autumnGrassLayer != null) ? theme.autumnGrassLayer :
                     (currentSeason == TacticalForestZone.Season.Winter && theme.winterGrassLayer != null) ? theme.winterGrassLayer :
                     theme.summerGrassLayer;
                l1 = theme.cliffRockLayer != null ? theme.cliffRockLayer : l0;
                l2 = theme.dirtRoadLayer != null ? theme.dirtRoadLayer : l0;
                l3 = theme.forestFloorLayer != null ? theme.forestFloorLayer : l0;
            }

            TerrainLayer l4 = shoreGravelLayer != null ? shoreGravelLayer : l2;
            TerrainLayer fallback = l0 ?? l4;
            if (fallback == null) return null;

            TerrainLayer[] set = new TerrainLayer[]
            {
                l0 ?? fallback,
                l1 ?? fallback,
                l2 ?? fallback,
                l3 ?? fallback,
                l4 ?? fallback
            };
            cachedClimateLayerSets[climate] = set;
            return set;
        }

        private MiniTotalWar.TotalWarTerrainTheme GetThemeForClimate(ContinentClimateId climate)
        {
            int idx = (int)climate;
            if (climateThemes != null && idx >= 0 && idx < climateThemes.Length && climateThemes[idx] != null)
            {
                return climateThemes[idx];
            }
            if (climateThemes != null && climateThemes.Length > 0)
            {
                return climateThemes[0];
            }
            return null;
        }

        private void SpawnSectorForests(int sx, int sz, Vector3 sectorLocalOrigin)
        {
            var key = (sx, sz);
            if (sectorForestContainers.ContainsKey(key)) return;

            List<MasterForestData> forests = ContinentSectorSynthesizer.ExtractSectorForests(meta, sourceCache, networks, sx, sz, 3);
            if (forests == null || forests.Count == 0) return;

            GameObject container = new GameObject($"Forests_{sx}_{sz}");
            container.transform.SetParent(forestRoot, false);
            sectorForestContainers[key] = container;

            int maxPerSector = Mathf.Min(4, forests.Count);
            for (int i = 0; i < maxPerSector; i++)
            {
                MasterForestData f = forests[i];
                Vector3 localPos = sectorLocalOrigin + new Vector3(f.center.localX, 0f, f.center.localZ);
                float h = meta.seaLevelY;
                Terrain tile = tileGrid[1, 1];
                if (tile != null)
                {
                    h = tile.SampleHeight(localPos) + tile.transform.position.y;
                }
                localPos.y = h;

                GameObject zoneObj = new GameObject(f.forestId);
                zoneObj.transform.SetParent(container.transform, false);
                zoneObj.transform.position = localPos;

                TacticalForestZone zone = zoneObj.AddComponent<TacticalForestZone>();
                zone.shape = TacticalForestZone.ZoneShape.Circle;
                zone.radius = f.radius;
                zone.currentSeason = currentSeason;
                zone.enableWinterSnow = currentSnowAmount > 0.1f;
                zone.winterSnowAmount = currentSnowAmount;
                zone.treeCount = 8;
                zone.bushCount = 10;
                zone.randomSeed = meta.seed + sx * 97 + sz * 31 + i;

                TacticalForestZone.ForestTheme fTheme = f.climate == ContinentClimateId.Nanman ? TacticalForestZone.ForestTheme.NanmanJungle :
                    f.climate == ContinentClimateId.Subtropical ? TacticalForestZone.ForestTheme.JiangnanRiverside :
                    f.climate == ContinentClimateId.Cold ? TacticalForestZone.ForestTheme.HuangshanPine :
                    TacticalForestZone.ForestTheme.CentralPlains;
                zone.ApplyThemePreset(fTheme);
            }
        }

        private void RemoveSectorForests((int sx, int sz) coordKey)
        {
            if (sectorForestContainers.TryGetValue(coordKey, out GameObject container))
            {
                if (container != null) Destroy(container);
                sectorForestContainers.Remove(coordKey);
            }
        }

        private void FinalizeTileGridBinding()
        {
            List<Terrain> activeList = new List<Terrain>(9);

            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    Terrain t = tileGrid[gx, gz];
                    if (t == null) continue;

                    Terrain left = (gx > 0) ? tileGrid[gx - 1, gz] : null;
                    Terrain right = (gx < 2) ? tileGrid[gx + 1, gz] : null;
                    Terrain bottom = (gz > 0) ? tileGrid[gx, gz - 1] : null;
                    Terrain top = (gz < 2) ? tileGrid[gx, gz + 1] : null;

                    t.SetNeighbors(left, top, right, bottom);
                    t.Flush();
                    activeList.Add(t);
                }
            }

            if (TerrainHeightManager.Instance != null)
            {
                TerrainHeightManager.Instance.InitializeFromTerrains(activeList, 129);
            }

            if (TotalWarSeaPlane.Instance != null && TerrainHeightManager.HasInstance)
            {
                TotalWarSeaPlane.Instance.ConfigureForWorldBounds(TerrainHeightManager.Instance.ActiveWorldBounds);
            }
        }

        private void HandleOriginShifted(Vector3 delta)
        {
            // 1. 3x3 터레인 타일 및 숲 평행 이동
            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    if (tileGrid[gx, gz] != null)
                    {
                        tileGrid[gx, gz].transform.position += delta;
                    }
                }
            }

            foreach (var kvp in sectorForestContainers)
            {
                if (kvp.Value != null) kvp.Value.transform.position += delta;
            }

            // 2. 카메라 이동
            CameraController camCtrl = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
            if (camCtrl != null)
            {
                camCtrl.ShiftPosition(delta);
            }
            else if (Camera.main != null)
            {
                Camera.main.transform.position += delta;
            }

            // 3. 바다 평면 이동
            if (TotalWarSeaPlane.Instance != null)
            {
                TotalWarSeaPlane.Instance.transform.position += delta;
            }

            // 4. 부대 및 유닛(GameObject + Pure ECS) 이동
            if (BattleManager.Instance != null)
            {
                var squads = BattleManager.Instance.GetAllSquads();
                for (int i = 0; i < squads.Count; i++)
                {
                    Squad s = squads[i];
                    if (s == null) continue;
                    s.transform.position += delta;
                    s.moveDestination += delta;
                    if (s.members != null)
                    {
                        for (int m = 0; m < s.members.Count; m++)
                        {
                            if (s.members[m] != null) s.members[m].transform.position += delta;
                        }
                    }
                }
            }

            var ecsWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (ecsWorld != null && ecsWorld.IsCreated)
            {
                EntityManager em = ecsWorld.EntityManager;
                using (EntityQuery query = em.CreateEntityQuery(typeof(MiniTotalWar.ECS.UnitMovementData)))
                {
                    if (!query.IsEmptyIgnoreFilter)
                    {
                        var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Allocator.Temp);
                        try
                        {
                            float3 fDelta = new float3(delta.x, delta.y, delta.z);
                            for (int i = 0; i < movs.Length; i++)
                            {
                                var m = movs[i];
                                m.Position += fDelta;
                                m.TargetPosition += fDelta;
                                movs[i] = m;
                            }
                            query.CopyFromComponentDataArray(movs);
                        }
                        finally
                        {
                            if (movs.IsCreated) movs.Dispose();
                        }
                    }
                }
            }

            // 5. 높이맵 원점 재동기화
            FinalizeTileGridBinding();
            Debug.Log($"[SeamlessBattleWorldStreamer] Floating Origin 재정렬 완료 (Delta: {delta})");
        }

        private void OnGUI()
        {
            if (!isInitialized || meta == null) return;

            float w = 310f;
            float x = Screen.width - w - 12f;
            GUI.Box(new Rect(x, 12, w, 92), GUIContent.none);

            WorldCoord centerAbs = new WorldCoord(centerSectorX, centerSectorZ, meta.sectorSizeMeters * 0.5f, meta.sectorSizeMeters * 0.5f);
            double2 d = centerAbs.ToDouble2(meta.sectorSizeMeters);

            GUI.Label(new Rect(x + 12, 18, w - 24, 22), $"오픈월드 전장 Sector({centerSectorX}, {centerSectorZ}) [3x3 활성]");
            GUI.Label(new Rect(x + 12, 38, w - 24, 22), $"대륙 절대 위치: ({d.x / 1000.0:F2}km, {d.y / 1000.0:F2}km)");

            if (GUI.Button(new Rect(x + 12, 64, w - 24, 30), "🗺️ 캠페인 지도(MainMap)로 복귀"))
            {
                OpenWorldSceneLoader.LoadMainMapViaLoadingScene();
            }
        }
    }
}
