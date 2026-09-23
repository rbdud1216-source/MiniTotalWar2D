using UnityEditor;
using UnityEngine;

/// <summary>
/// Squad 컴포넌트의 유니티 인스펙터를 직관적인 한국어 라벨, 친절한 전술 툴팁 및 실시간 실효 스탯 모니터링으로 제공하는 전용 커스텀 에디터입니다.
/// </summary>
[CustomEditor(typeof(Squad))]
[CanEditMultipleObjects]
public class SquadEditor : Editor
{
    // 1. 부대 기본 정보
    private SerializedProperty squadName;
    private SerializedProperty isPlayer;

    // 2. 부대원 수 및 속도
    private SerializedProperty initialUnitCount;
    private SerializedProperty currentAliveCount;
    private SerializedProperty isRunning;
    private SerializedProperty walkSpeed;
    private SerializedProperty runSpeed;
    private SerializedProperty targetSpeed;
    private SerializedProperty unitMaxSpeed;

    // 3. 대형 간격 및 산개도 임계값
    private SerializedProperty spacing;
    private SerializedProperty spacingX;
    private SerializedProperty spacingZ;
    private SerializedProperty useStaggeredFormation;
    private SerializedProperty rowReactionDelay;
    private SerializedProperty looseSpacingThreshold;
    private SerializedProperty tightSpacingThreshold;

    // 4. 방진별 스탯 계수
    private SerializedProperty normalBonus;
    private SerializedProperty wedgeBonus;
    private SerializedProperty squareBonus;
    private SerializedProperty circleBonus;
    private SerializedProperty diamondBonus;

    // 5. 접촉 방어 태세 (V키)
    private SerializedProperty bracingArmorBonus;
    private SerializedProperty bracingMassMultiplier;

    // 6. 기준 기본 스탯
    private SerializedProperty baseArmor;
    private SerializedProperty baseMass;
    private SerializedProperty baseAttackCooldown;

    // 7. 진형 및 명령 상태
    private SerializedProperty currentFormationType;
    private SerializedProperty isLooseFormation;
    private SerializedProperty currentColumns;
    private SerializedProperty currentCommandState;
    private SerializedProperty currentStance;
    private SerializedProperty autoAttackEnabled;
    private SerializedProperty maxRotationSpeed;

    // 8. 방진 기하학 배율
    private SerializedProperty formationWidthMultiplier;
    private SerializedProperty formationLengthMultiplier;
    private SerializedProperty formationLayers;
    private SerializedProperty hollowRadiusMultiplier;
    private SerializedProperty formationLayersSpacingMultiplier;

    // UI 접기/펼치기 상태 플래그
    private bool showFormationModifiers = true;
    private bool showGeometrySettings = true;
    private bool showBaseStats = true;

