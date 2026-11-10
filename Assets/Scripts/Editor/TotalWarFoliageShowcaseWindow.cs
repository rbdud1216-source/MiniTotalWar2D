using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌱 [삼탈워 식생 즉석 쇼케이스 & 지면 띄움 캘리브레이터]
    /// - 전체 청크를 무겁게 베이크(Bake)하지 않고도, 0.01초 만에 씬 뷰 지면에 3D 풀을 즉석 소환하여 확인합니다.
    /// - 364종의 삼탈워 식생 모델을 [◀ 이전], [▶ 다음] 단축키로 넘겨보며 원하는 그 풀을 즉시 색출합니다.
    /// - 지면 띄움(Ground Offset) 슬라이더를 실시간으로 조절하며 바닥에 완벽히 밀착되는 높이를 눈으로 확인합니다.
    /// - [✨ 지면 자동 밀착] 버튼으로 피벗이 제각각인 메쉬의 최하단 높이를 수학적으로 자동 계산합니다.
    /// - 확인된 식생과 띄움값을 현재 터레인 식생 팔레트나 특정 청크에 즉시 1클릭으로 바인딩합니다.
    /// </summary>
    public class TotalWarFoliageShowcaseWindow : EditorWindow
    {
        [MenuItem("TotalWar/🌱 삼탈워 식생 즉석 쇼케이스 & 지면 띄움 캘리브레이터...", false, 1)]
        public static void OpenWindow()
        {
            var window = GetWindow<TotalWarFoliageShowcaseWindow>("식생 쇼케이스 & 캘리브레이터");
            window.minSize = new Vector2(460, 680);
            window.Show();
        }

        public static void OpenWindowForChunk(int chunkX, int chunkZ)
        {
            var window = GetWindow<TotalWarFoliageShowcaseWindow>("식생 쇼케이스 & 캘리브레이터");
            window.minSize = new Vector2(460, 680);
            window.locationMode = LocationMode.SpecificChunk;
            window.targetChunkX = chunkX;
            window.targetChunkZ = chunkZ;
            window.Show();
            window.UpdateSpawnPositionFromChunk();
            window.FocusSceneCameraOnShowcase();
        }

        public enum LocationMode
        {
            InFrontOfCamera = 0, // 씬 카메라 앞 지면
            SpecificChunk = 1,   // 지정 청크 중심 지면
            CustomWorldPos = 2   // 커스텀 월드 좌표
        }

        public enum DisplayMode
        {
            Single = 0,          // 단일 포기 정밀 관찰 (밑둥 접촉면 1:1 관찰)
            Patch3x3 = 1         // 3x3 군락 패치 관찰 (겹침 및 지피 커버리지 확인)
        }

        public struct ShowcaseItem
        {
            public string displayName;
            public string modelFile;
            public string albedoFile;
            public string normalFile;
            public string category;
            public string fullPath;
            public float defaultWidth;
            public float defaultHeight;
            public float defaultOffset;
            public float defaultNormalAlign;
            public int vertexCount;
            public bool isCarpet;
            public bool isShrub;
            public bool flipY; // 상하 반전 여부 (카탈로그/사용자 설정)
        }

        [Serializable]
        public class MasterCatalogData
        {
            public CatalogEntry[] items;
        }

        [Serializable]
        public class CatalogEntry
        {
            public string modelFile;
            public string folder;
            public bool isShrub;
            public string category;
            public string texture;
            public float width;
            public float height;
            public float depth;
            public float bottomY;
            public float offset;
            public float normalAlign;
            public int vertices;
            public string fullPath;
            public bool flipY; // 상하 반전 기본값 (하수형/거꾸로 된 풀 보정)
        }

        // ==========================================
        // 상태 변수
        // ==========================================
        private List<ShowcaseItem> _allItems = new List<ShowcaseItem>();
        private List<ShowcaseItem> _filteredItems = new List<ShowcaseItem>();
        private int _currentIndex = 0;
        private string _searchKeyword = "";
        private int _selectedCategoryTab = 0;
        private Vector2 _scrollPos;
        private string _batchFlipInput = ""; // 사용자 입력 일괄 반전 번호 목록 (예: "3, 5, 12, 15-20")

        private static readonly string[] CategoryTabs = new[]
        {
            "전체 (All)",
            "🌾 지피 잔디",
            "🌿 3D 풀포기",
            "🎋 갈대",
            "🌲 고사리",
            "🪷 수생식물",
            "🌳 수목",
            "🌼 야생화",
            "🌾 농작물 (벼/밀)",
            "🍂 서량 건조",
            "❄️ 북방 한랭",
            "🌴 남만 열대"
        };

        // 📍 소환 위치 설정
        [SerializeField] private LocationMode locationMode = LocationMode.InFrontOfCamera;
        [SerializeField] private int targetChunkX = 31;
        [SerializeField] private int targetChunkZ = 31;
        [SerializeField] private Vector3 customWorldPos = Vector3.zero;
        [SerializeField] private DisplayMode displayMode = DisplayMode.Single;
        [SerializeField] private float patchSpacing = 1.2f;
        [SerializeField] private bool showGroundDisc = true; // 초록색 지표면 가이드 영역 디스크 표시 여부 (단축키 G)

        // 📏 실시간 캘리브레이션 파라미터
        [SerializeField] private float groundOffset = 0.0f; // 지면 띄움 오프셋 (-1.5m ~ +2.0m)
        [SerializeField] private float scale = 1.0f;
        [SerializeField] private float heightMultiplier = 1.0f;
        [SerializeField] private float alignToNormal = 1.0f; // 0: 수직 직립, 1: 지형 경사면 100% 밀착
        [SerializeField] private bool flipY = false; // 상하 반전 (거꾸로 자란 풀을 하늘로 똑바로 세우기)
        [SerializeField] private int selectedMaterialIndex = 0; // 0: 모델 전용 고유 텍스처 자동 매핑 (권장)

        private static readonly string[] MaterialOptions = new[]
        {
            "🌸 [자동 매핑] 모델 전용 텍스처 (꽃/고사리/원작 1:1 고유 색상)",
            "🌱 [강제 틴트] 봄철 신록 연두빛 (Lime Green Carpet)",
            "🌿 [강제 틴트] 여름 기본 녹색 (Summer Green)",
            "🍂 [강제 틴트] 가을 황갈색 단풍 (Autumn Tint)",
            "❄️ [강제 틴트] 겨울 설초 적설 (Winter Snow)"
        };

        // 프리뷰 게임오브젝트 참조 (HideAndDontSave)
        private GameObject _previewRoot;
        private Mesh _currentMesh;
        private Material _currentMat;
        private Vector3 _currentGroundPos;
        private Vector3 _currentGroundNormal = Vector3.up;
        private Terrain _cachedTerrain;

        // 🎨 머티리얼 셰이더 실시간 튜닝 & 안전 저장 샌드박스
        private Material _previewInstanceMat; // 원본 머티리얼 보존용 임시 복제 인스턴스
        private bool _foldMaterialTuning = true;
        private float _tuneTipFadeStart = 0.940f;
        private float _tuneTipFadeEnd = 0.985f;
        private float _tuneEdgeBleed = 0.006f;
        private float _tuneCutoff = 0.25f;
        private Color _tuneBaseColor = Color.white;
        private float _tuneWindMultiplier = 0.35f;
        private float _tuneLeafFlutter = 0.25f;
        private bool _hasUnsavedMaterialChanges = false;
        private string _lastLoadedMatPath = "";

        // ✂️ 절차적 텍스처 UV 자르기 & 메쉬 컷 실시간 조절 파라미터
        private bool _foldUvCropTuning = false;
        [SerializeField] private bool _useGroundedTuftMesh = false; // 정제된 절차적 메쉬는 이미 중심 피벗(0,0) 정규화가 완료되어 있으므로 기본 OFF
        [SerializeField] private float _tipCropPercent = 0.00f;    // 풀잎 끝 텍스처 자르기/압축 강도 (기본: 0%)
        [SerializeField] private float _sideMarginPercent = 0.00f; // 좌우 테두리 여백 클램핑 (기본: 0%)
        [SerializeField] private float _meshTipCutPercent = 0.00f; // 3D 풀잎 끝 메쉬 높이 직접 절단 강도 (기본: 0%)
        private Mesh _previewCustomMesh; // 실시간 UV 및 접지 변형이 주입된 프리뷰 전용 메쉬 인스턴스

        // 팔레트 덮어쓰기 슬롯 인덱스
        private int _overwriteSlotIndex = 0;

        private void OnEnable()
        {
            ScanAllModels();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            FindTerrain();
            UpdateSpawnPosition();
            if (_filteredItems != null && _filteredItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _filteredItems.Count)
            {
                ResetParamsForItem(_filteredItems[_currentIndex]);
            }
            CreateOrUpdatePreviewObject();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreviewObject();
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreviewObject();
        }

        private void FindTerrain()
        {
            _cachedTerrain = Terrain.activeTerrain;
            if (_cachedTerrain == null)
            {
                var mgr = UnityEngine.Object.FindAnyObjectByType<TotalWarTerrainManager>();
                if (mgr != null && mgr.targetTerrain != null) _cachedTerrain = mgr.targetTerrain;
            }
        }

        /// <summary>
        /// 4대 식생 유형 정제 메쉬 및 46종 수목 쇼케이스 표준 아이템 생성
        /// </summary>
        public static List<ShowcaseItem> GetStandardShowcaseItems()
        {
            var list = new List<ShowcaseItem>
            {
                // ① [지피 잔디 / 야생화 10열 교차 군락]
                new ShowcaseItem
                {
                    displayName = "🌾 [지피 잔디] 온대 순수 잔디 다방향 군락 (10열 교차)",
                    modelFile = "TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset",
                    albedoFile = "shrub_grassclump_sp_base_colour.png",
                    normalFile = "shrub_grassclump_normal.png",
                    category = "🌾 지피 잔디",
                    fullPath = "Assets/Settings/Vegetation/RadialMeshes/TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset",
                    defaultWidth = 0.90f, defaultHeight = 0.40f, defaultOffset = -0.015f, defaultNormalAlign = 0.25f, vertexCount = 60, isCarpet = true
                },
                new ShowcaseItem
                {
                    displayName = "🌸 [지피 잔디] 분홍 꽃망울 다방향 군락 (Photo 1)",
                    modelFile = "TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset",
                    albedoFile = "tem_grass_sp_base_colour.png",
                    normalFile = "shrub_grassclump_normal.png",
                    category = "🌾 지피 잔디",
                    fullPath = "Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset",
                    defaultWidth = 0.90f, defaultHeight = 0.42f, defaultOffset = -0.015f, defaultNormalAlign = 0.25f, vertexCount = 60, isCarpet = true
                },
                new ShowcaseItem
                {
                    displayName = "💜 [지피 잔디] 연보라 꽃송이 다방향 군락 (Photo 2)",
                    modelFile = "TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset",
                    albedoFile = "tem_grass_sp_base_colour.png",
                    normalFile = "shrub_grassclump_normal.png",
                    category = "🌾 지피 잔디",
                    fullPath = "Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset",
                    defaultWidth = 0.90f, defaultHeight = 0.42f, defaultOffset = -0.015f, defaultNormalAlign = 0.25f, vertexCount = 60, isCarpet = true
                },
                new ShowcaseItem
                {
                    displayName = "🧡 [지피 잔디] 주황 꽃이삭 다방향 군락 (Photo 3)",
                    modelFile = "TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset",
                    albedoFile = "sbt_grass0_sp_base_colour.png",
                    normalFile = "shrub_grassclump_normal.png",
                    category = "🌾 지피 잔디",
                    fullPath = "Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset",
                    defaultWidth = 0.90f, defaultHeight = 0.44f, defaultOffset = -0.015f, defaultNormalAlign = 0.25f, vertexCount = 60, isCarpet = true
                },
                new ShowcaseItem
                {
                    displayName = "💙 [지피 잔디] 남만 청보라 들꽃 다방향 군락",
                    modelFile = "TotalWar_FlowerCluster_BluePetals_Sbt_Mesh.asset",
                    albedoFile = "sbt_grass0_sp_base_colour.png",
                    normalFile = "shrub_grassclump_normal.png",
                    category = "🌾 지피 잔디",
                    fullPath = "Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_BluePetals_Sbt_Mesh.asset",
                    defaultWidth = 0.90f, defaultHeight = 0.42f, defaultOffset = -0.015f, defaultNormalAlign = 0.25f, vertexCount = 60, isCarpet = true
                }
            };

            // RadialMeshes 폴더에 생성된 프리팹 에셋 자동 등록 (마스터 메쉬 공유 및 사계절 전수 지원)
            string radialDir = "Assets/Settings/Vegetation/RadialMeshes";
            if (Directory.Exists(radialDir))
            {
                string[] prefabFiles = Directory.GetFiles(radialDir, "TotalWar_*_Prefab.prefab");
                foreach (var p in prefabFiles)
                {
                    string fileName = Path.GetFileName(p);
                    if (list.Exists(x => x.modelFile == fileName)) continue;

                    string cat = "🌾 지피 잔디";
                    string prefix = "🌾 [지피 잔디] ";
                    string albedo = "tem_grass_sp_base_colour.png";
                    string normal = "shrub_grassclump_normal.png";
                    float defW = 0.90f; float defH = 0.40f;

                    if (fileName.Contains("LotusCluster"))
                    {
                        cat = "🪷 수생식물";
                        prefix = "🪷 [수생식물] ";
                        albedo = "shrubs_ferns_su_base_colour.png";
                        normal = "shrubs_ferns_normal.png";
                        defW = 1.35f; defH = 0.55f;
                    }
                    else if (fileName.Contains("RadialFan3D"))
                    {
                        cat = "🌿 3D 풀포기";
                        prefix = "🌿 [3D 풀포기] ";
                        albedo = "shrub_grassclump_sp_base_colour.png";
                        defW = 0.95f; defH = 0.48f;
                    }
                    else if (fileName.Contains("TallReed3D"))
                    {
                        cat = "🎋 갈대";
                        prefix = "🎋 [갈대] ";
                        albedo = "shrub_grassclump_sp_base_colour.png";
                        defW = 0.90f; defH = 1.05f;
                    }
                    else if (fileName.Contains("ForestFern"))
                    {
                        cat = "🌲 고사리";
                        prefix = "🌲 [고사리] ";
                        albedo = "shrubs_ferns_su_base_colour.png";
                        normal = "shrubs_ferns_normal.png";
                        defW = 0.95f; defH = 0.35f;
                    }

                    list.Add(new ShowcaseItem
                    {
                        displayName = FormatProceduralFoliageKoreanName(fileName, prefix),
                        modelFile = fileName,
                        albedoFile = albedo,
                        normalFile = normal,
                        category = cat,
                        fullPath = p.Replace("\\", "/"),
                        defaultWidth = defW,
                        defaultHeight = defH,
                        defaultOffset = -0.015f,
                        defaultNormalAlign = 0.25f,
                        vertexCount = 120,
                        isCarpet = cat.Contains("지피"),
                        isShrub = !cat.Contains("지피")
                    });
                }
            }

            // ⑤ [수목 46종] TreePrefabs 또는 Trees 폴더 스캔
            string treePrefabDir = "Assets/Settings/Vegetation/TreePrefabs";
            string rawTreeDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees";
            if (Directory.Exists(treePrefabDir))
            {
                string[] prefabs = Directory.GetFiles(treePrefabDir, "*_Prefab.prefab");
                foreach (var p in prefabs)
                {
                    string fileName = Path.GetFileName(p);
                    string cleanName = fileName.Replace("_Prefab.prefab", "").Replace("Tree_", "").Replace("_", " ");
                    list.Add(new ShowcaseItem
                    {
                        displayName = "🌳 [수목] " + cleanName,
                        modelFile = fileName,
                        albedoFile = "",
                        normalFile = "",
                        category = "🌳 수목",
                        fullPath = p.Replace("\\", "/"),
                        defaultWidth = 1.0f,
                        defaultHeight = 1.0f,
                        defaultOffset = 0.0f,
                        defaultNormalAlign = 0.05f,
                        vertexCount = 1500,
                        isCarpet = false,
                        isShrub = false
                    });
                }
            }
            else if (Directory.Exists(rawTreeDir))
            {
                string[] objs = Directory.GetFiles(rawTreeDir, "*.obj");
                foreach (var o in objs)
                {
                    string fileName = Path.GetFileName(o);
                    string cleanName = fileName.Replace(".obj", "").Replace("Tree_", "").Replace("_", " ");
                    list.Add(new ShowcaseItem
                    {
                        displayName = "🌳 [수목] " + cleanName,
                        modelFile = fileName,
                        albedoFile = "",
                        normalFile = "",
                        category = "🌳 수목",
                        fullPath = o.Replace("\\", "/"),
                        defaultWidth = 1.0f,
                        defaultHeight = 1.0f,
                        defaultOffset = 0.0f,
                        defaultNormalAlign = 0.05f,
                        vertexCount = 1500,
                        isCarpet = false,
                        isShrub = false
                    });
                }
            }

            return list;
        }

        private static string FormatProceduralFoliageKoreanName(string fileName, string defaultPrefix)
        {
            string key = fileName.Replace("TotalWar_", "").Replace("_Mesh.asset", "").Replace("_Prefab.prefab", "");
            return key switch
            {
                // 원작 정품 3D 수생 연꽃 군락
                "LotusCluster_Summer" => "🪷 [수생식물] 강남 수생 분홍 연꽃 & 연잎 군락 (여름)",
                "LotusCluster_Autumn" => "🪷 [수생식물] 강남 수생 가을 연꽃 & 단풍 연잎 군락",
                "LotusCluster" => "🪷 [수생식물] 강남 수생 연꽃 군락 (원작 3D 정품)",

                // 야생화 지피 군락
                "FlowerCluster_Cornflower_Sbt" => "💎 [지피 잔디/야생화] 아열대 푸른 수레국화 들꽃 군락",
                "FlowerCluster_YellowDaisy_Sbt" => "🌼 [지피 잔디/야생화] 아열대 노란 들국화 야생화 군락",
                "FlowerCluster_WhiteAster_Sbt" => "🤍 [지피 잔디/야생화] 아열대 하얀 구절초 들꽃 군락",
                "FlowerCluster_ForestViolet_Tem" => "💜 [지피 잔디/야생화] 온대 수림 보라 들꽃 군락",
                "FlowerCluster_ForestPink_Tem" => "🌸 [지피 잔디/야생화] 온대 수림 연분홍 들꽃 군락",
                "FlowerCluster_ColdViolet_Cld" => "❄️ [지피 잔디/야생화] 한랭 설원 보라 들꽃 군락",
                "FlowerCluster_TropicalClover_Tro" => "🍀 [지피 잔디/야생화] 남만 열대 클로버 지피 군락",

                // 3D 풀포기 / 농작물 / 관목꽃
                "RadialFan3D_Spring" => "🌿 [3D 풀포기] 온대 봄 신록 방사형 풀포기",
                "RadialFan3D_Summer" => "🌿 [3D 풀포기] 온대 여름 무성한 방사형 풀포기",
                "RadialFan3D_Autumn" => "🍂 [3D 풀포기] 온대 가을 황금빛 방사형 풀포기",
                "RadialFan3D_Winter" => "❄️ [3D 풀포기] 한랭 겨울 눈 덮인 방사형 풀포기",
                "RadialFan3D_Harvest" => "🌾 [3D 풀포기/농작물] 추수철 황금 기장/조 3D 포기",
                "RadialFan3D_Arid" => "🏜️ [3D 풀포기] 서량 건조 황무지 억센 풀포기",
                "RadialFan3D_RicePaddy_Green" => "🌾 [3D 풀포기/농작물] 강남 수전 초록 벼(Rice Paddy) 포기",
                "RadialFan3D_RicePaddy_Gold" => "🌾 [3D 풀포기/농작물] 추수철 수전 황금 벼(Rice Paddy) 포기",
                "RadialFan3D_Wheat_Gold" => "🌾 [3D 풀포기/농작물] 추수철 황금 보리/밀(Wheat) 포기",
                "RadialFan3D_Wheat_Dry" => "🌾 [3D 풀포기/농작물] 서북 건조 보리/밀(Dry Wheat) 포기",
                "RadialFan3D_ElephantGrass" => "🌴 [3D 풀포기] 남만 열대 코끼리풀(Elephant Grass) 포기",
                "RadialFan3D_TropicalBigleaf" => "🌴 [3D 풀포기] 남만 열대 토란 활엽초(Bigleaf) 포기",
                "RadialFan3D_TropicalFlower" => "🌺 [3D 풀포기/야생화] 남만 열대 정글 야생화 포기",
                "RadialFan3D_Convallaria" => "🌸 [3D 풀포기/야생화] 온대 수림 은방울꽃(Convallaria) 포기",
                "RadialFan3D_Rhododendron_Spring" => "🌺 [3D 풀포기/야생화] 봄 만개 진달래/철쭉 꽃덤불",
                "RadialFan3D_Rhododendron_Summer" => "🌺 [3D 풀포기/야생화] 여름 진달래/철쭉 녹엽 꽃덤불",
                "RadialFan3D_TeaBush_Spring" => "🍃 [3D 풀포기] 강남 봄 햇차 차나무(Tea Bush) 포기",
                "RadialFan3D_TeaBush_Summer" => "🍃 [3D 풀포기] 강남 여름 짙은 차나무(Tea Bush) 포기",
                "RadialFan3D_Magnolia_Spring" => "💮 [3D 풀포기/야생화] 봄 백목련/자목련(Magnolia) 꽃덤불",
                "RadialFan3D_Magnolia_Autumn" => "🍂 [3D 풀포기/야생화] 가을 단풍 목련(Magnolia) 꽃덤불",

                // 키 큰 갈대 / 이삭
                "TallReed3D_Spring" => "🎋 [갈대] 봄 수변 키 큰 깃털 갈대 (0.95m)",
                "TallReed3D_Summer" => "🎋 [갈대] 여름 수변 짙은 깃털 갈대 (1.05m)",
                "TallReed3D_Autumn" => "🎋 [갈대] 가을 황금 억새 갈대 (1.10m)",
                "TallReed3D_Winter" => "❄️ [갈대] 겨울 서리 맺힌 눈 갈대 (0.90m)",
                "TallReed3D_Harvest" => "🌾 [갈대/농작물] 추수철 황금 기장/벼 이삭 줄기 (1.00m)",
                "TallReed3D_Arid" => "🏜️ [갈대] 서량 건조 사막 마른 갈대 (0.88m)",
                "TallReed3D_RiceEar_Green" => "🌾 [갈대/농작물] 강남 수전 초록 벼 이삭 줄기 (0.85m)",
                "TallReed3D_RiceEar_Gold" => "🌾 [갈대/농작물] 추수철 수전 황금 벼 이삭 줄기 (0.92m)",
                "TallReed3D_Spike" => "🎋 [갈대] 중원 온대 강변 억새 이삭 줄기 (1.08m)",
                "TallReed3D_RiverPlume" => "🎋 [갈대] 강남 아열대 수변 물억새 갈대 (1.15m)",
                "TallReed3D_BarleyEar_Gold" => "🌾 [갈대/농작물] 추수철 황금 보리/밀 이삭 줄기 (0.96m)",
                "TallReed3D_BarleyEar_Dry" => "🌾 [갈대/농작물] 건조 메마른 보리/밀 이삭 줄기 (0.90m)",
                "TallReed3D_ElephantTall" => "🌴 [갈대] 남만 열대 대왕 코끼리풀 줄기 (1.20m)",

                // 음지 고사리 / 양치류
                "ForestFern_Sword_Spring" => "🌲 [고사리] 봄 신록 음지 검고사리(Sword Fern)",
                "ForestFern_Sword_Summer" => "🌲 [고사리] 여름 짙은 음지 검고사리(Sword Fern)",
                "ForestFern_Sword_Autumn" => "🍂 [고사리] 가을 단풍 음지 검고사리(Sword Fern)",
                "ForestFern_Sword_Winter" => "❄️ [고사리] 겨울 적설 음지 검고사리(Sword Fern)",
                "ForestFern_Royal_Spring" => "🌲 [고사리] 봄 신록 음지 왕고사리(Royal Fern)",
                "ForestFern_Royal_Summer" => "🌲 [고사리] 여름 짙은 음지 왕고사리(Royal Fern)",
                "ForestFern_Royal_Autumn" => "🍂 [고사리] 가을 단풍 음지 왕고사리(Royal Fern)",
                "ForestFern_Royal_Winter" => "❄️ [고사리] 겨울 적설 음지 왕고사리(Royal Fern)",
                "ForestFern_Wood_Spring" => "🌲 [고사리] 봄 신록 산림 우드고사리(Wood Fern)",
                "ForestFern_Wood_Summer" => "🌲 [고사리] 여름 짙은 산림 우드고사리(Wood Fern)",
                "ForestFern_Wood_Autumn" => "🍂 [고사리] 가을 단풍 산림 우드고사리(Wood Fern)",
                "ForestFern_Wood_Winter" => "❄️ [고사리] 겨울 적설 산림 우드고사리(Wood Fern)",
                "ForestFern_Maidenhair_Summer" => "🌲 [고사리] 여름 음지 공작고사리(Maidenhair) 바닥 화초",
                "ForestFern_Maidenhair_Autumn" => "🍂 [고사리] 가을 단풍 공작고사리(Maidenhair) 바닥 화초",
                "ForestFern_ForestTriple_Spring" => "🌲 [고사리] 온대 수림 봄 3갈래 양치류(Triple Fern)",
                "ForestFern_ForestTriple_Summer" => "🌲 [고사리] 온대 수림 여름 3갈래 양치류(Triple Fern)",
                "ForestFern_TropicalPalm_Summer" => "🌴 [고사리] 남만 열대 정글 야자고사리(Palm Fern)",
                "ForestFern_TropicalStrap_Summer" => "🌴 [고사리] 남만 열대 파초일엽 활엽고사리(Strap Fern)",

                _ => defaultPrefix + key
                    .Replace("GrassCluster_", "지피 군락 - ")
                    .Replace("TemForest_", "온대 수림 ")
                    .Replace("SbtForest_", "아열대 수림 ")
                    .Replace("TroForest_", "열대 정글 수림 ")
                    .Replace("CldForest_", "한랭 침엽수림 ")
                    .Replace("ArdForest_", "건조 수림 ")
                    .Replace("Tem_", "온대 평원 ")
                    .Replace("Sbt_", "아열대 강남 ")
                    .Replace("Tro_", "남만 열대 ")
                    .Replace("Ard_", "서북 건조 ")
                    .Replace("Cld_", "북방 한랭 ")
                    .Replace("_", " ")
            };
        }

        /// <summary>
        /// 정제된 94종 절차적 식생 메쉬 및 46종 수목 프리팹(총 140종) 전용 스캔 (구버전 원시 OBJ 배제)
        /// </summary>
        public void ScanAllModels()
        {
            _allItems.Clear();
            _allItems.AddRange(GetStandardShowcaseItems());
            ApplyFilter();
        }

        private string MapCategoryToKorean(string catKey)
        {
            if (string.IsNullOrEmpty(catKey)) return "🌿 평야 지피";
            if (catKey.Contains("Flower") || catKey.Contains("Lotus") || catKey.Contains("Convallaria") || catKey.Contains("Magnolia") || catKey.Contains("Rhododendron"))
                return "🌼 야생화";
            if (catKey.Contains("Fern"))
                return "🌲 고사리";
            if (catKey.Contains("Seedhead") || catKey.Contains("Reeds") || catKey.Contains("Grain"))
                return "🌾 꽃대/이삭";
            if (catKey.Contains("Flat"))
                return "🌱 대형 융단 (Flat)";
            if (catKey.Contains("Thick"))
                return "🍃 풍성한 덤불 (Thick)";
            if (catKey.Contains("Arid"))
                return "🍂 서량 건조";
            if (catKey.Contains("Cold"))
                return "❄️ 북방 한랭";
            if (catKey.Contains("Tropical"))
                return "🌴 남만 아열대";
            return "🌿 평야 지피";
        }

        private void ScanDetailSubdir(string folderName, string catName, string albedo, bool isCarpet)
        {
            string dir = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/{folderName}";
            if (!Directory.Exists(dir)) return;

            string[] files = Directory.GetFiles(dir, "*.obj");
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                _allItems.Add(new ShowcaseItem
                {
                    displayName = CleanDisplayName(name) + " (지피 잔디)",
                    modelFile = name,
                    albedoFile = albedo,
                    normalFile = "shrub_grassclump_normal.png",
                    category = catName,
                    fullPath = f.Replace("\\", "/"),
                    defaultWidth = 1.3f,
                    defaultHeight = 0.7f,
                    isCarpet = isCarpet,
                    isShrub = false
                });
            }
        }

        private string CleanDisplayName(string raw)
        {
            return raw.Replace(".obj", "").Replace("Shrub_", "").Replace("_", " ");
        }

        private void ApplyFilter()
        {
            _filteredItems.Clear();
            string selectedCat = CategoryTabs[_selectedCategoryTab];

            for (int i = 0; i < _allItems.Count; i++)
            {
                var item = _allItems[i];
                if (_selectedCategoryTab != 0)
                {
                    bool match = false;
                    switch (_selectedCategoryTab)
                    {
                        case 1: // 🌾 지피 잔디
                            match = item.category == "🌾 지피 잔디" || item.modelFile.Contains("GrassCluster") || item.modelFile.Contains("FlowerCluster");
                            break;
                        case 2: // 🌿 3D 풀포기
                            match = item.category == "🌿 3D 풀포기" || item.modelFile.Contains("RadialFan3D") || (item.isShrub && item.modelFile.ToLower().Contains("grassclump") && !item.modelFile.ToLower().Contains("seedhead"));
                            break;
                        case 3: // 🎋 갈대 (연꽃 및 수생식물 배제)
                            match = (item.category == "🎋 갈대" || item.modelFile.Contains("TallReed3D") || item.category.Contains("이삭") || item.modelFile.ToLower().Contains("seedhead")) && !item.modelFile.Contains("Lotus") && !item.category.Contains("수생식물");
                            break;
                        case 4: // 🌲 고사리
                            match = item.category.Contains("고사리") || item.modelFile.ToLower().Contains("fern");
                            break;
                        case 5: // 🪷 수생식물
                            match = item.category.Contains("수생식물") || item.modelFile.Contains("Lotus");
                            break;
                        case 6: // 🌳 수목
                            match = item.category == "🌳 수목" || item.modelFile.StartsWith("Tree_");
                            break;
                        case 7: // 🌼 야생화
                            match = item.displayName.Contains("야생화") || item.displayName.Contains("들꽃") || item.modelFile.Contains("FlowerCluster") || item.modelFile.Contains("Convallaria") || item.modelFile.Contains("Rhododendron") || item.modelFile.Contains("Magnolia") || item.modelFile.Contains("Lotus");
                            break;
                        case 8: // 🌾 농작물 (벼/밀)
                            match = item.displayName.Contains("농작물") || item.modelFile.Contains("Rice") || item.modelFile.Contains("Wheat") || item.modelFile.Contains("Barley") || item.modelFile.Contains("Harvest") || item.modelFile.Contains("Millet");
                            break;
                        case 9: // 🍂 서량 건조
                            match = item.displayName.Contains("건조") || item.modelFile.Contains("Ard") || item.modelFile.Contains("Arid") || item.modelFile.Contains("Dry");
                            break;
                        case 10: // ❄️ 북방 한랭
                            match = item.displayName.Contains("한랭") || item.displayName.Contains("겨울") || item.modelFile.Contains("Cld") || item.modelFile.Contains("Winter");
                            break;
                        case 11: // 🌴 남만 열대
                            match = item.displayName.Contains("남만") || item.displayName.Contains("열대") || item.modelFile.Contains("Tro") || item.modelFile.Contains("Tropical") || item.modelFile.Contains("Elephant");
                            break;
                        default:
                            match = (item.category == selectedCat);
                            break;
                    }
                    if (!match) continue;
                }

                if (!string.IsNullOrEmpty(_searchKeyword))
                {
                    string kw = _searchKeyword.ToLower();
                    if (!item.displayName.ToLower().Contains(kw) && !item.modelFile.ToLower().Contains(kw))
                        continue;
                }
                _filteredItems.Add(item);
            }

            if (_currentIndex >= _filteredItems.Count) _currentIndex = Mathf.Max(0, _filteredItems.Count - 1);
        }

        /// <summary>
        /// 소환 위치 계산 및 업데이트
        /// </summary>
        private void UpdateSpawnPosition()
        {
            FindTerrain();
            if (_cachedTerrain == null) return;

            Vector3 terPos = _cachedTerrain.transform.position;
            Vector3 terSize = _cachedTerrain.terrainData != null ? _cachedTerrain.terrainData.size : Vector3.zero;

            switch (locationMode)
            {
                case LocationMode.InFrontOfCamera:
                    if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                    {
                        Camera cam = SceneView.lastActiveSceneView.camera;
                        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                        {
                            _currentGroundPos = hit.point;
                            _currentGroundNormal = hit.normal;
                        }
                        else
                        {
                            Vector3 targetP = cam.transform.position + cam.transform.forward * 12f;
                            targetP.x = Mathf.Clamp(targetP.x, terPos.x, terPos.x + terSize.x);
                            targetP.z = Mathf.Clamp(targetP.z, terPos.z, terPos.z + terSize.z);
                            targetP.y = _cachedTerrain.SampleHeight(targetP) + terPos.y;
                            _currentGroundPos = targetP;
                            _currentGroundNormal = Vector3.up;
                        }
                    }
                    break;

                case LocationMode.SpecificChunk:
                    UpdateSpawnPositionFromChunk();
                    break;

                case LocationMode.CustomWorldPos:
                    _currentGroundPos = customWorldPos;
                    if (_cachedTerrain != null)
                    {
                        _currentGroundPos.y = _cachedTerrain.SampleHeight(_currentGroundPos) + terPos.y;
                    }
                    break;
            }
        }

        public void UpdateSpawnPositionFromChunk()
        {
            FindTerrain();
            if (_cachedTerrain == null) return;

            Vector3 terPos = _cachedTerrain.transform.position;
            float cSize = 32f;
            float cCenterX = terPos.x + (targetChunkX + 0.5f) * cSize;
            float cCenterZ = terPos.z + (targetChunkZ + 0.5f) * cSize;
            float cCenterY = _cachedTerrain.SampleHeight(new Vector3(cCenterX, 0, cCenterZ)) + terPos.y;

            _currentGroundPos = new Vector3(cCenterX, cCenterY, cCenterZ);

            if (_cachedTerrain.terrainData != null)
            {
                Vector3 terSize = _cachedTerrain.terrainData.size;
                float normX = (cCenterX - terPos.x) / terSize.x;
                float normZ = (cCenterZ - terPos.z) / terSize.z;
                _currentGroundNormal = _cachedTerrain.terrainData.GetInterpolatedNormal(normX, normZ);
            }
        }

        /// <summary>
        /// 씬 뷰 카메라를 현재 쇼케이스 식물 위치로 집중 이동
        /// </summary>
        public void FocusSceneCameraOnShowcase(float distance = 6.0f)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 viewTarget = _currentGroundPos + Vector3.up * 0.5f;
                SceneView.lastActiveSceneView.LookAt(viewTarget, Quaternion.Euler(28f, 35f, 0f), distance);
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        /// <summary>
        /// 프리뷰 임시 게임오브젝트 생성/갱신 (0.01초 즉석 소환)
        /// </summary>
        private void CreateOrUpdatePreviewObject()
        {
            if (_filteredItems.Count == 0)
            {
                DestroyPreviewObject();
                return;
            }

            var item = _filteredItems[_currentIndex];

            if (_previewRoot == null)
            {
                _previewRoot = new GameObject("__Foliage_Showcase_Preview__");
                _previewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            // 기존 자식들 제거
            while (_previewRoot.transform.childCount > 0)
            {
                DestroyImmediate(_previewRoot.transform.GetChild(0).gameObject);
            }

            // 🌳 [수목(Trees) 카테고리 전용 다중 서브메쉬 프리팹/OBJ 소환]
            if (item.category == "🌳 수목" || item.modelFile.StartsWith("Tree_") || item.fullPath.EndsWith(".prefab"))
            {
                GameObject treeSource = AssetDatabase.LoadAssetAtPath<GameObject>(item.fullPath);
                if (treeSource != null)
                {
                    GameObject treeInst = Instantiate(treeSource);
                    treeInst.name = "Tree_Preview_0";
                    treeInst.transform.SetParent(_previewRoot.transform, false);

                    if (!item.fullPath.EndsWith(".prefab"))
                    {
                        string rawKey = Path.GetFileNameWithoutExtension(item.modelFile);
                        var dummyZone = UnityEngine.Object.FindAnyObjectByType<TacticalForestZone>();
                        if (dummyZone != null)
                        {
                            dummyZone.ApplyTreeMaterialsPublic(treeInst, rawKey);
                        }
                    }

                    Quaternion treeSlopeRot = Quaternion.FromToRotation(Vector3.up, _currentGroundNormal);
                    Quaternion treeFinalRot = Quaternion.Slerp(Quaternion.identity, treeSlopeRot, alignToNormal);
                    Vector3 treeOffsetDir = Vector3.Slerp(Vector3.up, _currentGroundNormal, alignToNormal);

                    treeInst.transform.position = _currentGroundPos + (treeOffsetDir * groundOffset);
                    treeInst.transform.rotation = treeFinalRot;
                    treeInst.transform.localScale = new Vector3(scale, scale * heightMultiplier, scale);

                    var mfTree = treeInst.GetComponentInChildren<MeshFilter>();
                    if (mfTree != null) _currentMesh = mfTree.sharedMesh;
                    var mrTree = treeInst.GetComponentInChildren<MeshRenderer>();
                    if (mrTree != null) _currentMat = mrTree.sharedMaterial;

                    SceneView.RepaintAll();
                    return;
                }
            }

            _currentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(item.fullPath);
            if (_currentMesh == null)
            {
                // 서브디렉토리 재탐색
                string[] found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(item.modelFile) + " t:Model");
                if (found != null && found.Length > 0)
                {
                    _currentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(found[0]));
                }
            }

            _currentMat = GetMaterialForPreview(item);

            // 🎨 원본 머티리얼 변경 감지 및 파라미터 로컬 동기화
            string currentMatPath = _currentMat != null ? AssetDatabase.GetAssetPath(_currentMat) : "";
            if (currentMatPath != _lastLoadedMatPath)
            {
                _lastLoadedMatPath = currentMatPath;
                SyncMaterialTuningFromSource(_currentMat);
            }

            // 🎨 원본 보존을 위한 프리뷰 전용 독립 인스턴스 복제본 생성
            if (_previewInstanceMat != null)
            {
                DestroyImmediate(_previewInstanceMat);
                _previewInstanceMat = null;
            }

            if (_currentMat != null)
            {
                _previewInstanceMat = new Material(_currentMat);
                _previewInstanceMat.hideFlags = HideFlags.DontSave;
                ApplyTuningToPreviewMaterial();
            }

            Material renderMat = _previewInstanceMat != null ? _previewInstanceMat : _currentMat;

            // ✂️ 실시간 절차적 UV 자르기 & 메쉬 컷 보정 메쉬 생성 (프리뷰 샌드박스)
            Mesh renderMesh = GetOrCreatePreviewMesh(_currentMesh, flipY, _useGroundedTuftMesh, _tipCropPercent, _sideMarginPercent, _meshTipCutPercent);
            if (renderMesh == null) renderMesh = _currentMesh;

            if (renderMesh == null || renderMat == null) return;

            // 소환 위치 및 회전 계산
            Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, _currentGroundNormal);
            Quaternion finalRot = Quaternion.Slerp(Quaternion.identity, slopeRot, alignToNormal);
            Vector3 offsetDir = Vector3.Slerp(Vector3.up, _currentGroundNormal, alignToNormal);

            if (displayMode == DisplayMode.Single)
            {
                // 단일 포기 소환
                GameObject inst = new GameObject("Instance_0");
                inst.transform.SetParent(_previewRoot.transform, false);
                var mf = inst.AddComponent<MeshFilter>();
                var mr = inst.AddComponent<MeshRenderer>();
                mf.sharedMesh = renderMesh;
                mr.sharedMaterial = renderMat;

                Vector3 targetPos = _currentGroundPos + (offsetDir * groundOffset);
                inst.transform.position = targetPos;
                inst.transform.rotation = finalRot;
                float effectiveYScale = (flipY ? -1.0f : 1.0f) * scale * heightMultiplier;
                inst.transform.localScale = new Vector3(scale, effectiveYScale, scale);
            }
            else
            {
                // 3x3 패치 소환
                for (int r = -1; r <= 1; r++)
                {
                    for (int c = -1; c <= 1; c++)
                    {
                        GameObject inst = new GameObject($"Instance_{r}_{c}");
                        inst.transform.SetParent(_previewRoot.transform, false);
                        var mf = inst.AddComponent<MeshFilter>();
                        var mr = inst.AddComponent<MeshRenderer>();
                        mf.sharedMesh = renderMesh;
                        mr.sharedMaterial = renderMat;

                        Vector3 offset = new Vector3(c * patchSpacing, 0, r * patchSpacing);
                        Vector3 worldP = _currentGroundPos + offset;
                        if (_cachedTerrain != null)
                        {
                            worldP.y = _cachedTerrain.SampleHeight(worldP) + _cachedTerrain.transform.position.y;
                        }

                        Vector3 targetPos = worldP + (offsetDir * groundOffset);
                        inst.transform.position = targetPos;
                        inst.transform.rotation = finalRot;
                        float effectiveYScale = (flipY ? -1.0f : 1.0f) * scale * heightMultiplier;
                        inst.transform.localScale = new Vector3(scale, effectiveYScale, scale);
                    }
                }
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// 현재 메쉬의 실제 UV 최소/최대 V 높이 범위를 산출합니다.
        /// (아틀라스 하단 슬롯 0.0~0.5, 상단 슬롯 0.8~1.0 등을 자동 감지)
        /// </summary>
        private Vector2 GetCurrentMeshUVRange()
        {
            if (_currentMesh == null || _currentMesh.uv == null || _currentMesh.uv.Length == 0) return new Vector2(0f, 1f);
            float minY = float.MaxValue, maxY = float.MinValue;
            var uvs = _currentMesh.uv;
            for (int i = 0; i < uvs.Length; i++)
            {
                if (uvs[i].y < minY) minY = uvs[i].y;
                if (uvs[i].y > maxY) maxY = uvs[i].y;
            }
            if (minY >= maxY) return new Vector2(0f, 1f);
            return new Vector2(minY, maxY);
        }

        /// <summary>
        /// ✂️ 실시간 절차적 UV 자르기 & 메쉬 컷이 적용된 쇼케이스 전용 프리뷰 메쉬를 생성합니다.
        /// </summary>
        private Mesh GetOrCreatePreviewMesh(Mesh sourceMesh, bool isFlipped, bool useGrounded, float tipCropRatio, float sideMarginRatio, float meshCutRatio)
        {
            if (sourceMesh == null) return null;

            if (_previewCustomMesh != null)
            {
                DestroyImmediate(_previewCustomMesh);
                _previewCustomMesh = null;
            }

            // 1. 접지 정규화 적용 여부에 따른 베이스 메쉬 획득 (정제된 TotalWar_* 절차적 메쉬는 원본 기하학 그대로 보존)
            Mesh baseMesh;
            bool isProceduralAsset = sourceMesh.name.StartsWith("TotalWar_") || sourceMesh.name.Contains("MultiDirectionalGrassCluster");
            if (useGrounded && !isProceduralAsset)
            {
                baseMesh = TacticalGrassRenderer.GetOrCreateGroundedTuftMesh(sourceMesh, isFlipped, 0.58f);
            }
            else
            {
                baseMesh = sourceMesh;
            }

            if (baseMesh == null) return sourceMesh;

            // 2. 프리뷰 전용 메쉬 인스턴스 복제 후 슬라이더 UV/메쉬 자르기 파라미터 실시간 주입
            _previewCustomMesh = Instantiate(baseMesh);
            _previewCustomMesh.name = baseMesh.name + "_ShowcaseTuned";
            _previewCustomMesh.hideFlags = HideFlags.DontSave;

            Vector2 uvRange = GetCurrentMeshUVRange();
            float minV = uvRange.x;
            float maxV = uvRange.y;
            float spanV = Mathf.Max(0.01f, maxV - minV);

            // 3. 텍스처 UV 자르기/압축 (0% ~ 40%) - 현재 풀의 실제 꼭대기 maxV 기준 적중!
            Vector2[] uvs = _previewCustomMesh.uv;
            if (uvs != null && uvs.Length > 0 && (tipCropRatio > 0.001f || sideMarginRatio > 0.001f))
            {
                Vector2[] tunedUvs = new Vector2[uvs.Length];
                float cropStartV = maxV - (spanV * tipCropRatio);
                float targetV = cropStartV + (maxV - cropStartV) * 0.35f;

                for (int i = 0; i < uvs.Length; i++)
                {
                    Vector2 uv = uvs[i];
                    if (uv.y > cropStartV)
                    {
                        float t = (uv.y - cropStartV) / Mathf.Max(0.001f, maxV - cropStartV);
                        uv.y = Mathf.Lerp(cropStartV, targetV, t);
                    }
                    if (uv.x < sideMarginRatio) uv.x = sideMarginRatio + 0.002f;
                    else if (uv.x > (1.0f - sideMarginRatio)) uv.x = 1.0f - sideMarginRatio - 0.002f;

                    tunedUvs[i] = uv;
                }
                _previewCustomMesh.uv = tunedUvs;
            }

            // 4. 3D 풀잎 끝 메쉬 높이 직접 절단 (0% ~ 35%) - 씬 뷰에서 3D 형상이 즉시 깎여나가는 눈에 확 띄는 피드백!
            if (meshCutRatio > 0.001f)
            {
                Vector3[] verts = _previewCustomMesh.vertices;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < verts.Length; i++)
                {
                    if (verts[i].y < minY) minY = verts[i].y;
                    if (verts[i].y > maxY) maxY = verts[i].y;
                }
                float spanY = Mathf.Max(0.01f, maxY - minY);
                float cutStartY = maxY - (spanY * meshCutRatio);

                for (int i = 0; i < verts.Length; i++)
                {
                    if (verts[i].y > cutStartY)
                    {
                        verts[i].y = Mathf.Lerp(cutStartY, cutStartY + (maxY - cutStartY) * 0.15f, (verts[i].y - cutStartY) / Mathf.Max(0.001f, maxY - cutStartY));
                    }
                }
                _previewCustomMesh.vertices = verts;
                _previewCustomMesh.RecalculateBounds();
                _previewCustomMesh.RecalculateNormals();
            }

            return _previewCustomMesh;
        }

        /// <summary>
        /// 원본 머티리얼 에셋으로부터 현재 셰이더 프로퍼티 값들을 안전하게 읽어옵니다.
        /// </summary>
        private void SyncMaterialTuningFromSource(Material sourceMat)
        {
            if (sourceMat == null) return;
            _tuneTipFadeStart = sourceMat.HasProperty("_TipFadeStart") ? sourceMat.GetFloat("_TipFadeStart") : 0.940f;
            _tuneTipFadeEnd = sourceMat.HasProperty("_TipFadeEnd") ? sourceMat.GetFloat("_TipFadeEnd") : 0.985f;
            _tuneEdgeBleed = sourceMat.HasProperty("_EdgeBleedCrop") ? sourceMat.GetFloat("_EdgeBleedCrop") : 0.006f;
            _tuneCutoff = sourceMat.HasProperty("_Cutoff") ? sourceMat.GetFloat("_Cutoff") : 0.25f;
            _tuneBaseColor = sourceMat.HasProperty("_BaseColor") ? sourceMat.GetColor("_BaseColor") : Color.white;
            _tuneWindMultiplier = sourceMat.HasProperty("_WindMultiplier") ? sourceMat.GetFloat("_WindMultiplier") : 0.35f;
            _tuneLeafFlutter = sourceMat.HasProperty("_LeafFlutter") ? sourceMat.GetFloat("_LeafFlutter") : 0.25f;
            _hasUnsavedMaterialChanges = false;
        }

        /// <summary>
        /// 현재 쇼케이스 슬라이더 값들을 씬 뷰의 프리뷰 전용 머티리얼 인스턴스에만 실시간 주입합니다. (원본 보존)
        /// 풀의 실제 꼭대기(maxV)에 맞춰 셰이더 페이드아웃 범위를 1:1 자동 스케일링하여 즉각적 시각 변화를 보장합니다.
        /// </summary>
        private void ApplyTuningToPreviewMaterial()
        {
            if (_previewInstanceMat == null) return;

            Vector2 uvRange = GetCurrentMeshUVRange();
            float minV = uvRange.x;
            float maxV = uvRange.y;
            float spanV = Mathf.Max(0.01f, maxV - minV);

            // 🌿 [핵심] 풀의 실제 꼭대기(maxV)에 맞춰 셰이더 페이드 범위를 1:1 자동 스케일링
            float startRatio = 1.0f - _tuneTipFadeStart;
            float endRatio = 1.0f - _tuneTipFadeEnd;

            float effectiveTipFadeStart = maxV - (spanV * startRatio);
            float effectiveTipFadeEnd = maxV - (spanV * endRatio);

            if (_previewInstanceMat.HasProperty("_TipFadeStart")) _previewInstanceMat.SetFloat("_TipFadeStart", effectiveTipFadeStart);
            if (_previewInstanceMat.HasProperty("_TipFadeEnd")) _previewInstanceMat.SetFloat("_TipFadeEnd", effectiveTipFadeEnd);
            if (_previewInstanceMat.HasProperty("_EdgeBleedCrop")) _previewInstanceMat.SetFloat("_EdgeBleedCrop", _tuneEdgeBleed);
            if (_previewInstanceMat.HasProperty("_Cutoff")) _previewInstanceMat.SetFloat("_Cutoff", _tuneCutoff);
            if (_previewInstanceMat.HasProperty("_BaseColor")) _previewInstanceMat.SetColor("_BaseColor", _tuneBaseColor);
            if (_previewInstanceMat.HasProperty("_WindMultiplier")) _previewInstanceMat.SetFloat("_WindMultiplier", _tuneWindMultiplier);
            if (_previewInstanceMat.HasProperty("_LeafFlutter")) _previewInstanceMat.SetFloat("_LeafFlutter", _tuneLeafFlutter);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 사용자가 [저장]을 명시적으로 눌렀을 때만 원본 머티리얼 에셋 파일에 영구 기록합니다.
        /// </summary>
        private void SaveTuningToSourceMaterial()
        {
            if (_currentMat == null)
            {
                EditorUtility.DisplayDialog("알림", "연결된 원본 머티리얼 에셋이 없습니다.", "확인");
                return;
            }

            Undo.RecordObject(_currentMat, "Save Foliage Material From Showcase");
            if (_currentMat.HasProperty("_TipFadeStart")) _currentMat.SetFloat("_TipFadeStart", _tuneTipFadeStart);
            if (_currentMat.HasProperty("_TipFadeEnd")) _currentMat.SetFloat("_TipFadeEnd", _tuneTipFadeEnd);
            if (_currentMat.HasProperty("_EdgeBleedCrop")) _currentMat.SetFloat("_EdgeBleedCrop", _tuneEdgeBleed);
            if (_currentMat.HasProperty("_Cutoff")) _currentMat.SetFloat("_Cutoff", _tuneCutoff);
            if (_currentMat.HasProperty("_BaseColor")) _currentMat.SetColor("_BaseColor", _tuneBaseColor);
            if (_currentMat.HasProperty("_WindMultiplier")) _currentMat.SetFloat("_WindMultiplier", _tuneWindMultiplier);
            if (_currentMat.HasProperty("_LeafFlutter")) _currentMat.SetFloat("_LeafFlutter", _tuneLeafFlutter);

            EditorUtility.SetDirty(_currentMat);
            AssetDatabase.SaveAssets();
            _hasUnsavedMaterialChanges = false;

            ShowNotification(new GUIContent($"💾 [머티리얼 저장 완료] '{_currentMat.name}'에 영구 반영되었습니다!"));
        }

        private Material GetMaterialForPreview(ShowcaseItem item)
        {
            Material mat = null;

            // 0: [모델 전용 고유 머티리얼 자동 바인딩 (권장)]
            if (selectedMaterialIndex == 0)
            {
                string baseName = Path.GetFileNameWithoutExtension(item.modelFile);

                // 0) 4대 유형 정제 메쉬(.asset) 전용 1:1 머티리얼 직결 로드
                if (item.modelFile.EndsWith(".asset"))
                {
                    if (baseName.Contains("MultiDirectionalGrassCluster"))
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
                    else if (baseName.Contains("PinkBuds") || baseName.Contains("PurplePetals"))
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TemGrassSp_Flower_Mat.mat");
                    else if (baseName.Contains("OrangeSpikes"))
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_SbtGrass0Sp_Flower_Mat.mat");
                    else if (baseName.EndsWith("_Mesh"))
                    {
                        string directMatPath = "Assets/Settings/Vegetation/RadialMaterials/" + baseName.Substring(0, baseName.Length - 5) + "_Mat.mat";
                        mat = AssetDatabase.LoadAssetAtPath<Material>(directMatPath);
                    }
                }

                // 1) 모델 파일명 기반 RadialMaterials 탐색
                if (mat == null)
                {
                    string[] foundGuids = AssetDatabase.FindAssets(baseName + " t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
                    if (foundGuids != null && foundGuids.Length > 0)
                    {
                        mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(foundGuids[0]));
                    }
                }

                // 2) 텍스처 아틀라스 파일명 기반 RadialMaterials 탐색
                if (mat == null && !string.IsNullOrEmpty(item.albedoFile))
                {
                    string texName = Path.GetFileNameWithoutExtension(item.albedoFile);
                    string[] texGuids = AssetDatabase.FindAssets(texName + " t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
                    if (texGuids != null && texGuids.Length > 0)
                    {
                        mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(texGuids[0]));
                    }
                }

                // 3) 그래도 없으면 원본 텍스처(PNG)를 찾아 전용 임시 머티리얼 자동 생성 (꽃/고사리 원색 보장)
                if (mat == null && !string.IsNullOrEmpty(item.albedoFile))
                {
                    Shader grassShader = Shader.Find("Universal Render Pipeline/TotalWarRadialGrassLOD");
                    if (grassShader == null) grassShader = Shader.Find("Standard");

                    mat = new Material(grassShader);
                    mat.hideFlags = HideFlags.DontSave;
                    mat.enableInstancing = true;
                    mat.SetFloat("_Cull", 0); // 양면 렌더링 (꽃잎/풀잎 투명화 방지)
                    mat.SetFloat("_Cutoff", 0.28f);

                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/" + item.albedoFile);
                    if (tex != null)
                    {
                        mat.mainTexture = tex;
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                    }
                }
            }
            else
            {
                // 수동 틴트 모드
                switch (selectedMaterialIndex)
                {
                    case 1: // 봄철 연두빛 잔디 틴트 (Lime Green)
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
                        break;
                    case 2: // 여름 기본 녹색
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Summer_Mat.mat");
                        break;
                    case 3: // 가을 황갈색 단풍
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Autumn_Mat.mat");
                        break;
                    case 4: // 겨울 설초 (진짜 겨울 텍스처 바인딩: 눈 덮이고 바짝 마른 겨울 설초)
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Cld_Winter_Mat.mat");
                        break;
                }
            }

            if (mat == null)
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
            }
            if (mat != null) mat.enableInstancing = true;
            return mat;
        }

        private void DestroyPreviewObject()
        {
            if (_previewInstanceMat != null)
            {
                DestroyImmediate(_previewInstanceMat);
                _previewInstanceMat = null;
            }
            if (_previewCustomMesh != null)
            {
                DestroyImmediate(_previewCustomMesh);
                _previewCustomMesh = null;
            }
            if (_previewRoot != null)
            {
                DestroyImmediate(_previewRoot);
                _previewRoot = null;
            }
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 지면 자동 밀착 오프셋 수학적 자동 계산 (AABB bounds.min.y 분석)
        /// </summary>
        private void AutoAlignToGround()
        {
            if (_currentMesh == null) return;

            // 메쉬의 로컬 최하단 Y 정점 (flipY 적용 시 -bounds.max.y가 최하단)
            float minY = flipY ? -_currentMesh.bounds.max.y : _currentMesh.bounds.min.y;
            // 스케일 및 높이 배율 반영
            float effectiveBottom = minY * scale * heightMultiplier;

            // 바닥이 지표면(Y=0)에 정확히 닿도록 하는 오프셋: -effectiveBottom
            groundOffset = (float)Math.Round(-effectiveBottom, 2);

            if (_filteredItems != null && _currentIndex >= 0 && _currentIndex < _filteredItems.Count)
            {
                var cur = _filteredItems[_currentIndex];
                cur.defaultOffset = groundOffset;
                _filteredItems[_currentIndex] = cur;

                for (int i = 0; i < _allItems.Count; i++)
                {
                    if (_allItems[i].modelFile == cur.modelFile)
                    {
                        var it = _allItems[i];
                        it.defaultOffset = groundOffset;
                        _allItems[i] = it;
                        break;
                    }
                }
                SaveCatalogChanges();
            }

            CreateOrUpdatePreviewObject();
            string flipMsg = flipY ? " [상하 반전 ON]" : "";
            ShowNotification(new GUIContent($"✨ 자동 계산 완료: 지면 띄움 {groundOffset:+#0.00;-#0.00;0.00}m{flipMsg} 적용 & 카탈로그 영구 보존!"));
        }

        /// <summary>
        /// 현재 보고 있는 식물의 상하 반전을 토글하고 카탈로그에 즉시 동기화/저장합니다.
        /// </summary>
        public void ToggleCurrentFlipY()
        {
            if (_filteredItems.Count == 0 || _currentIndex < 0 || _currentIndex >= _filteredItems.Count) return;

            flipY = !flipY;

            var cur = _filteredItems[_currentIndex];
            cur.flipY = flipY;
            _filteredItems[_currentIndex] = cur;

            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].modelFile == cur.modelFile)
                {
                    var a = _allItems[i];
                    a.flipY = flipY;
                    _allItems[i] = a;
                    break;
                }
            }

            AutoAlignToGround();
            SaveCatalogChanges();
        }

        /// <summary>
        /// 현재 메모리의 모든 식생 flipY 설정을 FoliageMasterCatalog.json에 영구 저장합니다.
        /// </summary>
        public void SaveCatalogChanges()
        {
            string catalogPath = "Assets/Settings/Vegetation/FoliageMasterCatalog.json";
            if (!File.Exists(catalogPath)) return;

            try
            {
                string json = File.ReadAllText(catalogPath);
                MasterCatalogData data = JsonUtility.FromJson<MasterCatalogData>(json);
                if (data != null && data.items != null)
                {
                    var flipDict = new Dictionary<string, bool>();
                    foreach (var it in _allItems)
                    {
                        flipDict[it.modelFile] = it.flipY;
                    }

                    foreach (var entry in data.items)
                    {
                        var match = _allItems.Find(x => x.modelFile == entry.modelFile);
                        if (match.modelFile != null)
                        {
                            entry.flipY = match.flipY;
                            entry.offset = match.defaultOffset;
                            entry.normalAlign = match.defaultNormalAlign;
                        }
                    }

                    string newJson = JsonUtility.ToJson(data, true);
                    File.WriteAllText(catalogPath, newJson);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TotalWarFoliageShowcaseWindow] 카탈로그 저장 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 🌾 [A방향 핵심 솔루션] 삼탈워 364종 전수 스마트 바닥 캘리브레이션 및 전장 프리팹 일괄 정렬
        /// 모든 모델의 메쉬 Bounds를 역산하여 최하단 버텍스가 지면(Y=0.0m)에 정확히 밀착되도록 groundOffset을 계산하고,
        /// 지피 잔디(238종)는 alignToGround=1.0f, 수풀(126종)은 alignToGround=0.35f로 카탈로그와 설정을 영구 동기화합니다.
        /// </summary>
        public void AutoCalibrateAll364Models()
        {
            if (_allItems == null || _allItems.Count == 0) ScanAllModels();
            if (_allItems == null || _allItems.Count == 0) return;

            int count = 0;
            for (int i = 0; i < _allItems.Count; i++)
            {
                var item = _allItems[i];
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(item.fullPath);
                if (mesh == null)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(item.fullPath);
                    if (go != null)
                    {
                        var mf = go.GetComponentInChildren<MeshFilter>();
                        if (mf != null) mesh = mf.sharedMesh;
                    }
                }

                if (mesh != null)
                {
                    float minY = item.flipY ? -mesh.bounds.max.y : mesh.bounds.min.y;
                    item.defaultOffset = (float)Math.Round(-minY, 2);
                    item.defaultNormalAlign = item.isCarpet ? 1.0f : 0.35f;
                    _allItems[i] = item;
                    count++;
                }
            }

            ApplyFilter();
            if (_filteredItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _filteredItems.Count)
            {
                ResetParamsForItem(_filteredItems[_currentIndex]);
                CreateOrUpdatePreviewObject();
            }

            SaveCatalogChanges();

            // 전장 터레인 매니저에도 반영
            var mgr = UnityEngine.Object.FindAnyObjectByType<TotalWarTerrainManager>();
            if (mgr != null && mgr.customGrassList != null)
            {
                foreach (var cfg in mgr.customGrassList)
                {
                    var match = _allItems.Find(x => x.modelFile == cfg.modelFile);
                    if (match.modelFile != null)
                    {
                        cfg.groundOffset = match.defaultOffset;
                        cfg.alignToGround = match.defaultNormalAlign;
                        cfg.flipY = match.flipY;
                    }
                }
                EditorUtility.SetDirty(mgr);
            }

            ShowNotification(new GUIContent($"🌾 [364종 전수 캘리브레이션 완료] 총 {count}종의 풀포기 바닥 피벗이 완벽 밀착되었습니다!"));
            Debug.Log($"<color=#00FF88><b>[식생 캘리브레이터] 🌾 삼탈워 364종 전수 지면 밀착 캘리브레이션 완료! (바닥 Y=0.0m 완벽 클램핑)</b></color>");
        }

        /// <summary>
        /// 사용자가 입력한 번호 목록(예: "3, 5, 12, 15-20")을 파싱하여 flipY 상태를 일괄 설정합니다.
        /// </summary>
        public void ApplyBatchFlip(bool state)
        {
            if (string.IsNullOrEmpty(_batchFlipInput) || _filteredItems.Count == 0) return;

            string[] tokens = _batchFlipInput.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;

            foreach (var token in tokens)
            {
                if (token.Contains("-"))
                {
                    string[] range = token.Split('-');
                    if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                    {
                        for (int num = Mathf.Min(start, end); num <= Mathf.Max(start, end); num++)
                        {
                            if (SetItemFlipYByIndex(num - 1, state)) count++;
                        }
                    }
                }
                else if (int.TryParse(token, out int singleNum))
                {
                    if (SetItemFlipYByIndex(singleNum - 1, state)) count++;
                }
            }

            if (_currentIndex >= 0 && _currentIndex < _filteredItems.Count)
            {
                ResetParamsForItem(_filteredItems[_currentIndex]);
                CreateOrUpdatePreviewObject();
            }

            SaveCatalogChanges();
            ShowNotification(new GUIContent($"✅ {count}개 모델에 반전 설정({(state ? "하늘로 세움" : "원작 하수형")}) 일괄 적용 완료!"));
        }

        private bool SetItemFlipYByIndex(int targetIdx, bool state)
        {
            if (targetIdx < 0 || targetIdx >= _filteredItems.Count) return false;

            var item = _filteredItems[targetIdx];
            item.flipY = state;
            _filteredItems[targetIdx] = item;

            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].modelFile == item.modelFile)
                {
                    var allIt = _allItems[i];
                    allIt.flipY = state;
                    _allItems[i] = allIt;
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// 이전 식물로 즉시 전환
        /// </summary>
        private void PreviousItem()
        {
            if (_filteredItems.Count == 0) return;
            _currentIndex--;
            if (_currentIndex < 0) _currentIndex = _filteredItems.Count - 1;
            ResetParamsForItem(_filteredItems[_currentIndex]);
            CreateOrUpdatePreviewObject();
        }

        /// <summary>
        /// 다음 식물로 즉시 전환
        /// </summary>
        private void NextItem()
        {
            if (_filteredItems.Count == 0) return;
            _currentIndex++;
            if (_currentIndex >= _filteredItems.Count) _currentIndex = 0;
            ResetParamsForItem(_filteredItems[_currentIndex]);
            CreateOrUpdatePreviewObject();
        }

        private void ResetParamsForItem(ShowcaseItem item)
        {
            // 원작 삼탈워 OBJ 메쉬는 이미 1:1 실측 미터 단위로 버텍스가 모델링되어 있으므로 기본 스케일은 1.0f가 정답 (이중 곱셈 방지)
            scale = 1.0f;
            heightMultiplier = 1.0f;
            alignToNormal = item.defaultNormalAlign;
            flipY = item.flipY; // 카탈로그 기하학적 분석 판별값(또는 사용자 지정값) 즉시 동기화!

            // 1. 카탈로그에 기록된 전수 REA 정밀 오프셋 우선 적용
            if (Mathf.Abs(item.defaultOffset) > 0.001f)
            {
                groundOffset = item.defaultOffset;
            }
            else
            {
                // 2. 메쉬 바운딩 박스를 읽어 자동으로 기본 바닥 오프셋 추정
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(item.fullPath);
                if (m != null)
                {
                    float minY = (flipY ? -m.bounds.max.y : m.bounds.min.y) * scale * heightMultiplier;
                    groundOffset = (float)Math.Round(-minY, 2);
                }
                else
                {
                    groundOffset = item.isCarpet ? 0.35f : 0.0f;
                }
            }
        }

        // ==========================================
        // 씬 뷰 실시간 렌더링 및 가이드 핸들
        // ==========================================
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_filteredItems.Count == 0 || _currentGroundPos == Vector3.zero) return;

            // 1. 단축키 입력 처리 (Z: 이전, X: 다음, C: 카메라 포커스, V: 자동 밀착)
            Event e = Event.current;
            if (e.type == EventType.KeyDown && !e.alt && !e.control && !e.shift)
            {
                if (e.keyCode == KeyCode.Z)
                {
                    PreviousItem();
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.X)
                {
                    NextItem();
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.C)
                {
                    FocusSceneCameraOnShowcase();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.V)
                {
                    AutoAlignToGround();
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.R)
                {
                    ToggleCurrentFlipY();
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.G)
                {
                    showGroundDisc = !showGroundDisc;
                    SceneView.RepaintAll();
                    Repaint();
                    e.Use();
                }
            }

            // 2. 지표면 기준면 디스크 (Ground Level Disc - 토글 ON일 때만 렌더링)
            if (showGroundDisc)
            {
                Handles.color = new Color(0.1f, 0.95f, 0.4f, 0.25f);
                Handles.DrawSolidDisc(_currentGroundPos, _currentGroundNormal, 2.0f);
                Handles.color = new Color(0.1f, 0.95f, 0.4f, 0.85f);
                Handles.DrawWireDisc(_currentGroundPos, _currentGroundNormal, 2.0f);
            }

            // 3. 지면 법선 화살표
            Handles.color = Color.cyan;
            Handles.DrawLine(_currentGroundPos, _currentGroundPos + _currentGroundNormal * 1.5f);

            // 4. 식생 실제 밑둥 오프셋 라인
            Vector3 offsetDir = Vector3.Slerp(Vector3.up, _currentGroundNormal, alignToNormal);
            Vector3 rootPos = _currentGroundPos + (offsetDir * groundOffset);
            if (Mathf.Abs(groundOffset) > 0.005f)
            {
                // 지면보다 아래(파묻힘): 빨간선, 지면보다 위(공중부양): 노란선
                Handles.color = groundOffset < 0 ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.9f);
                Handles.DrawLine(_currentGroundPos, rootPos);
                Handles.DrawWireDisc(rootPos, offsetDir, 0.6f);
            }

            // 5. 씬 뷰 오버레이 정보 라벨
            var curItem = _filteredItems[_currentIndex];
            string status = groundOffset > 0 ? $"공중 부양 +{groundOffset:F2}m" : (groundOffset < 0 ? $"땅속 파묻힘 {groundOffset:F2}m" : "지면 0.0m");
            string flipStatus = flipY ? "🔄 [하늘로 똑바로 세움 (Flip Y ON)]" : "🌿 [원작 조형 (Normal)]";
            string info = $"🌱 [삼탈워 식생 쇼케이스 #{_currentIndex + 1}/{_filteredItems.Count}]\n" +
                          $"• 모델: {curItem.displayName} ({curItem.modelFile})\n" +
                          $"• 지면 띄움: {groundOffset:+#0.00;-#0.00;0.00}m ({status}) | {flipStatus}\n" +
                          $"• 단축키: [Z] 이전 | [X] 다음 | [C] 카메라포커스 | [V] 자동밀착 | [R] 세우기/반전 | [G] 영역토글";

            GUIStyle labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            Handles.Label(_currentGroundPos + Vector3.up * 1.8f, info, labelStyle);
        }

        // ==========================================
        // GUI 렌더링
        // ==========================================
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🌱 삼탈워 식생 즉석 쇼케이스 & 지면 띄움 캘리브레이터", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("전체 청크를 베이크하지 않고도 0.01초 만에 씬 뷰 지면에 3D 풀을 소환하여 실시간으로 확인합니다.\n" +
                                   "364종 식생 모델을 순회하며 스크린샷의 그 풀을 색출하고, [✨ 지면 자동 밀착]으로 바닥 띄움을 완벽히 맞추세요.", MessageType.Info);

            EditorGUILayout.Space(4);

            // =========================================================================
            // 📍 1. 소환 위치 & 관찰 모드
            // =========================================================================
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📍 1. 쇼케이스 소환 위치 & 관찰 모드", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            locationMode = (LocationMode)EditorGUILayout.EnumPopup("소환 위치 모드", locationMode);
            if (locationMode == LocationMode.SpecificChunk)
            {
                EditorGUILayout.BeginHorizontal();
                targetChunkX = EditorGUILayout.IntField("청크 X", targetChunkX);
                targetChunkZ = EditorGUILayout.IntField("청크 Z", targetChunkZ);
                EditorGUILayout.EndHorizontal();
            }
            else if (locationMode == LocationMode.CustomWorldPos)
            {
                customWorldPos = EditorGUILayout.Vector3Field("월드 좌표", customWorldPos);
            }

            EditorGUILayout.BeginHorizontal();
            displayMode = (DisplayMode)EditorGUILayout.EnumPopup("관찰 모드", displayMode);
            if (displayMode == DisplayMode.Patch3x3)
            {
                patchSpacing = EditorGUILayout.Slider("패치 간격", patchSpacing, 0.5f, 3.5f);
            }
            EditorGUILayout.EndHorizontal();

            // 🟢 초록색 지면 가이드 디스크 영역 켜기/끄기 토글 버튼
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUI.backgroundColor = showGroundDisc ? new Color(0.2f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
            showGroundDisc = GUILayout.Toggle(showGroundDisc, showGroundDisc ? "🟢 초록색 지면 가이드 영역: 켜짐 (ON) [단축키 G]" : "⚪ 초록색 지면 가이드 영역: 꺼짐 (OFF) [단축키 G]", "Button", GUILayout.Height(28));
            GUI.backgroundColor = Color.white;
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateSpawnPosition();
                CreateOrUpdatePreviewObject();
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 1f, 0.6f);
            if (GUILayout.Button("🚀 씬 카메라를 식생 앞으로 이동 (단축키 C)", GUILayout.Height(26)))
            {
                UpdateSpawnPosition();
                FocusSceneCameraOnShowcase(displayMode == DisplayMode.Single ? 4.5f : 8.0f);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("🔄 위치 새로고침", GUILayout.Width(110), GUILayout.Height(26)))
            {
                UpdateSpawnPosition();
                CreateOrUpdatePreviewObject();
            }
            EditorGUILayout.EndHorizontal();

            // 🏞️ 평원 소환 퀵 버튼 (경사면 왜곡 없는 순수 지형 관찰)
            if (GUILayout.Button("🏞️ 평원 청크(31, 31)로 소환 위치 이동 (평지에서 관찰)", GUILayout.Height(24)))
            {
                locationMode = LocationMode.SpecificChunk;
                targetChunkX = 31;
                targetChunkZ = 31;
                UpdateSpawnPosition();
                CreateOrUpdatePreviewObject();
                FocusSceneCameraOnShowcase(displayMode == DisplayMode.Single ? 4.5f : 8.0f);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // =========================================================================
            // 🔍 2. 364종 식생 모델 고속 탐색기
            // =========================================================================
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"🔍 2. 삼탈워 식생 모델 탐색기 (총 {_allItems.Count}종 중 {_filteredItems.Count}개)", EditorStyles.boldLabel);

            // 카테고리 셀렉션 그리드 (3열 x 4행 12개 탭 정갈한 배치)
            EditorGUI.BeginChangeCheck();
            _selectedCategoryTab = GUILayout.SelectionGrid(_selectedCategoryTab, CategoryTabs, 3, GUILayout.Height(88));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("검색:", GUILayout.Width(40));
            _searchKeyword = EditorGUILayout.TextField(_searchKeyword);
            if (GUILayout.Button("초기화", GUILayout.Width(55)))
            {
                _searchKeyword = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFilter();
                CreateOrUpdatePreviewObject();
            }

            // 고속 넘김 버튼 툴바
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
            if (GUILayout.Button("◀◀ 이전 10개", GUILayout.Height(30)))
            {
                _currentIndex = Mathf.Max(0, _currentIndex - 10);
                if (_filteredItems.Count > 0) ResetParamsForItem(_filteredItems[_currentIndex]);
                CreateOrUpdatePreviewObject();
            }

            GUI.backgroundColor = new Color(0.2f, 1f, 0.5f);
            if (GUILayout.Button("◀ 이전 식물 [Z]", GUILayout.Height(30)))
            {
                PreviousItem();
            }

            GUI.backgroundColor = new Color(0.2f, 1f, 0.5f);
            if (GUILayout.Button("다음 식물 [X] ▶", GUILayout.Height(30)))
            {
                NextItem();
            }

            GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
            if (GUILayout.Button("다음 10개 ▶▶", GUILayout.Height(30)))
            {
                _currentIndex = Mathf.Min(_filteredItems.Count - 1, _currentIndex + 10);
                if (_filteredItems.Count > 0) ResetParamsForItem(_filteredItems[_currentIndex]);
                CreateOrUpdatePreviewObject();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // 인덱스 슬라이더
            if (_filteredItems.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                _currentIndex = EditorGUILayout.IntSlider($"식물 번호 (1~{_filteredItems.Count})", _currentIndex + 1, 1, _filteredItems.Count) - 1;
                if (EditorGUI.EndChangeCheck())
                {
                    ResetParamsForItem(_filteredItems[_currentIndex]);
                    CreateOrUpdatePreviewObject();
                }

                var item = _filteredItems[_currentIndex];

                // 모델 상세 정보 카드
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.LabelField($"<b>#{_currentIndex + 1}. {item.displayName}</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true, fontSize = 13 });
                EditorGUILayout.LabelField($"• 파일: {item.modelFile}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"• 카테고리: {item.category} | 텍스처: {item.albedoFile}", EditorStyles.miniLabel);

                if (_currentMesh != null)
                {
                    Vector3 bSize = _currentMesh.bounds.size;
                    EditorGUILayout.LabelField($"• 원작 메쉬 고유 규격: 가로 {bSize.x:F2}m × 세로 {bSize.y:F2}m × 폭 {bSize.z:F2}m | 버텍스 {_currentMesh.vertexCount:N0}개", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"• 씬 렌더링 규격: 가로 {bSize.x * scale:F2}m × 높이 {bSize.y * scale * heightMultiplier:F2}m (스케일 {scale:F2}x, 높이배율 {heightMultiplier:F2}x)", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"• 메쉬 바운딩 바닥(AABB Min Y): {_currentMesh.bounds.min.y:F3}m (피벗 오차)", EditorStyles.miniLabel);

                    // 🌿 식생 생태 조형 특성 자동 판정 (직립형 vs 절벽 늘어짐형 vs 융단형)
                    string growthType;
                    if (item.modelFile.Contains("Rice") || item.modelFile.Contains("Seedhead") || item.modelFile.Contains("Millet") || (bSize.y > 1.2f && _currentMesh.bounds.max.y > 0.5f))
                    {
                        growthType = "🌾 하늘 직립형 (Upright) - 지면에서 위로 곧게 솟아오르는 벼/갈대형 풀";
                    }
                    else if (_currentMesh.bounds.min.y <= -0.5f && _currentMesh.bounds.max.y <= 0.2f)
                    {
                        growthType = "🌿 언덕 늘어짐형 (Drooping / Weeping) - 경사면/언덕 턱에서 아래로 축 늘어지는 하수형 잡초";
                    }
                    else if (bSize.x > 3.0f && bSize.y < 0.8f)
                    {
                        growthType = "🌱 바닥 융단형 (Flat Carpet) - 지표면을 넓게 덮는 포복성 지피 패치";
                    }
                    else
                    {
                        growthType = "🍃 일반 수풀/관목형 (Bush / Shrub) - 둥근 덤불 형태";
                    }
                    EditorGUILayout.LabelField($"• 식생 조형 특성: {growthType}", EditorStyles.miniBoldLabel);
                }

                // 🔄 현재 식생 모델의 상하 반전 상태 및 즉시 토글
                EditorGUILayout.BeginHorizontal();
                if (flipY)
                {
                    GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
                    EditorGUILayout.LabelField("🟢 현재 상태: [하늘로 똑바로 세움 (FlipY ON)]", EditorStyles.boldLabel);
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                    EditorGUILayout.LabelField("🍂 현재 상태: [원작 하수형/늘어짐 (FlipY OFF)]", EditorStyles.boldLabel);
                }
                if (GUILayout.Button("🔄 현재 풀 반전 토글 [R]", GUILayout.Width(160), GUILayout.Height(22)))
                {
                    ToggleCurrentFlipY();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // 🔢 [사용자 직관 튜닝] 번호 목록으로 일괄 상하 반전 툴바
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("🔢 번호 목록으로 일괄 상하 반전 (사용자 지정 튜닝)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("뒤집힌 풀 번호를 적어주시면(예: 3, 5, 12, 20-35) 해당 모델들의 상하 반전을 일괄 적용하고 카탈로그에 영구 저장합니다.", MessageType.None);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("식물 번호:", GUILayout.Width(65));
                _batchFlipInput = EditorGUILayout.TextField(_batchFlipInput);
                GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
                if (GUILayout.Button("⬆️ 하늘로 세우기", GUILayout.Width(110), GUILayout.Height(22)))
                {
                    ApplyBatchFlip(true);
                }
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
                if (GUILayout.Button("⬇️ 원작 하수형", GUILayout.Width(110), GUILayout.Height(22)))
                {
                    ApplyBatchFlip(false);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("조건에 맞는 식생 모델이 없습니다.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // =========================================================================
            // 📏 3. 지면 띄움 (Ground Offset) 실시간 캘리브레이터
            // =========================================================================
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📏 3. 지면 띄움 (Ground Offset) & 규격 캘리브레이터", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // 🎯 지면 띄움 정밀 슬라이더
            EditorGUILayout.BeginHorizontal();
            groundOffset = EditorGUILayout.Slider(new GUIContent("지면 띄움 (Offset)", "양수: 위로 솟아오름, 음수: 땅속으로 파묻힘"), groundOffset, -1.50f, 2.00f);
            groundOffset = (float)Math.Round(groundOffset, 2);
            EditorGUILayout.EndHorizontal();

            // 미세 조절 버튼 모음
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-0.10m", GUILayout.Height(20))) groundOffset = (float)Math.Round(groundOffset - 0.10f, 2);
            if (GUILayout.Button("-0.02m", GUILayout.Height(20))) groundOffset = (float)Math.Round(groundOffset - 0.02f, 2);
            if (GUILayout.Button("0.00m (지면)", GUILayout.Height(20))) groundOffset = 0.0f;
            if (GUILayout.Button("+0.02m", GUILayout.Height(20))) groundOffset = (float)Math.Round(groundOffset + 0.02f, 2);
            if (GUILayout.Button("+0.10m", GUILayout.Height(20))) groundOffset = (float)Math.Round(groundOffset + 0.10f, 2);
            EditorGUILayout.EndHorizontal();

            // ✨ 지면 자동 밀착 버튼
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.1f, 0.95f, 0.35f);
            if (GUILayout.Button("✨ [현재 풀 지면 자동 밀착 (Auto Align)] 수학적 바닥 맞춤 (단축키 V)", GUILayout.Height(32)))
            {
                AutoAlignToGround();
            }
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
            if (GUILayout.Button("🌾 [삼탈워 364종 전수 스마트 바닥 캘리브레이션 & 전장 동기화]", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("삼탈워 364종 전수 캘리브레이션",
                    "364종 전체 식생 모델의 바닥 높이(AABB Bounds)를 분석하여,\n" +
                    "- 지피 잔디(238종): 지면 100% 밀착(alignToGround=1.0) & 바닥 Y=0 정확 클램핑\n" +
                    "- 수풀/덤불(126종): 자연스러운 수직 기립(alignToGround=0.35) & 바닥 Y=0 클램핑\n\n" +
                    "을 전수 자동 계산하고 카탈로그에 영구 보존하시겠습니까?", "예, 일괄 정렬", "취소"))
                {
                    AutoCalibrateAll364Models();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 크기 및 형태 슬라이더
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = flipY ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button(flipY ? "🔄 [상하 반전 ON] 하늘로 똑바로 세움 (단축키 R)" : "🔄 [상하 반전 OFF] 원작 조형 그대로 (단축키 R)", GUILayout.Height(28)))
            {
                ToggleCurrentFlipY();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            scale = EditorGUILayout.Slider("너비/전체 크기 (Scale)", scale, 0.2f, 3.0f);
            if (GUILayout.Button("1.0 (실측 원본)", GUILayout.Width(95), GUILayout.Height(18)))
            {
                scale = 1.0f;
                heightMultiplier = 1.0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            heightMultiplier = EditorGUILayout.Slider("높이 배율 (Height Multiplier)", heightMultiplier, 0.2f, 2.5f);
            if (GUILayout.Button("1.0 (기본 비율)", GUILayout.Width(95), GUILayout.Height(18)))
            {
                heightMultiplier = 1.0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            alignToNormal = EditorGUILayout.Slider("경사면 노멀 밀착 (Normal Align)", alignToNormal, 0.0f, 1.0f);
            if (GUILayout.Button("0.0 (수직 직립)", GUILayout.Width(90), GUILayout.Height(18))) alignToNormal = 0.0f;
            if (GUILayout.Button("0.3 (자연스러운 경사)", GUILayout.Width(125), GUILayout.Height(18))) alignToNormal = 0.3f;
            if (GUILayout.Button("1.0 (지면 밀착)", GUILayout.Width(90), GUILayout.Height(18))) alignToNormal = 1.0f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🍁 사계절 프리셋:", GUILayout.Width(100));
            if (GUILayout.Button("🌸 봄 (Spring)", GUILayout.Height(22))) { selectedMaterialIndex = 1; CreateOrUpdatePreviewObject(); }
            if (GUILayout.Button("🌿 여름 (Summer)", GUILayout.Height(22))) { selectedMaterialIndex = 2; CreateOrUpdatePreviewObject(); }
            if (GUILayout.Button("🍂 가을 (Autumn)", GUILayout.Height(22))) { selectedMaterialIndex = 3; CreateOrUpdatePreviewObject(); }
            if (GUILayout.Button("❄️ 겨울 (Winter)", GUILayout.Height(22))) { selectedMaterialIndex = 4; CreateOrUpdatePreviewObject(); }
            if (GUILayout.Button("🔄 원색", GUILayout.Width(50), GUILayout.Height(22))) { selectedMaterialIndex = 0; CreateOrUpdatePreviewObject(); }
            EditorGUILayout.EndHorizontal();

            selectedMaterialIndex = EditorGUILayout.Popup("적용 머티리얼 틴트", selectedMaterialIndex, MaterialOptions);

            if (EditorGUI.EndChangeCheck())
            {
                CreateOrUpdatePreviewObject();
            }

            EditorGUILayout.EndVertical();

            // =========================================================================
            // ✂️ 3-1. 텍스처 UV 자르기 & 메쉬 컷 실시간 조절 (Sandbox)
            // =========================================================================
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            _foldUvCropTuning = EditorGUILayout.Foldout(_foldUvCropTuning, "✂️ 3-1. 텍스처 UV 자르기 & 메쉬 컷 실시간 조절 (Sandbox)", true, EditorStyles.foldoutHeader);
            if (_foldUvCropTuning)
            {
                EditorGUI.indentLevel++;
                Vector2 uvR = GetCurrentMeshUVRange();
                string slotPos = uvR.y <= 0.55f ? "하단 슬롯 (0.00 ~ 0.50)" : (uvR.x >= 0.75f ? "상단 슬롯 (0.80 ~ 1.00)" : "중간 슬롯 (0.50 ~ 0.80)");
                EditorGUILayout.HelpBox($"• 현재 식생 모델의 아틀라스 텍스처 위치: V = [{uvR.x:F3} ~ {uvR.y:F3}] ({slotPos})\n• 슬라이더를 움직이면 풀의 위치와 상관없이 잎끝이 0.01초 만에 시원하게 실시간으로 잘리고 압축됩니다.", MessageType.Info);

                EditorGUI.BeginChangeCheck();

                _useGroundedTuftMesh = EditorGUILayout.ToggleLeft(new GUIContent("🌿 중심 뿌리 기준 접지 정규화 메쉬 (Grounded Tuft) 적용", "체크 시 8m 광역 평판을 단일 풀포기 기저부로 모으고 지면에 밀착시킵니다. 체크 해제 시 원작 순수 OBJ 원형으로 관찰합니다."), _useGroundedTuftMesh);

                EditorGUILayout.Space(2);
                _tipCropPercent = EditorGUILayout.Slider(new GUIContent("풀잎 끝 텍스처 자르기/압축 강도 (Tip Crop)", "풀잎 꼭대기에서 몇 % 영역을 아래로 강하게 압축하여 윗변 잘림을 방지할지 조절합니다 (0% ~ 40%)"), _tipCropPercent, 0.000f, 0.400f);
                _meshTipCutPercent = EditorGUILayout.Slider(new GUIContent("3D 풀잎 끝 메쉬 직접 깎기 (Mesh Cut)", "3D 버텍스 자체의 윗부분을 직접 깎아내려 잎끝을 물리적으로 단정하게 만듭니다 (0% ~ 35%)"), _meshTipCutPercent, 0.000f, 0.350f);
                _sideMarginPercent = EditorGUILayout.Slider(new GUIContent("좌우 테두리 여백 클램핑 (Side Margin)", "좌우 슬롯 텍스처 침범 아티팩트를 차단하는 안전 여백 (0% ~ 5%)"), _sideMarginPercent, 0.000f, 0.050f);

                if (EditorGUI.EndChangeCheck())
                {
                    CreateOrUpdatePreviewObject();
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔄 기본 권장값 (8% 텍스처 압축, 0% 메쉬 컷)으로 복원", GUILayout.Height(22)))
                {
                    _useGroundedTuftMesh = true;
                    _tipCropPercent = 0.08f;
                    _meshTipCutPercent = 0.00f;
                    _sideMarginPercent = 0.008f;
                    CreateOrUpdatePreviewObject();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // =========================================================================
            // 🎨 4. 머티리얼 셰이더 실시간 튜닝 & 안전 저장 (Material Sandbox & Commit)
            // =========================================================================
            EditorGUILayout.BeginVertical("box");
            _foldMaterialTuning = EditorGUILayout.Foldout(_foldMaterialTuning, "🎨 4. 머티리얼 셰이더 실시간 튜닝 & 안전 저장 (Material Sandbox)", true, EditorStyles.foldoutHeader);
            if (_foldMaterialTuning)
            {
                EditorGUI.indentLevel++;
                if (_currentMat != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"• 원본 머티리얼: {_currentMat.name}", EditorStyles.boldLabel);
                    GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                    if (GUILayout.Button("🔍 에셋 선택 (Ping)", GUILayout.Width(130), GUILayout.Height(20)))
                    {
                        EditorGUIUtility.PingObject(_currentMat);
                        Selection.activeObject = _currentMat;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox("• 슬라이더를 조작하면 씬 뷰 프리뷰 풀에만 실시간 반영되며, 원본 머티리얼 에셋은 훼손되지 않습니다.\n• 만족스러운 형태가 완성되었을 때 아래 [💾 머티리얼에 영구 저장] 버튼을 누르면 원본 에셋에 안전하게 기록됩니다.", MessageType.Info);

                    EditorGUI.BeginChangeCheck();

                    // 1) 풀잎 끝 칼자국 방지 테이퍼링
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("🌿 풀잎 끝 칼자국 방지 테이퍼링 (Tip & Edge Feathering)", EditorStyles.boldLabel);
                    _tuneTipFadeStart = EditorGUILayout.Slider(new GUIContent("풀잎 끝 감쇄 시작점 (V)", "Tip Fade Start: 풀잎 끝이 투명해지기 시작하는 높이 (기본 0.940)"), _tuneTipFadeStart, 0.850f, 1.000f);
                    _tuneTipFadeEnd = EditorGUILayout.Slider(new GUIContent("풀잎 끝 완전 소멸점 (V)", "Tip Fade End: 텍스처 맨 윗줄(1.000) 이전에 완전 투명화되는 높이 (기본 0.985)"), _tuneTipFadeEnd, 0.900f, 1.000f);
                    _tuneEdgeBleed = EditorGUILayout.Slider(new GUIContent("좌우 테두리 마진 (U)", "Edge Bleed Crop: 좌우 슬롯 침범 아티팩트 차단 여백 (기본 0.006)"), _tuneEdgeBleed, 0.000f, 0.030f);

                    // 2) 투명도 컷아웃 및 색상
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("✂️ 투명도 컷아웃 및 색상 (Cutout & Color)", EditorStyles.boldLabel);
                    _tuneCutoff = EditorGUILayout.Slider(new GUIContent("알파 컷오프 임계값", "Alpha Cutoff: 풀잎 두께감 및 투명도 경계 (기본 0.25)"), _tuneCutoff, 0.05f, 0.90f);
                    _tuneBaseColor = EditorGUILayout.ColorField(new GUIContent("기본 색조 틴트", "Base Color: 식생 기본 색상 조절 틴트"), _tuneBaseColor);

                    // 3) 바람 물결 애니메이션
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("💨 바람 물결 애니메이션 (Wind Animation)", EditorStyles.boldLabel);
                    _tuneWindMultiplier = EditorGUILayout.Slider(new GUIContent("바람 흔들림 강도 배율", "Wind Multiplier: 바람에 흔들리는 세기 (기본 0.35)"), _tuneWindMultiplier, 0.0f, 2.0f);
                    _tuneLeafFlutter = EditorGUILayout.Slider(new GUIContent("풀잎 끝 펄럭임 세기", "Leaf Flutter: 풀잎 끝단의 잔물결 펄럭임 강도 (기본 0.25)"), _tuneLeafFlutter, 0.0f, 1.0f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        _hasUnsavedMaterialChanges = true;
                        ApplyTuningToPreviewMaterial();
                        if (_previewRoot != null)
                        {
                            var mrs = _previewRoot.GetComponentsInChildren<MeshRenderer>(true);
                            for (int r = 0; r < mrs.Length; r++)
                            {
                                mrs[r].sharedMaterial = _previewInstanceMat;
                            }
                        }
                        SceneView.RepaintAll();
                    }

                    // 저장 및 복원 버튼 바
                    EditorGUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal();

                    if (_hasUnsavedMaterialChanges)
                    {
                        GUI.backgroundColor = new Color(0.2f, 1.0f, 0.4f);
                        if (GUILayout.Button("💾 [머티리얼에 영구 저장 (Save Changes)] *수정됨*", GUILayout.Height(32)))
                        {
                            SaveTuningToSourceMaterial();
                        }
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f);
                        if (GUILayout.Button("💾 [머티리얼에 영구 저장 (Save Changes)]", GUILayout.Height(32)))
                        {
                            SaveTuningToSourceMaterial();
                        }
                    }

                    GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
                    if (GUILayout.Button("🔄 [원본 수치로 되돌리기 (Reset)]", GUILayout.Width(190), GUILayout.Height(32)))
                    {
                        SyncMaterialTuningFromSource(_currentMat);
                        ApplyTuningToPreviewMaterial();
                        if (_previewRoot != null)
                        {
                            var mrs = _previewRoot.GetComponentsInChildren<MeshRenderer>(true);
                            for (int r = 0; r < mrs.Length; r++)
                            {
                                mrs[r].sharedMaterial = _previewInstanceMat;
                            }
                        }
                        SceneView.RepaintAll();
                        ShowNotification(new GUIContent("🔄 원본 머티리얼 수치로 복원되었습니다."));
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("현재 선택된 모델에 연결된 머티리얼 에셋이 없습니다.", MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // =========================================================================
            // 💾 5. 결과 즉시 등록 및 터레인 매니저 연동
            // =========================================================================
            if (_filteredItems.Count > 0)
            {
                var curItem = _filteredItems[_currentIndex];
                var mgr = UnityEngine.Object.FindAnyObjectByType<TotalWarTerrainManager>();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("💾 5. 확인된 식물 & 띄움값 팔레트에 즉시 반영", EditorStyles.boldLabel);

                // 1) 신규 항목으로 추가
                GUI.backgroundColor = new Color(0.2f, 0.85f, 1.0f);
                if (GUILayout.Button($"➕ [식생 팔레트에 신규 추가] #{_currentIndex + 1} '{curItem.displayName}' 등록", GUILayout.Height(30)))
                {
                    if (mgr != null)
                    {
                        Undo.RecordObject(mgr, "Add Foliage From Showcase");
                        if (mgr.customGrassList == null) mgr.customGrassList = new List<RadialGrassConfig>();

                        mgr.customGrassList.Add(new RadialGrassConfig
                        {
                            displayName = curItem.displayName,
                            modelFile = curItem.modelFile,
                            albedoFile = curItem.albedoFile,
                            normalFile = curItem.normalFile,
                            isEnabled = true,
                            minWidth = scale * 0.9f,
                            maxWidth = scale * 1.25f,
                            minHeight = (scale * heightMultiplier) * 0.9f,
                            maxHeight = (scale * heightMultiplier) * 1.25f,
                            groundOffset = groundOffset,
                            alignToGround = alignToNormal,
                            flipY = flipY,
                            minDensity = 30,
                            maxDensity = 80,
                            noiseThreshold = 0.25f,
                            growOnGrass = true,
                            growOnDirt = curItem.category.Contains("건조"),
                            growOnRock = false
                        });

                        EditorUtility.SetDirty(mgr);
                        ShowNotification(new GUIContent($"✅ '{curItem.displayName}' (띄움 {groundOffset:F2}m, 반전: {flipY}) 팔레트 추가 완료!"));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("알림", "씬에 TotalWarTerrainManager가 없습니다.", "확인");
                    }
                }
                GUI.backgroundColor = Color.white;

                // 2) 기존 팔레트 슬롯 덮어쓰기
                if (mgr != null && mgr.customGrassList != null && mgr.customGrassList.Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("기존 슬롯 덮어쓰기:", GUILayout.Width(120));
                    _overwriteSlotIndex = EditorGUILayout.IntSlider(_overwriteSlotIndex, 0, mgr.customGrassList.Count - 1);
                    EditorGUILayout.EndHorizontal();

                    string slotName = mgr.customGrassList[_overwriteSlotIndex].displayName;
                    GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
                    if (GUILayout.Button($"🔄 [슬롯 #{_overwriteSlotIndex} '{slotName}']을 현재 식물 & 띄움값으로 교체", GUILayout.Height(28)))
                    {
                        Undo.RecordObject(mgr, "Overwrite Foliage Slot");
                        var slot = mgr.customGrassList[_overwriteSlotIndex];
                        slot.displayName = curItem.displayName;
                        slot.modelFile = curItem.modelFile;
                        slot.albedoFile = curItem.albedoFile;
                        slot.normalFile = curItem.normalFile;
                        slot.minWidth = scale * 0.9f;
                        slot.maxWidth = scale * 1.25f;
                        slot.minHeight = (scale * heightMultiplier) * 0.9f;
                        slot.maxHeight = (scale * heightMultiplier) * 1.25f;
                        slot.groundOffset = groundOffset;
                        slot.alignToGround = alignToNormal;
                        slot.flipY = flipY;

                        EditorUtility.SetDirty(mgr);
                        ShowNotification(new GUIContent($"✅ 슬롯 #{_overwriteSlotIndex} '{curItem.displayName}' 교체 완료!"));
                    }
                    GUI.backgroundColor = Color.white;
                }

                // 3) 청크 오버라이드에 바로 바인딩
                if (locationMode == LocationMode.SpecificChunk && mgr != null)
                {
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.6f);
                    if (GUILayout.Button($"🎯 [청크 X: {targetChunkX}, Z: {targetChunkZ}]의 오버라이드에 현재 설정 즉시 바인딩", GUILayout.Height(28)))
                    {
                        Undo.RecordObject(mgr, "Assign Foliage to Chunk");
                        if (mgr.chunkFoliageOverrides == null) mgr.chunkFoliageOverrides = new List<ChunkFoliageOverride>();

                        var existing = mgr.chunkFoliageOverrides.Find(o => o.chunkCoord.x == targetChunkX && o.chunkCoord.y == targetChunkZ);
                        if (existing == null)
                        {
                            existing = new ChunkFoliageOverride
                            {
                                chunkCoord = new Vector2Int(targetChunkX, targetChunkZ),
                                biomeType = FoliageBiomeType.PlainsCarpet,
                                densityMultiplier = 1.0f,
                                allowedModelIndices = new List<int> { _overwriteSlotIndex }
                            };
                            mgr.chunkFoliageOverrides.Add(existing);
                        }
                        else
                        {
                            if (existing.allowedModelIndices == null) existing.allowedModelIndices = new List<int>();
                            if (!existing.allowedModelIndices.Contains(_overwriteSlotIndex))
                            {
                                existing.allowedModelIndices.Add(_overwriteSlotIndex);
                            }
                        }

                        EditorUtility.SetDirty(mgr);
                        ShowNotification(new GUIContent($"✅ 청크({targetChunkX}, {targetChunkZ})에 슬롯 #{_overwriteSlotIndex} 바인딩 완료!"));
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
