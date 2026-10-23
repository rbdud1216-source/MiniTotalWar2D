using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🌲 [전술 숲 및 장애물 구역 (Tactical Forest Zone)]
/// 삼국지 토탈워(Total War: Three Kingdoms) 본편에서 직접 추출/변환한 실제 3D 모델(온대 상록수, 열대 식생, 낙엽수/꽃나무, 수변/농경지, 전장 고목 등)을 
/// 지형에 자연스럽게 배치하고, 부대/유닛이 진입했을 때 토탈워 고유의 시야 은폐(매복) 및 사선(Line of Sight) 차폐를 실시간 수행합니다.
/// 사용자의 요구에 따라 [온대 상록수], [열대/남만 상록 식생], [낙엽수 & 꽃나무], [수변 & 농경지], [지표 관목], [전장 고목/그루터기]로 카테고리를 정밀 분리 제공합니다.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class TacticalForestZone : MonoBehaviour
{
    public enum ZoneShape
    {
        [InspectorName("원형 구역 (Circle)")]
        Circle, // 원형 숲 구역
        [InspectorName("직사각형 구역 (Box)")]
        Box     // 직사각형 숲 구역
    }

    public enum ForestTheme
    {
        [InspectorName("사용자 지정 조합 (Custom Selection)")]
        CustomSelection,    // 사용자 지정 (체크박스로 자유 조합)
        [InspectorName("전체 혼합림 (Mixed Forest)")]
        Mixed,              // 전체 혼합림 (모든 수종 골고루 배치)
        [InspectorName("남만 원시 밀림 (Nanman Jungle)")]
        NanmanJungle,       // 남만 밀림 (반얀트리, 바나나/코코넛 야자, 정글구실잣밤, 종려야자, 코끼리풀, 검고사리)
        [InspectorName("강남 수변림 (Jiangnan Riverside)")]
        JiangnanRiverside,  // 강남 수변림 (수양버들, 대나무, 단풍나무, 연꽃, 벼 작물)
        [InspectorName("중원 평야림 (Central Plains)")]
        CentralPlains,      // 중원 평야림 (은행나무, 회화나무, 매화나무, 목련나무, 기장/벼 농경지)
        [InspectorName("황산 소나무림 (Huangshan Pine)")]
        HuangshanPine,      // 황산 소나무림 (황산소나무, 직립소나무 고산 침엽수림)
        [InspectorName("대나무 군락지 (Bamboo Grove)")]
        BambooGrove,        // 울창한 대나무숲 (삼탈워 순수 대나무 군락지)
        [InspectorName("울창한 활엽수림 (Castanopsis Wood)")]
        CastanopsisWood,    // 울창한 활엽수림 (남방/중원 넓은 잎 녹음)
        [InspectorName("격전지 고목림 (Battlefield Ruins)")]
        BattlefieldRuins    // 격전지 고목림 (부러진 소나무, 앙상한 고사목, 그루터기)
    }

    public enum Season
    {
        [InspectorName("여름 (Summer - 짙푸른 녹음)")]
        Summer, // 여름 (울창하고 짙은 녹음)
        [InspectorName("봄 (Spring - 연두빛 새싹과 화사한 개화)")]
        Spring, // 봄 (연두빛 새싹과 화사한 매화/목련 개화)
        [InspectorName("가을 (Autumn - 선홍빛 단풍과 황금빛 은행)")]
        Autumn, // 가을 (선홍빛 단풍과 황금빛 은행)
        [InspectorName("겨울 (Winter - 설경 및 앙상한 가지)")]
        Winter  // 겨울 (하얗게 눈이 쌓인 설목 및 마른 가지)
    }

    // 전장 내 모든 활성 숲 구역 정적 레지스트리
    private static readonly List<TacticalForestZone> activeZones = new List<TacticalForestZone>();
    public static IReadOnlyList<TacticalForestZone> ActiveZones => activeZones;

    [Header("🌲 숲 구역 형태 및 테마 프리셋")]
    [Tooltip("숲 구역 형태 (원형 또는 직사각형)")]
    public ZoneShape shape = ZoneShape.Circle;

    [Tooltip("숲 테마 프리셋 (원하는 지역 테마 선택 시 아래 토글이 자동 세팅됩니다)")]
    public ForestTheme theme = ForestTheme.CustomSelection;

    [Tooltip("사계절 환경 모드 (봄, 여름, 가을, 겨울 - 변경 시 즉시 나뭇잎 텍스처 실시간 전환)")]
    public Season currentSeason = Season.Summer;

    [Tooltip("원형 숲의 유효 반경 (단위: m, 기본: 30m)")]
    public float radius = 30f;

    [Tooltip("직사각형 숲의 가로(X) 및 세로(Z) 크기 (단위: m, 기본: 50x35m)")]
    public Vector2 boxSize = new Vector2(50f, 35f);

    // =========================================================================
    // 1. 🌲 [온대/한대 상록수] 사계절 푸른 잎사귀 유지 (소나무는 겨울 설화)
    // =========================================================================
    [Header("🌲 [온대/한대 상록수] 사계절 푸른 잎사귀 유지 (소나무는 겨울 설화)")]
    [Tooltip("[온대 상록수] 황산 소나무 (Huangshan Pine) - 굽이치는 가지와 넓은 솔잎 수관 (겨울 설화)")]
    public bool spawnHuangshanPine = true;

    [Tooltip("[온대 상록수] 직립 소나무 (Straight Pine) - 하늘로 곧게 뻗은 일반 소나무 (겨울 설화)")]
    public bool spawnPine = true;

    [Tooltip("[온대 상록수] 대나무 군락 (Bamboo) - 사계절 내내 푸른 청록색 대나무림 (빠른 전소)")]
    public bool spawnBamboo = true;

    [Tooltip("[온대 상록수] 구실잣밤 활엽수 (Castanopsis) - 온대/난대의 울창한 상록 참나무과 활엽수")]
    public bool spawnCastanopsis = true;

    [Tooltip("[온대 상록수] 녹나무 (Camphor) - 거대한 상록 활엽 거목 (장뇌유 폭발 연소)")]
    public bool spawnCamphor = true;

    [Tooltip("[온대 상록수] 차나무 (Tea Bush) - 중국 남방 명차 밭의 둥글고 푸른 상록 관목/소교목")]
    public bool spawnShrubTea = true;

    // =========================================================================
    // 2. 🌴 [열대/남만 상록 식생] 사계절 짙고 울창한 원시 정글 (사계절 불변)
    // =========================================================================
    [Header("🌴 [열대/남만 상록 식생] 사계절 짙고 울창한 원시 정글 (사계절 불변)")]
    [Tooltip("[열대 상록수] 남만 반얀트리 (Ficus Banyan) - 덩굴과 뿌리가 뒤엉킨 남만 밀림 웅장한 거목")]
    public bool spawnFicus = true;

    [Tooltip("[열대 상록수] 남만 바나나 야자 (Banana Palm) - 남만 밀림의 거대한 열대 바나나 잎사귀")]
    public bool spawnBananaPalm = true;

    [Tooltip("[열대 상록수] 남만 코코넛 야자 (Coconut Palm) - 원시 밀림 높게 뻗은 열대 코코넛 야자수")]
    public bool spawnCoconutPalm = true;

    [Tooltip("[열대 상록수] 남만 정글 구실잣밤 (Jungle Castanopsis) - 이끼와 덩굴이 얽힌 짙은 정글 수관")]
    public bool spawnJungleCastanopsis = true;

    [Tooltip("[열대 상록수] 종려 야자 (Palm Windmill) - 남만 밀림 및 남방 전장의 열대 부채꼴 야자")]
    public bool spawnPalm = true;

    [Tooltip("[열대 관목] 열대 코끼리풀 (Elephant Grass) - 열대 정글 바닥을 무성하게 뒤덮는 거대 초본")]
    public bool spawnShrubElephantGrass = true;

    [Tooltip("[열대 관목] 열대 검고사리 (Sword Fern) - 남만 밀림 숲속의 날렵한 열대 양치식물")]
    public bool spawnShrubSwordFern = true;

    // =========================================================================
    // 3. 🍂 [낙엽수 & 꽃나무] 계절 반응 (봄 개화/새싹, 가을 단풍, 겨울 설화)
    // =========================================================================
    [Header("🍂 [낙엽수 & 꽃나무] 계절 반응 (봄 개화/새싹, 가을 단풍, 겨울 설화)")]
    [Tooltip("[낙엽수] 목련나무 거목 (Magnolia Tree) - 봄철 크고 화사한 백목련 꽃이 만개하는 중국 고유 거목")]
    public bool spawnMagnolia = true;

    [Tooltip("[낙엽수] 매화나무 (Plum Tree) - 봄철 화사한 분홍/백매화 꽃잎 만개 개화, 가을 단풍")]
    public bool spawnPlum = true;

    [Tooltip("[낙엽수] 은행나무 (Ginkgo) - 가을에 눈부신 황금빛 단풍, 중원 평야의 장엄한 고목")]
    public bool spawnGinkgo = true;

    [Tooltip("[낙엽수] 단풍나무 (Maple) - 가을에 불타는 선홍빛 단풍, 전장의 화려한 정취")]
    public bool spawnMaple = true;

    [Tooltip("[낙엽수] 수양버들 (Willow) - 강남 수변의 축 늘어진 가지, 봄 연두 새싹, 가을 황록색")]
    public bool spawnWillow = true;

    [Tooltip("[낙엽수] 회화나무 / 선비나무 (Scholar) - 봄 연두 새싹, 가을 고즈넉한 갈색 단풍, 겨울 설화")]
    public bool spawnScholar = true;

    [Tooltip("[낙엽 관목] 목련 관목 (Shrub Magnolia) - 숲 가장자리와 정원을 수놓는 목련 덤불")]
    public bool spawnShrubMagnolia = true;

    [Tooltip("[낙엽 관목] 진달래 / 철쭉 (Shrub Rhododendron) - 봄철 분홍빛 꽃잎이 화사하게 만개하는 꽃덤불")]
    public bool spawnShrubRhododendron = true;

    // =========================================================================
    // 4. 🌾 [수변 & 농경지 식생] 강변 습지 및 평야 논밭
    // =========================================================================
    [Header("🌾 [수변 & 농경지 식생] 강변 습지 및 평야 논밭")]
    [Tooltip("[수변 수생] 연꽃 (Lotus) - 강남 수변, 연못, 습지대에 넓게 펼쳐진 둥근 연잎과 연꽃")]
    public bool spawnShrubLotus = true;

    [Tooltip("[농경지 작물] 벼 / 논 (Rice Crop) - 중원 및 강남 평야의 비옥한 논밭 벼 이삭")]
    public bool spawnShrubRice = true;

    [Tooltip("[농경지 작물] 기장 / 조 밭 (Millet Crop) - 북방 및 건조 평야의 거친 밭작물")]
    public bool spawnShrubMillet = true;

    // =========================================================================
    // 5. 🌿 [지표 하층 관목 및 양치식물]
    // =========================================================================
    [Header("🌿 [지표 하층 관목 및 양치식물]")]
    [Tooltip("솔관목 (Pine Shrub) - 소나무 숲 바닥에 자생하는 키 작은 침엽 관목")]
    public bool spawnShrubPine = true;

    [Tooltip("왕고사리 (Royal Fern) - 숲 바닥을 울창하게 덮는 양치식물 덤불")]
    public bool spawnShrubFern = true;

    [Tooltip("선비덤불 (Scholar Bush) - 숲 가장자리와 길가에 풍성한 녹색 덤불")]
    public bool spawnShrubScholar = true;

    [Tooltip("은방울꽃 수풀 (Convallaria) - 지표면을 은은하게 수놓는 야생 야초")]
    public bool spawnShrubConvallaria = true;

    // =========================================================================
    // 6. 🪵 [전장 고목 & 그루터기] 격전지의 비장한 흔적
    // =========================================================================
    [Header("🪵 [전장 고목 & 그루터기] 격전지의 비장한 흔적")]
    [Tooltip("[전장 고목] 소나무 고사목 (Dead Pine) - 잎사귀 없이 앙상한 줄기와 가지만 남은 마른 소나무 뼈대")]
    public bool spawnPineDead = true;

    [Tooltip("[전장 고목] 부러진 소나무 (Broken Pine) - 화살과 공성 무기, 격전으로 부러진 전장의 소나무 (일부 잔여 솔잎)")]
    public bool spawnBrokenPine = true;

    [Tooltip("[전장 고목] 부러진 회화나무 (Broken Scholar) - 전장에서 부러진 회화나무/선비나무 고목 (뼈대 및 잔가지)")]
    public bool spawnBrokenScholar = true;

    [Tooltip("[전장 고목] 앙상한 고사목 (Dead Castanopsis) - 잎이 모두 떨어지고 비장하게 마른 전장의 활엽 고목 뼈대")]
    public bool spawnDeadCastanopsis = true;

    [Tooltip("[전장 고목] 남만 정글 고사목 (Jungle Dead) - 열대 덩굴과 이끼가 뒤엉킨 남만 밀림의 마른 고목")]
    public bool spawnJungleDead = true;

    [Tooltip("[벌채 밑동] 대나무 밑동/군락 (Bamboo Stumps) - 잘려나간 대나무 밑동 및 그루터기 군락")]
    public bool spawnBambooStump = true;

    [Tooltip("[벌채 밑동] 거목 그루터기 (Tree Stumps) - 군용 목재로 벌목되었거나 잘려나간 거목 밑동")]
    public bool spawnStump = true;

    [Tooltip("[전장 잔해] 부러진 나뭇가지 더미 (Broken Branches) - 지표면에 나뒹구는 부러진 굵은 나뭇가지 잔해")]
    public bool spawnBrokenBranches = true;

    [Header("🌿 토탈워 삼국지 초목 스폰 설정")]
    [Tooltip("게임 시작 시 초목(나무/수풀)을 자동 생성할지 여부")]
    public bool autoGenerateOnStart = true;

    [Tooltip("스폰할 토탈워 3D 나무 개수")]
    public int treeCount = 27;

    [Tooltip("스폰할 토탈워 3D 관목 및 지표 수풀 개수")]
    public int bushCount = 48;

    [Tooltip("나무 크기 배율 (기본: 0.28, 토탈워 35m 원본 -> 약 10m 실전 크기)")]
    [Range(0.1f, 1.0f)]
    public float treeScaleMultiplier = 0.28f;

    [Tooltip("수풀 크기 배율 (기본: 0.32, 약 1.2m 실전 크기)")]
    [Range(0.1f, 1.0f)]
    public float bushScaleMultiplier = 0.32f;

    [Tooltip("초목 무작위 시드 (동일한 시드 = 동일한 배치)")]
    public int randomSeed = 12345;

    [Header("⛰️ 지형 적응 및 뿌리 고정 (Terrain Anchoring)")]
    [Tooltip("나무 뿌리를 지면에 깊게 묻는 기본 매립 깊이 (단위: m, 경사지 공중 부양 원천 방지)")]
    [Range(0.1f, 1.5f)]
    public float baseGroundSinkDepth = 0.45f;

    [Tooltip("지형 경사면 정렬 가중치 (0 = 완전 수직, 1 = 지형 경사와 100% 일치)")]
    [Range(0.0f, 0.6f)]
    public float slopeAlignmentWeight = 0.22f;

    [Tooltip("나무가 자랄 수 있는 최대 지형 경사각 (도 단위, 이 각도 이상의 가파른 절벽에는 스폰 제한)")]
    [Range(20f, 60f)]
    public float maxSlopeAngle = 40f;

    [Header("👁️ 전술 은폐 및 시야 차폐")]
    [Tooltip("숲 내부 유닛의 기본 피탐지 거리 (단위: m, 기본: 25m)")]
    public float stealthDetectionRange = 25.0f;

    [Tooltip("숲 내부 유닛이 전력질주(구보) 시 발각되는 거리 (단위: m, 기본: 45m)")]
    public float runningDetectionRange = 45.0f;

    [Tooltip("숲을 가로지르는 사선 차폐(LoS Block) 활성화 여부")]
    public bool blockLineOfSight = true;

    [Header("❄️ 겨울 적설 환경 설정 (Winter Snow Settings)")]
    [Tooltip("겨울(Winter)일 때 눈이 내린 설경을 연출할지 여부\n- 체크 시: 삼탈워 설경 텍스처(_wi_) + 셰이더 프로시저럴 상향 적설 연출\n- 체크 해제 시: 눈이 오지 않은 삭막한 마른 겨울(_br_ 앙상한 가지) 연출\n※ 봄/여름/가을에는 눈 연출이 절대 나타나지 않습니다.")]
    public bool enableWinterSnow = true;

    [Tooltip("겨울철 눈 쌓임 강도 (0 = 눈 없음, 1 = 폭설)")]
    [Range(0.0f, 1.0f)]
    public float winterSnowAmount = 0.85f;

    [Tooltip("겨울철 나뭇잎 눈 덮임 비율 (0 = 눈 없음, 0.55 = 수관 상단 윗면에만 소복이 적설, 1.0 = 나무 전체 잎사귀 덮임)")]
    [Range(0.0f, 1.0f)]
    public float leafSnowRatio = 0.55f;

    [Header("🔥 삼탈워 전장 화재 전파 시뮬레이션 (Wildfire Propagation)")]
    [Tooltip("화재 시뮬레이션 활성화 여부")]
    public bool enableFireVFX = false;

    [Tooltip("바람 연동 이웃 나무 연쇄 화재 전파 활성화")]
    public bool enableWildfirePropagation = true;

    [Tooltip("기본 전파 유효 반경 (단위: m, 기본: 9.0m)")]
    public float basePropagationRadius = 9.0f;

    [Tooltip("바람 방향 화재 전파 가속 배율 (순풍 최대 16m)")]
    public float windSpreadMultiplier = 2.5f;

    [Tooltip("숲 은폐 해제 여부 (연소율 40% 이상 시 자동 은폐 해제)")]
    public bool isForestRevealedByFire = false;

    [Tooltip("동시 착화 나무 개수 (기본 8그루)")]
    [Range(1, 20)]
    public int burningTreesCount = 8;

    [Tooltip("개별 화염 크기 배율 (0.15 ~ 1.5)")]
    [Range(0.15f, 1.5f)]
    public float flameScaleMultiplier = 0.45f;

    [Tooltip("개별 연기 크기 / 볼륨 배율 (0.2 ~ 3.0, 기본값 1.0 = 삼탈워 표준 크기)")]
    [Range(0.2f, 3.0f)]
    public float smokeScaleMultiplier = 1.0f;

    public enum SmokeVisualType
    {
        [InspectorName("🏮 삼탈워 원본 웜 차콜 훈연 (Warm Charcoal - 권장)")]
        [Tooltip("삼국지 토탈워 본편 화재 원본 색상: 송진 및 유기물 연소의 따스한 흙빛 탄화 훈연 (R:0.28, G:0.20, B:0.14)")]
        TotalWarWarmCharcoal,
        [InspectorName("🌫️ 삼탈워 토탈 잿빛 훈연 (Total War Ash Gray)")]
        [Tooltip("삼탈워 본편 텍스처 원본의 자연스러운 산불 잿빛 훈연 (R:0.48, G:0.44, B:0.40)")]
        AshGray,
        [InspectorName("⬛ 삼탈워 짙은 흑갈색 훈연 (Dark Brownish Soot)")]
        [Tooltip("송진/오일 급격 연소로 인한 짙은 흑갈색 검댕 훈연 (R:0.18, G:0.15, B:0.14)")]
        PitchBlack,
        [InspectorName("🌲 수종별 지능형 자동 전환 (Species Adaptive)")]
        [Tooltip("소나무/녹나무는 웜 차콜 훈연, 활엽수/대나무는 잿빛 훈연 자동 적용")]
        SpeciesAdaptive,
        [InspectorName("🎨 사용자 지정 색상 (Custom Color)")]
        [Tooltip("인스펙터의 연기 시작/상공 색상 컬러 피커를 직접 설정 및 저장")]
        CustomColor
    }

    [Tooltip("화재 연기 시각 유형 프리셋\n- TotalWarWarmCharcoal: 삼탈워 원본 웜 차콜 훈연 (권장)\n- AshGray: 토탈워 표준 은회색/잿빛 연기\n- PitchBlack: 송진/기름 연소 흑연\n- SpeciesAdaptive: 수종별 자동 적응\n- CustomColor: 사용자 지정 색상")]
    public SmokeVisualType smokeVisualType = SmokeVisualType.TotalWarWarmCharcoal;

    [Tooltip("연기 진하기 / 농도 배율 (0.1 = 매우 옅은 은은한 훈연, 0.75 = 삼탈워 표준 훈연, 1.0 = 아주 짙고 묵직한 연기)")]
    [Range(0.1f, 1.0f)]
    public float smokeDensity = 0.75f;

    [Tooltip("연기 시작 색상 (나뭇잎 표면에서 막 피어오를 때의 연기 색상, 삼탈워 훈연 회갈색 기본값)")]
    public Color smokeStartColor = new Color(0.62f, 0.56f, 0.50f, 1.0f);

    [Tooltip("연기 상공 확산 색상 (하늘로 피어오르며 흩어질 때의 연기 색상, 삼탈워 잿빛 훈연 기본값)")]
    public Color smokeEndColor = new Color(0.75f, 0.70f, 0.65f, 1.0f);

    // 생성된 초목 컨테이너
    private Transform vegetationRoot;
    private Transform fireVfxRoot;

    // 수목 연소 시뮬레이션 노드 구조체
    public enum TreeCombustionState { Healthy, Smoking, Burning, Charred }

    [System.Serializable]
    public class TreeCombustionNode
    {
        public Transform transform;
        public GameObject treeObject;
        public Renderer[] renderers;
        public TreeCombustionState state = TreeCombustionState.Healthy;
        public float fireGauge = 0f;
        public float burnProgress = 0f;
        public float burnTimer = 0f;
        public float burnDuration = 40.0f;
        public float typeFlameMult = 1.0f;
        public float typeSmokeMult = 1.0f;
        public TotalWarFireVFXController fireController;
    }

    private readonly List<TreeCombustionNode> treeCombustionNodes = new List<TreeCombustionNode>();
    public IReadOnlyList<TreeCombustionNode> CombustionNodes => treeCombustionNodes;

    // 토탈워 원본 텍스처 머티리얼 캐시
    private static Material matCompositeFlames;
    private static Material matCharredWood;
    private static Material matWispySmoke;
    private static Material matTotalWarEmbers;

    private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    private Season lastSeason;
    private bool lastWinterSnow = true;
    private float lastWinterSnowAmount = 0.85f;
    private float lastLeafSnowRatio = 0.55f;

    private void Awake()
    {
        lastSeason = currentSeason;
        enableFireVFX = false; // 게임 시작 시 자동 화재 발생 방지
        isForestRevealedByFire = false;
        ClearFireVFX(); // 에디터 씬에 남아있던 잔여 화재 파티클 완전 파괴
        ExtinguishFire(); // 탄화된 나뭇잎 100% 정상 녹색/설경으로 원상 복구
        InitTotalWarMaterials();
        SnapToTerrain();
    }

    private void OnEnable()
    {
        if (!activeZones.Contains(this)) activeZones.Add(this);
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
    }

    private void Start()
    {
        InitTotalWarMaterials();
        if (autoGenerateOnStart && (vegetationRoot == null || vegetationRoot.childCount == 0))
        {
            GenerateVegetation();
        }
        else
        {
            if (treeCombustionNodes.Count == 0)
            {
                InitTreeCombustionNodes();
            }
            RefreshVegetationMaterials();
        }
    }

    private void Update()
    {
        if (currentSeason != lastSeason || enableWinterSnow != lastWinterSnow ||
            Mathf.Abs(winterSnowAmount - lastWinterSnowAmount) > 0.01f ||
            Mathf.Abs(leafSnowRatio - lastLeafSnowRatio) > 0.01f)
        {
            lastSeason = currentSeason;
            lastWinterSnow = enableWinterSnow;
            lastWinterSnowAmount = winterSnowAmount;
            lastLeafSnowRatio = leafSnowRatio;
            RefreshVegetationMaterials();
        }

        UpdateWildfireSimulation();

#if UNITY_EDITOR
        CheckCaptureFlag();
#endif
    }

#if UNITY_EDITOR
    private void CheckCaptureFlag()
    {
        string flagPath = @"c:\unityProject\MiniTotalWar2D\scratch\trigger_capture.flag";
        if (System.IO.File.Exists(flagPath))
        {
            try { System.IO.File.Delete(flagPath); } catch { }
            PerformRuntimeFireAndSmokeCapture();
        }
    }

    [ContextMenu("🔥 화재 및 연기 렌더링 캡처 (Capture Fire & Smoke)")]
    public void PerformRuntimeFireAndSmokeCapture()
    {
        // 1. 최신 FoliageWind 셰이더 및 탄화 머티리얼 즉시 동기화
        RefreshVegetationMaterials();

        ClearFireVFX();
        enableFireVFX = true;
        SpawnFireVFX();

        // 파티클 2.5초 시뮬레이션으로 연기와 화염이 만개한 상태 생성
        var allPS = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allPS)
        {
            ps.Simulate(2.5f, true, true);
        }

        Transform targetTree = null;
        var fireControllers = GetComponentsInChildren<TotalWarFireVFXController>(true);
        if (fireControllers.Length > 0)
        {
            targetTree = fireControllers[0].transform;
        }

        if (targetTree == null)
        {
            Debug.LogWarning("[TacticalForestZone] 화재 대상을 찾을 수 없습니다.");
            return;
        }

        string saveDir = @"C:\Users\PC\.gemini\antigravity-ide\brain\3cc4d2a0-2845-4b27-b568-d5053c4f194b\scratch";
        System.IO.Directory.CreateDirectory(saveDir);

        Vector3 pos = targetTree.position;

        GameObject camObj = new GameObject("TempFireCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 50f;

        int width = 1280;
        int height = 960;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        // --- 0. 겨울 설경 상태의 정상 나무 캡처 (winter_snow_trees_rendered.png) ---
        camObj.transform.position = pos + new Vector3(3.0f, 6.0f, -14.0f);
        camObj.transform.LookAt(pos + Vector3.up * 4.5f);
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();
        byte[] snowTreeBytes = screenShot.EncodeToPNG();
        DestroyImmediate(screenShot);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(saveDir, "winter_snow_trees_rendered.png"), snowTreeBytes);
        System.IO.File.WriteAllBytes(@"c:\unityProject\MiniTotalWar2D\scratch\winter_snow_trees_rendered.png", snowTreeBytes);

        // --- 1. 화재 및 연기 만개 상태 캡처 (fire_and_smoke_rendered.png) ---
        camObj.transform.position = pos + new Vector3(6f, 12f, -20f);
        camObj.transform.LookAt(pos + Vector3.up * 7.0f);
        cam.Render();

        screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();
        byte[] bytes = screenShot.EncodeToPNG();
        DestroyImmediate(screenShot);

        string savePath = System.IO.Path.Combine(saveDir, "fire_and_smoke_rendered.png");
        System.IO.File.WriteAllBytes(savePath, bytes);
        System.IO.File.WriteAllBytes(@"c:\unityProject\MiniTotalWar2D\scratch\fire_and_smoke_rendered.png", bytes);

        // --- 2. 전소 완료된 숯 나무 상태 캡처 (charred_tree_rendered.png) ---
        // 화염 파티클 끄고 _BurnProgress = 1.0 주입
        ClearFireVFX();
        MaterialPropertyBlock charredMpb = new MaterialPropertyBlock();
        charredMpb.SetFloat(Shader.PropertyToID("_BurnProgress"), 1.0f);

        var targetRenderers = targetTree.GetComponentsInChildren<Renderer>(true);
        foreach (var r in targetRenderers)
        {
            if (r != null) r.SetPropertyBlock(charredMpb);
        }

        // 숯으로 탄화된 기둥과 가지 골격을 한눈에 보는 근접 각도
        camObj.transform.position = pos + new Vector3(3.0f, 6.0f, -14.0f);
        camObj.transform.LookAt(pos + Vector3.up * 4.5f);
        cam.Render();

        screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenShot.Apply();
        byte[] charredBytes = screenShot.EncodeToPNG();
        DestroyImmediate(screenShot);

        cam.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);
        DestroyImmediate(camObj);

        string charredSavePath = System.IO.Path.Combine(saveDir, "charred_tree_rendered.png");
        System.IO.File.WriteAllBytes(charredSavePath, charredBytes);
        System.IO.File.WriteAllBytes(@"c:\unityProject\MiniTotalWar2D\scratch\charred_tree_rendered.png", charredBytes);

        Debug.Log($"<color=#00FF88><b>[TacticalForestZone] ❄️ 겨울 눈 나무 및 숯 나무 캡처 완료!</b></color>");

        Debug.Log($"<color=#00FF88><b>[TacticalForestZone] 🔥 화재 및 숯 나무 캡처 완료: {charredSavePath}</b></color>");
    }
