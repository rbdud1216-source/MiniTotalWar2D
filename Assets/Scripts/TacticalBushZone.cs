using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🌾 [전술 관목 및 들풀 구역 (Tactical Bush Zone)]
/// 삼국지 토탈워(Total War: Three Kingdoms) 본편에서 직접 추출한 실제 3D 모델
/// (코끼리풀, 고사리, 벼/기장 작물, 차나무, 철쭉, 들풀 다발, 대나무 밑동군 등 118종 관목)을
/// 지형에 자연스럽게 배치하고, TacticalForestZone의 형태/계절/적설/바람/화재 규격을 100% 계승합니다.
/// 전술적으로 부대 은신(Stealth), 기병 돌격 둔화(Charge Penalty), 탄도 차폐(Cover) 및
/// 화재 시 바람을 타고 폭풍처럼 질주하는 화염 고속도로(Wildfire Highway)의 핵심 매개체로 동작합니다.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
[ExecuteAlways]
public class TacticalBushZone : MonoBehaviour
{
    public enum BushTheme
    {
        [InspectorName("사용자 지정 (Custom Selection)")]
        CustomSelection,
        [InspectorName("🌟 126종 관목·초본 전수 혼합 (All 126 Foliage)")]
        AllInclusive126,
        [InspectorName("🌱 장사·임상 지표 넓은잎 화초 (Changsha Broadleaf Herbs)")]
        ChangshaBroadleafHerbs,
        [InspectorName("🔥 초고속 인화 마른 풀 들판 (Flammable Prairie)")]
        FlammablePrairie,
        [InspectorName("야생 들풀·갈대 지대 (Wild Grasslands)")]
        WildGrasslands,
        [InspectorName("중원 농경지 벼·기장 밭 (Central Farm Crops)")]
        CentralFarmCrops,
        [InspectorName("🌿 남만 밀림 하층 식생 (Nanman Jungle Undergrowth)")]
        NanmanJungleUndergrowth,
        [InspectorName("남만 코끼리풀 밀림 (Elephant Grass Jungle)")]
        NanmanElephantGrass,
        [InspectorName("강남 수변 연꽃·갈대 습지 (Jiangnan Wetland)")]
        JiangnanWetland,
        [InspectorName("고산 철쭉·화초 지대 (Alpine Rhododendron)")]
        AlpineRhododendron,
        [InspectorName("대나무 밑동 격전지 (Bamboo Stump Ruins)")]
        BambooStumpRuins
    }

    private static readonly List<TacticalBushZone> activeBushZones = new List<TacticalBushZone>();
    public static IReadOnlyList<TacticalBushZone> ActiveBushZones => activeBushZones;

    [Header("🌿 구역 형태 및 테마 (TacticalForestZone 규격 100% 동기화)")]
    [Tooltip("구역 형태 (원형 또는 직사각형)")]
    public TacticalForestZone.ZoneShape shape = TacticalForestZone.ZoneShape.Circle;

    [Tooltip("관목 테마 프리셋 (선택 시 아래 토글들이 자동 세팅됩니다)")]
    public BushTheme theme = BushTheme.ChangshaBroadleafHerbs;

    [Tooltip("사계절 환경 모드 (봄, 여름, 가을, 겨울 - 나뭇잎 및 작물 텍스처 실시간 전환)")]
    public TacticalForestZone.Season currentSeason = TacticalForestZone.Season.Summer;

    [Tooltip("원형 구역 반경 (m)")]
    public float radius = 20f;

    [Tooltip("직사각형 구역 크기 (m)")]
    public Vector2 boxSize = new Vector2(30f, 20f);

    [Range(3, 150)]
    [Tooltip("구역 내 관목 배치 수량")]
    public int bushCount = 35;

    [Tooltip("절차적 배치 난수 시드")]
    public int randomSeed = 12345;

    // =========================================================================
    // 0. 🌱 [장사·임상 지표 넓은잎 화초군] 스크린샷 고증 저상형 둥근잎 풀 & 부채꼴 야자 고사리
    // =========================================================================
    [Header("🌱 0. 장사 전장 지표 넓은잎 화초 (Changsha Broadleaf Herbs)")]
    [Tooltip("둥근 넓은잎 지표 화초 (Peliosanthes A~D) - 지표면에 낮게 깔린 둥글넓적한 초본 (4종)")]
    public bool spawnBroadleafHerbs = true;

    [Tooltip("지표 납작 수풀 (Flat Shrubs A~E) - 지표면에 넓게 퍼지는 납작한 초본 (5종)")]
    public bool spawnFlatHerbs = true;

    [Tooltip("부채꼴 야자형 고사리 (Fern Palm B & Shrub Palm A~B) - 지표면 방사형 부채꼴 양치식물 (3종)")]
    public bool spawnPalmFern = true;

    // =========================================================================
    // 1. 🌾 [야생 들풀 & 잡초군] 바람에 넘실거리는 긴 풀과 마른 풀 (가연성 극상)
    // =========================================================================
    [Header("🌾 1. 야생 들풀 & 마른 풀 (Wild Grasses - 가연성 극상)")]
    [Tooltip("키 큰 갈대형 들풀 (Grassclump Tall A~D) - 바람에 넘실거리는 긴 억새/갈대 (4종)")]
    public bool spawnTallGrass = true;

