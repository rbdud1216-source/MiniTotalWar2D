using UnityEngine;
using UnityEditor;

/// <summary>
/// 🌲 TacticalForestZone 완전 한국어 커스텀 인스펙터 에디터
/// 인스펙터의 모든 필드와 드롭다운을 100% 한글화하고,
/// 카테고리별 폴드아웃과 원클릭 퀵 액션 버튼을 제공합니다.
/// </summary>
[CustomEditor(typeof(TacticalForestZone))]
[CanEditMultipleObjects]
public class TacticalForestZoneEditor : Editor
{
    // 카테고리 접이식 폴드아웃 상태
    private static bool foldoutShape = true;
    private static bool foldoutSeason = true;
    private static bool foldoutTemperate = true;
    private static bool foldoutTropical = false;
    private static bool foldoutDeciduous = false;
    private static bool foldoutCrops = false;
    private static bool foldoutShrubs = false;
    private static bool foldoutRuins = false;
    private static bool foldoutDensity = true;
    private static bool foldoutTerrain = false;
    private static bool foldoutStealth = false;
    private static bool foldoutFire = true;

    // Serialized Properties
    private SerializedProperty shapeProp;
    private SerializedProperty themeProp;
    private SerializedProperty currentSeasonProp;
    private SerializedProperty radiusProp;
    private SerializedProperty boxSizeProp;

    private SerializedProperty spawnHuangshanPineProp;
    private SerializedProperty spawnPineProp;
    private SerializedProperty spawnBambooProp;
    private SerializedProperty spawnCastanopsisProp;
    private SerializedProperty spawnCamphorProp;
    private SerializedProperty spawnShrubTeaProp;

    private SerializedProperty spawnFicusProp;
    private SerializedProperty spawnBananaPalmProp;
    private SerializedProperty spawnCoconutPalmProp;
    private SerializedProperty spawnJungleCastanopsisProp;
    private SerializedProperty spawnPalmProp;
    private SerializedProperty spawnShrubElephantGrassProp;
    private SerializedProperty spawnShrubSwordFernProp;

    private SerializedProperty spawnMagnoliaProp;
    private SerializedProperty spawnPlumProp;
    private SerializedProperty spawnGinkgoProp;
    private SerializedProperty spawnMapleProp;
    private SerializedProperty spawnWillowProp;
    private SerializedProperty spawnScholarProp;
    private SerializedProperty spawnShrubMagnoliaProp;
    private SerializedProperty spawnShrubRhododendronProp;

    private SerializedProperty spawnShrubLotusProp;
    private SerializedProperty spawnShrubRiceProp;
    private SerializedProperty spawnShrubMilletProp;

    private SerializedProperty spawnShrubPineProp;
    private SerializedProperty spawnShrubFernProp;
    private SerializedProperty spawnShrubScholarProp;
    private SerializedProperty spawnShrubConvallariaProp;

    private SerializedProperty spawnPineDeadProp;
    private SerializedProperty spawnBrokenPineProp;
    private SerializedProperty spawnBrokenScholarProp;
    private SerializedProperty spawnDeadCastanopsisProp;
    private SerializedProperty spawnJungleDeadProp;
    private SerializedProperty spawnBambooStumpProp;
    private SerializedProperty spawnStumpProp;
    private SerializedProperty spawnBrokenBranchesProp;

    private SerializedProperty autoGenerateOnStartProp;
    private SerializedProperty treeCountProp;
    private SerializedProperty bushCountProp;
    private SerializedProperty treeScaleMultiplierProp;
    private SerializedProperty bushScaleMultiplierProp;
    private SerializedProperty randomSeedProp;

    private SerializedProperty baseGroundSinkDepthProp;
    private SerializedProperty slopeAlignmentWeightProp;
    private SerializedProperty maxSlopeAngleProp;

    private SerializedProperty stealthDetectionRangeProp;
    private SerializedProperty runningDetectionRangeProp;
    private SerializedProperty blockLineOfSightProp;

    private SerializedProperty enableWinterSnowProp;
    private SerializedProperty winterSnowAmountProp;
    private SerializedProperty leafSnowRatioProp;

