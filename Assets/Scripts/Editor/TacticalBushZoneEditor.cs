using UnityEngine;
using UnityEditor;

/// <summary>
/// 🌾 TacticalBushZone 한국어 커스텀 인스펙터 에디터
/// TacticalForestZone처럼 개별 식생(들풀, 마른 풀, 벼/작물, 코끼리풀, 고사리 등)의 
/// 스폰 여부를 카테고리별 토글로 자유롭게 On/Off 할 수 있습니다.
/// </summary>
[CustomEditor(typeof(TacticalBushZone))]
[CanEditMultipleObjects]
public class TacticalBushZoneEditor : Editor
{
    private SerializedProperty shapeProp;
    private SerializedProperty themeProp;
    private SerializedProperty currentSeasonProp;
    private SerializedProperty radiusProp;
    private SerializedProperty boxSizeProp;
    private SerializedProperty bushCountProp;
    private SerializedProperty randomSeedProp;
    private SerializedProperty snowAmountProp;
    private SerializedProperty autumnTintProp;
    private SerializedProperty providesStealthProp;
    private SerializedProperty cavalrySpeedPenaltyProp;
    private SerializedProperty chargeBonusReductionProp;
    private SerializedProperty missileCoverBonusProp;
    private SerializedProperty isBurningProp;
    private SerializedProperty burnProgressProp;

    // 식생 세부 토글 프로퍼티
    private SerializedProperty spawnBroadleafHerbsProp;
    private SerializedProperty spawnPalmFernProp;

    private SerializedProperty spawnTallGrassProp;
    private SerializedProperty spawnDryGrassProp;
    private SerializedProperty spawnThickGrassProp;
    private SerializedProperty spawnWildHerbsProp;

    private SerializedProperty spawnRiceCropsProp;
    private SerializedProperty spawnMilletCropsProp;
    private SerializedProperty spawnFlatHerbsProp;

    private SerializedProperty spawnElephantGrassProp;
    private SerializedProperty spawnSwordFernProp;
    private SerializedProperty spawnRoyalFernProp;
    private SerializedProperty spawnTropicalBigleafProp;

    private SerializedProperty spawnLotusProp;
    private SerializedProperty spawnReedSeedheadsProp;

    private SerializedProperty spawnWildFlowersProp;
    private SerializedProperty spawnRhododendronProp;
    private SerializedProperty spawnTeaBushProp;
    private SerializedProperty spawnMagnoliaBushProp;
    private SerializedProperty spawnScholarPineBushProp;

    private SerializedProperty spawnBambooStumpProp;
    private SerializedProperty spawnWoodStumpProp;

    // 폴드아웃 상태 (기본적으로 모두 펼쳐서 사용자가 126종 토글을 한눈에 제어 가능)
    private bool foldChangsha = true;
    private bool foldGrass = true;
    private bool foldCrops = true;
    private bool foldJungle = true;
    private bool foldWetland = true;
    private bool foldFlowers = true;
    private bool foldDebris = false;