    [Tooltip("바짝 마른 들풀 (Grassclump Dry A~D, Seedheads A~C) - 화염 착화 시 순식간에 폭발 연소 (7종)")]
    public bool spawnDryGrass = true;

    [Tooltip("빽빽한 들풀 다발 (Grassclump Thick A~C, Semidry A~B) - 덤불형 짙은 풀 (5종)")]
    public bool spawnThickGrass = true;

    [Tooltip("야생 잡초 및 은방울꽃 (Convallaria A~D & Shrub) - 지표면을 덮는 야생 초본 (5종)")]
    public bool spawnWildHerbs = true;

    // =========================================================================
    // 2. 🌾 [농경지 작물] 전장 농경지 고증 (가을 황금빛 벼/기장 밭)
    // =========================================================================
    [Header("🌾 2. 농경지 작물 (Farm Crops - 가을 수확기 작물)")]
    [Tooltip("벼 군락 (Rice Crops A~E) - 논두렁 및 평야의 벼 (가을 황금 이삭, 급속 전소) (5종)")]
    public bool spawnRiceCrops = false;

    [Tooltip("조·기장 군락 (Millet Crops A~D) - 중원 농경지 밭 이삭 작물 (4종)")]
    public bool spawnMilletCrops = false;

    // =========================================================================
    // 3. 🌿 [남만·열대 거대 초본] 성인 키를 넘는 거대한 밀림 풀숲 (은신 최적)
    // =========================================================================
    [Header("🌿 3. 남만·열대 거대 초본 (Tropical Herbs - 은신 최적)")]
    [Tooltip("열대 코끼리풀 (Elephant Grass A~D) - 거대 장신 초본 (보병 완벽 은신) (4종)")]
    public bool spawnElephantGrass = false;

    [Tooltip("열대 검고사리 (Sword Fern A~B, Fishbone Fern A~E, Strap A) - 날렵한 양치식물 (8종)")]
    public bool spawnSwordFern = false;

    [Tooltip("대형 고사리 군락 (Royal Fern A~D, Wood Fern A~D) - 넓게 퍼진 고사리 숲 (9종)")]
    public bool spawnRoyalFern = false;

    [Tooltip("열대 넓은 잎 초본 (Tropical Bigleaf A~C, Ficus Elephant A~F) - 남만 밀림 대형 잎 (9종)")]
    public bool spawnTropicalBigleaf = false;

    // =========================================================================
    // 4. 🪷 [수변·습지 수생식물] 물가 갈대 및 연꽃
    // =========================================================================
    [Header("🪷 4. 수변·습지 수생식물 (Wetland Plants)")]
    [Tooltip("수변 연꽃 (Lotus A~C) - 사계절 지원 수변/진흙탕 수련 (3종)")]
    public bool spawnLotus = false;

    [Tooltip("물가 수초 및 씨방 이삭 (Tall Seedhead A~C) (3종)")]
    public bool spawnReedSeedheads = true;

    // =========================================================================
    // 5. 🌸 [야생화 및 꽃 관목]
    // =========================================================================
    [Header("🌸 5. 야생화 및 꽃 관목 (Wild Flowers & Shrubs)")]
    [Tooltip("전장 야생화 (Flower A~C) - 들판에 피어난 알록달록 야생화 (3종)")]
    public bool spawnWildFlowers = false;

    [Tooltip("고산 철쭉 (Rhododendron A~E) - 봄철 진달래/철쭉 꽃 개화 (5종)")]
    public bool spawnRhododendron = false;

    [Tooltip("차나무 관목 (Tea Bush A~E) - 둥근 녹차 밭 덤불 (5종)")]
    public bool spawnTeaBush = false;

    [Tooltip("목련 꽃 관목 (Magnolia A~G) - 화사한 목련 덤불 (7종)")]
    public bool spawnMagnoliaBush = false;

    [Tooltip("학자수·소나무 관목 (Scholar K~M, Pine Q~R) - 관목형 작은 침엽수 (7종)")]
    public bool spawnScholarPineBush = false;

    // =========================================================================
    // 6. 🪵 [자연 장애물 & 잔가지 - 포레스트 존 외 선택용 (기본 OFF)]
    // =========================================================================
    [Header("🪵 6. 자연 장애물 (Debris & Stumps - 기본 OFF)")]
    [Tooltip("대나무 잘린 밑동 (Bamboo Stump A~D, Group A~D) (8종)")]
    public bool spawnBambooStump = false;

    [Tooltip("고목 밑동 및 부러진 나뭇가지 (Stump A~E, Brokenbranch A~D) (13종)")]
    public bool spawnWoodStump = false;

    [Header("❄️ 적설 및 환경 튜닝")]
    [Range(0f, 1f)]
    [Tooltip("수풀 눈 덮임 비율 (0.0: 완전 녹색 -> 1.0: 100% 설초)")]
    public float snowAmount = 0.0f;