    private SerializedProperty enableFireVFXProp;
    private SerializedProperty enableWildfirePropagationProp;
    private SerializedProperty basePropagationRadiusProp;
    private SerializedProperty windSpreadMultiplierProp;
    private SerializedProperty isForestRevealedByFireProp;
    private SerializedProperty burningTreesCountProp;
    private SerializedProperty flameScaleMultiplierProp;
    private SerializedProperty smokeScaleMultiplierProp;
    private SerializedProperty smokeVisualTypeProp;
    private SerializedProperty smokeDensityProp;
    private SerializedProperty smokeStartColorProp;
    private SerializedProperty smokeEndColorProp;

    private void OnEnable()
    {
        shapeProp = serializedObject.FindProperty("shape");
        themeProp = serializedObject.FindProperty("theme");
        currentSeasonProp = serializedObject.FindProperty("currentSeason");
        radiusProp = serializedObject.FindProperty("radius");
        boxSizeProp = serializedObject.FindProperty("boxSize");

        spawnHuangshanPineProp = serializedObject.FindProperty("spawnHuangshanPine");
        spawnPineProp = serializedObject.FindProperty("spawnPine");
        spawnBambooProp = serializedObject.FindProperty("spawnBamboo");
        spawnCastanopsisProp = serializedObject.FindProperty("spawnCastanopsis");
        spawnCamphorProp = serializedObject.FindProperty("spawnCamphor");
        spawnShrubTeaProp = serializedObject.FindProperty("spawnShrubTea");

        spawnFicusProp = serializedObject.FindProperty("spawnFicus");
        spawnBananaPalmProp = serializedObject.FindProperty("spawnBananaPalm");
        spawnCoconutPalmProp = serializedObject.FindProperty("spawnCoconutPalm");
        spawnJungleCastanopsisProp = serializedObject.FindProperty("spawnJungleCastanopsis");
        spawnPalmProp = serializedObject.FindProperty("spawnPalm");
        spawnShrubElephantGrassProp = serializedObject.FindProperty("spawnShrubElephantGrass");
        spawnShrubSwordFernProp = serializedObject.FindProperty("spawnShrubSwordFern");

        spawnMagnoliaProp = serializedObject.FindProperty("spawnMagnolia");
        spawnPlumProp = serializedObject.FindProperty("spawnPlum");
        spawnGinkgoProp = serializedObject.FindProperty("spawnGinkgo");
        spawnMapleProp = serializedObject.FindProperty("spawnMaple");
        spawnWillowProp = serializedObject.FindProperty("spawnWillow");
        spawnScholarProp = serializedObject.FindProperty("spawnScholar");
        spawnShrubMagnoliaProp = serializedObject.FindProperty("spawnShrubMagnolia");
        spawnShrubRhododendronProp = serializedObject.FindProperty("spawnShrubRhododendron");

        spawnShrubLotusProp = serializedObject.FindProperty("spawnShrubLotus");
        spawnShrubRiceProp = serializedObject.FindProperty("spawnShrubRice");
        spawnShrubMilletProp = serializedObject.FindProperty("spawnShrubMillet");

        spawnShrubPineProp = serializedObject.FindProperty("spawnShrubPine");
        spawnShrubFernProp = serializedObject.FindProperty("spawnShrubFern");
        spawnShrubScholarProp = serializedObject.FindProperty("spawnShrubScholar");
        spawnShrubConvallariaProp = serializedObject.FindProperty("spawnShrubConvallaria");

        spawnPineDeadProp = serializedObject.FindProperty("spawnPineDead");
        spawnBrokenPineProp = serializedObject.FindProperty("spawnBrokenPine");
        spawnBrokenScholarProp = serializedObject.FindProperty("spawnBrokenScholar");
        spawnDeadCastanopsisProp = serializedObject.FindProperty("spawnDeadCastanopsis");
        spawnJungleDeadProp = serializedObject.FindProperty("spawnJungleDead");
        spawnBambooStumpProp = serializedObject.FindProperty("spawnBambooStump");
        spawnStumpProp = serializedObject.FindProperty("spawnStump");
        spawnBrokenBranchesProp = serializedObject.FindProperty("spawnBrokenBranches");

        autoGenerateOnStartProp = serializedObject.FindProperty("autoGenerateOnStart");
        treeCountProp = serializedObject.FindProperty("treeCount");
        bushCountProp = serializedObject.FindProperty("bushCount");
        treeScaleMultiplierProp = serializedObject.FindProperty("treeScaleMultiplier");
        bushScaleMultiplierProp = serializedObject.FindProperty("bushScaleMultiplier");
        randomSeedProp = serializedObject.FindProperty("randomSeed");

        baseGroundSinkDepthProp = serializedObject.FindProperty("baseGroundSinkDepth");
        slopeAlignmentWeightProp = serializedObject.FindProperty("slopeAlignmentWeight");
        maxSlopeAngleProp = serializedObject.FindProperty("maxSlopeAngle");

        stealthDetectionRangeProp = serializedObject.FindProperty("stealthDetectionRange");
        runningDetectionRangeProp = serializedObject.FindProperty("runningDetectionRange");
        blockLineOfSightProp = serializedObject.FindProperty("blockLineOfSight");

        enableWinterSnowProp = serializedObject.FindProperty("enableWinterSnow");
        winterSnowAmountProp = serializedObject.FindProperty("winterSnowAmount");
        leafSnowRatioProp = serializedObject.FindProperty("leafSnowRatio");

        enableFireVFXProp = serializedObject.FindProperty("enableFireVFX");
        enableWildfirePropagationProp = serializedObject.FindProperty("enableWildfirePropagation");
        basePropagationRadiusProp = serializedObject.FindProperty("basePropagationRadius");
        windSpreadMultiplierProp = serializedObject.FindProperty("windSpreadMultiplier");
        isForestRevealedByFireProp = serializedObject.FindProperty("isForestRevealedByFire");
        burningTreesCountProp = serializedObject.FindProperty("burningTreesCount");
        flameScaleMultiplierProp = serializedObject.FindProperty("flameScaleMultiplier");
        smokeScaleMultiplierProp = serializedObject.FindProperty("smokeScaleMultiplier");
        smokeVisualTypeProp = serializedObject.FindProperty("smokeVisualType");
        smokeDensityProp = serializedObject.FindProperty("smokeDensity");
        smokeStartColorProp = serializedObject.FindProperty("smokeStartColor");
        smokeEndColorProp = serializedObject.FindProperty("smokeEndColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        TacticalForestZone zone = (TacticalForestZone)target;

        // =========================================================================
        // 1. 상단 원클릭 퀵 컨트롤 배너
        // =========================================================================
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("🌲 초목 즉시 재생성 (Regenerate Vegetation)", GUILayout.Height(36)))
        {
            Undo.RecordObject(zone, "Regenerate Vegetation");
            zone.GenerateVegetation();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🌸 봄 (Spring)", GUILayout.Height(25)))
        {
            Undo.RecordObject(zone, "Set Season Spring");
            zone.SetSeason(TacticalForestZone.Season.Spring);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("☀️ 여름 (Summer)", GUILayout.Height(25)))
        {
            Undo.RecordObject(zone, "Set Season Summer");
            zone.SetSeason(TacticalForestZone.Season.Summer);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("🍁 가을 (Autumn)", GUILayout.Height(25)))
        {
            Undo.RecordObject(zone, "Set Season Autumn");
            zone.SetSeason(TacticalForestZone.Season.Autumn);
            EditorUtility.SetDirty(zone);
        }
        if (GUILayout.Button("❄️ 겨울 (Winter)", GUILayout.Height(25)))
        {
            Undo.RecordObject(zone, "Set Season Winter");
            zone.SetSeason(TacticalForestZone.Season.Winter);
            EditorUtility.SetDirty(zone);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1.0f, 0.42f, 0.2f);
        if (GUILayout.Button("🔥 화재 점화 (Ignite Fire)", GUILayout.Height(26)))
        {
            Undo.RecordObject(zone, "Ignite Fire");
            zone.IgniteForestFire();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
        if (GUILayout.Button("🧯 화재 진압 (Extinguish)", GUILayout.Height(26)))
        {
            Undo.RecordObject(zone, "Extinguish Fire");
            zone.ExtinguishFire();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
        if (GUILayout.Button("🧹 전체 제거 (Clear)", GUILayout.Height(26)))
        {
            Undo.RecordObject(zone, "Clear Vegetation");
            zone.ClearVegetation();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // =========================================================================
        // 2. 🌐 구역 형태 및 크기 설정
        // =========================================================================
        foldoutShape = DrawSectionHeader("🌐 숲 구역 형태 및 크기 (Zone Shape & Size)", foldoutShape);
        if (foldoutShape)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(shapeProp, new GUIContent("구역 형태 (Shape)", "숲 구역의 형태를 원형(Circle) 또는 직사각형(Box)으로 지정합니다."));
            if (shapeProp.enumValueIndex == (int)TacticalForestZone.ZoneShape.Circle)
            {
                EditorGUILayout.PropertyField(radiusProp, new GUIContent("원형 반경 (Radius, m)", "원형 숲의 유효 반경(단위: m, 기본값 30m)."));
            }
            else
            {
                EditorGUILayout.PropertyField(boxSizeProp, new GUIContent("직사각형 크기 (Box Size, m)", "직사각형 숲 구역의 가로(X) 및 세로(Z) 크기(단위: m)."));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 3. 🍂 생태계 테마 및 사계절 환경
        // =========================================================================
        foldoutSeason = DrawSectionHeader("🍂 생태계 테마 및 사계절 설정 (Theme & Seasons)", foldoutSeason);
        if (foldoutSeason)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(themeProp, new GUIContent("지역 테마 프리셋 (Theme)", "원하는 전장 지역 테마를 선택하면 아래 수종 체크박스가 최적의 조합으로 자동 동기화됩니다."));
            EditorGUILayout.PropertyField(currentSeasonProp, new GUIContent("현재 계절 (Season)", "봄, 여름, 가을, 겨울 중 선택합니다. 숲을 다시 생성하지 않고도 나뭇잎 텍스처가 즉시 실시간 전환됩니다."));

            if (currentSeasonProp.enumValueIndex == (int)TacticalForestZone.Season.Winter)
            {
                EditorGUILayout.PropertyField(enableWinterSnowProp, new GUIContent("❄️ 겨울 적설 설경 연출", "체크 시 하얗게 눈이 쌓인 설목과 설경 텍스처를 적용합니다. 해제 시 앙상한 마른 가지 상태를 연출합니다."));
                if (enableWinterSnowProp.boolValue)
                {
                    EditorGUILayout.PropertyField(winterSnowAmountProp, new GUIContent("전체 눈 쌓임 강도 (Snow Amount)", "겨울철 눈이 쌓이는 두께와 강도를 조절합니다 (0 = 눈 없음, 1 = 폭설)."));
                    EditorGUILayout.Slider(leafSnowRatioProp, 0.0f, 1.0f, new GUIContent("❄️ 나뭇잎 눈 덮임 비율 (Leaf Snow Ratio)", "나뭇잎에 눈이 묻는 높이와 면적 비율을 조절합니다.\n- 0.0: 나뭇잎에 눈 없음\n- 0.55 (권장): 나무 윗쪽(수관 상단) 잎사귀 표면에만 자연스럽게 적설\n- 1.0: 아래쪽 나뭇잎까지 전반적으로 눈 적설"));
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 4. 🌲 [온대/한대 상록수] 수종 토글
        // =========================================================================
        foldoutTemperate = DrawSectionHeader("🌲 온대/한대 상록수 (Temperate Evergreen)", foldoutTemperate);
        if (foldoutTemperate)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnHuangshanPineProp, new GUIContent("황산 소나무 (Huangshan Pine)", "굽이치는 가지와 넓은 솔잎 수관 (겨울 설화 대응)"));
            EditorGUILayout.PropertyField(spawnPineProp, new GUIContent("직립 소나무 (Straight Pine)", "하늘로 곧게 뻗은 정통 소나무 (겨울 설화 대응)"));
            EditorGUILayout.PropertyField(spawnBambooProp, new GUIContent("대나무 군락 (Bamboo)", "사계절 내내 푸른 청록색 대나무림 (빠른 전소)"));
            EditorGUILayout.PropertyField(spawnCastanopsisProp, new GUIContent("구실잣밤 활엽수 (Castanopsis)", "온대/난대의 울창한 상록 참나무과 활엽수"));
            EditorGUILayout.PropertyField(spawnCamphorProp, new GUIContent("녹나무 (Camphor)", "거대한 상록 활엽 거목 (장뇌유 폭발 연소)"));
            EditorGUILayout.PropertyField(spawnShrubTeaProp, new GUIContent("차나무 관목 (Tea Bush)", "중국 남방 명차 밭의 둥글고 푸른 상록 관목"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 5. 🌴 [열대/남만 상록 식생] 수종 토글
        // =========================================================================
        foldoutTropical = DrawSectionHeader("🌴 열대/남만 상록 식생 (Tropical & Nanman)", foldoutTropical);
        if (foldoutTropical)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnFicusProp, new GUIContent("남만 반얀트리 (Ficus Banyan)", "덩굴과 뿌리가 뒤엉킨 남만 밀림의 웅장한 거목"));
            EditorGUILayout.PropertyField(spawnBananaPalmProp, new GUIContent("남만 바나나 야자 (Banana Palm)", "남만 밀림의 거대한 열대 바나나 잎사귀"));
            EditorGUILayout.PropertyField(spawnCoconutPalmProp, new GUIContent("남만 코코넛 야자 (Coconut Palm)", "원시 밀림의 높게 뻗은 열대 코코넛 야자수"));
            EditorGUILayout.PropertyField(spawnJungleCastanopsisProp, new GUIContent("남만 정글 구실잣밤 (Jungle Oak)", "이끼와 덩굴이 얽힌 짙은 정글 수관"));
            EditorGUILayout.PropertyField(spawnPalmProp, new GUIContent("종려 야자 (Palm Windmill)", "남만 밀림 및 남방 전장의 부채꼴 야자수"));
            EditorGUILayout.PropertyField(spawnShrubElephantGrassProp, new GUIContent("열대 코끼리풀 (Elephant Grass)", "열대 정글 바닥을 무성하게 뒤덮는 거대 초본"));
            EditorGUILayout.PropertyField(spawnShrubSwordFernProp, new GUIContent("열대 검고사리 (Sword Fern)", "남만 밀림 숲속의 날렵한 열대 양치식물"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 6. 🍂 [낙엽수 & 꽃나무] 수종 토글
        // =========================================================================
        foldoutDeciduous = DrawSectionHeader("🍂 낙엽수 및 꽃나무 (Deciduous & Blossoms)", foldoutDeciduous);
        if (foldoutDeciduous)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnMagnoliaProp, new GUIContent("목련나무 거목 (Magnolia Tree)", "봄철 크고 화사한 백목련 꽃이 만개하는 중국 고유 거목"));
            EditorGUILayout.PropertyField(spawnPlumProp, new GUIContent("매화나무 (Plum Tree)", "봄철 분홍/백매화 꽃잎 만개, 가을 단풍"));
            EditorGUILayout.PropertyField(spawnGinkgoProp, new GUIContent("은행나무 (Ginkgo Tree)", "가을에 눈부신 황금빛 단풍을 자랑하는 중원 평야의 고목"));
            EditorGUILayout.PropertyField(spawnMapleProp, new GUIContent("단풍나무 (Maple Tree)", "가을에 불타는 선홍빛 단풍을 뽐내는 전장의 명목"));
            EditorGUILayout.PropertyField(spawnWillowProp, new GUIContent("수양버들 (Willow Tree)", "강남 수변의 늘어진 가지, 봄 연두빛 새싹, 가을 황록색"));
            EditorGUILayout.PropertyField(spawnScholarProp, new GUIContent("회화나무 / 선비나무 (Scholar Tree)", "봄 연두 새싹, 가을 고즈넉한 갈색 단풍, 겨울 설화"));
            EditorGUILayout.PropertyField(spawnShrubMagnoliaProp, new GUIContent("목련 관목 (Shrub Magnolia)", "숲 가장자리와 정원을 수놓는 목련 덤불"));
            EditorGUILayout.PropertyField(spawnShrubRhododendronProp, new GUIContent("진달래 / 철쭉 (Rhododendron)", "봄철 분홍빛 꽃잎이 화사하게 만개하는 꽃덤불"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 7. 🌾 [수변 & 농경지 식생]
        // =========================================================================
        foldoutCrops = DrawSectionHeader("🌾 수변 및 농경지 작물 (Wetlands & Farmland)", foldoutCrops);
        if (foldoutCrops)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnShrubLotusProp, new GUIContent("연꽃 수생식생 (Lotus)", "수변, 연못, 습지대에 넓게 펼쳐진 둥근 연잎과 연꽃"));
            EditorGUILayout.PropertyField(spawnShrubRiceProp, new GUIContent("벼 / 논작물 (Rice Crop)", "중원 및 강남 평야의 비옥한 논밭 벼 이삭"));
            EditorGUILayout.PropertyField(spawnShrubMilletProp, new GUIContent("기장 / 조 밭 (Millet Crop)", "북방 및 건조 평야의 거친 밭작물"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 8. 🌿 [지표 하층 관목 및 초본]
        // =========================================================================
        foldoutShrubs = DrawSectionHeader("🌿 지표 하층 관목 및 초본 (Understory Shrubs)", foldoutShrubs);
        if (foldoutShrubs)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnShrubPineProp, new GUIContent("솔관목 (Pine Shrub)", "소나무 숲 바닥에 자생하는 키 작은 침엽 관목"));
            EditorGUILayout.PropertyField(spawnShrubFernProp, new GUIContent("왕고사리 (Royal Fern)", "숲 바닥을 울창하게 덮는 양치식물 덤불"));
            EditorGUILayout.PropertyField(spawnShrubScholarProp, new GUIContent("선비덤불 (Scholar Bush)", "숲 가장자리와 길가에 풍성한 녹색 덤불"));
            EditorGUILayout.PropertyField(spawnShrubConvallariaProp, new GUIContent("은방울꽃 (Convallaria)", "지표면을 은은하게 수놓는 야생 야초"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 9. 🪵 [전장 고목, 그루터기 & 잔해]
        // =========================================================================
        foldoutRuins = DrawSectionHeader("🪵 전장 고목 및 그루터기 잔해 (Battlefield Clutter)", foldoutRuins);
        if (foldoutRuins)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnPineDeadProp, new GUIContent("소나무 고사목 (Dead Pine)", "잎사귀 없이 앙상한 줄기와 가지만 남은 마른 소나무 뼈대"));
            EditorGUILayout.PropertyField(spawnBrokenPineProp, new GUIContent("부러진 소나무 (Broken Pine)", "격전으로 줄기가 꺾이고 부러진 소나무 (일부 잔여 솔잎)"));
            EditorGUILayout.PropertyField(spawnBrokenScholarProp, new GUIContent("부러진 회화나무 (Broken Scholar)", "전장에서 부러진 회화나무 고목 뼈대 및 잔가지"));
            EditorGUILayout.PropertyField(spawnDeadCastanopsisProp, new GUIContent("앙상한 활엽 고사목 (Dead Oak)", "잎이 모두 떨어지고 비장하게 마른 전장의 활엽 고목"));
            EditorGUILayout.PropertyField(spawnJungleDeadProp, new GUIContent("남만 정글 고사목 (Jungle Dead)", "열대 덩굴과 이끼가 뒤엉킨 남만 밀림의 마른 고목"));
            EditorGUILayout.PropertyField(spawnBambooStumpProp, new GUIContent("대나무 밑동/군락 (Bamboo Stumps)", "잘려나간 대나무 밑동 및 그루터기 군락"));
            EditorGUILayout.PropertyField(spawnStumpProp, new GUIContent("거목 그루터기 (Tree Stumps)", "군용 목재로 벌목되었거나 잘려나간 거목 밑동"));
            EditorGUILayout.PropertyField(spawnBrokenBranchesProp, new GUIContent("부러진 나뭇가지 더미 (Broken Branches)", "지표면에 나뒹구는 부러진 굵은 나뭇가지 잔해"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 10. 🌿 [초목 스폰 밀도 및 크기 배율]
        // =========================================================================
        foldoutDensity = DrawSectionHeader("🌿 초목 스폰 밀도 및 크기 (Density & Scale)", foldoutDensity);
        if (foldoutDensity)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(autoGenerateOnStartProp, new GUIContent("시작 시 자동 생성 (Auto Generate)", "게임 씬 로드 시 초목을 자동으로 생성할지 여부"));
            EditorGUILayout.PropertyField(treeCountProp, new GUIContent("나무 스폰 개수 (Tree Count)", "숲 구역 내에 스폰할 거목의 총 개수"));
            EditorGUILayout.PropertyField(bushCountProp, new GUIContent("수풀 스폰 개수 (Bush Count)", "숲 구역 내에 스폰할 관목 및 지표 수풀의 총 개수"));
            EditorGUILayout.PropertyField(treeScaleMultiplierProp, new GUIContent("나무 크기 배율 (Tree Scale)", "토탈워 원본 35m 거목을 실전 전장 크기로 축소하는 배율 (기본: 0.28 -> 약 10m)"));
            EditorGUILayout.PropertyField(bushScaleMultiplierProp, new GUIContent("수풀 크기 배율 (Bush Scale)", "지표 수풀의 크기 배율 (기본: 0.32 -> 약 1.2m)"));
            EditorGUILayout.PropertyField(randomSeedProp, new GUIContent("무작위 배치 시드 (Random Seed)", "동일한 시드 번호에서는 항상 동일한 위치와 형태로 초목이 배치됩니다."));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 11. ⛰️ [지형 적응 및 뿌리 물리 매립]
        // =========================================================================
        foldoutTerrain = DrawSectionHeader("⛰️ 지형 적응 및 물리 매립 (Terrain Anchoring)", foldoutTerrain);
        if (foldoutTerrain)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(baseGroundSinkDepthProp, new GUIContent("기본 지면 매립 깊이 (Sink Depth, m)", "경사지에서 뿌리가 공중에 뜨지 않도록 땅속으로 깊게 파묻는 기본 깊이 (단위: m)"));
            EditorGUILayout.PropertyField(slopeAlignmentWeightProp, new GUIContent("지형 경사 정렬 가중치 (Slope Align)", "나무가 지형의 기울기에 맞춰 자연스럽게 기울어지는 정도 (0 = 완전 수직, 1 = 경사와 100% 일치)"));
            EditorGUILayout.PropertyField(maxSlopeAngleProp, new GUIContent("최대 허용 경사각 (Max Slope Angle, 도)", "이 각도 이상의 가파른 절벽에는 나무가 스폰되지 않도록 제한합니다."));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 12. 👁️ [전술 은폐 및 시야 차폐]
        // =========================================================================
        foldoutStealth = DrawSectionHeader("👁️ 전술 은폐 및 시야 차폐 (Stealth & LoS)", foldoutStealth);
        if (foldoutStealth)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(stealthDetectionRangeProp, new GUIContent("기본 피탐지 거리 (Stealth Range, m)", "숲 내부에 매복한 유닛이 적에게 발각되는 기본 탐지 거리 (기본: 25m)"));
            EditorGUILayout.PropertyField(runningDetectionRangeProp, new GUIContent("질주 시 발각 거리 (Running Range, m)", "숲 내부 유닛이 전력질주(구보)할 때 발각되는 확장 거리 (기본: 45m)"));
            EditorGUILayout.PropertyField(blockLineOfSightProp, new GUIContent("사선 차폐 활성화 (Block LoS)", "숲을 가로지르는 원거리 사선 및 시야를 완벽하게 차폐할지 여부"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // =========================================================================
        // 13. 🔥 [삼탈워 전장 화재 및 연기 시뮬레이션]
        // =========================================================================
        foldoutFire = DrawSectionHeader("🔥 삼탈워 전장 화재 & 연기 시뮬레이션 (Fire & Smoke)", foldoutFire);
        if (foldoutFire)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(enableFireVFXProp, new GUIContent("화재 시뮬레이션 활성화 (Enable Fire)", "숲 화재 시스템 활성화 여부"));
            EditorGUILayout.PropertyField(smokeVisualTypeProp, new GUIContent("연기 유형 프리셋 (Smoke Type Preset)", "삼탈워 원본 웜 차콜, 토탈워 잿빛, 흑갈색, 수종별 자동, 또는 사용자 지정 색상 중 선택합니다."));
            EditorGUILayout.Slider(smokeScaleMultiplierProp, 0.2f, 3.0f, new GUIContent("연기 크기 배율 (Smoke Scale)", "화재 연기의 부피 및 크기 배율을 조절합니다 (0.2 = 아담한 훈연, 1.0 = 삼탈워 기본 크기, 3.0 = 거대 연기 기둥)."));
            EditorGUILayout.Slider(smokeDensityProp, 0.1f, 1.0f, new GUIContent("연기 진하기 / 농도 (Smoke Density)", "화재 연기의 불투명도 및 밀도 진하기를 조절합니다 (0.1 = 매우 연한 훈연, 0.75 = 삼탈워 표준, 1.0 = 아주 짙고 묵직한 연기)."));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("🎨 삼탈워 연기 색상 설정 및 저장 (Smoke Colors)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(smokeStartColorProp, new GUIContent("연기 시작 색상 (Start Color)", "나뭇잎 표면에서 막 피어오르는 연기 하단 색상 (삼탈워 원본 웜 차콜 기본값)."));
            EditorGUILayout.PropertyField(smokeEndColorProp, new GUIContent("연기 상공 색상 (End Color)", "하늘로 높이 피어오르며 흩어지는 연기 상단 색상 (삼탈워 원본 탄화 훈연 기본값)."));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🏮 삼탈워 원본 웜 차콜", GUILayout.Height(24)))
            {
                Undo.RecordObject(zone, "Apply Warm Charcoal Smoke");
                zone.ApplySmokeColorPreset(TacticalForestZone.SmokeVisualType.TotalWarWarmCharcoal);
                EditorUtility.SetDirty(zone);
            }
            if (GUILayout.Button("🌫️ 삼탈워 잿빛 훈연", GUILayout.Height(24)))
            {
                Undo.RecordObject(zone, "Apply Ash Gray Smoke");
                zone.ApplySmokeColorPreset(TacticalForestZone.SmokeVisualType.AshGray);
                EditorUtility.SetDirty(zone);
            }
            if (GUILayout.Button("⬛ 삼탈워 흑갈색 훈연", GUILayout.Height(24)))
            {
                Undo.RecordObject(zone, "Apply Pitch Black Smoke");
                zone.ApplySmokeColorPreset(TacticalForestZone.SmokeVisualType.PitchBlack);
                EditorUtility.SetDirty(zone);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(burningTreesCountProp, new GUIContent("동시 착화 나무 수 (Burning Trees)", "화재 발생 시 동시에 불이 붙는 나무의 최대 개수 (1~20그루)"));
            EditorGUILayout.PropertyField(flameScaleMultiplierProp, new GUIContent("화염 크기 배율 (Flame Scale)", "나뭇잎 표면에서 타오르는 주황색 화염 파티클의 크기 배율"));
            EditorGUILayout.PropertyField(enableWildfirePropagationProp, new GUIContent("풍향 연동 연쇄 화재 전파", "바람이 불어가는 방향으로 이웃 나무들에 불길이 번지는 시뮬레이션 활성화"));
            EditorGUILayout.PropertyField(basePropagationRadiusProp, new GUIContent("기본 전파 반경 (Spread Radius, m)", "바람이 없을 때 화재가 전파되는 기본 유효 반경 (단위: m)"));
            EditorGUILayout.PropertyField(windSpreadMultiplierProp, new GUIContent("바람 가속 배율 (Wind Spread Multiplier)", "순풍을 타고 화재 전파가 가속되는 배율"));
            EditorGUILayout.PropertyField(isForestRevealedByFireProp, new GUIContent("숲 은폐 강제 해제 상태", "숲의 40% 이상이 전소되어 은폐 및 사선 차폐가 영구 해제되었는지 여부"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(10);

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(zone);
        }
    }

    /// <summary>
    /// 세련된 색상의 접이식 섹션 헤더 렌더링 헬퍼
    /// </summary>
    private static bool DrawSectionHeader(string title, bool state)
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        Rect rect = GUILayoutUtility.GetRect(16f, 22f, headerStyle);
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.22f, 0.26f, 0.35f));
        return EditorGUI.Foldout(rect, state, title, true, headerStyle);
    }
}