    private void OnEnable()
    {
        squadName = serializedObject.FindProperty("squadName");
        isPlayer = serializedObject.FindProperty("isPlayer");

        initialUnitCount = serializedObject.FindProperty("initialUnitCount");
        currentAliveCount = serializedObject.FindProperty("currentAliveCount");
        isRunning = serializedObject.FindProperty("isRunning");
        walkSpeed = serializedObject.FindProperty("walkSpeed");
        runSpeed = serializedObject.FindProperty("runSpeed");
        targetSpeed = serializedObject.FindProperty("targetSpeed");
        unitMaxSpeed = serializedObject.FindProperty("unitMaxSpeed");

        spacing = serializedObject.FindProperty("spacing");
        spacingX = serializedObject.FindProperty("spacingX");
        spacingZ = serializedObject.FindProperty("spacingZ");
        useStaggeredFormation = serializedObject.FindProperty("useStaggeredFormation");
        rowReactionDelay = serializedObject.FindProperty("rowReactionDelay");
        looseSpacingThreshold = serializedObject.FindProperty("looseSpacingThreshold");
        tightSpacingThreshold = serializedObject.FindProperty("tightSpacingThreshold");

        normalBonus = serializedObject.FindProperty("normalBonus");
        wedgeBonus = serializedObject.FindProperty("wedgeBonus");
        squareBonus = serializedObject.FindProperty("squareBonus");
        circleBonus = serializedObject.FindProperty("circleBonus");
        diamondBonus = serializedObject.FindProperty("diamondBonus");

        bracingArmorBonus = serializedObject.FindProperty("bracingArmorBonus");
        bracingMassMultiplier = serializedObject.FindProperty("bracingMassMultiplier");

        baseArmor = serializedObject.FindProperty("baseArmor");
        baseMass = serializedObject.FindProperty("baseMass");
        baseAttackCooldown = serializedObject.FindProperty("baseAttackCooldown");

        currentFormationType = serializedObject.FindProperty("currentFormationType");
        isLooseFormation = serializedObject.FindProperty("isLooseFormation");
        currentColumns = serializedObject.FindProperty("currentColumns");
        currentCommandState = serializedObject.FindProperty("currentCommandState");
        currentStance = serializedObject.FindProperty("currentStance");
        autoAttackEnabled = serializedObject.FindProperty("autoAttackEnabled");
        maxRotationSpeed = serializedObject.FindProperty("maxRotationSpeed");

        formationWidthMultiplier = serializedObject.FindProperty("formationWidthMultiplier");
        formationLengthMultiplier = serializedObject.FindProperty("formationLengthMultiplier");
        formationLayers = serializedObject.FindProperty("formationLayers");
        hollowRadiusMultiplier = serializedObject.FindProperty("hollowRadiusMultiplier");
        formationLayersSpacingMultiplier = serializedObject.FindProperty("formationLayersSpacingMultiplier");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Squad squad = (Squad)target;

        // 🛡️ [실시간 실효 스탯 요약 박스]
        DrawLiveStatSummary(squad);

        EditorGUILayout.Space(8);

        // 1. 부대 기본 식별 정보
        EditorGUILayout.LabelField("🚩 부대 기본 정보", EditorStyles.boldLabel);
        if (squadName != null)
            EditorGUILayout.PropertyField(squadName, new GUIContent("부대 명칭", "UI 및 전황 로그에 표시되는 부대의 고유 이름입니다."));
        if (isPlayer != null)
            EditorGUILayout.PropertyField(isPlayer, new GUIContent("플레이어 진영 여부", "체크 시 아군(Player), 체크 해제 시 적군(Enemy) 진영으로 설정됩니다."));

        EditorGUILayout.Space(6);

        // 2. 부대원 수 및 속도 설정
        EditorGUILayout.LabelField("👥 병력 수 및 제식 속도 설정", EditorStyles.boldLabel);
        if (initialUnitCount != null)
            EditorGUILayout.PropertyField(initialUnitCount, new GUIContent("초기 창설 병력 수", "부대 생성 시 초기 총 인원수입니다."));
        if (currentAliveCount != null)
            EditorGUILayout.PropertyField(currentAliveCount, new GUIContent("현재 생존 병력 수", "실시간으로 생존해 있는 부대원 수입니다. (전멸 시 0)"));
        if (walkSpeed != null)
            EditorGUILayout.PropertyField(walkSpeed, new GUIContent("제식 보행 속도 (m/s)", "대형을 유지하며 평상시 제식으로 걸어갈 때의 부대 기준 속도입니다. (기본 1.0m/s)"));
        if (runSpeed != null)
            EditorGUILayout.PropertyField(runSpeed, new GUIContent("전술 구보 속도 (m/s)", "R키를 눌러 달리기(구보) 모드일 때의 부대 기준 속도입니다. (기본 2.4m/s)"));
        if (unitMaxSpeed != null)
            EditorGUILayout.PropertyField(unitMaxSpeed, new GUIContent("병사 최대 추격 속도 (m/s)", "대형에서 뒤처진 병사가 제자리를 찾기 위해 가속할 수 있는 물리적 최고 한계 속도입니다. (기본 5.5m/s)"));
        if (isRunning != null)
            EditorGUILayout.PropertyField(isRunning, new GUIContent("현재 구보(달리기) 중", "현재 부대가 달리는 중인지 여부입니다. (R키로 토글 가능)"));

        EditorGUILayout.Space(6);

        // 3. 진형 및 전투 태세
        EditorGUILayout.LabelField("⚔️ 진형 및 지휘 상태", EditorStyles.boldLabel);
        if (currentFormationType != null)
            EditorGUILayout.PropertyField(currentFormationType, new GUIContent("현재 진형 형태", "부대가 취하고 있는 전술 진형입니다. (일반 일자진, 쐐기진, 사각방진, 원형진, 마름모진 등)"));
        if (isLooseFormation != null)
            EditorGUILayout.PropertyField(isLooseFormation, new GUIContent("산개 대형 여부 (Loose)", "체크 시 유닛 간격이 2배로 넓어지며 산개진이 활성화됩니다. 산개진에서는 방진 보너스가 0이 됩니다."));
        if (currentColumns != null)
            EditorGUILayout.PropertyField(currentColumns, new GUIContent("현재 가로 열(Columns) 수", "대형의 전면 가로 열 수입니다. 드래그나 Alt 조작으로 동적 확장/축소됩니다."));
        if (autoAttackEnabled != null)
            EditorGUILayout.PropertyField(autoAttackEnabled, new GUIContent("자동 요격 허용 (V키)", "체크 시 적 발견 시 자동 돌격, 해제 시 제자리에 무기를 박고 버티는 '접촉 방어 태세(Bracing)'가 발동됩니다."));
        if (currentCommandState != null)
            EditorGUILayout.PropertyField(currentCommandState, new GUIContent("현재 명령 상태", "부대의 지휘 상태 (Idle: 대기, Move: 이동, AttackMove: 공격 이동, MeleeEngaged: 백병전)."));

        EditorGUILayout.Space(6);

        // 4. 대형 간격 및 산개도 임계값
        EditorGUILayout.LabelField("📐 대형 간격 및 산개도 임계값 설정", EditorStyles.boldLabel);
        if (spacing != null)
            EditorGUILayout.PropertyField(spacing, new GUIContent("기본 격자 간격 (m)", "진형 슬롯 간의 기본 표준 간격입니다. (기본 1.15m)"));
        if (spacingX != null)
            EditorGUILayout.PropertyField(spacingX, new GUIContent("가로(횡) 간격 (m)", "좌우 병사 사이의 기본 간격입니다."));
        if (spacingZ != null)
            EditorGUILayout.PropertyField(spacingZ, new GUIContent("세로(종) 간격 (m)", "앞뒤 병사 사이의 기본 열 간격입니다."));
        if (useStaggeredFormation != null)
            EditorGUILayout.PropertyField(useStaggeredFormation, new GUIContent("체커보드 지그재그 배치", "체크 시 뒷열 병사가 앞열 병사 사이 빈틈에 엇갈려 서서 시야와 무기 거리를 확보합니다."));
        if (rowReactionDelay != null)
            EditorGUILayout.PropertyField(rowReactionDelay, new GUIContent("열 단위 순차 출발 지연 (초)", "전진/후진 시 앞열과 뒷열 간의 파동형 순차 출발 반응 시간입니다. (기본 0.08초)"));

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📏 산개도(밀집도) 판정 임계 간격", EditorStyles.miniBoldLabel);
        if (looseSpacingThreshold != null)
            EditorGUILayout.PropertyField(looseSpacingThreshold, new GUIContent("완전 산개 기준 간격 (m)", "평균 간격이 이 거리 이상으로 넓어지면 밀집도(Tightness)가 0%가 되어 밀집 보너스가 일절 적용되지 않습니다. (기본 2.2m)"));
        if (tightSpacingThreshold != null)
            EditorGUILayout.PropertyField(tightSpacingThreshold, new GUIContent("최대 밀집 기준 간격 (m)", "평균 간격이 이 거리 이하로 밀착되면 밀집도(Tightness)가 100% 최대로 적용되어 방어력과 무게가 극대화됩니다. (기본 0.7m)"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 5. 접촉 방어 태세 (V키) 보너스 설정
        EditorGUILayout.LabelField("🛡️ 접촉 방어 태세 (Bracing - V키) 보너스", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (bracingArmorBonus != null)
            EditorGUILayout.IntSlider(bracingArmorBonus, 0, 5000, new GUIContent("접촉 방어 추가 방어력", "V키가 꺼져 접촉 방어 모드일 때 추가 부여되는 방어력 (+만분율). 예: 2000 = +20.00% 추가 감쇄."));
        if (bracingMassMultiplier != null)
            EditorGUILayout.Slider(bracingMassMultiplier, 1.0f, 3.0f, new GUIContent("접촉 방어 무게 배율", "창이나 방패를 땅에 박고 버틸 때의 질량 배율. 1.8 = +80% 증가로 적의 돌격 충격을 완벽 흡수."));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 6. 기준 기본 스탯 (Base Stats)
        showBaseStats = EditorGUILayout.Foldout(showBaseStats, "📊 부대 기준 기본 스탯 (프리팹 동기화)", true);
        if (showBaseStats)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (baseArmor != null)
                EditorGUILayout.PropertyField(baseArmor, new GUIContent("기본 원본 방어력 (Base Armor)", "유닛 프리팹 원본의 방어력 (0 ~ 10,000 만분율)."));
            if (baseMass != null)
                EditorGUILayout.PropertyField(baseMass, new GUIContent("기본 원본 무게 (Base Mass)", "유닛 프리팹 원본의 기본 무게 (기본 100.0)."));
            if (baseAttackCooldown != null)
                EditorGUILayout.PropertyField(baseAttackCooldown, new GUIContent("기본 원본 공격 주기 (초)", "유닛 프리팹 원본의 기본 공격 쿨다운 (기본 1.0초)."));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // 7. 방진별 스탯 튜닝 (Formation Modifiers)
        showFormationModifiers = EditorGUILayout.Foldout(showFormationModifiers, "🎛️ 방진별 스탯 계수 튜닝 (기본 효과 vs 산개도 비례 효과)", true);
        if (showFormationModifiers)
        {
            if (normalBonus != null)
                DrawFormationModifier(normalBonus, "일반 방진 (Normal)", "표준적인 보병 방진으로 균형 잡힌 기본 방어력과 밀집 저지력을 제공합니다.");
            if (wedgeBonus != null)
                DrawFormationModifier(wedgeBonus, "쐐기진 (Wedge)", "돌격 특화 진형으로 전두부 무게를 집중시켜 적진을 쐐기처럼 뚫고 들어갑니다.");
            if (squareBonus != null)
                DrawFormationModifier(squareBonus, "사각방진 (Square)", "방패벽 특화 진형으로 사방의 기병 돌격을 저지하고 높은 방어력과 무게를 자랑합니다.");
            if (circleBonus != null)
                DrawFormationModifier(circleBonus, "원형진 (Circle)", "포위 상황 결사항전 진형으로 극도의 방어력과 무게를 발휘하지만, 협소한 공간으로 공속이 크게 둔화됩니다.");
            if (diamondBonus != null)
                DrawFormationModifier(diamondBonus, "마름모진 (Diamond)", "기동 돌파형 진형으로 기동성과 전선 분할 돌파에 최적화되어 있습니다.");
        }

        EditorGUILayout.Space(6);

        // 8. 방진 기하학 배율 (Geometry Multipliers)
        showGeometrySettings = EditorGUILayout.Foldout(showGeometrySettings, "📐 방진 기하학 배율 (Alt+드래그 연동)", true);
        if (showGeometrySettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (formationWidthMultiplier != null)
                EditorGUILayout.Slider(formationWidthMultiplier, 0.15f, 3.0f, new GUIContent("가로폭 배율 (Alt+우클릭)", "횡방향 대형 폭 배율입니다. 좁힐수록 가로 밀집도가 상승합니다."));
            if (formationLengthMultiplier != null)
                EditorGUILayout.Slider(formationLengthMultiplier, 0.15f, 3.0f, new GUIContent("세로길이 배율 (Alt+좌클릭)", "종방향 대형 깊이 배율입니다. 좁힐수록 전후 밀집도가 상승합니다."));
            if (formationLayers != null)
                EditorGUILayout.IntSlider(formationLayers, 1, 16, new GUIContent("방진 겹수 (Layers)", "사각방진 및 원형진의 두께(겹수)입니다."));
            if (formationLayersSpacingMultiplier != null)
                EditorGUILayout.Slider(formationLayersSpacingMultiplier, 0.3f, 4.0f, new GUIContent("방진 층간 간격 배율", "방진 겹과 겹 사이의 층간 거리 배율입니다."));
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();

        // 에디터 상에서 슬라이더 조절 시 즉시 실효 스탯 동기화 반영
        if (GUI.changed && !Application.isPlaying)
        {
            squad.ApplyFormationAndStanceModifiers();
        }
    }

    /// <summary>
    /// 방진별 2단계 스탯 설정 (기본 효과 + 산개도 비례 추가 효과)을 렌더링합니다.
    /// </summary>
    private void DrawFormationModifier(SerializedProperty prop, string formationName, string description)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"🛡️ {formationName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
        EditorGUILayout.Space(2);

        SerializedProperty baseArmor = prop.FindPropertyRelative("baseArmorBonus");
        SerializedProperty baseMass = prop.FindPropertyRelative("baseMassMultiplier");
        SerializedProperty densityArmor = prop.FindPropertyRelative("densityArmorBonus");
        SerializedProperty densityMass = prop.FindPropertyRelative("densityMassMultiplier");
        SerializedProperty densitySpeedRatio = prop.FindPropertyRelative("densityAttackSpeedRatio");

        EditorGUILayout.LabelField("  [1단계: 방진 형성 기본 효과 - 진형 전환 즉시 부여]", EditorStyles.miniBoldLabel);
        if (baseArmor != null)
            EditorGUILayout.IntSlider(baseArmor, 0, 5000, new GUIContent("    기본 추가 방어력", "이 방진으로 전환했을 때 진형 형태 자체로 기본 부여되는 방어력 (+만분율). 500 = +5.00%"));
        if (baseMass != null)
            EditorGUILayout.Slider(baseMass, 1.0f, 3.0f, new GUIContent("    기본 무게 배율", "이 방진으로 전환 시 기본 적용되는 무게 배율. 1.2 = +20% 증가로 돌격 저지력 향상."));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("  [2단계: 산개/밀집도 비례 추가 효과 - 밀집할수록 점진적 가산]", EditorStyles.miniBoldLabel);
        if (densityArmor != null)
            EditorGUILayout.IntSlider(densityArmor, 0, 5000, new GUIContent("    최대 밀집 추가 방어력", "대형이 최대로 좁혀졌을 때(초밀집) 추가 가산되는 최대 방어력 (+만분율). 1000 = +10.00% 추가."));
        if (densityMass != null)
            EditorGUILayout.Slider(densityMass, 1.0f, 4.0f, new GUIContent("    최대 밀집 무게 배율", "대형이 최대로 좁혀졌을 때(초밀집) 추가 적용되는 최종 무게 배율. 1.5 = +50% 추가."));
        if (densitySpeedRatio != null)
        {
            float ratio = densitySpeedRatio.floatValue;
            float slowPercent = Mathf.Max(0f, (1.0f - ratio) * 100f);
            float aps = 1.0f * ratio; // 기본 1.0초 기준 초당 공격 횟수
            float cd = (ratio > 0.01f) ? (1.0f / ratio) : 10.0f;

            EditorGUILayout.Slider(densitySpeedRatio, 0.1f, 1.0f, new GUIContent($"    🔻 최대 밀집 시 공속 둔화 (남은 속도)", $"대형이 최대로 밀집했을 때의 최종 공격 속도 비율입니다. 현재 설정: {ratio * 100:F0}% 속도 (-{slowPercent:F0}% 둔화)."));
            EditorGUILayout.LabelField($"       ↳ 🔻 밀집 패널티: {slowPercent:F0}% 둔화 (공속 {ratio * 100:F0}%) | 초당 {aps:F2}회 타격 ({cd:F2}초당 1회)", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    /// <summary>
    /// 현재 부대의 실시간 실효 스탯 요약 상태를 박스로 시각화합니다.
    /// </summary>
    private void DrawLiveStatSummary(Squad squad)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        float tightness = squad.GetFormationTightness();
        float effSpacingX = squad.spacingX * squad.formationWidthMultiplier;
        float effSpacingZ = squad.spacingZ * squad.formationLengthMultiplier;
        float avgSpacing = (effSpacingX + effSpacingZ) * 0.5f;

        // 기준 스탯
        int bArmor = squad.baseArmor;
        float bMass = squad.baseMass;
        float bCd = squad.baseAttackCooldown;

        Squad.FormationStatModifier mod = squad.GetFormationModifier(squad.currentFormationType);
        bool isLoose = (squad.isLooseFormation || squad.currentFormationType == SquadFormationType.Loose);

        int formArmor = isLoose ? 0 : (mod.baseArmorBonus + Mathf.RoundToInt(mod.densityArmorBonus * tightness));
        int stanceArmor = (!squad.autoAttackEnabled) ? squad.bracingArmorBonus : 0;
        int totalArmor = Mathf.Clamp(bArmor + formArmor + stanceArmor, 0, 10000);
        float damageReduction = totalArmor / 100.0f;

        float formMassMult = isLoose ? 1.0f : (mod.baseMassMultiplier + ((mod.densityMassMultiplier - 1.0f) * tightness));
        float stanceMassMult = (!squad.autoAttackEnabled) ? squad.bracingMassMultiplier : 1.0f;
        float totalMass = bMass * formMassMult * stanceMassMult;

        float targetSpeedRatio = (mod.densityAttackSpeedRatio > 0.05f) ? mod.densityAttackSpeedRatio : 0.50f;
        float currentSpeedRatio = isLoose ? 1.0f : Mathf.Lerp(1.0f, targetSpeedRatio, tightness);
        float totalCd = bCd / Mathf.Max(0.01f, currentSpeedRatio);
        float attackSpeedPercent = currentSpeedRatio * 100.0f;
        float currentAps = 1.0f / Mathf.Max(0.01f, totalCd);
        float currentSlowdownPercent = Mathf.Max(0f, (1.0f - currentSpeedRatio) * 100.0f);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.2f, 0.8f, 1.0f) }
        };

        EditorGUILayout.LabelField("📊 [실시간 실효 스탯 요약]", headerStyle);
        EditorGUILayout.LabelField($"• 현재 진형: {squad.currentFormationType} {(isLoose ? "(산개 상태 - 보너스 0%)" : "")}");
        EditorGUILayout.LabelField($"• 평균 간격: {avgSpacing:F2}m (산개 기준: {squad.looseSpacingThreshold:F1}m / 밀집 기준: {squad.tightSpacingThreshold:F1}m)");
        EditorGUILayout.LabelField($"• 현재 밀집도 (Tightness): {(tightness * 100f):F1}%");

        EditorGUILayout.Space(2);
        string stanceText = (!squad.autoAttackEnabled) ? " [접촉 방어 태세 가산 중]" : "";
        EditorGUILayout.LabelField($"🛡️ 실효 방어력: {totalArmor} (피해 감쇄율: {damageReduction:F2}%){stanceText}");
        EditorGUILayout.LabelField($"⚖️ 실효 무게 (돌격 저지력): {totalMass:F1} (기본 대비 {(totalMass / Mathf.Max(1f, bMass) * 100f):F0}%)");

        string speedStatusText = currentSlowdownPercent > 0.1f
            ? $"[🔻 {currentSlowdownPercent:F1}% 둔화됨 (공속 {attackSpeedPercent:F1}% / 초당 {currentAps:F2}회 타격)]"
            : $"[정상 속도 (초당 {currentAps:F2}회 타격 / 100%)]";
        EditorGUILayout.LabelField($"⏱️ 실효 공격 주기: {totalCd:F2}초 {speedStatusText}");

        EditorGUILayout.EndVertical();
    }
}