#endif

    private void OnValidate()
    {
        radius = Mathf.Max(5f, radius);
        boxSize.x = Mathf.Max(5f, boxSize.x);
        boxSize.y = Mathf.Max(5f, boxSize.y);
        treeCount = Mathf.Clamp(treeCount, 0, 150);
        bushCount = Mathf.Clamp(bushCount, 0, 200);

        ApplyThemePreset();

        if (currentSeason != lastSeason || enableWinterSnow != lastWinterSnow ||
            Mathf.Abs(winterSnowAmount - lastWinterSnowAmount) > 0.01f ||
            Mathf.Abs(leafSnowRatio - lastLeafSnowRatio) > 0.01f)
        {
            lastSeason = currentSeason;
            lastWinterSnow = enableWinterSnow;
            lastWinterSnowAmount = winterSnowAmount;
            lastLeafSnowRatio = leafSnowRatio;
            RefreshVegetationMaterials();
        }

        if (smokeVisualType != lastSmokeVisualType)
        {
            lastSmokeVisualType = smokeVisualType;
            if (smokeVisualType != SmokeVisualType.CustomColor)
            {
                ApplySmokeColorPreset(smokeVisualType);
            }
        }

        bool colorChanged = (smokeStartColor != lastSmokeStartColor || smokeEndColor != lastSmokeEndColor);
        if (colorChanged && smokeVisualType != SmokeVisualType.CustomColor)
        {
            smokeVisualType = SmokeVisualType.CustomColor;
            lastSmokeVisualType = SmokeVisualType.CustomColor;
        }

        if (Mathf.Abs(smokeDensity - lastSmokeDensity) > 0.01f ||
            Mathf.Abs(smokeScaleMultiplier - lastSmokeScaleMultiplier) > 0.01f ||
            colorChanged)
        {
            lastSmokeDensity = smokeDensity;
            lastSmokeScaleMultiplier = smokeScaleMultiplier;
            lastSmokeStartColor = smokeStartColor;
            lastSmokeEndColor = smokeEndColor;
            RefreshActiveSmokeVisuals();
        }
    }

    private float lastSmokeDensity = 0.75f;
    private float lastSmokeScaleMultiplier = 1.0f;
    private SmokeVisualType lastSmokeVisualType = SmokeVisualType.TotalWarWarmCharcoal;
    private Color lastSmokeStartColor = new Color(0.28f, 0.20f, 0.14f, 1.0f);
    private Color lastSmokeEndColor = new Color(0.18f, 0.14f, 0.10f, 1.0f);

    /// <summary>
    /// 삼국지 토탈워 공식 연기 색상 프리셋 즉시 적용
    /// </summary>
    public void ApplySmokeColorPreset(SmokeVisualType type)
    {
        smokeVisualType = type;
        switch (type)
        {
            case SmokeVisualType.TotalWarWarmCharcoal:
            case SmokeVisualType.SpeciesAdaptive:
                // [삼탈워 원본 훈연 회갈색] 삼탈워 영상 일치 볼륨 훈연 회갈색
                smokeStartColor = new Color(0.62f, 0.56f, 0.50f, 1.0f);
                smokeEndColor = new Color(0.75f, 0.70f, 0.65f, 1.0f);
                break;
            case SmokeVisualType.AshGray:
                // [토탈워 표준 잿빛] 삼탈워 텍스처 원본 산불 잿빛 훈연
                smokeStartColor = new Color(0.75f, 0.73f, 0.70f, 1.0f);
                smokeEndColor = new Color(0.88f, 0.86f, 0.84f, 1.0f);
                break;
            case SmokeVisualType.PitchBlack:
                // [삼탈워 짙은 흑갈색] 송진 급격 연소로 인한 짙은 검댕
                smokeStartColor = new Color(0.35f, 0.32f, 0.30f, 1.0f);
                smokeEndColor = new Color(0.48f, 0.44f, 0.42f, 1.0f);
                break;
            case SmokeVisualType.CustomColor:
                break;
        }

        lastSmokeStartColor = smokeStartColor;
        lastSmokeEndColor = smokeEndColor;
        lastSmokeVisualType = smokeVisualType;

        RefreshActiveSmokeVisuals();
    }

    /// <summary>
    /// 현재 숲에서 활성화된 모든 연기 파티클의 크기, 진하기(농도) 및 색상 실시간 즉시 갱신
    /// </summary>
    public void RefreshActiveSmokeVisuals()
    {
        var allPS = GetComponentsInChildren<ParticleSystem>(true);
        if (allPS == null || allPS.Length == 0) return;

        float densityMult = Mathf.Clamp(smokeDensity, 0.1f, 1.0f);
        float peakAlpha = Mathf.Clamp01(densityMult * 0.95f);
        Color cStart = smokeStartColor;
        Color cEnd = smokeEndColor;

        foreach (var ps in allPS)
        {
            if (ps != null && ps.gameObject.name.Contains("VolumetricSmoke"))
            {
                var col = ps.colorOverLifetime;
                if (col.enabled)
                {
                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(cStart, 0f), new GradientColorKey(cEnd, 1f) },
                        new GradientAlphaKey[] { 
                            new GradientAlphaKey(0f, 0f), 
                            new GradientAlphaKey(peakAlpha, 0.18f), 
                            new GradientAlphaKey(peakAlpha * 0.65f, 0.65f), 
                            new GradientAlphaKey(0f, 1f) 
                        }
                    );
                    col.color = grad;
                }

                var main = ps.main;
                main.startColor = new Color(cStart.r, cStart.g, cStart.b, peakAlpha);
                main.startSize = new ParticleSystem.MinMaxCurve(0.85f * smokeScaleMultiplier, 1.65f * smokeScaleMultiplier);

                var emission = ps.emission;
                emission.rateOverTime = 16f * Mathf.Clamp(smokeScaleMultiplier, 0.5f, 2.5f) * Mathf.Lerp(0.85f, 1.35f, densityMult);
            }
        }
    }

    /// <summary>
    /// 이전 버전과의 호환용 래퍼
    /// </summary>
    public void RefreshActiveSmokeDensity() => RefreshActiveSmokeVisuals();

    /// <summary>
    /// 지역 테마 프리셋 설정에 따른 체크박스 동기화
    /// </summary>
    private void ApplyThemePreset()
    {
        if (theme == ForestTheme.CustomSelection) return;

        // 초기화
        spawnHuangshanPine = false;
        spawnPine = false;
        spawnBamboo = false;
        spawnCastanopsis = false;
        spawnCamphor = false;
        spawnShrubTea = false;

        spawnFicus = false;
        spawnBananaPalm = false;
        spawnCoconutPalm = false;
        spawnJungleCastanopsis = false;
        spawnPalm = false;
        spawnShrubElephantGrass = false;
        spawnShrubSwordFern = false;

        spawnMagnolia = false;
        spawnPlum = false;
        spawnGinkgo = false;
        spawnMaple = false;
        spawnWillow = false;
        spawnScholar = false;
        spawnShrubMagnolia = false;
        spawnShrubRhododendron = false;

        spawnShrubLotus = false;
        spawnShrubRice = false;
        spawnShrubMillet = false;

        spawnShrubPine = false;
        spawnShrubFern = false;
        spawnShrubScholar = false;
        spawnShrubConvallaria = false;

        spawnPineDead = false;
        spawnBrokenPine = false;
        spawnBrokenScholar = false;
        spawnDeadCastanopsis = false;
        spawnJungleDead = false;
        spawnBambooStump = false;
        spawnStump = false;
        spawnBrokenBranches = false;

        switch (theme)
        {
            case ForestTheme.NanmanJungle: // 남만 밀림 (열대 상록 식생 집중)
                spawnFicus = true;
                spawnBananaPalm = true;
                spawnCoconutPalm = true;
                spawnJungleCastanopsis = true;
                spawnPalm = true;
                spawnShrubElephantGrass = true;
                spawnShrubSwordFern = true;
                spawnBamboo = true;
                spawnCastanopsis = true;
                spawnCamphor = true;
                spawnShrubFern = true;
                spawnJungleDead = true; // 열대 정글 고사목
                break;

            case ForestTheme.JiangnanRiverside: // 강남 수변림 (버들, 대나무, 단풍, 연꽃, 벼)
                spawnWillow = true;
                spawnBamboo = true;
                spawnMaple = true;
                spawnCamphor = true;
                spawnShrubLotus = true;
                spawnShrubRice = true;
                spawnShrubTea = true;
                spawnShrubFern = true;
                break;

            case ForestTheme.CentralPlains: // 중원 평야림 (은행, 회화, 매화, 목련, 기장)
                spawnGinkgo = true;
                spawnScholar = true;
                spawnMagnolia = true;
                spawnPlum = true;
                spawnShrubScholar = true;
                spawnShrubMagnolia = true;
                spawnShrubMillet = true;
                spawnShrubConvallaria = true;
                break;

            case ForestTheme.HuangshanPine: // 황산 소나무림
                spawnHuangshanPine = true;
                spawnPine = true;
                spawnShrubPine = true;
                spawnShrubRhododendron = true;
                spawnBrokenPine = true;
                spawnPineDead = true;
                break;

            case ForestTheme.BambooGrove: // 대나무 군락지
                spawnBamboo = true;
                spawnShrubFern = true;
                spawnShrubTea = true;
                spawnBambooStump = true; // 대나무 밑동/그루터기
                break;

            case ForestTheme.CastanopsisWood: // 활엽수림
                spawnCastanopsis = true;
                spawnCamphor = true;
                spawnScholar = true;
                spawnShrubFern = true;
                spawnShrubScholar = true;
                break;

            case ForestTheme.BattlefieldRuins: // 격전지 고목림 (전장 파괴 식생 풀세트)
                spawnPineDead = true;
                spawnBrokenPine = true;
                spawnBrokenScholar = true;
                spawnDeadCastanopsis = true;
                spawnJungleDead = true;
                spawnBambooStump = true;
                spawnStump = true;
                spawnBrokenBranches = true;
                spawnPine = true;
                spawnShrubPine = true;
                break;

            case ForestTheme.Mixed: // 혼합림 (전 수종 골고루)
                spawnHuangshanPine = true;
                spawnPine = true;
                spawnBamboo = true;
                spawnCastanopsis = true;
                spawnCamphor = true;
                spawnShrubTea = true;

                spawnFicus = true;
                spawnBananaPalm = true;
                spawnCoconutPalm = true;
                spawnJungleCastanopsis = true;
                spawnPalm = true;
                spawnShrubElephantGrass = true;
                spawnShrubSwordFern = true;

                spawnMagnolia = true;
                spawnPlum = true;
                spawnGinkgo = true;
                spawnMaple = true;
                spawnWillow = true;
                spawnScholar = true;
                spawnShrubMagnolia = true;
                spawnShrubRhododendron = true;

                spawnShrubLotus = true;
                spawnShrubRice = true;
                spawnShrubMillet = true;

                spawnShrubPine = true;
                spawnShrubFern = true;
                spawnShrubScholar = true;
                spawnShrubConvallaria = true;

                spawnPineDead = true;
                spawnBrokenPine = true;
                spawnBrokenScholar = true;
                spawnDeadCastanopsis = true;
                spawnJungleDead = true;
                spawnBambooStump = true;
                spawnStump = true;
                spawnBrokenBranches = true;
                break;
        }
    }

    [ContextMenu("🌸 봄 계절 적용 (Spring)")]
    public void SetSeasonSpring() => SetSeason(Season.Spring);

    [ContextMenu("☀️ 여름 계절 적용 (Summer)")]
    public void SetSeasonSummer() => SetSeason(Season.Summer);

    [ContextMenu("🍁 가을 계절 적용 (Autumn)")]
    public void SetSeasonAutumn() => SetSeason(Season.Autumn);

    [ContextMenu("❄️ 눈 내린 겨울 적용 (Snowy Winter)")]
    public void SetSeasonSnowyWinter()
    {
        enableWinterSnow = true;
        winterSnowAmount = 0.85f;
        SetSeason(Season.Winter);
        Debug.Log($"<color=#00FFFF><b>[TacticalForestZone] ❄️ {gameObject.name}에 눈 내린 겨울 설경(Snowy Winter)이 적용되었습니다!</b></color>");
    }

    [ContextMenu("🍂 눈 안 온 마른 겨울 적용 (Dry Winter)")]
    public void SetSeasonDryWinter()
    {
        enableWinterSnow = false;
        SetSeason(Season.Winter);
        Debug.Log($"<color=#FFD700><b>[TacticalForestZone] 🍂 {gameObject.name}에 눈이 오지 않은 삭막한 마른 겨울(Dry Winter)이 적용되었습니다!</b></color>");
    }

    public void SetSeason(Season s)
    {
        currentSeason = s;
        lastSeason = s;
        RefreshVegetationMaterials();
    }

    public static void SetGlobalSeason(Season s)
    {
        for (int i = 0; i < activeZones.Count; i++)
        {
            if (activeZones[i] != null)
            {
                activeZones[i].SetSeason(s);
            }
        }
        var lighting = FindFirstObjectByType<TotalWarEnvironmentLighting>();
        if (lighting != null)
        {
            lighting.ApplySeasonAtmosphere(s);
        }
    }

    public void SnapToTerrain()
    {
        Vector3 pos = transform.position;
        pos.y = TerrainHeightManager.SampleHeightFast(pos.x, pos.z);
        transform.position = pos;
    }

    /// <summary>
    /// 지형 법선 벡터(Terrain Normal) 정밀 샘플링
    /// </summary>
    public static Vector3 SampleTerrainNormal(float x, float z)
    {
        float step = 0.5f;
        float hL = TerrainHeightManager.SampleHeightFast(x - step, z);
        float hR = TerrainHeightManager.SampleHeightFast(x + step, z);
        float hD = TerrainHeightManager.SampleHeightFast(x, z - step);
        float hU = TerrainHeightManager.SampleHeightFast(x, z + step);
        return new Vector3(hL - hR, step * 2f, hD - hU).normalized;
    }

    [ContextMenu("초목 재생성 (Generate Vegetation)")]
    public void GenerateVegetation()
    {
        ClearVegetation();
        SnapToTerrain();

        InitTotalWarMaterials();

        GameObject rootGo = new GameObject("Vegetation_Container");
        rootGo.transform.SetParent(transform, false);
        vegetationRoot = rootGo.transform;

        Random.InitState(randomSeed);

        List<GameObject> treePrefabs = LoadTreeModels();
        List<GameObject> shrubPrefabs = LoadShrubModels();

        // 1. 나무 스폰
        if (treePrefabs != null && treePrefabs.Count > 0 && treeCount > 0)
        {
            int spawnedTrees = 0;
            int maxAttempts = treeCount * 3;
            int attempts = 0;

            while (spawnedTrees < treeCount && attempts < maxAttempts)
            {
                attempts++;
                Vector3 localPos = GetRandomPointInShape();
                Vector3 worldPos = transform.TransformPoint(localPos);

                Vector3 normal = SampleTerrainNormal(worldPos.x, worldPos.z);
                float slopeAngle = Vector3.Angle(Vector3.up, normal);

                // 절벽 경사각 제한 (너무 가파른 절벽은 통과)
                if (slopeAngle > maxSlopeAngle) continue;

                GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
                if (prefab == null) continue;

                float scale = treeScaleMultiplier * Random.Range(0.85f, 1.25f);

                // 경사지 뿌리 매립 깊이 계산 (경사가 가파를수록 더 깊이 파묻어 공중 부양 원천 방지)
                float sink = (baseGroundSinkDepth + Mathf.Lerp(0f, 0.55f, slopeAngle / Mathf.Max(1f, maxSlopeAngle))) * (scale / 0.28f);
                worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z) - sink;

                // 지형 경사면에 맞춘 자연스러운 기울임 (법선과 수직의 블렌딩)
                Vector3 blendedUp = Vector3.Slerp(Vector3.up, normal, slopeAlignmentWeight);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, blendedUp) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject treeObj = Instantiate(prefab, worldPos, rot, vegetationRoot);
                treeObj.transform.localScale = Vector3.one * scale;

                ApplyTreeMaterials(treeObj, prefab.name);
                spawnedTrees++;
            }
        }

        // 2. 관목/수풀 스폰
        if (shrubPrefabs != null && shrubPrefabs.Count > 0 && bushCount > 0)
        {
            int spawnedBushes = 0;
            int maxAttempts = bushCount * 3;
            int attempts = 0;

            while (spawnedBushes < bushCount && attempts < maxAttempts)
            {
                attempts++;
                Vector3 localPos = GetRandomPointInShape();
                Vector3 worldPos = transform.TransformPoint(localPos);

                Vector3 normal = SampleTerrainNormal(worldPos.x, worldPos.z);
                float slopeAngle = Vector3.Angle(Vector3.up, normal);
                if (slopeAngle > 55f) continue; // 관목은 경사지에도 더 잘 붙음

                GameObject prefab = shrubPrefabs[Random.Range(0, shrubPrefabs.Count)];
                if (prefab == null) continue;

                float scale = bushScaleMultiplier * Random.Range(0.80f, 1.30f);
                float sink = 0.15f * (scale / 0.32f);
                worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z) - sink;

                Vector3 blendedUp = Vector3.Slerp(Vector3.up, normal, slopeAlignmentWeight * 1.5f);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, blendedUp) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject bushObj = Instantiate(prefab, worldPos, rot, vegetationRoot);
                bushObj.transform.localScale = Vector3.one * scale;

                ApplyShrubMaterials(bushObj, prefab.name);
                spawnedBushes++;
            }
        }

        InitTreeCombustionNodes();

        if (enableFireVFX)
        {
            SpawnFireVFX();
        }
    }

    public void ClearVegetation()
    {
        ClearFireVFX();
        treeCombustionNodes.Clear();
        materialCache.Clear();

        if (vegetationRoot != null)
        {
            if (Application.isPlaying) Destroy(vegetationRoot.gameObject);
            else DestroyImmediate(vegetationRoot.gameObject);
            vegetationRoot = null;
        }

        var oldRoot = transform.Find("Vegetation_Container");
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
        }
    }

    [ContextMenu("❄️ 적설 상태 강제 동기화 (Force Refresh Snow)")]
    public void RefreshVegetationMaterials()
    {
        materialCache.Clear();

        if (vegetationRoot == null)
        {
            vegetationRoot = transform.Find("Vegetation_Container");
        }
        if (vegetationRoot == null) return;

        for (int i = 0; i < vegetationRoot.childCount; i++)
        {
            Transform treeTransform = vegetationRoot.GetChild(i);
            if (treeTransform == null) continue;
            string objName = treeTransform.gameObject.name;

            if (objName.Contains("Shrub") || objName.Contains("Fern") || objName.Contains("Stump") || objName.Contains("Rice") || objName.Contains("Millet") || objName.Contains("Lotus") || objName.Contains("BrokenBranch"))
            {
                ApplyShrubMaterials(treeTransform.gameObject, objName);
            }
            else
            {
                ApplyTreeMaterials(treeTransform.gameObject, objName);
            }
        }

        // 오직 겨울(Winter)이고 enableWinterSnow가 켜져 있을 때만 눈 연출 활성화 (타 계절은 강제 0.0)
        float effectiveSnow = (currentSeason == Season.Winter && enableWinterSnow) ? winterSnowAmount : 0f;
        var renderers = vegetationRoot.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in renderers)
        {
            if (mr == null) continue;
            foreach (var m in mr.sharedMaterials)
            {
                if (m != null)
                {
                    if (m.HasProperty("_SnowAmount"))
                    {
                        m.SetFloat("_SnowAmount", effectiveSnow);
                    }
                    if (m.HasProperty("_LeafSnowRatio"))
                    {
                        m.SetFloat("_LeafSnowRatio", leafSnowRatio);
                    }
                    if (m.HasProperty("_SnowThreshold"))
                    {
                        m.SetFloat("_SnowThreshold", -0.15f);
                    }
                    if (m.HasProperty("_IsBamboo"))
                    {
                        bool isBambooMat = m.name.ToLower().Contains("bamboo");
                        m.SetFloat("_IsBamboo", isBambooMat ? 1.0f : 0.0f);
                    }
                }
            }
        }
    }

    private Vector3 GetRandomPointInShape()
    {
        if (shape == ZoneShape.Circle)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.Range(0.04f, 1f)) * radius;
            return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }
        else
        {
            float hx = (boxSize.x * 0.5f) * Random.Range(-0.95f, 0.95f);
            float hz = (boxSize.y * 0.5f) * Random.Range(-0.95f, 0.95f);
            return new Vector3(hx, 0f, hz);
        }
    }

    public void InitTreeCombustionNodes()
    {
        treeCombustionNodes.Clear();
        if (vegetationRoot == null)
        {
            vegetationRoot = transform.Find("Vegetation_Container");
        }
        if (vegetationRoot == null) return;

        for (int i = 0; i < vegetationRoot.childCount; i++)
        {
            Transform child = vegetationRoot.GetChild(i);
            string nameLower = child.gameObject.name.ToLower();

            // 관목, 벼, 기장, 잔디 등은 소형 초본으로 제외하고 목본/수목만 노드로 등록
            if (nameLower.Contains("shrub_") || nameLower.Contains("stump") || nameLower.Contains("brokenbranch")) continue;

            var (leavesCenter, leavesRadius, leavesHeight) = CalculateTreeLeavesCenterAndRadius(child.gameObject);
            float vol = Mathf.Clamp((leavesRadius / 2.5f) * (leavesRadius / 2.5f) * (leavesHeight / 5.0f), 0.35f, 3.2f);
            float randVal = Mathf.Abs((child.position.x * 7.13f + child.position.z * 13.37f) % 1.0f);
            float moisture = Mathf.Lerp(0.85f, 1.25f, randVal);

            float typeFlameMult = 1.0f;
            float typeSmokeMult = 1.0f;
            float typeDurationMult = 1.0f;

            if (nameLower.Contains("bamboo"))
            {
                typeFlameMult = 0.65f;
                typeSmokeMult = 0.55f;
                typeDurationMult = 0.65f;
            }
            else if (nameLower.Contains("pine"))
            {
                typeFlameMult = 1.15f;
                typeSmokeMult = 0.95f;
                typeDurationMult = 0.85f;
            }
            else if (nameLower.Contains("camphor") || nameLower.Contains("ficus"))
            {
                typeFlameMult = 1.25f;
                typeSmokeMult = 1.40f;
                typeDurationMult = 1.20f;
            }
            else if (nameLower.Contains("palm") || nameLower.Contains("banana") || nameLower.Contains("coconut"))
            {
                typeFlameMult = 1.15f;
                typeSmokeMult = 0.75f;
                typeDurationMult = 0.75f;
            }

            float duration = 34.0f * Mathf.Pow(vol, 0.40f) * typeDurationMult * Mathf.Lerp(0.92f, 1.10f, randVal);
            duration = Mathf.Clamp(duration, 20.0f, 50.0f);

            TreeCombustionNode node = new TreeCombustionNode
            {
                transform = child,
                treeObject = child.gameObject,
                renderers = child.GetComponentsInChildren<Renderer>(true),
                state = TreeCombustionState.Healthy,
                fireGauge = 0f,
                burnProgress = 0f,
                burnTimer = 0f,
                burnDuration = duration,
                typeFlameMult = typeFlameMult,
                typeSmokeMult = typeSmokeMult
            };

            treeCombustionNodes.Add(node);
        }
    }

    private void UpdateWildfireSimulation()
    {
        if (!enableFireVFX || treeCombustionNodes.Count == 0) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        TotalWarWindManager windMgr = TotalWarWindManager.Instance;
        Vector3 windDir = (windMgr != null) ? windMgr.GetWindDirectionVector() : Vector3.forward;
        float windSpeed = (windMgr != null) ? windMgr.windSpeed : 4.0f;

        int burningCount = 0;
        int charredCount = 0;
        int totalNodes = treeCombustionNodes.Count;

        for (int i = 0; i < totalNodes; i++)
        {
            var node = treeCombustionNodes[i];
            if (node == null || node.treeObject == null) continue;

            if (node.state == TreeCombustionState.Burning)
            {
                burningCount++;
                node.burnTimer += dt;
                node.burnProgress = Mathf.Clamp01(node.burnTimer / node.burnDuration);

                if (node.fireController != null)
                {
                    node.fireController.burnProgress = node.burnProgress;
                }

                if (enableWildfirePropagation)
                {
                    SpreadFireToNeighbors(node, windDir, windSpeed, dt);
                }

                if (node.burnProgress >= 1.0f)
                {
                    node.state = TreeCombustionState.Charred;
                }
            }
            else if (node.state == TreeCombustionState.Charred)
            {
                charredCount++;
            }
            else if (node.state == TreeCombustionState.Smoking)
            {
                if (enableWildfirePropagation)
                {
                    SpreadFireToNeighbors(node, windDir, windSpeed, dt);
                }
                if (node.fireGauge >= 1.0f)
                {
                    IgniteTreeNode(node);
                }
            }
        }

        float destroyedRatio = (float)(burningCount + charredCount) / Mathf.Max(1, totalNodes);
        if (destroyedRatio >= 0.40f && !isForestRevealedByFire)
        {
            isForestRevealedByFire = true;
            Debug.Log($"<color=#FF3300><b>[TacticalForestZone] 🔥 {gameObject.name}의 40% 이상이 전소되어 은폐(스텔스) 및 사선 차폐가 영구 해제되었습니다!</b></color>");
        }
    }

    private void SpreadFireToNeighbors(TreeCombustionNode source, Vector3 windDir, float windSpeed, float dt)
    {
        Vector3 srcPos = source.transform.position;

        for (int i = 0; i < treeCombustionNodes.Count; i++)
        {
            var neighbor = treeCombustionNodes[i];
            if (neighbor == null || neighbor == source || neighbor.state == TreeCombustionState.Burning || neighbor.state == TreeCombustionState.Charred) continue;

            Vector3 toNeighbor = neighbor.transform.position - srcPos;
            toNeighbor.y = 0f;
            float dist = toNeighbor.magnitude;

            float dot = Vector3.Dot(toNeighbor.normalized, windDir.normalized);
            float effectiveRadius = basePropagationRadius + windSpeed * Mathf.Max(0f, dot) * windSpreadMultiplier;

            if (dist <= effectiveRadius)
            {
                float spreadRate = 0.22f * (1.0f + Mathf.Max(0f, dot * 1.4f)) * (1.0f - dist / effectiveRadius);
                neighbor.fireGauge += spreadRate * dt;

                if (neighbor.fireGauge >= 0.20f && neighbor.state == TreeCombustionState.Healthy)
                {
                    neighbor.state = TreeCombustionState.Smoking;
                }

                if (neighbor.fireGauge >= 1.0f)
                {
                    IgniteTreeNode(neighbor);
                }
            }
        }
    }

    public void IgniteTreeNode(TreeCombustionNode node)
    {
        if (node == null || node.treeObject == null) return;
        if (node.state == TreeCombustionState.Burning || node.state == TreeCombustionState.Charred) return;

        node.state = TreeCombustionState.Burning;
        node.fireGauge = 1.0f;

        if (fireVfxRoot == null)
        {
            GameObject fRoot = new GameObject("Fire_VFX_Container");
            fRoot.transform.SetParent(transform, false);
            fireVfxRoot = fRoot.transform;
        }

        var (leavesCenter, leavesRadius, leavesHeight) = CalculateTreeLeavesCenterAndRadius(node.treeObject);
        float vol = Mathf.Clamp((leavesRadius / 2.5f) * (leavesRadius / 2.5f) * (leavesHeight / 5.0f), 0.35f, 3.2f);
        float randVal = Mathf.Abs((node.transform.position.x * 7.13f + node.transform.position.z * 13.37f) % 1.0f);
        float moisture = Mathf.Lerp(0.85f, 1.25f, randVal);

        float flameScale = Mathf.Sqrt(vol) * (2.05f - moisture) * flameScaleMultiplier * node.typeFlameMult;
        float smokeScale = vol * moisture * node.typeSmokeMult * smokeScaleMultiplier;

        GameObject fireInstance = CreateTreeFireInstance(
            leavesCenter,
            leavesRadius,
            leavesHeight,
            fireVfxRoot,
            node.treeObject,
            flameScale,
            smokeScale,
            node.burnDuration
        );

        node.fireController = fireInstance.GetComponent<TotalWarFireVFXController>();
    }

    [ContextMenu("🔥 숲 화재 점화 연출 (Ignite Forest Fire)")]
    public void IgniteForestFire()
    {
        enableFireVFX = true;
        SpawnFireVFX();
    }

    [ContextMenu("🧯 화재 진압 및 초기화 (Extinguish Fire)")]
    public void ExtinguishFire()
    {
        enableFireVFX = false;
        isForestRevealedByFire = false;
        ClearFireVFX();

        MaterialPropertyBlock resetMpb = new MaterialPropertyBlock();
        resetMpb.SetFloat(Shader.PropertyToID("_BurnProgress"), 0f);

        for (int i = 0; i < treeCombustionNodes.Count; i++)
        {
            var node = treeCombustionNodes[i];
            if (node == null) continue;
            node.state = TreeCombustionState.Healthy;
            node.fireGauge = 0f;
            node.burnProgress = 0f;
            node.burnTimer = 0f;

            if (node.fireController != null)
            {
                node.fireController.ResetFireState();
            }

            // 모든 렌더러의 MaterialPropertyBlock _BurnProgress를 0.0으로 즉시 초기화하여 녹색 나뭇잎 복원
            if (node.renderers != null)
            {
                for (int r = 0; r < node.renderers.Length; r++)
                {
                    if (node.renderers[r] != null)
                    {
                        node.renderers[r].SetPropertyBlock(resetMpb);
                    }
                }
            }
        }

        Debug.Log($"<color=#00FF88><b>[TacticalForestZone] 🧯 {gameObject.name}의 모든 화재가 완전히 진압되었으며, 나뭇잎과 은폐(스텔스)가 정상 복구되었습니다!</b></color>");
    }


    public void ClearFireVFX()
    {
        if (fireVfxRoot != null)
        {
            if (Application.isPlaying) Destroy(fireVfxRoot.gameObject);
            else DestroyImmediate(fireVfxRoot.gameObject);
            fireVfxRoot = null;
        }

        var oldRoot = transform.Find("Fire_VFX_Container");
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
        }

        // 나뭇잎(Leaves) 자식 등에 붙어있는 모든 잔여 화재 파티클/오브젝트 전수 검색 및 완전 파괴
        string[] fireNames = new string[] {
            "CompositeFlames_TotalWar", "VolumetricSmoke_TotalWar", "Rising_Embers_TotalWar",
            "Fire_PointLight", "TotalWar_TreeFire", "Flames_Composite_TotalWar", "Smoke_Plume", "Embers_Sparks"
        };

        var allTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = allTransforms.Length - 1; i >= 0; i--)
        {
            var t = allTransforms[i];
            if (t == null) continue;
            for (int k = 0; k < fireNames.Length; k++)
            {
                if (t.gameObject.name.Contains(fireNames[k]))
                {
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                    break;
                }
            }
        }
    }

    public void SpawnFireVFX()
    {
        ClearFireVFX();

        if (treeCombustionNodes.Count == 0)
        {
            InitTreeCombustionNodes();
        }

        if (treeCombustionNodes.Count == 0) return;

        int spawnCount = Mathf.Min(burningTreesCount, treeCombustionNodes.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            IgniteTreeNode(treeCombustionNodes[i]);
        }
    }

    public static (Vector3 leavesCenter, float leavesRadius, float leavesHeight) CalculateTreeLeavesCenterAndRadius(GameObject treeObj)
    {
        if (treeObj == null)
            return (Vector3.zero, 2.5f, 5.0f);

        Transform treeTransform = treeObj.transform;

        // 1. [핵심] 자식 렌더러 중 "Leaves" 또는 "Branch" 이름이 포함된 순수 나뭇잎 전용 렌더러 최우선 탐색
        Renderer[] allRenderers = treeObj.GetComponentsInChildren<Renderer>(true);
        Renderer leavesRen = null;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            string n = allRenderers[i].gameObject.name.ToLower();
            if (n.Contains("leaves") || n.Contains("branch"))
            {
                leavesRen = allRenderers[i];
                break;
            }
        }

        if (leavesRen != null)
        {
            Bounds b = leavesRen.bounds;
            Vector3 worldCenter = b.center; // 땅바닥이 아닌 순수 나뭇잎 수관 정중앙 (Y = 6~12m 상공)
            float radius = Mathf.Max(b.extents.x, b.extents.z);
            float height = b.size.y;
            return (worldCenter, Mathf.Max(2.0f, radius), Mathf.Max(3.0f, height));
        }

        // 2. 단일 메쉬인 경우 Submesh 1(Leaves) 바운즈 탐색
        MeshFilter mf = treeObj.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.subMeshCount >= 2)
        {
            var submeshLeaves = mf.sharedMesh.GetSubMesh(1);
            if (submeshLeaves.bounds.size.sqrMagnitude > 0.01f)
            {
                Bounds localBounds = submeshLeaves.bounds;
                Transform meshTrans = mf.transform;
                Vector3 worldCenter = meshTrans.TransformPoint(localBounds.center);
                Vector3 worldExtents = Vector3.Scale(localBounds.extents, meshTrans.lossyScale);
                float radius = Mathf.Max(Mathf.Abs(worldExtents.x), Mathf.Abs(worldExtents.z));
                float height = Mathf.Abs(worldExtents.y) * 2f;
                return (worldCenter, Mathf.Max(2.0f, radius), Mathf.Max(3.0f, height));
            }
        }

        // 3. Fallback: 전체 렌더러 결합 바운즈의 상단 65% 지점 (절대 지면 밑동으로 가지 않음)
        if (allRenderers.Length > 0)
        {
            Bounds combined = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                combined.Encapsulate(allRenderers[i].bounds);
            }
            float centerY = combined.min.y + combined.size.y * 0.68f;
            Vector3 worldCenter = new Vector3(combined.center.x, centerY, combined.center.z);
            float radius = Mathf.Max(combined.extents.x, combined.extents.z) * 0.90f;
            float height = combined.size.y * 0.55f;
            return (worldCenter, Mathf.Max(2.0f, radius), Mathf.Max(3.0f, height));
        }

        return (treeTransform.position + Vector3.up * 8.0f, 2.5f, 5.0f);
    }

    private GameObject CreateTreeFireInstance(
        Vector3 leavesPos, 
        float leavesRadius, 
        float leavesHeight, 
        Transform parent, 
        GameObject targetTree = null,
        float flameScale = 1.0f,
        float smokeScale = 1.0f,
        float burnDuration = 40.0f)
    {
        GameObject fireGo = new GameObject("TotalWar_TreeFire");
        fireGo.transform.SetParent(parent, false);
        fireGo.transform.position = leavesPos;
        fireGo.transform.rotation = Quaternion.identity;

        Texture2D flameTex = null;
        Texture2D smokeTex = null;
        Texture2D emberTex = null;
#if UNITY_EDITOR
        string vfxTexPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/";
        flameTex = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_fire_a_diffuse.png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_flame_particle.png");
        smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_smoke_a_diffuse.png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_smoke_particle.png");
        emberTex = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_ember_particle.png");
#endif
        if (flameTex == null) flameTex = Resources.Load<Texture2D>("VFX/Textures/3k_anim_fire_a_diffuse");
        if (smokeTex == null) smokeTex = Resources.Load<Texture2D>("VFX/Textures/3k_anim_smoke_a_diffuse");
        Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");

        Material finalFlamesMat = CreateParticleMaterial(matCompositeFlames, flameTex, particleUnlit, new Color(2.0f, 1.35f, 0.45f, 1.0f), true);
        Material finalSmokeMat = CreateParticleMaterial(matWispySmoke, smokeTex, particleUnlit, new Color(0.85f, 0.82f, 0.78f, 1.0f), false);
        Material finalEmberMat = CreateParticleMaterial(matTotalWarEmbers, emberTex, particleUnlit, new Color(3.5f, 1.8f, 0.5f, 1f), true);

        // 1. [삼국지 토탈워 본편 공식 메커니즘] 실제 나뭇잎 서브메시(Leaves) 표면 삼각형(Triangle) 직접 방출
        MeshRenderer leavesRenderer = null;
        MeshFilter leavesFilter = null;
        if (targetTree != null)
        {
            var renderers = targetTree.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string rName = r.gameObject.name.ToLower();
                if (rName.Contains("leave") || rName.Contains("leaf") || rName.Contains("branch"))
                {
                    leavesRenderer = r;
                    leavesFilter = r.GetComponent<MeshFilter>();
                    break;
                }
            }
            if (leavesRenderer == null && renderers.Length > 0)
            {
                leavesRenderer = renderers[0];
                leavesFilter = leavesRenderer.GetComponent<MeshFilter>();
            }
        }

        Transform meshTrans = (leavesRenderer != null) ? leavesRenderer.transform : fireGo.transform;

        // [핵심] 허공 구체가 아닌 나뭇잎 트랜스폼에 1:1로 밀착하여 메쉬 표면에서 직접 불꽃 방출
        GameObject flamesGo = new GameObject("CompositeFlames_TotalWar");
        flamesGo.transform.SetParent(meshTrans, false);
        flamesGo.transform.localPosition = Vector3.zero;
        flamesGo.transform.localRotation = Quaternion.identity;
        flamesGo.transform.localScale = Vector3.one;
        ParticleSystem flamesPS = flamesGo.AddComponent<ParticleSystem>();
        SetupCompositeFlamesPS(flamesPS, leavesRadius, leavesHeight, finalFlamesMat, flameScale, leavesRenderer, leavesFilter);

        // 2. Volumetric Smoke (나뭇잎 상단에서 하늘로 확산 솟구치는 토탈워 정품 훈연 연기)
        Color actualStartCol = smokeStartColor;
        Color actualEndCol = smokeEndColor;
        if (smokeVisualType == SmokeVisualType.SpeciesAdaptive && targetTree != null)
        {
            string treeName = targetTree.name.ToLower();
            if (treeName.Contains("pine") || treeName.Contains("camphor"))
            {
                actualStartCol = new Color(0.28f, 0.20f, 0.14f, 1.0f);
                actualEndCol = new Color(0.18f, 0.14f, 0.10f, 1.0f);
            }
            else
            {
                actualStartCol = new Color(0.48f, 0.44f, 0.40f, 1.0f);
                actualEndCol = new Color(0.58f, 0.54f, 0.50f, 1.0f);
            }
        }

        // [핵심 해결] 불이 나는 곳에서 연기가 함께 나오도록 나뭇잎 트랜스폼(meshTrans)에 1:1로 밀착
        GameObject smokeGo = new GameObject("VolumetricSmoke_TotalWar");
        smokeGo.transform.SetParent(meshTrans, false);
        smokeGo.transform.localPosition = Vector3.zero;
        smokeGo.transform.localRotation = Quaternion.identity;
        smokeGo.transform.localScale = Vector3.one;
        ParticleSystem smokePS = smokeGo.AddComponent<ParticleSystem>();
        SetupVolumetricSmokePS(smokePS, leavesRadius, leavesHeight, finalSmokeMat, smokeScale, actualStartCol, actualEndCol, leavesRenderer, leavesFilter, smokeDensity);

        // 3. Rising Embers (나뭇잎 주변 비산 불티)
        GameObject embersGo = new GameObject("Rising_Embers_TotalWar");
        embersGo.transform.position = leavesPos;
        embersGo.transform.SetParent(fireGo.transform, true);
        ParticleSystem embersPS = embersGo.AddComponent<ParticleSystem>();
        SetupEmbersPS(embersPS, finalEmberMat, leavesRadius, flameScale);

        // 4. Fire Point Light (나뭇잎과 지면을 따스하게 비추는 2200K 화재 광원)
        GameObject lightGo = new GameObject("Fire_PointLight");
        lightGo.transform.position = leavesPos;
        lightGo.transform.SetParent(fireGo.transform, true);
        Light fireLight = lightGo.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1.0f, 0.55f, 0.15f);
        fireLight.intensity = 3.2f * flameScale;
        fireLight.range = Mathf.Max(14.0f, leavesRadius * 3.5f);
        fireLight.shadows = LightShadows.None;

        TotalWarFireVFXController controller = fireGo.AddComponent<TotalWarFireVFXController>();
        controller.SetTargetTree(targetTree);
        controller.burnDuration = burnDuration;
        controller.RegisterFlameParticle(flamesPS);
        controller.RegisterSmokeParticle(smokePS);
        controller.RegisterEmbersParticle(embersPS);

        // 즉시 재생 시작
        flamesPS.Play();
        smokePS.Play();
        embersPS.Play();

        return fireGo;
    }

    /// <summary>
    /// URP Particle Unlit 투명 파이프라인 속성을 강제 설정하고 고해상도 텍스처를 보장하는 머티리얼 빌더
    /// </summary>
    private static Material CreateParticleMaterial(Material presetMat, Texture2D texture, Shader fallbackShader, Color color, bool isAdditive)
    {
        Material mat = null;
        if (presetMat != null) mat = new Material(presetMat);
        else if (fallbackShader != null) mat = new Material(fallbackShader);

        if (mat == null) return null;

        if (texture != null)
        {
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        mat.SetFloat("_Surface", 1.0f); // 투명 표면 (Transparent)
        mat.SetFloat("_Blend", isAdditive ? 1.0f : 0.0f); // 1 = 가산 혼합(Additive), 0 = 알파 블렌드(Alpha Blend)
        mat.SetInt("_SrcBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        mat.SetInt("_DstBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + (isAdditive ? 100 : 50);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (isAdditive)
        {
            mat.EnableKeyword("_SOFTPARTICLES_ON");
            mat.SetFloat("_SoftParticlesEnabled", 1.0f);
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.DisableKeyword("_BLENDMODE_ALPHA");
            mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
            mat.SetFloat("_FlipbookBlending", 1.0f);
            mat.SetFloat("_FlipbookMode", 1.0f);
        }
        else
        {
            // [연기 Smoke 핵심: 소프트 파티클 완전 비활성화 - 나뭇잎 교차 뎁스 페이딩 투명화 100% 방지]
            mat.DisableKeyword("_SOFTPARTICLES_ON");
            mat.SetFloat("_SoftParticlesEnabled", 0.0f);
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.DisableKeyword("_BLENDMODE_ADD");
            mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
            mat.SetFloat("_FlipbookBlending", 1.0f);
            mat.SetFloat("_FlipbookMode", 1.0f);
        }

        return mat;
    }

    private static void SetupCompositeFlamesPS(ParticleSystem ps, float radius, float height, Material mat, float scale, MeshRenderer leavesRenderer, MeshFilter leavesFilter)
    {
        // [오류 완전 차단] 수정 전 StopEmittingAndClear로 시스템 정지하여 duration 변경 오류 원천 방지
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 초당 24프레임 (24 FPS): 64프레임 / 24fps = 정확히 2.67초 주기
        main.startLifetime = 2.67f;
        // [삼탈워 REA 명세] 나뭇잎 표면 제자리 고정 (속도 0으로 잎사귀에 달라붙음)
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f * scale, 0.72f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        main.flipRotation = 0.5f;
        main.gravityModifier = 0f;
        main.startColor = Color.white;
        main.maxParticles = 140;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 36f * scale;

        // [Git 검증 1:1 복원] 삼탈워 8x8 (64프레임) 플립북 24 FPS 실시간 춤추는 애니메이션
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true;
        tsa.numTilesX = 8;
        tsa.numTilesY = 8;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.timeMode = ParticleSystemAnimationTimeMode.FPS;
        tsa.fps = 24.0f; // 초당 24 FPS 끊김 없는 재생
        AnimationCurve frameCurve = new AnimationCurve();
        frameCurve.AddKey(0f, 0f);
        frameCurve.AddKey(1f, 1f); // 0.0f ~ 1.0f 정규화 커브
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.15f); // 파티클별 위상 분산
        tsa.cycleCount = 1;

        var noise = ps.noise;
        noise.enabled = false;
        var sol = ps.sizeOverLifetime;
        sol.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(1.0f, 0.08f), 
                new GradientAlphaKey(1.0f, 0.85f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        col.color = grad;

        // [핵심: 나뭇잎 메쉬 표면 삼각형(Triangle) 직접 방출 - 허공 화재 100% 박멸]
        var shape = ps.shape;
        shape.enabled = true;
        shape.normalOffset = 0.18f;
        bool shapeBound = false;

        if (leavesRenderer != null)
        {
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = leavesRenderer;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;

            if (leavesFilter != null && leavesFilter.sharedMesh != null && leavesFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1; // Submesh 1 = 나뭇잎(Leaves)
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }
        else if (leavesFilter != null && leavesFilter.sharedMesh != null)
        {
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = leavesFilter.sharedMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;

            if (leavesFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1;
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }

        if (!shapeBound)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(1.6f, radius * 0.85f);
            shape.position = Vector3.zero;
        }

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;
        psr.sortMode = ParticleSystemSortMode.Distance;
        psr.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.AnimBlend
        });
    }

    private static void SetupVolumetricSmokePS(
        ParticleSystem ps, 
        float radius, 
        float height, 
        Material mat, 
        float scale, 
        Color startCol,
        Color endCol,
        MeshRenderer leavesRenderer = null, 
        MeshFilter leavesFilter = null,
        float density = 0.75f)
    {
        // [오류 완전 차단] 수정 전 StopEmittingAndClear로 시스템 정지하여 duration 변경 오류 원천 방지
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // [삼탈워 영상 일치화] 대기 중에 오래 머물러 거대 구름이 되지 않도록 2.2초~3.2초 후 자연 소멸
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);

        // [삼탈워 영상 일치화] 나뭇가지/화염 크기 및 사용자 설정 scale 정밀 비례
        main.startSize = new ParticleSystem.MinMaxCurve(0.85f * scale, 1.65f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.07f; // 상공으로 스멀스멀 피어오르는 부드러운 부력

        // [연기 농도 / 진하기 배율 적용: 0.1(매우 연함) ~ 0.75(기본) ~ 1.0(아주 진함)]
        float densityMult = Mathf.Clamp(density, 0.1f, 1.0f);
        float peakAlpha = Mathf.Clamp01(densityMult * 0.95f);

        main.startColor = new Color(startCol.r, startCol.g, startCol.b, peakAlpha);
        main.maxParticles = (int)(60 * Mathf.Lerp(0.8f, 1.4f, densityMult));

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 16f * Mathf.Clamp(scale, 0.5f, 2.5f) * Mathf.Lerp(0.85f, 1.35f, densityMult);

        // [삼국지 토탈워 본편 공식 8x8 플립북 연기 실시간 애니메이션]
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true;
        tsa.numTilesX = 8;
        tsa.numTilesY = 8;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        AnimationCurve frameCurve = new AnimationCurve();
        frameCurve.AddKey(0f, 0f);
        frameCurve.AddKey(1f, 1f);
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.25f);
        tsa.cycleCount = 1;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);

        // [삼탈워 공식] 볼륨 훈연 일렁임 노이즈 모듈
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.60f;
        noise.damping = true;

        // [삼탈워 영상 일치화] 과도한 팽창 억제 (1.45배만 자연스럽게 확산)
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.85f);
        sizeCurve.AddKey(0.40f, 1.15f);
        sizeCurve.AddKey(1f, 1.45f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // [삼탈워 영상 일치화: 은은하게 투과되는 반투명 훈연 그라디언트]
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(startCol, 0f), 
                new GradientColorKey(endCol, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(peakAlpha, 0.15f), // 15% 시점에서 은은한 반투명 피크!
                new GradientAlphaKey(peakAlpha * 0.60f, 0.65f), // 65% 시점에서 서서히 흩어짐
                new GradientAlphaKey(0f, 1f)        // 소멸 시 자연스러운 페이드아웃
            }
        );
        col.color = grad;

        // [핵심 해결: 불이 나는 곳에서 연기가 함께 발생하도록 나뭇잎 메쉬 표면 삼각형(Triangle) 직접 방출]
        var shape = ps.shape;
        shape.enabled = true;
        shape.normalOffset = 0.22f; // 나뭇잎 표면에서 위로 살짝 방출
        bool shapeBound = false;

        if (leavesRenderer != null)
        {
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = leavesRenderer;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;

            if (leavesFilter != null && leavesFilter.sharedMesh != null && leavesFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1; // Submesh 1 = 나뭇잎(Leaves)
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }
        else if (leavesFilter != null && leavesFilter.sharedMesh != null)
        {
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = leavesFilter.sharedMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;

            if (leavesFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1;
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }

        if (!shapeBound)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(1.4f, radius * 0.75f);
            shape.position = Vector3.zero;
        }

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;
        psr.sortMode = ParticleSystemSortMode.Distance;
        psr.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.AnimBlend
        });
    }

    private static void SetupEmbersPS(ParticleSystem ps, Material mat, float radius, float scale)
    {
        // [오류 완전 차단] 수정 전 StopEmittingAndClear로 시스템 정지하여 duration 변경 오류 원천 방지
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.16f * scale);
        main.startColor = new Color(1.0f, 0.65f, 0.2f, 0.9f);
        main.gravityModifier = -0.15f; // 하늘로 솟구치는 불티
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f * scale;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(1.6f, radius * 0.9f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;
    }

    private List<GameObject> LoadTreeModels()
    {
        List<GameObject> list = new List<GameObject>();
        string basePath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/";

        // 1. 🌲 온대/한대 상록수
        if (spawnHuangshanPine)
        {
            AddTreeModelIfValid(list, basePath + "Huangshan_Pine_A.obj");
            AddTreeModelIfValid(list, basePath + "Huangshan_Pine_B.obj");
        }
        if (spawnPine)
        {
            AddTreeModelIfValid(list, basePath + "Pine_A.obj");
        }
        if (spawnBamboo)
        {
            AddTreeModelIfValid(list, basePath + "Bamboo_A.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_C.obj");
        }
        if (spawnCastanopsis)
        {
            AddTreeModelIfValid(list, basePath + "Castanopsis_A.obj");
        }
        if (spawnCamphor)
        {
            AddTreeModelIfValid(list, basePath + "Camphor_A.obj");
        }

        // 2. 🌴 열대/남만 상록 식생
        if (spawnFicus)
        {
            AddTreeModelIfValid(list, basePath + "Ficus_C.obj");
            AddTreeModelIfValid(list, basePath + "Ficus_D.obj");
        }
        if (spawnBananaPalm)
        {
            AddTreeModelIfValid(list, basePath + "Palm_Banana_A.obj");
            AddTreeModelIfValid(list, basePath + "Palm_Banana_B.obj");
        }
        if (spawnCoconutPalm)
        {
            AddTreeModelIfValid(list, basePath + "Palm_Coconut_A.obj");
            AddTreeModelIfValid(list, basePath + "Palm_Coconut_C.obj");
        }
        if (spawnJungleCastanopsis)
        {
            AddTreeModelIfValid(list, basePath + "Castanopsis_Jungle_A.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Jungle_B.obj");
        }
        if (spawnPalm)
        {
            AddTreeModelIfValid(list, basePath + "Palm_Windmill_A.obj");
        }

        // 3. 🍂 낙엽수 & 꽃나무
        if (spawnMagnolia)
        {
            AddTreeModelIfValid(list, basePath + "Magnolia_A.obj");
            AddTreeModelIfValid(list, basePath + "Magnolia_B.obj");
        }
        if (spawnPlum)
        {
            AddTreeModelIfValid(list, basePath + "Plum_A.obj");
        }
        if (spawnGinkgo)
        {
            AddTreeModelIfValid(list, basePath + "Ginkgo_A.obj");
            AddTreeModelIfValid(list, basePath + "Ginkgo_B.obj");
        }
        if (spawnMaple)
        {
            AddTreeModelIfValid(list, basePath + "Maple_A.obj");
            AddTreeModelIfValid(list, basePath + "Maple_B.obj");
        }
        if (spawnWillow)
        {
            AddTreeModelIfValid(list, basePath + "Willow_A.obj");
            AddTreeModelIfValid(list, basePath + "Willow_B.obj");
        }
        if (spawnScholar)
        {
            AddTreeModelIfValid(list, basePath + "Scholar_A.obj");
            AddTreeModelIfValid(list, basePath + "Scholar_B.obj");
        }

        // 6. 🪵 전장 고목 & 고사목
        if (spawnPineDead)
        {
            AddTreeModelIfValid(list, basePath + "Pine_Dead_M.obj");
            AddTreeModelIfValid(list, basePath + "Pine_Dead_O.obj");
            AddTreeModelIfValid(list, basePath + "Pine_Dead_S.obj");
        }
        if (spawnBrokenPine)
        {
            AddTreeModelIfValid(list, basePath + "Pine_Broken_H.obj");
            AddTreeModelIfValid(list, basePath + "Pine_Broken_K.obj");
            AddTreeModelIfValid(list, basePath + "Pine_Broken_L.obj");
            AddTreeModelIfValid(list, basePath + "Pine_Broken_N.obj");
        }
        if (spawnBrokenScholar)
        {
            AddTreeModelIfValid(list, basePath + "Scholar_Broken_A.obj");
            AddTreeModelIfValid(list, basePath + "Scholar_Broken_B.obj");
            AddTreeModelIfValid(list, basePath + "Scholar_Broken_C.obj");
            AddTreeModelIfValid(list, basePath + "Scholar_Broken_D.obj");
        }
        if (spawnDeadCastanopsis)
        {
            AddTreeModelIfValid(list, basePath + "Castanopsis_Dead_A.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Dead_B.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Dead_C.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Dead_D.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Dead_E.obj");
        }
        if (spawnJungleDead)
        {
            AddTreeModelIfValid(list, basePath + "Castanopsis_Jungle_Dead_A.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Jungle_Dead_B.obj");
            AddTreeModelIfValid(list, basePath + "Castanopsis_Jungle_Dead_C.obj");
        }

        return list;
    }

    private List<GameObject> LoadShrubModels()
    {
        List<GameObject> list = new List<GameObject>();
        string basePath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/";

        // 1. 온대 관목
        if (spawnShrubTea) AddTreeModelIfValid(list, basePath + "Shrub_Tea_A.obj");

        // 2. 열대 관목
        if (spawnShrubElephantGrass) AddTreeModelIfValid(list, basePath + "Shrub_ElephantGrass_A.obj");
        if (spawnShrubSwordFern) AddTreeModelIfValid(list, basePath + "Shrub_Fern_Sword_A.obj");

        // 3. 꽃덤불 및 관목
        if (spawnShrubMagnolia) AddTreeModelIfValid(list, basePath + "Shrub_Magnolia_A.obj");
        if (spawnShrubRhododendron) AddTreeModelIfValid(list, basePath + "Shrub_Rhododendron_A.obj");

        // 4. 수변 및 농경지
        if (spawnShrubLotus) AddTreeModelIfValid(list, basePath + "Shrub_Lotus_A.obj");
        if (spawnShrubRice) AddTreeModelIfValid(list, basePath + "Shrub_Rice_A.obj");
        if (spawnShrubMillet) AddTreeModelIfValid(list, basePath + "Shrub_Millet_A.obj");

        // 5. 지표 관목
        if (spawnShrubPine) AddTreeModelIfValid(list, basePath + "Pine_Shrub_Q.obj");
        if (spawnShrubFern) AddTreeModelIfValid(list, basePath + "Fern_Royal_A.obj");
        if (spawnShrubScholar) AddTreeModelIfValid(list, basePath + "Scholar_Bush_K.obj");
        if (spawnShrubConvallaria) AddTreeModelIfValid(list, basePath + "Convallaria_Shrub_A.obj");

        // 6. 🪵 밑동, 그루터기 및 나뭇가지 잔해
        if (spawnBambooStump)
        {
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_A.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_B.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_C.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_D.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_Group_A.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_Group_B.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_Group_C.obj");
            AddTreeModelIfValid(list, basePath + "Bamboo_Stump_Group_D.obj");
        }
        if (spawnStump)
        {
            AddTreeModelIfValid(list, basePath + "Stump_A.obj");
            AddTreeModelIfValid(list, basePath + "Stump_B.obj");
            AddTreeModelIfValid(list, basePath + "Stump_D.obj");
            AddTreeModelIfValid(list, basePath + "Stump_C.obj"); // 싹튼 밑동 (Sprouting Stump)
        }
        if (spawnBrokenBranches)
        {
            AddTreeModelIfValid(list, basePath + "BrokenBranch_A.obj");
            AddTreeModelIfValid(list, basePath + "BrokenBranch_B.obj");
            AddTreeModelIfValid(list, basePath + "BrokenBranch_C.obj");
            AddTreeModelIfValid(list, basePath + "BrokenBranch_D.obj");
            AddTreeModelIfValid(list, basePath + "Stump_E.obj"); // 지표 잔가지 파편 (Ground Twig Clutter)
        }

        return list;
    }

    private void AddTreeModelIfValid(List<GameObject> list, string path)
    {
#if UNITY_EDITOR
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go != null) list.Add(go);
#endif
    }

    private void InitTotalWarMaterials()
    {
        // 1단계: Resources.Load 우선 시도
        if (matCompositeFlames == null)
            matCompositeFlames = Resources.Load<Material>("VFX/M_TotalWar_CompositeFlames");
        if (matCharredWood == null)
            matCharredWood = Resources.Load<Material>("VFX/M_TotalWar_CharredWood");
        if (matWispySmoke == null)
            matWispySmoke = Resources.Load<Material>("VFX/M_TotalWar_WispySmoke");
        if (matTotalWarEmbers == null)
            matTotalWarEmbers = Resources.Load<Material>("VFX/M_TotalWar_Embers");

#if UNITY_EDITOR
        // 2단계: 에디터 환경 AssetDatabase 직접 로드 보강 (Resources 미등록 대비)
        string vfxMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
        if (matCompositeFlames == null)
            matCompositeFlames = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_CompositeFlames.mat");
        if (matCharredWood == null)
            matCharredWood = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_CharredWood.mat");
        if (matWispySmoke == null)
            matWispySmoke = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_WispySmoke.mat");
        if (matTotalWarEmbers == null)
            matTotalWarEmbers = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_Embers.mat");
#endif

        // 3단계: 런타임 완전 무결 폴백 머티리얼 동적 생성 (핑크색 깨짐 100% 원천 차단)
        if (matCompositeFlames == null)
        {
            Shader fireShader = Shader.Find("TotalWar/VFX/CompositeFlames") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            matCompositeFlames = new Material(fireShader) { name = "RuntimeFallback_CompositeFlames" };
            matCompositeFlames.SetColor("_BaseColor", new Color(1.8f, 0.9f, 0.2f, 0.9f));
        }
        if (matWispySmoke == null)
        {
            Shader smokeShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            matWispySmoke = new Material(smokeShader) { name = "RuntimeFallback_WispySmoke" };
            matWispySmoke.SetColor("_BaseColor", new Color(0.85f, 0.83f, 0.80f, 1.0f));
        }
        if (matTotalWarEmbers == null)
        {
            Shader emberShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            matTotalWarEmbers = new Material(emberShader) { name = "RuntimeFallback_Embers" };
            matTotalWarEmbers.SetColor("_BaseColor", new Color(1.0f, 0.65f, 0.2f, 0.9f));
        }
    }

        /// <summary>
    /// 수종별 자식 메쉬(Trunk, Leaves, Roots 등) 1:1 완벽 텍스처 및 머티리얼 바인딩
    /// OBJ 임포트 특성상 루트 아래에 'Trunk'와 'Leaves' 자식 오브젝트가 독립 분리되므로
    /// GetComponentsInChildren로 모든 자식 렌더러를 탐색하여 각각 정확한 전용 머티리얼을 바인딩합니다.
    /// </summary>
    private void ApplyTreeMaterials(GameObject treeObj, string modelName)
    {
        var renderers = treeObj.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        string nameLower = modelName.ToLower();

        // 수종별 전용 머티리얼 준비
        Material trunkMat = null;
        Material leavesMat = null;
        Material extraMat1 = null; // 기근(공기뿌리) 또는 잔가지 전용 머티리얼
        Material extraMat2 = null; // 관목 또는 꽃잎 전용 머티리얼

        bool isWinter = (currentSeason == Season.Winter);
        bool isSnowyWinter = isWinter && enableWinterSnow && (winterSnowAmount > 0.001f);
        bool isDryWinter = isWinter && !isSnowyWinter;

        if (nameLower.Contains("bamboo"))
        {
            trunkMat = GetOrCreateMaterialExact("bamboo_trunk_base_colour", "bamboo_trunk_normal", false, false);
            // 삼탈워 원본 고증: 대나무는 사계절 상록수로서 별도의 겨울 텍스처(tree_bamboo_wi_base_colour)가 존재하지 않으며,
            // 사계절 내내 bamboo_leaf_base_colour 정품 텍스처를 온전히 사용하고 동적 FoliageWind 셰이더를 통해 눈을 얹음
            leavesMat = GetOrCreateMaterialExact("bamboo_leaf_base_colour", "bamboo_leaf_normal", true, true);
        }
        else if (nameLower.Contains("huangshan"))
        {
            trunkMat = GetOrCreateMaterialExact("huangshan_pine_trunk_base_colour", "huangshan_pine_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("huangshan_pine_branch_base_colour", "huangshan_pine_branch_normal", true, true);
        }
        else if (nameLower.Contains("pine_dead"))
        {
            trunkMat = GetOrCreateMaterialExact("pine_trunk_base_colour", "pine_trunk_normal", false, false);
            leavesMat = trunkMat; // 잎이 없는 순수 고사목 뼈대
        }
        else if (nameLower.Contains("pine_broken"))
        {
            trunkMat = GetOrCreateMaterialExact("pine_trunk_base_colour", "pine_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("pine_base_colour", "pine_normal", true, true);
        }
        else if (nameLower.Contains("pine"))
        {
            trunkMat = GetOrCreateMaterialExact("pine_trunk_base_colour", "pine_trunk_normal", false, false);
            string pineLeaf = isSnowyWinter ? "tree_pine_wi_base_colour" : "tree_pine_su_base_colour";
            leavesMat = GetOrCreateMaterialExact(pineLeaf, "pine_normal", true, true);
        }
        else if (nameLower.Contains("camphor"))
        {
            trunkMat = GetOrCreateMaterialExact("3k_tree_camphor_trunk_base_colour", "tree_camphor_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("tree_camphor_branch_base_colour", "tree_camphor_branch_normal", true, true);
        }
        else if (nameLower.Contains("castanopsis_jungle_dead"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_castanopsis_trunk_base_colour", "tree_castanopsis_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("3k_tropical_shrubs_base_colour", null, true, true);
        }
        else if (nameLower.Contains("castanopsis_dead"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_castanopsis_trunk_base_colour", "tree_castanopsis_trunk_normal", false, false);
            leavesMat = trunkMat;
        }
        else if (nameLower.Contains("castanopsis"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_castanopsis_trunk_base_colour", "tree_castanopsis_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("tree_castanopsis_leaves_base_colour", "tree_castanopsis_leaves_normal", true, true);
        }
        else if (nameLower.Contains("ficus"))
        {
            trunkMat = GetOrCreateMaterialExact("3k_tree_ficus_trunk_base_colour", "3k_tree_ficus_trunk_normal", false, false);
            extraMat1 = GetOrCreateMaterialExact("3k_tree_ficus_aerialroots_base_colour", "3k_tree_ficus_aerialroots_normal", true, true);
            leavesMat = GetOrCreateMaterialExact("3k_tree_ficus_branch_base_colour", "3k_tree_ficus_branch_normal", true, true);
            extraMat2 = GetOrCreateMaterialExact("3k_tropical_shrubs_base_colour", null, true, true);
        }
        else if (nameLower.Contains("palm_banana"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_palm_coconut_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("tree_palm_banana_base_colour", null, true, true);
        }
        else if (nameLower.Contains("palm_coconut"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_palm_coconut_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("tree_palm_coconut_leaf_base_colour", null, true, true);
            extraMat2 = GetOrCreateMaterialExact("3k_tropical_shrubs_base_colour", null, true, true);
        }
        else if (nameLower.Contains("palm_windmill"))
        {
            trunkMat = GetOrCreateMaterialExact("palm_windmill_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("palm_windmill_base_colour", null, true, true);
        }
        else if (nameLower.Contains("magnolia"))
        {
            string seasonKey = (currentSeason == Season.Spring) ? "tree_magnolia_sp_base_colour" :
                              ((currentSeason == Season.Autumn) ? "tree_magnolia_au_base_colour" :
                              (isSnowyWinter ? "tree_magnolia_wi_base_colour" : "tree_magnolia_su_base_colour"));
            leavesMat = GetOrCreateMaterialExact(seasonKey, "tree_magnolia_normal", true, true);
            trunkMat = leavesMat;
        }
        else if (nameLower.Contains("plum"))
        {
            trunkMat = GetOrCreateMaterialExact("plum_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("plum_leaves_base_colour", null, true, true);
            extraMat1 = GetOrCreateMaterialExact("plum_twigs_base_colour", "plum_twigs_normal", true, true);
        }
        else if (nameLower.Contains("ginkgo"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_ginko_trunk_base_colour", "tree_ginko_trunk_normal", false, false);
            string ginkgoLeaf = (currentSeason == Season.Autumn) ? "tree_ginko_leaves_au_base_colour" :
                               (isSnowyWinter ? "tree_ginko_leaves_wi_base_colour" :
                               (isDryWinter ? "tree_ginko_leaves_br_base_colour" :
                               ((currentSeason == Season.Summer) ? "tree_ginko_leaves_su_base_colour" : "tree_ginko_leaves_base_colour")));
            leavesMat = GetOrCreateMaterialExact(ginkgoLeaf, "tree_ginko_leaves_normal", true, true);
        }
        else if (nameLower.Contains("maple"))
        {
            trunkMat = GetOrCreateMaterialExact("tree_maple_bark_base_colour", "tree_maple_bark_normal", false, false);
            string mapleLeaf = (currentSeason == Season.Autumn) ? "tree_maple_leaves_au_base_colour" :
                              (isSnowyWinter ? "tree_maple_leaves_wi_base_colour" :
                              (isDryWinter ? "tree_maple_leaves_br_base_colour" :
                              ((currentSeason == Season.Summer) ? "tree_maple_leaves_su_base_colour" : "tree_maple_leaves_base_colour")));
            leavesMat = GetOrCreateMaterialExact(mapleLeaf, "tree_maple_leaves_normal", true, true);
        }
        else if (nameLower.Contains("willow"))
        {
            trunkMat = GetOrCreateMaterialExact("willow_trunk_base_colour", "willow_trunk_normal", false, false);
            string willowLeaf = (currentSeason == Season.Spring) ? "willow_leaves_sp_base_colour" :
                               ((currentSeason == Season.Autumn) ? "willow_leaves_au_base_colour" :
                               (isSnowyWinter ? "willow_leaves_wi_base_colour" :
                               (isDryWinter ? "willow_leaves_br_base_colour" : "willow_leaves_su_base_colour")));
            leavesMat = GetOrCreateMaterialExact(willowLeaf, "willow_leaves_normal", true, true);
        }
        else if (nameLower.Contains("scholar_broken"))
        {
            trunkMat = GetOrCreateMaterialExact("scholar_tree_trunk_base_colour", "scholar_tree_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("scholar_tree_branch_base_colour", "scholar_tree_branch_normal", true, true);
        }
        else if (nameLower.Contains("scholar"))
        {
            trunkMat = GetOrCreateMaterialExact("scholar_tree_trunk_base_colour", "scholar_tree_trunk_normal", false, false);
            string branchKey = (currentSeason == Season.Spring) ? "scholar_tree_branch_sp_base_colour" :
                              ((currentSeason == Season.Autumn) ? "scholar_tree_branch_au_base_colour" :
                              ((currentSeason == Season.Winter) ? "scholar_tree_branch_wi_base_colour" : "scholar_tree_branch_base_colour"));
            leavesMat = GetOrCreateMaterialExact(branchKey, "scholar_tree_branch_normal", true, true);
        }

        // 자식 렌더러 순회 바인딩
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;

            string childName = r.gameObject.name.ToLower();

            // 1. 단일 서브메시 자식 오브젝트 (Trunk vs Leaves)
            if (childName.Contains("trunk"))
            {
                r.sharedMaterial = (trunkMat != null) ? trunkMat : leavesMat;
            }
            else if (childName.Contains("leaves") || childName.Contains("branch"))
            {
                r.sharedMaterial = (leavesMat != null) ? leavesMat : trunkMat;
            }
            else
            {
                // 다중 서브메시 일체형 렌더러인 경우
                if (r.sharedMaterials.Length >= 2)
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    mats[0] = (trunkMat != null) ? trunkMat : leavesMat;
                    mats[1] = (leavesMat != null) ? leavesMat : trunkMat;
                    if (mats.Length >= 3 && extraMat1 != null) mats[2] = extraMat1;
                    if (mats.Length >= 4 && extraMat2 != null) mats[3] = extraMat2;
                    r.sharedMaterials = mats;
                }
                else
                {
                    // 단일 서브메시
                    r.sharedMaterial = (leavesMat != null) ? leavesMat : trunkMat;
                }
            }
        }
    }

    /// <summary>
    /// 관목, 초본, 농경지 및 그루터기 1:1 완벽 텍스처 바인딩
    /// </summary>
    private void ApplyShrubMaterials(GameObject shrubObj, string modelName)
    {
        var renderers = shrubObj.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        string nameLower = modelName.ToLower();

        Material trunkMat = null;
        Material leavesMat = null;
        Material twigsMat = null;

        if (nameLower.Contains("rhododendron"))
        {
            trunkMat = GetOrCreateMaterialExact("shrub_rhododendron_trunk_base_colour", null, false, false);
            string flowerKey = (currentSeason == Season.Spring) ? "shrub_rhododendron_sp_base_colour" : "shrub_rhododendron_base_colour";
            leavesMat = GetOrCreateMaterialExact(flowerKey, "shrub_rhododendron_normal", true, true);
        }
        else if (nameLower.Contains("magnolia"))
        {
            trunkMat = GetOrCreateMaterialExact("shrub_magnolia_trunk_base_colour", null, false, false);
            string petalKey = (currentSeason == Season.Spring) ? "shrub_magnolia_sp_base_colour" :
                             ((currentSeason == Season.Autumn) ? "shrub_magnolia_au_base_colour" :
                             ((currentSeason == Season.Winter) ? "shrub_magnolia_wi_base_colour" : "shrub_magnolia_base_colour"));
            leavesMat = GetOrCreateMaterialExact(petalKey, "shrub_magnolia_normal", true, true);
        }
        else if (nameLower.Contains("tea"))
        {
            trunkMat = GetOrCreateMaterialExact("shrub_tea_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("shrub_tea_base_colour", "shrub_tea_normal", true, true);
        }
        else if (nameLower.Contains("pine"))
        {
            trunkMat = GetOrCreateMaterialExact("pine_bark_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("pine_q_shrub_billboard_base_colour", "pine_q_shrub_billboard_normal", true, true);
        }
        else if (nameLower.Contains("scholar"))
        {
            trunkMat = GetOrCreateMaterialExact("scholar_tree_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("scholar_tree_branch_base_colour", "scholar_tree_branch_normal", true, true);
        }
        else if (nameLower.Contains("elephantgrass") || nameLower.Contains("sword"))
        {
            leavesMat = GetOrCreateMaterialExact("3k_tropical_shrubs_base_colour", null, true, true);
        }
        else if (nameLower.Contains("lotus"))
        {
            leavesMat = GetOrCreateMaterialExact("shrubs_ferns_base_colour", null, true, true);
        }
        else if (nameLower.Contains("rice"))
        {
            leavesMat = GetOrCreateMaterialExact("shrub_grassclump_su_base_colour", null, true, true);
        }
        else if (nameLower.Contains("millet"))
        {
            leavesMat = GetOrCreateMaterialExact("shrub_grassclump_base_colour", null, true, true);
        }
        else if (nameLower.Contains("fern") || nameLower.Contains("convallaria"))
        {
            leavesMat = GetOrCreateMaterialExact("shrubs_ferns_base_colour", null, true, true);
        }
        else if (nameLower.Contains("bamboo_stump"))
        {
            leavesMat = GetOrCreateMaterialExact("bamboo_trunk_base_colour", "bamboo_trunk_normal", false, false);
            trunkMat = leavesMat;
        }
        else if (nameLower.Contains("brokenbranch"))
        {
            trunkMat = GetOrCreateMaterialExact("3k_tree_camphor_trunk_base_colour", "tree_camphor_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("tree_camphor_branch_base_colour", "tree_camphor_branch_normal", true, true);
        }
        else if (nameLower.Contains("stump_e"))
        {
            // Stump_E는 지표면 잔가지 파편(Ground Clutter)이므로 투명 알파 컷아웃 plum_twigs 바인딩 (검은 판때기 원천 방지)
            Material twigsCutout = GetOrCreateMaterialExact("plum_twigs_base_colour", "plum_twigs_normal", true, true);
            trunkMat = twigsCutout;
            leavesMat = twigsCutout;
            twigsMat = twigsCutout;
        }
        else if (nameLower.Contains("stump_c"))
        {
            // Stump_C는 새순이 돋아난 싹튼 밑동(Sprouting Stump): 밑동 + 사계절 새싹 잎 + 잔가지
            trunkMat = GetOrCreateMaterialExact("plum_trunk_base_colour", "plum_trunk_normal", false, false);
            string sproutLeaf = (currentSeason == Season.Winter) ? "plum_leaves_wi_base_colour" :
                               ((currentSeason == Season.Autumn) ? "plum_leaves_au_base_colour" :
                               ((currentSeason == Season.Spring) ? "plum_leaves_sp_base_colour" : "plum_leaves_base_colour"));
            leavesMat = GetOrCreateMaterialExact(sproutLeaf, "plum_leaves_normal", true, true);
            twigsMat = GetOrCreateMaterialExact("plum_twigs_base_colour", "plum_twigs_normal", true, true);
        }
        else if (nameLower.Contains("stump"))
        {
            // 삼탈워 원본 Stump_A, B, D는 하단 단면/나이테 아틀라스를 포함하는 plum_trunk에 1:1 고정 바인딩
            trunkMat = GetOrCreateMaterialExact("plum_trunk_base_colour", "plum_trunk_normal", false, false);
            leavesMat = trunkMat;
            twigsMat = trunkMat;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;

            string childName = r.gameObject.name.ToLower();

            if (childName.Contains("trunk"))
            {
                r.sharedMaterial = (trunkMat != null) ? trunkMat : leavesMat;
            }
            else if (childName.Contains("twigs"))
            {
                r.sharedMaterial = (twigsMat != null) ? twigsMat : leavesMat;
            }
            else if (childName.Contains("leaves") || childName.Contains("branch"))
            {
                r.sharedMaterial = (leavesMat != null) ? leavesMat : trunkMat;
            }
            else
            {
                if (r.sharedMaterials.Length >= 3 && trunkMat != null && leavesMat != null && twigsMat != null)
                {
                    r.sharedMaterials = new Material[] { trunkMat, leavesMat, twigsMat };
                }
                else if (r.sharedMaterials.Length >= 2 && trunkMat != null && leavesMat != null)
                {
                    r.sharedMaterials = new Material[] { trunkMat, leavesMat };
                }
                else
                {
                    r.sharedMaterial = (trunkMat != null) ? trunkMat : leavesMat;
                }
            }
        }
    }

    private Material GetOrCreateMaterialExact(string baseColourFileName, string normalFileName, bool isLeaves, bool isCutout)
    {
        string cacheKey = $"{baseColourFileName}_{(isCutout ? "Cutout" : "Opaque")}";
        if (materialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
        {
            return cached;
        }

        // 쉐이더 탐색: URP Foliage Wind 우선 적용, 차선 URP Lit (줄기와 나뭇잎 모두 FoliageWind 셰이더를 우선 적용하여 숯 탄화 완벽 구현)
        Shader foliageShader = Shader.Find("Universal Render Pipeline/TotalWarFoliageWind");
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader targetShader = (foliageShader != null) ? foliageShader : (litShader != null ? litShader : Shader.Find("Standard"));

        Material mat = new Material(targetShader);
        mat.name = cacheKey;

        string texDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/";
        Texture2D albedo = null;
        Texture2D normal = null;

#if UNITY_EDITOR
        string albedoPath = $"{texDir}{baseColourFileName}.dds";
        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        if (albedo == null)
        {
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}{baseColourFileName}.png");
        }
        if (albedo == null)
        {
            Debug.LogWarning($"[TacticalForestZone] 식생 텍스처 '{baseColourFileName}'를 찾을 수 없습니다! ({albedoPath})");
        }

        if (!string.IsNullOrEmpty(normalFileName))
        {
            string normalPath = $"{texDir}{normalFileName}.dds";
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal == null)
            {
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}{normalFileName}.png");
            }
        }
#endif

        if (albedo != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        }

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_EnableNormalMap", 1.0f);
        }

        if (isCutout)
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.35f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // 나뭇잎 양면 렌더링
            mat.SetFloat("_WindMultiplier", 1.0f);
            mat.SetFloat("_LeafFlutter", 0.8f);
        }
        else
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            mat.SetFloat("_WindMultiplier", 0.04f); // 기둥은 지면에 단단히 고정 (바람에 펄럭이지 않음)
            mat.SetFloat("_LeafFlutter", 0.0f);    // 나뭇잎 떨림 없음
        }

        // 오직 겨울(Winter)이고 enableWinterSnow가 켜져 있을 때만 눈 연출 활성화 (타 계절은 강제 0.0)
        if (mat.HasProperty("_SnowAmount"))
        {
            float effectiveSnow = (currentSeason == Season.Winter && enableWinterSnow) ? winterSnowAmount : 0f;
            mat.SetFloat("_SnowAmount", effectiveSnow);
        }
        if (mat.HasProperty("_LeafSnowRatio"))
        {
            mat.SetFloat("_LeafSnowRatio", leafSnowRatio);
        }
        if (mat.HasProperty("_SnowThreshold"))
        {
            // 삼탈워 원본 기준: 나뭇잎은 상단 윗면(0.20)에만 서리, 기둥은 수직면 배제하고 완만한 상단 가지(0.72)에만 얇은 눈선
            mat.SetFloat("_SnowThreshold", isCutout ? 0.20f : 0.72f);
        }
        if (mat.HasProperty("_IsBamboo"))
        {
            bool isBambooMat = baseColourFileName.ToLower().Contains("bamboo");
            mat.SetFloat("_IsBamboo", isBambooMat ? 1.0f : 0.0f);
        }

        // 삼탈워 원본 탄화(Charred) 텍스처 연결 (줄기와 나뭇잎 모두에 바인딩하여 전소 시 칠흑 같은 숯으로 변모)
#if UNITY_EDITOR
        Texture2D charred = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/3k_cityprop_mines_wood_ashes_base_colour.png");
        if (charred != null && mat.HasProperty("_CharredMap"))
        {
            mat.SetTexture("_CharredMap", charred);
        }
#endif

        materialCache[cacheKey] = mat;
        return mat;
    }

    #region [공간 쿼리 및 전술 은폐/시야 차폐 엔진]

    /// <summary>
    /// 특정 월드 좌표가 이 숲 구역 내부인지 판정 (원형 또는 박스)
    /// </summary>
    public bool ContainsPosition(Vector3 worldPos)
    {
        if (shape == ZoneShape.Circle)
        {
            Vector2 diff = new Vector2(worldPos.x - transform.position.x, worldPos.z - transform.position.z);
            return diff.sqrMagnitude <= (radius * radius);
        }
        else
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float halfX = boxSize.x * 0.5f;
            float halfZ = boxSize.y * 0.5f;
            return Mathf.Abs(localPos.x) <= halfX && Mathf.Abs(localPos.z) <= halfZ;
        }
    }

    /// <summary>
    /// 전역 정적 쿼리: 특정 월드 위치가 전장의 활성 숲 영역에 있는지 판정
    /// </summary>
    public static bool IsPositionInForest(Vector3 position)
    {
        return IsPositionInForest(position, out _);
    }

    public static bool IsPositionInForest(Vector3 position, out TacticalForestZone foundZone)
    {
        for (int i = 0; i < activeZones.Count; i++)
        {
            var zone = activeZones[i];
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                if (zone.ContainsPosition(position))
                {
                    foundZone = zone;
                    return true;
                }
            }
        }
        foundZone = null;
        return false;
    }

    /// <summary>
    /// 전역 정적 쿼리: 두 지점 사이의 사선(Line of Sight)이 숲을 통과하여 차폐되는지 판정
    /// </summary>
    public static bool IsLineBlockedByForest(Vector3 start, Vector3 end)
    {
        return IsLineBlockedByForest(start, end, out _);
    }

    public static bool IsLineBlockedByForest(Vector3 start, Vector3 end, out TacticalForestZone blockingZone)
    {
        for (int i = 0; i < activeZones.Count; i++)
        {
            var zone = activeZones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy || !zone.blockLineOfSight) continue;
            if (zone.isForestRevealedByFire) continue; // 화재로 전소된 숲은 차폐 해제

            if (zone.IntersectsLineSegment(start, end))
            {
                blockingZone = zone;
                return true;
            }
        }
        blockingZone = null;
        return false;
    }

    private bool IntersectsLineSegment(Vector3 a, Vector3 b)
    {
        if (shape == ZoneShape.Circle)
        {
            Vector2 p1 = new Vector2(a.x, a.z);
            Vector2 p2 = new Vector2(b.x, b.z);
            Vector2 c = new Vector2(transform.position.x, transform.position.z);

            Vector2 d = p2 - p1;
            Vector2 f = p1 - c;

            float aCoeff = Vector2.Dot(d, d);
            if (aCoeff < 0.0001f) return f.sqrMagnitude <= (radius * radius);

            float bCoeff = 2f * Vector2.Dot(f, d);
            float cCoeff = Vector2.Dot(f, f) - radius * radius;

            float discriminant = bCoeff * bCoeff - 4f * aCoeff * cCoeff;
            if (discriminant < 0) return false;

            discriminant = Mathf.Sqrt(discriminant);
            float t1 = (-bCoeff - discriminant) / (2f * aCoeff);
            float t2 = (-bCoeff + discriminant) / (2f * aCoeff);

            return (t1 >= 0f && t1 <= 1f) || (t2 >= 0f && t2 <= 1f) || (t1 < 0f && t2 > 1f);
        }
        else
        {
            Vector3 la = transform.InverseTransformPoint(a);
            Vector3 lb = transform.InverseTransformPoint(b);
            float hx = boxSize.x * 0.5f;
            float hz = boxSize.y * 0.5f;

            return LineIntersectsAABB2D(la.x, la.z, lb.x, lb.z, -hx, -hz, hx, hz);
        }
    }

    private static bool LineIntersectsAABB2D(float x1, float y1, float x2, float y2, float minX, float minY, float maxX, float maxY)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;

        float p1 = -dx, q1 = x1 - minX;
        float p2 = dx, q2 = maxX - x1;
        float p3 = -dy, q3 = y1 - minY;
        float p4 = dy, q4 = maxY - y1;

        float u1 = 0.0f;
        float u2 = 1.0f;

        if (ClipTest(p1, q1, ref u1, ref u2) &&
            ClipTest(p2, q2, ref u1, ref u2) &&
            ClipTest(p3, q3, ref u1, ref u2) &&
            ClipTest(p4, q4, ref u1, ref u2))
        {
            return true;
        }

        return false;
    }

    private static bool ClipTest(float p, float q, ref float u1, ref float u2)
    {
        if (p < 0.0f)
        {
            float r = q / p;
            if (r > u2) return false;
            if (r > u1) u1 = r;
        }
        else if (p > 0.0f)
        {
            float r = q / p;
            if (r < u1) return false;
            if (r < u2) u2 = r;
        }
        else if (q < 0.0f)
        {
            return false;
        }
        return true;
    }

    #endregion

    #region [하이어라키 기본 숲 자동 생성 및 에디터 기즈모]

    /// <summary>
    /// 전장 기본 요충지 3개소(서쪽 황산소나무림, 동쪽 대나무숲, 남쪽 남만 밀림) 자동 생성
    /// </summary>
    public static void AutoCreateDefaultForests()
    {
        var existingZones = Object.FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (existingZones != null && existingZones.Length > 0) return;

        // 1. 서쪽 황산 소나무림
        GameObject westForestGo = new GameObject("Forest_West_HuangshanPine");
        westForestGo.transform.position = new Vector3(-85f, 0f, 15f);
        TacticalForestZone westZone = westForestGo.AddComponent<TacticalForestZone>();
        westZone.shape = ZoneShape.Circle;
        westZone.radius = 32f;
        westZone.theme = ForestTheme.HuangshanPine;
        westZone.treeCount = 26;
        westZone.bushCount = 45;
        westZone.SnapToTerrain();
        westZone.GenerateVegetation();

        // 2. 동쪽 대나무 군락지
        GameObject eastForestGo = new GameObject("Forest_East_BambooGrove");
        eastForestGo.transform.position = new Vector3(85f, 0f, -20f);
        TacticalForestZone eastZone = eastForestGo.AddComponent<TacticalForestZone>();
        eastZone.shape = ZoneShape.Circle;
        eastZone.radius = 30f;
        eastZone.theme = ForestTheme.BambooGrove;
        eastZone.treeCount = 34;
        eastZone.bushCount = 50;
        eastZone.SnapToTerrain();
        eastZone.GenerateVegetation();

        // 3. 남만 밀림 (남방 정글 요충지)
        GameObject southForestGo = new GameObject("Forest_South_NanmanJungle");
        southForestGo.transform.position = new Vector3(0f, 0f, -95f);
        TacticalForestZone southZone = southForestGo.AddComponent<TacticalForestZone>();
        southZone.shape = ZoneShape.Circle;
        southZone.radius = 35f;
        southZone.theme = ForestTheme.NanmanJungle;
        southZone.treeCount = 28;
        southZone.bushCount = 50;
        southZone.SnapToTerrain();
        southZone.GenerateVegetation();

        Debug.Log("<color=#00FF88><b>[TacticalForestZone] 전장 요충지 3개소(서쪽 소나무림, 동쪽 대나무숲, 남쪽 남만 밀림)가 성공적으로 자동 배치되었습니다.</b></color>");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color forestColor = new Color(0.15f, 0.75f, 0.25f, 0.35f);
        Color wireColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);

        if (isForestRevealedByFire)
        {
            forestColor = new Color(0.3f, 0.15f, 0.05f, 0.25f);
            wireColor = new Color(0.5f, 0.25f, 0.1f, 0.6f);
        }

        Gizmos.color = forestColor;
        Matrix4x4 oldMatrix = Gizmos.matrix;

        if (shape == ZoneShape.Circle)
        {
            Vector3 center = transform.position;
            center.y = TerrainHeightManager.SampleHeightFast(center.x, center.z) + 0.1f;
            Handles.color = wireColor;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.color = forestColor;
            Handles.DrawSolidDisc(center, Vector3.up, radius);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawCube(new Vector3(0f, 1f, 0f), new Vector3(boxSize.x, 2f, boxSize.y));
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(new Vector3(0f, 1f, 0f), new Vector3(boxSize.x, 2f, boxSize.y));
            Gizmos.matrix = oldMatrix;
        }
    }
#endif

    #endregion
}