    [Range(0f, 1f)]
    [Tooltip("가을 수확기/마른 풀 틴트 강도")]
    public float autumnTint = 0.0f;

    [Header("⚔️ 전술 전장 기능 (Tactical Combat)")]
    [Tooltip("보병 부대 완전 은신(Stealth) 제공 여부 (적 15m 접근 시 발각)")]
    public bool providesStealth = true;

    [Range(0f, 0.5f)]
    [Tooltip("통과 시 기병 이동속도 저하 비율 (기본 30% 감쇄)")]
    public float cavalrySpeedPenalty = 0.30f;

    [Range(0f, 0.6f)]
    [Tooltip("통과 시 기병 돌격 보너스 상쇄 비율 (기본 40% 감쇄)")]
    public float chargeBonusReduction = 0.40f;

    [Range(0f, 0.4f)]
    [Tooltip("저각 직사 사격 탄도 차폐(Cover) 보너스")]
    public float missileCoverBonus = 0.20f;

    [Header("🔥 초고속 화재 시뮬레이션")]
    [Tooltip("현재 구역 연소 진행 여부")]
    public bool isBurning = false;
    [Range(0f, 1f)]
    [Tooltip("연소 진행도 (0.0: 정상 -> 1.0: 완전 전소 소멸)")]
    public float burnProgress = 0f;

    [Tooltip("화재 제어 컴포넌트")]
    public ShrubFireController fireController;

    // 생성된 인스턴스 캐시
    private readonly List<GameObject> spawnedInstances = new List<GameObject>();
    private MaterialPropertyBlock propBlock;

    private void OnEnable()
    {
        if (!activeBushZones.Contains(this))
            activeBushZones.Add(this);

        if (fireController == null)
        {
            fireController = GetComponent<ShrubFireController>();
            if (fireController == null)
                fireController = gameObject.AddComponent<ShrubFireController>();
        }
        fireController.Init(this);

        RefreshBushMaterials();
    }

    private void OnDisable()
    {
        activeBushZones.Remove(this);
    }

    private void Update()
    {
        if (isBurning && fireController != null)
        {
            burnProgress = fireController.burnProgress;
            UpdateShaderBurnProgress();
        }
    }

    private Transform bushContainer;

    /// <summary>
    /// 구역 위치를 지형 표면 높이에 밀착시킵니다.
    /// </summary>
    public void SnapToTerrain()
    {
        Vector3 pos = transform.position;
        pos.y = TerrainHeightManager.SampleHeightFast(pos.x, pos.z);
        transform.position = pos;
    }

    [ContextMenu("🌿 관목 생성 (Generate Bushes)")]
    public void GenerateBushes()
    {
        ClearBushes();
        SnapToTerrain();

        GameObject containerGo = new GameObject("Bush_Container");
        containerGo.transform.SetParent(transform, false);
        bushContainer = containerGo.transform;

        Random.InitState(randomSeed);

        List<GameObject> prefabs = LoadBushModelsForTheme();
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("[TacticalBushZone] 테마에 맞는 관목 모델을 찾지 못했습니다.");
            return;
        }

        int spawned = 0;
        int maxAttempts = bushCount * 3;
        int attempts = 0;

        while (spawned < bushCount && attempts < maxAttempts)
        {
            attempts++;
            Vector3 localPos = GetRandomPointInShape();
            Vector3 worldPos = transform.TransformPoint(localPos);

            Vector3 normal = TacticalForestZone.SampleTerrainNormal(worldPos.x, worldPos.z);
            float slopeAngle = Vector3.Angle(Vector3.up, normal);
            if (slopeAngle > 60f) continue; // 너무 가파른 절벽은 제외

            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            if (prefab == null) continue;

            float scale = Random.Range(0.85f, 1.25f);
            float sink = 0.08f * scale;
            worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z) - sink;

            Vector3 blendedUp = Vector3.Slerp(Vector3.up, normal, 0.5f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, blendedUp) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject bushObj = Instantiate(prefab, worldPos, rot, bushContainer);
            bushObj.transform.localScale = Vector3.one * scale;

            // 🌾 삼탈워 정품 텍스처 및 FoliageWind 셰이더 1:1 자동 바인딩
            ApplyShrubMaterials(bushObj, prefab.name);