    private void OnEnable()
    {
        shapeProp = serializedObject.FindProperty("shape");
        themeProp = serializedObject.FindProperty("theme");
        currentSeasonProp = serializedObject.FindProperty("currentSeason");
        radiusProp = serializedObject.FindProperty("radius");
        boxSizeProp = serializedObject.FindProperty("boxSize");
        bushCountProp = serializedObject.FindProperty("bushCount");
        randomSeedProp = serializedObject.FindProperty("randomSeed");
        snowAmountProp = serializedObject.FindProperty("snowAmount");
        autumnTintProp = serializedObject.FindProperty("autumnTint");
        providesStealthProp = serializedObject.FindProperty("providesStealth");
        cavalrySpeedPenaltyProp = serializedObject.FindProperty("cavalrySpeedPenalty");
        chargeBonusReductionProp = serializedObject.FindProperty("chargeBonusReduction");
        missileCoverBonusProp = serializedObject.FindProperty("missileCoverBonus");
        isBurningProp = serializedObject.FindProperty("isBurning");
        burnProgressProp = serializedObject.FindProperty("burnProgress");

        // 식생 토글
        spawnBroadleafHerbsProp = serializedObject.FindProperty("spawnBroadleafHerbs");
        spawnPalmFernProp = serializedObject.FindProperty("spawnPalmFern");

        spawnTallGrassProp = serializedObject.FindProperty("spawnTallGrass");
        spawnDryGrassProp = serializedObject.FindProperty("spawnDryGrass");
        spawnThickGrassProp = serializedObject.FindProperty("spawnThickGrass");
        spawnWildHerbsProp = serializedObject.FindProperty("spawnWildHerbs");

        spawnRiceCropsProp = serializedObject.FindProperty("spawnRiceCrops");
        spawnMilletCropsProp = serializedObject.FindProperty("spawnMilletCrops");
        spawnFlatHerbsProp = serializedObject.FindProperty("spawnFlatHerbs");

        spawnElephantGrassProp = serializedObject.FindProperty("spawnElephantGrass");
        spawnSwordFernProp = serializedObject.FindProperty("spawnSwordFern");
        spawnRoyalFernProp = serializedObject.FindProperty("spawnRoyalFern");
        spawnTropicalBigleafProp = serializedObject.FindProperty("spawnTropicalBigleaf");

        spawnLotusProp = serializedObject.FindProperty("spawnLotus");
        spawnReedSeedheadsProp = serializedObject.FindProperty("spawnReedSeedheads");

        spawnWildFlowersProp = serializedObject.FindProperty("spawnWildFlowers");
        spawnRhododendronProp = serializedObject.FindProperty("spawnRhododendron");
        spawnTeaBushProp = serializedObject.FindProperty("spawnTeaBush");
        spawnMagnoliaBushProp = serializedObject.FindProperty("spawnMagnoliaBush");
        spawnScholarPineBushProp = serializedObject.FindProperty("spawnScholarPineBush");

        spawnBambooStumpProp = serializedObject.FindProperty("spawnBambooStump");
        spawnWoodStumpProp = serializedObject.FindProperty("spawnWoodStump");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TacticalBushZone zone = (TacticalBushZone)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🌾 [삼국지 토탈워 전술 풀·관목 구역]", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("TacticalForestZone의 규격을 계승하며, 불타기 쉬운 야생 들풀/마른 풀/작물/고사리를 자유롭게 토글하여 절차적 배치합니다. 보병 은신, 기병 돌격 둔화 및 5초 초고속 화염 고속도로를 지원합니다.", MessageType.Info);

        // =========================================================================
        // 1. 구역 형태 및 테마
        // =========================================================================
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🌿 1. 구역 형태 및 테마 프리셋", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shapeProp, new GUIContent("구역 형태 (Shape)"));

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(themeProp, new GUIContent("관목 테마 (Theme)"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            zone.ApplyThemePreset((TacticalBushZone.BushTheme)themeProp.enumValueIndex);
            serializedObject.Update();
        }

        if (shapeProp.enumValueIndex == 0) // Circle
        {
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("원형 반경 (Radius) [m]"));
        }
        else // Box
        {
            EditorGUILayout.PropertyField(boxSizeProp, new GUIContent("직사각형 크기 (Box Size) [m]"));
        }

        EditorGUILayout.PropertyField(bushCountProp, new GUIContent("풀/관목 수량 (Count)"));
        EditorGUILayout.PropertyField(randomSeedProp, new GUIContent("랜덤 시드 (Random Seed)"));

        // =========================================================================
        // 🌾 2. 개별 식생 세부 토글 (포레스트 존 스타일)
        // =========================================================================
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🌾 2. 식생 종류별 개별 스폰 토글 (Vegetation Toggles)", EditorStyles.boldLabel);

        // 퀵 액션 프리셋 버튼 (2줄 배치)
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
        if (GUILayout.Button("🌟 126종 전수 스폰", EditorStyles.miniButtonLeft))
        {
            zone.ApplyThemePreset(TacticalBushZone.BushTheme.AllInclusive126);
            serializedObject.Update();
        }
        GUI.backgroundColor = new Color(0.5f, 1f, 0.6f);
        if (GUILayout.Button("🌱 장사 전장 넓은잎 풀", EditorStyles.miniButtonMid))
        {
            zone.ApplyThemePreset(TacticalBushZone.BushTheme.ChangshaBroadleafHerbs);
            serializedObject.Update();
        }
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("🔥 순수 불타는 들풀", EditorStyles.miniButtonRight))
        {
            zone.ApplyThemePreset(TacticalBushZone.BushTheme.FlammablePrairie);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌾 농경지 작물 밭", EditorStyles.miniButtonLeft))
        {
            zone.ApplyThemePreset(TacticalBushZone.BushTheme.CentralFarmCrops);
            serializedObject.Update();
        }
        if (GUILayout.Button("🌿 남만 정글 고사리", EditorStyles.miniButtonMid))
        {
            zone.ApplyThemePreset(TacticalBushZone.BushTheme.NanmanJungleUndergrowth);
            serializedObject.Update();
        }
        if (GUILayout.Button("전체 On", EditorStyles.miniButtonMid))
        {
            zone.SetAllToggles(true);
            themeProp.enumValueIndex = (int)TacticalBushZone.BushTheme.CustomSelection;
            serializedObject.Update();
        }
        if (GUILayout.Button("전체 Off", EditorStyles.miniButtonRight))
        {
            zone.SetAllToggles(false);
            themeProp.enumValueIndex = (int)TacticalBushZone.BushTheme.CustomSelection;
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 모든 토글 펼치기", EditorStyles.miniButtonLeft))
        {
            foldChangsha = foldGrass = foldCrops = foldJungle = foldWetland = foldFlowers = foldDebris = true;
        }
        if (GUILayout.Button("📁 모든 토글 접기", EditorStyles.miniButtonRight))
        {
            foldChangsha = foldGrass = foldCrops = foldJungle = foldWetland = foldFlowers = foldDebris = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUI.BeginChangeCheck();

        // 0. 장사 전장 지표 넓은잎 화초군 (12종)
        foldChangsha = EditorGUILayout.Foldout(foldChangsha, "🌱 [0. 장사 전장 고증 지표 화초군] (12종)", true, EditorStyles.foldoutHeader);
        if (foldChangsha)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnBroadleafHerbsProp, new GUIContent("둥근 넓은잎 지표 화초 (Peliosanthes A~D, 4종)"));
            EditorGUILayout.PropertyField(spawnFlatHerbsProp, new GUIContent("지표 납작 수풀 (Flat Shrubs A~E, 5종)"));
            EditorGUILayout.PropertyField(spawnPalmFernProp, new GUIContent("부채꼴 야자형 고사리 (Palm Fern B & Palm A~B, 3종)"));
            EditorGUI.indentLevel--;
        }

        // 1. 야생 들풀군 (21종)
        foldGrass = EditorGUILayout.Foldout(foldGrass, "🌾 [1. 야생 들풀 & 마른 풀] (21종 - 가연성 극상)", true, EditorStyles.foldoutHeader);
        if (foldGrass)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnTallGrassProp, new GUIContent("키 큰 갈대형 들풀 (Grassclump Tall A~D, 4종)"));
            EditorGUILayout.PropertyField(spawnDryGrassProp, new GUIContent("바짝 마른 들풀 (Dry Grass & Seedheads, 7종) [급속 착화]"));
            EditorGUILayout.PropertyField(spawnThickGrassProp, new GUIContent("빽빽한 들풀 다발 (Thick & Semidry Grass, 5종)"));
            EditorGUILayout.PropertyField(spawnWildHerbsProp, new GUIContent("야생 잡초 및 은방울꽃 (Convallaria A~D & Shrub, 5종)"));
            EditorGUI.indentLevel--;
        }

        // 2. 농경지 작물 (9종)
        foldCrops = EditorGUILayout.Foldout(foldCrops, "🌾 [2. 농경지 작물 밭] (9종 - 가을 황금 이삭)", true, EditorStyles.foldoutHeader);
        if (foldCrops)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnRiceCropsProp, new GUIContent("벼 군락 (Rice Crops A~E, 5종) [황금 벼, 급속 연소]"));
            EditorGUILayout.PropertyField(spawnMilletCropsProp, new GUIContent("조·기장 군락 (Millet Crops A~D, 4종)"));
            EditorGUI.indentLevel--;
        }

        // 3. 남만 밀림 초본 (30종)
        foldJungle = EditorGUILayout.Foldout(foldJungle, "🌿 [3. 남만·열대 거대 초본] (30종 - 은신 최적)", true, EditorStyles.foldoutHeader);
        if (foldJungle)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnElephantGrassProp, new GUIContent("열대 코끼리풀 (Elephant Grass A~D, 4종) [완전 은신]"));
            EditorGUILayout.PropertyField(spawnSwordFernProp, new GUIContent("열대 검고사리 (Sword & Fishbone & Strap Fern, 8종)"));
            EditorGUILayout.PropertyField(spawnRoyalFernProp, new GUIContent("대형 고사리 군락 (Royal & Wood Fern, 9종)"));
            EditorGUILayout.PropertyField(spawnTropicalBigleafProp, new GUIContent("넓은 잎 열대 초본 및 반얀 관목 (Bigleaf & Ficus, 9종)"));
            EditorGUI.indentLevel--;
        }

        // 4. 수변/습지 수생식물 (6종)
        foldWetland = EditorGUILayout.Foldout(foldWetland, "🪷 [4. 수변·습지 수생식물] (6종 - 물가 연꽃 및 갈대)", true, EditorStyles.foldoutHeader);
        if (foldWetland)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnLotusProp, new GUIContent("수변 연꽃 (Lotus A~C, 3종)"));
            EditorGUILayout.PropertyField(spawnReedSeedheadsProp, new GUIContent("물가 수초 씨방 이삭 (Seedhead Grass, 3종)"));
            EditorGUI.indentLevel--;
        }

        // 5. 야생화 & 꽃 관목 (27종)
        foldFlowers = EditorGUILayout.Foldout(foldFlowers, "🌸 [5. 야생화 및 꽃 관목] (27종)", true, EditorStyles.foldoutHeader);
        if (foldFlowers)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnWildFlowersProp, new GUIContent("전장 야생화 (Wild Flowers A~C, 3종)"));
            EditorGUILayout.PropertyField(spawnRhododendronProp, new GUIContent("고산 철쭉 (Rhododendron A~E, 5종)"));
            EditorGUILayout.PropertyField(spawnTeaBushProp, new GUIContent("차나무 관목 (Tea Bush A~E, 5종)"));
            EditorGUILayout.PropertyField(spawnMagnoliaBushProp, new GUIContent("목련 꽃 관목 (Magnolia A~G, 7종)"));
            EditorGUILayout.PropertyField(spawnScholarPineBushProp, new GUIContent("소나무·선비수 왜성 관목 (Scholar & Pine Bush, 7종)"));
            EditorGUI.indentLevel--;
        }

        // 6. 자연 장애물 (21종 - 기본 OFF)
        foldDebris = EditorGUILayout.Foldout(foldDebris, "🪵 [6. 자연 장애물 & 잔가지] (21종 - 기본 OFF)", true, EditorStyles.foldoutHeader);
        if (foldDebris)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("나무밑동이나 부러진 나뭇가지는 포레스트 존에 포함되어 있으므로, 순수 풀밭을 원하실 경우 꺼두시는 것을 권장합니다.", MessageType.None);
            EditorGUILayout.PropertyField(spawnBambooStumpProp, new GUIContent("대나무 잘린 밑동 (Bamboo Stump A~D, Group A~D, 8종)"));
            EditorGUILayout.PropertyField(spawnWoodStumpProp, new GUIContent("고목 밑동 및 가지 (Stump A~E, Brokenbranch A~D, 13종)"));
            EditorGUI.indentLevel--;
        }

        // 개별 토글 변경 시 테마를 '사용자 지정'으로 자동 전환
        if (EditorGUI.EndChangeCheck())
        {
            themeProp.enumValueIndex = (int)TacticalBushZone.BushTheme.CustomSelection;
        }

        // =========================================================================
        // 3. 사계절 및 환경
        // =========================================================================
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🍂 3. 사계절 및 환경 튜닝", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(currentSeasonProp, new GUIContent("사계절 모드 (Season)"));
        EditorGUILayout.PropertyField(snowAmountProp, new GUIContent("❄️ 눈 덮임 비율 (Snow Amount)"));
        EditorGUILayout.PropertyField(autumnTintProp, new GUIContent("🌾 가을 단풍 틴트 (Autumn Tint)"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            zone.RefreshBushMaterials();
        }

        // =========================================================================
        // 4. 전술 전투 기능
        // =========================================================================
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("⚔️ 4. 전술 전장 기능 (Tactical Combat)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(providesStealthProp, new GUIContent("보병 완전 은신 제공 (Stealth)"));
        EditorGUILayout.PropertyField(cavalrySpeedPenaltyProp, new GUIContent("기병 속도 감소율 (Speed Penalty)"));
        EditorGUILayout.PropertyField(chargeBonusReductionProp, new GUIContent("기병 돌격 보너스 상쇄 (Charge Penalty)"));
        EditorGUILayout.PropertyField(missileCoverBonusProp, new GUIContent("직사 사격 차폐 보너스 (Cover)"));

        // =========================================================================
        // 5. 화재 시뮬레이션
        // =========================================================================
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🔥 5. 화재 시뮬레이션 및 테스트", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(isBurningProp, new GUIContent("연소 중 여부 (Is Burning)"));
        EditorGUILayout.PropertyField(burnProgressProp, new GUIContent("연소 진행도 (Burn Progress)"));

        // =========================================================================
        // 6. 생성 및 관리 버튼
        // =========================================================================
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🛠️ 6. 관목 생성 및 관리", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1.0f, 0.4f);
        if (GUILayout.Button("🌿 관목 생성 (Generate)", GUILayout.Height(34)))
        {
            serializedObject.ApplyModifiedProperties();
            zone.GenerateBushes();
        }
        GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
        if (GUILayout.Button("🧹 관목 제거 (Clear)", GUILayout.Height(34)))
        {
            zone.ClearBushes();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔥 불화살 명중 (착화 테스트)", GUILayout.Height(28)))
        {
            zone.Ignite(zone.transform.position);
        }
        if (GUILayout.Button("🔄 머티리얼 일괄 갱신", GUILayout.Height(28)))
        {
            zone.RefreshBushMaterials();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem("GameObject/3D Object/🌾 Tactical Bush Zone", false, 15)]
    public static void CreateTacticalBushZone(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("TacticalBushZone");
        go.AddComponent<TacticalBushZone>();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create TacticalBushZone");
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/3D Object/🌍 TotalWar Terrain Manager", false, 16)]
    public static void CreateTerrainManager(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("TotalWar_TerrainManager");
        go.AddComponent<TotalWarTerrainManager>();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create TotalWar Terrain Manager");
        Selection.activeObject = go;
    }
}