            spawnedInstances.Add(bushObj);
            spawned++;
        }

        RefreshBushMaterials();
        Debug.Log($"<color=#00FF88><b>[TacticalBushZone] 🌿 {gameObject.name}에 {spawned}개의 관목({theme})이 지형에 밀착 생성되었습니다!</b></color>");
    }

    [ContextMenu("🧹 관목 제거 (Clear Bushes)")]
    public void ClearBushes()
    {
        spawnedInstances.Clear();

        if (bushContainer != null)
        {
            if (Application.isPlaying) Destroy(bushContainer.gameObject);
            else DestroyImmediate(bushContainer.gameObject);
            bushContainer = null;
        }

        var oldContainer = transform.Find("Bush_Container");
        if (oldContainer != null)
        {
            if (Application.isPlaying) Destroy(oldContainer.gameObject);
            else DestroyImmediate(oldContainer.gameObject);
        }
    }

    private Vector3 GetRandomPointInShape()
    {
        if (shape == TacticalForestZone.ZoneShape.Circle)
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

    private void OnValidate()
    {
        if (theme != BushTheme.CustomSelection)
        {
            ApplyThemePreset(theme);
        }
    }

    /// <summary>
    /// 테마 프리셋 선택 시 해당 테마에 맞는 풀/식생 토글을 자동 세팅합니다.
    /// </summary>
    public void ApplyThemePreset(BushTheme t)
    {
        theme = t;
        if (t == BushTheme.CustomSelection) return;

        // 전체 토글 초기화
        SetAllToggles(false);

        switch (t)
        {
            case BushTheme.AllInclusive126:
                // 🌟 삼탈워 126종 전체 관목·초본 전수 활성화
                SetAllToggles(true);
                break;

            case BushTheme.ChangshaBroadleafHerbs:
                // 🌱 장사·임상 스크린샷 고증: 지표면 둥근 넓은잎 화초 & 부채꼴 야자 고사리
                spawnBroadleafHerbs = true;
                spawnFlatHerbs = true;
                spawnPalmFern = true;
                spawnDryGrass = true;
                spawnWildHerbs = true;
                break;

            case BushTheme.FlammablePrairie:
                // 🔥 초고속 인화 마른 풀 들판 (가장 불타기 쉬운 마른 들풀/갈대 위주)
                spawnTallGrass = true;
                spawnDryGrass = true;
                spawnThickGrass = true;
                spawnWildHerbs = true;
                spawnReedSeedheads = true;
                break;

            case BushTheme.WildGrasslands:
                // 🌾 야생 들풀·갈대 지대 (녹색/황색 들풀 및 야생화)
                spawnTallGrass = true;
                spawnDryGrass = true;
                spawnThickGrass = true;
                spawnWildHerbs = true;
                spawnWildFlowers = true;
                spawnReedSeedheads = true;
                break;

            case BushTheme.CentralFarmCrops:
                // 🌾 중원 농경지 벼·기장 밭 (가을 수확기 작물)
                spawnRiceCrops = true;
                spawnMilletCrops = true;
                spawnFlatHerbs = true;
                spawnTallGrass = true;
                spawnDryGrass = true;
                break;

            case BushTheme.NanmanJungleUndergrowth:
            case BushTheme.NanmanElephantGrass:
                // 🌿 남만 밀림 하층 식생 / 코끼리풀 밀림 (거대 초본 및 양치식물)
                spawnElephantGrass = true;
                spawnSwordFern = true;
                spawnRoyalFern = true;
                spawnTropicalBigleaf = true;
                spawnThickGrass = true;
                break;

            case BushTheme.JiangnanWetland:
                // 🪷 강남 수변 연꽃·갈대 습지 (수련 및 물갈대)
                spawnLotus = true;
                spawnTallGrass = true;
                spawnReedSeedheads = true;
                spawnThickGrass = true;
                break;

            case BushTheme.AlpineRhododendron:
                // 🌸 고산 철쭉·화초 지대
                spawnRhododendron = true;
                spawnWildFlowers = true;
                spawnTeaBush = true;
                spawnScholarPineBush = true;
                spawnTallGrass = true;
                break;

            case BushTheme.BambooStumpRuins:
                // 🪵 대나무 밑동 격전지
                spawnBambooStump = true;
                spawnWoodStump = true;
                spawnDryGrass = true;
                break;
        }
    }

    /// <summary>
    /// 모든 식생 토글 일괄 On/Off (126종 전수 제어)
    /// </summary>
    public void SetAllToggles(bool enable)
    {
        spawnBroadleafHerbs = enable;
        spawnFlatHerbs = enable;
        spawnPalmFern = enable;
        spawnTallGrass = enable;
        spawnDryGrass = enable;
        spawnThickGrass = enable;
        spawnWildHerbs = enable;
        spawnRiceCrops = enable;
        spawnMilletCrops = enable;
        spawnElephantGrass = enable;
        spawnSwordFern = enable;
        spawnRoyalFern = enable;
        spawnTropicalBigleaf = enable;
        spawnLotus = enable;
        spawnReedSeedheads = enable;
        spawnWildFlowers = enable;
        spawnRhododendron = enable;
        spawnTeaBush = enable;
        spawnMagnoliaBush = enable;
        spawnScholarPineBush = enable;
        spawnBambooStump = enable;
        spawnWoodStump = enable;
    }

    /// <summary>
    /// 활성화된 토글에 따라 삼탈워 126종 관목·초본 모델 중 해당하는 모든 모델을 로드합니다.
    /// </summary>
    private List<GameObject> LoadBushModelsForTheme()
    {
        List<GameObject> list = new List<GameObject>();
        string baseDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs";

#if UNITY_EDITOR
        List<string> activeModelNames = new List<string>();

        // 0. 🌱 장사 전장 지표 넓은잎 화초 (4종)
        if (spawnBroadleafHerbs)
            activeModelNames.AddRange(new[] { "Shrub_Peliosanthes_A", "Shrub_Peliosanthes_B", "Shrub_Peliosanthes_C", "Shrub_Peliosanthes_D" });
        // 지표 납작 수풀 (5종)
        if (spawnFlatHerbs)
            activeModelNames.AddRange(new[] { "Shrub_Flat_A", "Shrub_Flat_B", "Shrub_Flat_C", "Shrub_Flat_D", "Shrub_Flat_E" });
        // 부채꼴 야자형 고사리 (3종)
        if (spawnPalmFern)
            activeModelNames.AddRange(new[] { "Shrub_Fern_Palm_B", "Shrub_Palm_A", "Shrub_Palm_B" });

        // 1. 🌾 야생 들풀 & 마른 풀 (Wild Grasses - 21종)
        if (spawnTallGrass)
            activeModelNames.AddRange(new[] { "Shrub_Grassclump_Tall_A", "Shrub_Grassclump_Tall_B", "Shrub_Grassclump_Tall_C", "Shrub_Grassclump_Tall_D" });
        if (spawnDryGrass)
            activeModelNames.AddRange(new[] { "Shrub_Grassclump_Dry_A", "Shrub_Grassclump_Dry_B", "Shrub_Grassclump_Dry_C", "Shrub_Grassclump_Dry_D", "Shrub_Grassclump_Dry_Seedheads_A", "Shrub_Grassclump_Dry_Seedheads_B", "Shrub_Grassclump_Dry_Seedheads_C" });
        if (spawnThickGrass)
            activeModelNames.AddRange(new[] { "Shrub_Grassclump_Thick_A", "Shrub_Grassclump_Thick_B", "Shrub_Grassclump_Thick_C", "Shrub_Grassclump_Semidry_A", "Shrub_Grassclump_Semidry_B" });
        if (spawnWildHerbs)
            activeModelNames.AddRange(new[] { "Shrub_Convallaria_A", "Shrub_Convallaria_B", "Shrub_Convallaria_C", "Shrub_Convallaria_D", "Convallaria_Shrub_A" });

        // 2. 🌾 농경지 작물 (Farm Crops - 9종)
        if (spawnRiceCrops)
            activeModelNames.AddRange(new[] { "Shrub_Rice_A", "Shrub_Rice_B", "Shrub_Rice_C", "Shrub_Rice_D", "Shrub_Rice_E" });
        if (spawnMilletCrops)
            activeModelNames.AddRange(new[] { "Shrub_Millet_A", "Shrub_Millet_B", "Shrub_Millet_C", "Shrub_Millet_D" });

        // 3. 🌿 남만·열대 거대 초본 (Tropical Herbs - 30종)
        if (spawnElephantGrass)
            activeModelNames.AddRange(new[] { "Shrub_ElephantGrass_A", "Shrub_Elephantgrass_B", "Shrub_Elephantgrass_C", "Shrub_Elephantgrass_D" });
        if (spawnSwordFern)
            activeModelNames.AddRange(new[] { "Shrub_Fern_Sword_A", "Shrub_Fern_Sword_B", "Shrub_Fern_Fishbone_A", "Shrub_Fern_Fishbone_B", "Shrub_Fern_Fishbone_C", "Shrub_Fern_Fishbone_D", "Shrub_Fern_Fishbone_E", "Shrub_Fern_Strap_A" });
        if (spawnRoyalFern)
            activeModelNames.AddRange(new[] { "Shrub_Fern_Royal_A", "Shrub_Fern_Royal_B", "Shrub_Fern_Royal_C", "Shrub_Fern_Royal_D", "Fern_Royal_A", "Shrub_Fern_Wood_A", "Shrub_Fern_Wood_B", "Shrub_Fern_Wood_C", "Shrub_Fern_Wood_D" });
        if (spawnTropicalBigleaf)
            activeModelNames.AddRange(new[] { "Plant_Tropical_Bigleaf_A", "Plant_Tropical_Bigleaf_B", "Plant_Tropical_Bigleaf_C", "Shrub_Ficus_Elephant_A", "Shrub_Ficus_Elephant_B", "Shrub_Ficus_Elephant_C", "Shrub_Ficus_Elephant_D", "Shrub_Ficus_Elephant_E", "Shrub_Ficus_Elephant_F" });

        // 4. 🪷 수변·습지 수생식물 (6종)
        if (spawnLotus)
            activeModelNames.AddRange(new[] { "Shrub_Lotus_A", "Shrub_Lotus_B", "Shrub_Lotus_C" });
        if (spawnReedSeedheads)
            activeModelNames.AddRange(new[] { "Shrub_Grassclump_Tall_Seedhead_A", "Shrub_Grassclump_Tall_Seedhead_B", "Shrub_Grassclump_Tall_Seedhead_C" });

        // 5. 🌸 야생화 및 꽃 관목 (27종)
        if (spawnWildFlowers)
            activeModelNames.AddRange(new[] { "Shrub_Flower_A", "Shrub_Flower_B", "Shrub_Flower_C" });
        if (spawnRhododendron)
            activeModelNames.AddRange(new[] { "Shrub_Rhododendron_A", "Shrub_Rhododendron_B", "Shrub_Rhododendron_C", "Shrub_Rhododendron_D", "Shrub_Rhododendron_E" });
        if (spawnTeaBush)
            activeModelNames.AddRange(new[] { "Shrub_Tea_A", "Shrub_Tea_B", "Shrub_Tea_C", "Shrub_Tea_D", "Shrub_Tea_E" });
        if (spawnMagnoliaBush)
            activeModelNames.AddRange(new[] { "Shrub_Magnolia_A", "Shrub_Magnolia_B", "Shrub_Magnolia_C", "Shrub_Magnolia_D", "Shrub_Magnolia_E", "Shrub_Magnolia_F", "Shrub_Magnolia_G" });
        if (spawnScholarPineBush)
            activeModelNames.AddRange(new[] { "Scholar_Bush_K", "Scholar_K_Bush", "Scholar_L_Bush", "Scholar_M_Bush", "Pine_Q_Shrub", "Pine_R_Shrub", "Pine_Shrub_Q" });

        // 6. 🪵 자연 장애물 (21종 - 기본 OFF)
        if (spawnBambooStump)
            activeModelNames.AddRange(new[] { "Bamboo_Stump_A", "Bamboo_Stump_B", "Bamboo_Stump_C", "Bamboo_Stump_D", "Bamboo_Stump_Group_A", "Bamboo_Stump_Group_B", "Bamboo_Stump_Group_C", "Bamboo_Stump_Group_D" });
        if (spawnWoodStump)
            activeModelNames.AddRange(new[] { "Stump_A", "Stump_B", "Stump_C", "Stump_D", "Stump_E", "BrokenBranch_A", "BrokenBranch_B", "BrokenBranch_C", "BrokenBranch_D", "Shrub_Brokenbranch_A", "Shrub_Brokenbranch_B", "Shrub_Brokenbranch_C", "Shrub_Brokenbranch_D" });

        // 총 126종 전수 매핑 검증 및 안전 폴백
        if (activeModelNames.Count == 0)
        {
            activeModelNames.AddRange(new[] { "Shrub_Grassclump_Tall_A", "Shrub_Grassclump_Dry_A", "Shrub_Peliosanthes_A", "Shrub_Rice_A" });
        }

        for (int i = 0; i < activeModelNames.Count; i++)
        {
            string assetPath = $"{baseDir}/{activeModelNames[i]}.obj";
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go != null) list.Add(go);
        }

        // 2차 안전 폴백: 만약 그래도 비어있다면 디렉터리 내 아무 OBJ나 로드
        if (list.Count == 0 && System.IO.Directory.Exists(baseDir))
        {
            string[] allObjs = System.IO.Directory.GetFiles(baseDir, "*.obj");
            int take = Mathf.Min(10, allObjs.Length);
            for (int i = 0; i < take; i++)
            {
                string rel = allObjs[i].Replace('\\', '/');
                GameObject fallbackGo = AssetDatabase.LoadAssetAtPath<GameObject>(rel);
                if (fallbackGo != null) list.Add(fallbackGo);
            }
        }
#endif
        return list;
    }

    /// <summary>
    /// 좌표 (worldPos)가 이 관목 구역의 바운즈 내부에 위치하는지 검사합니다.
    /// </summary>
    public bool IsPositionInside(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        if (shape == TacticalForestZone.ZoneShape.Circle)
        {
            return (localPos.x * localPos.x + localPos.z * localPos.z) <= (radius * radius);
        }
        else
        {
            return Mathf.Abs(localPos.x) <= (boxSize.x * 0.5f) && Mathf.Abs(localPos.z) <= (boxSize.y * 0.5f);
        }
    }

    /// <summary>
    /// 화재 착화 (불화살 명중 또는 인접 화염 전파)
    /// </summary>
    public void Ignite(Vector3 hitPoint)
    {
        if (fireController != null)
        {
            fireController.Ignite(hitPoint);
        }
    }

    /// <summary>
    /// 셰이더 프로퍼티 블록으로 연소 진행도 실시간 갱신
    /// </summary>
    public void UpdateShaderBurnProgress()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        propBlock.SetFloat("_BurnProgress", burnProgress);
        propBlock.SetFloat("_SnowAmount", snowAmount);

        for (int i = 0; i < spawnedInstances.Count; i++)
        {
            if (spawnedInstances[i] != null)
            {
                var renderers = spawnedInstances[i].GetComponentsInChildren<Renderer>();
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].SetPropertyBlock(propBlock);
                }
            }
        }
    }

    private static readonly Dictionary<string, Material> bushMaterialCache = new Dictionary<string, Material>();

    /// <summary>
    /// 사계절 및 적설 머티리얼 일괄 갱신
    /// </summary>
    public void RefreshBushMaterials()
    {
        // 1. 하위 오브젝트가 있다면 텍스처(계절/모델) 재할당
        if (bushContainer != null)
        {
            for (int i = 0; i < bushContainer.childCount; i++)
            {
                Transform child = bushContainer.GetChild(i);
                if (child != null)
                {
                    string baseName = child.name.Replace("(Clone)", "").Trim();
                    ApplyShrubMaterials(child.gameObject, baseName);
                }
            }
        }

        // 2. 머티리얼 프로퍼티 블록 갱신
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        propBlock.SetFloat("_SnowAmount", snowAmount);
        propBlock.SetFloat("_AutumnTint", autumnTint);
        propBlock.SetFloat("_IsShrubGrass", 1.0f);

        UpdateShaderBurnProgress();
    }

    /// <summary>
    /// 관목/초본 3D 모델에 삼탈워 정품 텍스처 및 FoliageWind 셰이더를 1:1 매핑합니다.
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
            trunkMat = GetOrCreateMaterialExact("shrub_rhododendron_trunk_base_colour", "shrub_rhododendron_trunk_normal", false, false);
            string flowerKey = (currentSeason == TacticalForestZone.Season.Spring) ? "shrub_rhododendron_sp_base_colour" : "shrub_rhododendron_base_colour";
            leavesMat = GetOrCreateMaterialExact(flowerKey, "shrub_rhododendron_normal", true, true);
        }
        else if (nameLower.Contains("magnolia"))
        {
            trunkMat = GetOrCreateMaterialExact("shrub_magnolia_trunk_base_colour", null, false, false);
            string petalKey = (currentSeason == TacticalForestZone.Season.Spring) ? "shrub_magnolia_sp_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Autumn) ? "shrub_magnolia_au_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Winter) ? "shrub_magnolia_wi_base_colour" : "shrub_magnolia_base_colour"));
            leavesMat = GetOrCreateMaterialExact(petalKey, "shrub_magnolia_normal", true, true);
        }
        else if (nameLower.Contains("tea"))
        {
            trunkMat = GetOrCreateMaterialExact("shrub_tea_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("shrub_tea_base_colour", "shrub_tea_normal", true, true);
        }
        else if (nameLower.Contains("pine"))
        {
            trunkMat = GetOrCreateMaterialExact("pine_bark_base_colour", "pine_bark_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("pine_q_shrub_billboard_base_colour", "pine_q_shrub_billboard_normal", true, true);
        }
        else if (nameLower.Contains("scholar"))
        {
            trunkMat = GetOrCreateMaterialExact("scholar_tree_trunk_base_colour", "scholar_tree_trunk_normal", false, false);
            leavesMat = GetOrCreateMaterialExact("scholar_tree_branch_base_colour", "scholar_tree_branch_normal", true, true);
        }
        else if (nameLower.Contains("elephantgrass") || nameLower.Contains("sword") || nameLower.Contains("plant_tropical"))
        {
            leavesMat = GetOrCreateMaterialExact("3k_tropical_shrubs_base_colour", "3k_tropical_shrubs_normal", true, true);
        }
        else if (nameLower.Contains("lotus"))
        {
            // 연꽃(Lotus): 사계절 지원 수변 관목
            string lotusKey = (currentSeason == TacticalForestZone.Season.Autumn) ? "shrubs_ferns_au_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Winter) ? "shrubs_ferns_wi_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Summer) ? "shrubs_ferns_su_base_colour" : "shrubs_ferns_base_colour"));
            leavesMat = GetOrCreateMaterialExact(lotusKey, "shrubs_ferns_normal", true, true);
        }
        else if (nameLower.Contains("rice"))
        {
            // 벼(Rice): 여름 짙은 녹색, 가을 황금빛 벼이삭
            string riceKey = (currentSeason == TacticalForestZone.Season.Autumn) ? "shrub_grassclump_au_base_colour" :
                            ((currentSeason == TacticalForestZone.Season.Winter) ? "shrub_grassclump_wi_base_colour" :
                            ((currentSeason == TacticalForestZone.Season.Spring) ? "shrub_grassclump_sp_base_colour" : "shrub_grassclump_su_base_colour"));
            leavesMat = GetOrCreateMaterialExact(riceKey, "shrub_grassclump_normal", true, true);
        }
        else if (nameLower.Contains("millet"))
        {
            // 기장(Millet): 기본 조 이삭
            string milletKey = (currentSeason == TacticalForestZone.Season.Autumn) ? "shrub_grassclump_au_base_colour" : "shrub_grassclump_base_colour";
            leavesMat = GetOrCreateMaterialExact(milletKey, "shrub_grassclump_normal", true, true);
        }
        else if (nameLower.Contains("fern") || nameLower.Contains("convallaria") || nameLower.Contains("peliosanthes") || nameLower.Contains("flower") || nameLower.Contains("flat"))
        {
            string fernKey = (currentSeason == TacticalForestZone.Season.Autumn) ? "shrubs_ferns_au_base_colour" :
                            ((currentSeason == TacticalForestZone.Season.Winter) ? "shrubs_ferns_wi_base_colour" :
                            ((currentSeason == TacticalForestZone.Season.Summer) ? "shrubs_ferns_su_base_colour" : "shrubs_ferns_base_colour"));
            leavesMat = GetOrCreateMaterialExact(fernKey, "shrubs_ferns_normal", true, true);
        }
        else if (nameLower.Contains("bamboo_stump"))
        {
            leavesMat = GetOrCreateMaterialExact("bamboo_trunk_base_colour", "bamboo_trunk_normal", false, false);
            trunkMat = leavesMat;
        }
        else if (nameLower.Contains("brokenbranch"))
        {
            trunkMat = GetOrCreateMaterialExact("3k_tree_camphor_trunk_base_colour", null, false, false);
            leavesMat = GetOrCreateMaterialExact("tree_camphor_branch_base_colour", null, true, true);
        }
        else if (nameLower.Contains("stump_e"))
        {
            Material twigsCutout = GetOrCreateMaterialExact("plum_twigs_base_colour", "plum_twigs_normal", true, true);
            trunkMat = twigsCutout;
            leavesMat = twigsCutout;
            twigsMat = twigsCutout;
        }
        else if (nameLower.Contains("stump_c"))
        {
            trunkMat = GetOrCreateMaterialExact("plum_trunk_base_colour", "plum_trunk_normal", false, false);
            string sproutLeaf = (currentSeason == TacticalForestZone.Season.Winter) ? "plum_leaves_wi_base_colour" :
                               ((currentSeason == TacticalForestZone.Season.Autumn) ? "plum_leaves_au_base_colour" :
                               ((currentSeason == TacticalForestZone.Season.Spring) ? "plum_leaves_sp_base_colour" : "plum_leaves_base_colour"));
            leavesMat = GetOrCreateMaterialExact(sproutLeaf, "plum_leaves_normal", true, true);
            twigsMat = GetOrCreateMaterialExact("plum_twigs_base_colour", "plum_twigs_normal", true, true);
        }
        else if (nameLower.Contains("stump"))
        {
            trunkMat = GetOrCreateMaterialExact("plum_trunk_base_colour", "plum_trunk_normal", false, false);
            leavesMat = trunkMat;
            twigsMat = trunkMat;
        }
        else
        {
            // 들풀(Grassclump) 및 기본 관목
            string grassKey = (currentSeason == TacticalForestZone.Season.Autumn) ? "shrub_grassclump_au_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Winter) ? "shrub_grassclump_wi_base_colour" :
                             ((currentSeason == TacticalForestZone.Season.Spring) ? "shrub_grassclump_sp_base_colour" : "shrub_grassclump_base_colour"));
            leavesMat = GetOrCreateMaterialExact(grassKey, "shrub_grassclump_normal", true, true);
        }

        // 안전 폴백: leavesMat이 null이면 가장 범용적인 shrubs_ferns 할당
        if (leavesMat == null)
        {
            leavesMat = GetOrCreateMaterialExact("shrubs_ferns_base_colour", "shrubs_ferns_normal", true, true);
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
                    r.sharedMaterial = (trunkMat != null && (nameLower.Contains("stump") || nameLower.Contains("trunk"))) ? trunkMat : leavesMat;
                }
            }
        }
    }

    /// <summary>
    /// 삼탈워 식생 머티리얼을 셰이더와 텍스처를 바인딩하여 생성 또는 캐시에서 반환합니다.
    /// </summary>
    private Material GetOrCreateMaterialExact(string baseColourFileName, string normalFileName, bool isLeaves, bool isCutout)
    {
        string cacheKey = $"{baseColourFileName}_{(isCutout ? "Cutout" : "Opaque")}";
        if (bushMaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
        {
            return cached;
        }

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
            // 폴백 검색: 만약 요청된 파일이 없다면 기본 shrubs_ferns_base_colour 시도
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}shrubs_ferns_base_colour.dds");
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

        // 관목 및 초본 전용 파라미터 셋업
        mat.SetFloat("_IsShrubGrass", 1.0f);

        if (isCutout)
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.35f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // 양면 렌더링
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
            mat.SetFloat("_WindMultiplier", 0.05f);
            mat.SetFloat("_LeafFlutter", 0.0f);
        }

        mat.SetFloat("_SnowAmount", snowAmount);
        mat.SetFloat("_AutumnTint", autumnTint);

        bushMaterialCache[cacheKey] = mat;
        return mat;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color gizmoCol = isBurning ? new Color(1f, 0.3f, 0.1f, 0.75f) : new Color(0.2f, 0.85f, 0.35f, 0.6f);
        Gizmos.color = gizmoCol;

        Matrix4x4 oldMat = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (shape == TacticalForestZone.ZoneShape.Circle)
        {
            DrawWireCircle(Vector3.zero, radius, 36);
        }
        else
        {
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, 2.0f, boxSize.y));
        }

        Gizmos.matrix = oldMat;
    }

    private void DrawWireCircle(Vector3 center, float r, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prev = center + new Vector3(r, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            Vector3 next = center + new Vector3(Mathf.Cos(rad) * r, 0, Mathf.Sin(rad) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
