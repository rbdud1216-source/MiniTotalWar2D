using UnityEditor;
using UnityEngine;

/// <summary>
/// Unit 컴포넌트의 유니티 인스펙터를 직관적인 한국어 라벨, 전술 설명 툴팁, 실시간 전투 스탯 요약 박스로 시각화하는 커스텀 에디터입니다.
/// </summary>
[CustomEditor(typeof(Unit))]
[CanEditMultipleObjects]
public class UnitEditor : Editor
{
    private SerializedProperty unitType;
    private SerializedProperty isRangedUnit;
    private SerializedProperty canFireWhileMoving;
    private SerializedProperty maxAmmo;
    private SerializedProperty currentAmmo;
    private SerializedProperty rangedAttackRange;
    private SerializedProperty optimalRange;
    private SerializedProperty rangedMinRange;
    private SerializedProperty rangedBaseDamage;
    private SerializedProperty minDamageRatioAtMax;
    private SerializedProperty rangedAttackCooldown;
    private SerializedProperty minSpreadRadius;
    private SerializedProperty maxSpreadRadius;
    private SerializedProperty trajectoryMode;
    private SerializedProperty projectileSpeed;
    private SerializedProperty projectileDrag;
    private SerializedProperty gravityScale;
    private SerializedProperty launchPoint;
    private SerializedProperty launchOffset;
    private SerializedProperty ignoreArmor;
    private SerializedProperty armorPiercingRatio;
    private SerializedProperty armorShredAmount;
    private SerializedProperty meleeSwitchDistance;
    private SerializedProperty rangedKnockbackPower;

    // 원거리 3축 공간 및 사선 클리어런스
    private SerializedProperty minRearSpacing;
    private SerializedProperty minLateralSpacing;
    private SerializedProperty headClearanceMargin;
    private SerializedProperty allowStaggeredRank2DirectFire;

    // 권장 소속 부대 진형 및 사격 전술 설정
    private SerializedProperty overrideSquadFormationDefaults;
    private SerializedProperty recommendedSquadSpacingX;
    private SerializedProperty recommendedSquadSpacingZ;
    private SerializedProperty recommendedStaggeredFormation;
    private SerializedProperty recommendedSequentialFire;
    private SerializedProperty recommendedSequentialRowDelay;
    private static bool showSquadFormationDefaults = true;

    private SerializedProperty isPlayer;
    private SerializedProperty maxHp;
    private SerializedProperty currentHp;
    private SerializedProperty damage;
    private SerializedProperty armor;
    private SerializedProperty attackCooldown;
    private SerializedProperty attackRange;
    private SerializedProperty minAttackRange;
    private SerializedProperty optimalRangeMin;
    private SerializedProperty closeRangeDamageRatio;
    private SerializedProperty knockbackPower;
    private SerializedProperty combatStoppingDistance;
    private SerializedProperty engagementOffset;
    private SerializedProperty baseMeleeKnockback;
    private SerializedProperty maxMeleeKnockbackCap;

    private SerializedProperty useSidearm;
    private SerializedProperty sidearmSwitchDistance;
    private SerializedProperty sidearmAttackRange;
    private SerializedProperty sidearmDamage;
    private SerializedProperty sidearmAttackCooldown;
    private SerializedProperty sidearmKnockbackPower;
    private SerializedProperty sidearmBaseKnockback;
    private SerializedProperty sidearmMaxKnockbackCap;

    private SerializedProperty isRunning;
    private SerializedProperty walkSpeed;
    private SerializedProperty runSpeed;
    private SerializedProperty chargeSpeed;
    private SerializedProperty unitMaxSpeed;

    private SerializedProperty canReflectCharge;
    private SerializedProperty mass;
    private SerializedProperty chargeBonus;
    private SerializedProperty maxChargeDamage;
    private SerializedProperty isImmuneToKnockdown;
    private SerializedProperty knockdownSpeedThreshold;
    private SerializedProperty knockdownDuration;

    private SerializedProperty isSelected;
    private SerializedProperty currentState;
    private SerializedProperty currentStance;
    private SerializedProperty autoAttackEnabled;
    private SerializedProperty detectRange;

    private SerializedProperty reformThreshold;
    private SerializedProperty reformDelay;
    private SerializedProperty separationRadius;
    private SerializedProperty separationForce;

    private SerializedProperty mySquad;
    private SerializedProperty fixedTargetPos;

    private SerializedProperty alignToSlope;
    private SerializedProperty groundYOffset;

    // UI 폴드아웃 플래그
    private static bool showRangedWeapon = true;
    private static bool showSurvival = true;
    private static bool showPrimaryWeapon = true;
    private static bool showSidearm = false;
    private static bool showMobility = true;
    private static bool showImpact = true;
    private static bool showCommand = false;
    private static bool showFormation = false;
    private static bool showRuntime = false;
    private static bool showSlope = true;

    private void OnEnable()
    {
        unitType = serializedObject.FindProperty("unitType");
        isRangedUnit = serializedObject.FindProperty("isRangedUnit");
        canFireWhileMoving = serializedObject.FindProperty("canFireWhileMoving");
        maxAmmo = serializedObject.FindProperty("maxAmmo");
        currentAmmo = serializedObject.FindProperty("currentAmmo");
        rangedAttackRange = serializedObject.FindProperty("rangedAttackRange");
        optimalRange = serializedObject.FindProperty("optimalRange");
        rangedMinRange = serializedObject.FindProperty("rangedMinRange");
        rangedBaseDamage = serializedObject.FindProperty("rangedBaseDamage");
        minDamageRatioAtMax = serializedObject.FindProperty("minDamageRatioAtMax");
        rangedAttackCooldown = serializedObject.FindProperty("rangedAttackCooldown");
        minSpreadRadius = serializedObject.FindProperty("minSpreadRadius");
        maxSpreadRadius = serializedObject.FindProperty("maxSpreadRadius");
        trajectoryMode = serializedObject.FindProperty("trajectoryMode");
        projectileSpeed = serializedObject.FindProperty("projectileSpeed");
        projectileDrag = serializedObject.FindProperty("projectileDrag");
        gravityScale = serializedObject.FindProperty("gravityScale");
        launchPoint = serializedObject.FindProperty("launchPoint");
        launchOffset = serializedObject.FindProperty("launchOffset");
        ignoreArmor = serializedObject.FindProperty("ignoreArmor");
        armorPiercingRatio = serializedObject.FindProperty("armorPiercingRatio");
        armorShredAmount = serializedObject.FindProperty("armorShredAmount");
        meleeSwitchDistance = serializedObject.FindProperty("meleeSwitchDistance");
        rangedKnockbackPower = serializedObject.FindProperty("rangedKnockbackPower");

        minRearSpacing = serializedObject.FindProperty("minRearSpacing");
        minLateralSpacing = serializedObject.FindProperty("minLateralSpacing");
        headClearanceMargin = serializedObject.FindProperty("headClearanceMargin");
        allowStaggeredRank2DirectFire = serializedObject.FindProperty("allowStaggeredRank2DirectFire");

        overrideSquadFormationDefaults = serializedObject.FindProperty("overrideSquadFormationDefaults");
        recommendedSquadSpacingX = serializedObject.FindProperty("recommendedSquadSpacingX");
        recommendedSquadSpacingZ = serializedObject.FindProperty("recommendedSquadSpacingZ");
        recommendedStaggeredFormation = serializedObject.FindProperty("recommendedStaggeredFormation");
        recommendedSequentialFire = serializedObject.FindProperty("recommendedSequentialFire");
        recommendedSequentialRowDelay = serializedObject.FindProperty("recommendedSequentialRowDelay");

        isPlayer = serializedObject.FindProperty("isPlayer");
        maxHp = serializedObject.FindProperty("maxHp");
        currentHp = serializedObject.FindProperty("currentHp");
        damage = serializedObject.FindProperty("damage");
        armor = serializedObject.FindProperty("armor");
        attackCooldown = serializedObject.FindProperty("attackCooldown");
        attackRange = serializedObject.FindProperty("attackRange");
        minAttackRange = serializedObject.FindProperty("minAttackRange");
        optimalRangeMin = serializedObject.FindProperty("optimalRangeMin");
        closeRangeDamageRatio = serializedObject.FindProperty("closeRangeDamageRatio");
        knockbackPower = serializedObject.FindProperty("knockbackPower");
        combatStoppingDistance = serializedObject.FindProperty("combatStoppingDistance");
        engagementOffset = serializedObject.FindProperty("engagementOffset");
        baseMeleeKnockback = serializedObject.FindProperty("baseMeleeKnockback");
        maxMeleeKnockbackCap = serializedObject.FindProperty("maxMeleeKnockbackCap");

        useSidearm = serializedObject.FindProperty("useSidearm");
        sidearmSwitchDistance = serializedObject.FindProperty("sidearmSwitchDistance");
        sidearmAttackRange = serializedObject.FindProperty("sidearmAttackRange");
        sidearmDamage = serializedObject.FindProperty("sidearmDamage");
        sidearmAttackCooldown = serializedObject.FindProperty("sidearmAttackCooldown");
        sidearmKnockbackPower = serializedObject.FindProperty("sidearmKnockbackPower");
        sidearmBaseKnockback = serializedObject.FindProperty("sidearmBaseKnockback");
        sidearmMaxKnockbackCap = serializedObject.FindProperty("sidearmMaxKnockbackCap");

        isRunning = serializedObject.FindProperty("isRunning");
        walkSpeed = serializedObject.FindProperty("walkSpeed");
        runSpeed = serializedObject.FindProperty("runSpeed");
        chargeSpeed = serializedObject.FindProperty("chargeSpeed");
        unitMaxSpeed = serializedObject.FindProperty("unitMaxSpeed");

        canReflectCharge = serializedObject.FindProperty("canReflectCharge");
        mass = serializedObject.FindProperty("mass");
        chargeBonus = serializedObject.FindProperty("chargeBonus");
        maxChargeDamage = serializedObject.FindProperty("maxChargeDamage");
        isImmuneToKnockdown = serializedObject.FindProperty("isImmuneToKnockdown");
        knockdownSpeedThreshold = serializedObject.FindProperty("knockdownSpeedThreshold");
        knockdownDuration = serializedObject.FindProperty("knockdownDuration");

        isSelected = serializedObject.FindProperty("isSelected");
        currentState = serializedObject.FindProperty("currentState");
        currentStance = serializedObject.FindProperty("currentStance");
        autoAttackEnabled = serializedObject.FindProperty("autoAttackEnabled");
        detectRange = serializedObject.FindProperty("detectRange");

        reformThreshold = serializedObject.FindProperty("reformThreshold");
        reformDelay = serializedObject.FindProperty("reformDelay");
        separationRadius = serializedObject.FindProperty("separationRadius");
        separationForce = serializedObject.FindProperty("separationForce");

        mySquad = serializedObject.FindProperty("mySquad");
        fixedTargetPos = serializedObject.FindProperty("fixedTargetPos");

        alignToSlope = serializedObject.FindProperty("alignToSlope");
        groundYOffset = serializedObject.FindProperty("groundYOffset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Unit unit = (Unit)target;

        // 상단 실시간 전투 스탯 요약 박스 (단일 오브젝트 선택 시)
        if (!serializedObject.isEditingMultipleObjects && unit != null)
        {
            DrawLiveCombatSummary(unit);
            EditorGUILayout.Space(6);
        }

        // 유닛 병종 및 원거리 활성화 제어 스위치
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (unitType != null)
            EditorGUILayout.PropertyField(unitType, new GUIContent("병종 분류", "유닛의 병종 및 전술적 역할을 지정합니다."));

        if (isRangedUnit != null)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(isRangedUnit, new GUIContent("원거리(사격) 유닛 활성화", "체크 시 활 사격, 전용 탄약 및 투사체 물리 시스템이 활성화됩니다. 해제 시 순수 근접 보병으로 동작합니다."));
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);

        // [원거리 전용 설정창]: isRangedUnit이 체크되었을 때만 동적으로 노출
        if (isRangedUnit != null && isRangedUnit.boolValue)
        {
            showRangedWeapon = EditorGUILayout.Foldout(showRangedWeapon, "0. 원거리 무기 및 탄도학 (활/쇠뇌/총)", true);
            if (showRangedWeapon)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // A. 탄약 관리
                EditorGUILayout.LabelField("전용 탄약 관리", EditorStyles.miniBoldLabel);
                if (canFireWhileMoving != null)
                    EditorGUILayout.PropertyField(canFireWhileMoving, new GUIContent("이동 중 사격 허용", "체크 시 행군/이동 중에도 화살을 발사하며, 해제 시 제자리에 멈춰 서서만 일제사격을 수행합니다."));
                if (maxAmmo != null)
                    EditorGUILayout.PropertyField(maxAmmo, new GUIContent("최대 소지 화살 수", "병사 1인당 소지하는 화살 총량입니다. (단위: 발, 기본: 25발)"));
                if (currentAmmo != null)
                    EditorGUILayout.PropertyField(currentAmmo, new GUIContent("실시간 잔여 화살 수", "현재 남아있는 화살 수량입니다. (단위: 발, 0발 시 근접 보병으로 전환)"));

                EditorGUILayout.Space(4);
                // B. 사거리 및 대미지 감쇠
                EditorGUILayout.LabelField("사거리 및 거리별 대미지 감쇠", EditorStyles.miniBoldLabel);
                if (rangedAttackRange != null)
                    EditorGUILayout.PropertyField(rangedAttackRange, new GUIContent("최대 유효 사거리", "화살을 쏠 수 있는 최대 사거리입니다. (단위: m, 권장: 150m)"));
                if (optimalRange != null)
                    EditorGUILayout.PropertyField(optimalRange, new GUIContent("최적 사거리", "이 거리 안에서는 100% 명중 및 최대 대미지가 적용됩니다. (단위: m, 기본: 50m)"));
                if (rangedMinRange != null)
                    EditorGUILayout.PropertyField(rangedMinRange, new GUIContent("최소 사거리", "이 거리 미만으로 적이 다가오면 활 사격을 중단합니다. (단위: m, 기본: 5m)"));
                if (rangedBaseDamage != null)
                    EditorGUILayout.PropertyField(rangedBaseDamage, new GUIContent("원거리 기본 대미지", "화살 직격 시의 기본 공격력입니다. (기본: 15.0)"));
                if (minDamageRatioAtMax != null)
                    EditorGUILayout.Slider(minDamageRatioAtMax, 0.1f, 1.0f, new GUIContent("최대 사거리 최소 대미지 비율", "최대 사거리 끝에서 적용될 최소 대미지 비율입니다. (단위: 배, 기본: 0.55 = 55%)"));
                if (rangedAttackCooldown != null)
                    EditorGUILayout.PropertyField(rangedAttackCooldown, new GUIContent("사격 간격 쿨다운", "다음 일제사격까지의 대기 시간입니다. (단위: 초, 기본: 2.2초)"));

                EditorGUILayout.Space(4);
                // C. 명중률 및 탄착군 분산
                EditorGUILayout.LabelField("명중률 및 탄착군 분산", EditorStyles.miniBoldLabel);
                if (minSpreadRadius != null)
                    EditorGUILayout.PropertyField(minSpreadRadius, new GUIContent("근거리 오차 반경", "최적 사거리(50m) 이내에서의 탄착군 오차 반경입니다. (단위: m, 기본: 0.3m)"));
                if (maxSpreadRadius != null)
                    EditorGUILayout.PropertyField(maxSpreadRadius, new GUIContent("최대 사거리 오차 반경", "최대 사거리(150m)에서의 탄착군 오차 반경입니다. (단위: m, 기본: 3.5m)"));

                EditorGUILayout.Space(4);
                // D. 탄도학 및 비행 물리
                EditorGUILayout.LabelField("탄도학 및 비행 물리", EditorStyles.miniBoldLabel);
                if (trajectoryMode != null)
                    EditorGUILayout.PropertyField(trajectoryMode, new GUIContent("사격 궤적 모드", "HighArc: 하늘 높이 넘겨 쏘는 곡사(활), Flat: 낮고 빠르게 쏘는 직사(쇠뇌/총)"));
                if (projectileSpeed != null)
                    EditorGUILayout.PropertyField(projectileSpeed, new GUIContent("화살 초기 발사 속도", "화살이 시위를 떠날 때의 초기 물리 속력입니다. (단위: m/s, 기본: 30.0m/s)"));
                if (projectileDrag != null)
                    EditorGUILayout.Slider(projectileDrag, 0.0f, 1.0f, new GUIContent("화살 공기 저항 감속률", "비행 중 공기 저항(항력) 감속 비율입니다. (0 = 무저항 등속, 0.25 = 자연스러운 감속 후 착탄)"));
                if (gravityScale != null)
                    EditorGUILayout.PropertyField(gravityScale, new GUIContent("중력 포물선 높이 계수", "곡사 시 포물선 최고점 높이를 조절하는 배율입니다. (단위: 배, 기본: 1.0)"));

                EditorGUILayout.Space(4);
                // D-2. 원거리 무기 발사 위치 설정
                EditorGUILayout.LabelField("원거리 무기 발사 위치 (Muzzle Point)", EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();
                if (launchPoint != null)
                    EditorGUILayout.PropertyField(launchPoint, new GUIContent("발사점 빈 오브젝트", "씬 뷰에서 기즈모로 조절 가능한 자식 빈 오브젝트입니다."));
                if (launchOffset != null)
                    EditorGUILayout.PropertyField(launchOffset, new GUIContent("발사 위치 로컬 오프셋", "피벗(발) 기준 로컬 오프셋입니다. (단위: m, X:우측손, Y:어깨높이, Z:전방)"));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    if (unit.launchPoint != null)
                    {
                        unit.launchPoint.localPosition = unit.launchOffset;
                        EditorUtility.SetDirty(unit.launchPoint);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("발사점 오브젝트 자동 생성/연결", EditorStyles.miniButton))
                {
                    CreateOrAttachLaunchPoint(unit);
                }
                if (GUILayout.Button("표준 오프셋으로 리셋 (0.25, 0.15, 0.45)", EditorStyles.miniButton))
                {
                    ResetLaunchPointToDefault(unit);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                // E. 관통 및 특수 효과
                EditorGUILayout.LabelField("방어력 관통 및 피격 넉백", EditorStyles.miniBoldLabel);
                if (ignoreArmor != null)
                    EditorGUILayout.PropertyField(ignoreArmor, new GUIContent("방어력 완전 무시 (고정 피해)", "체크 시 대상의 방어력을 100% 무시하고 트루 데미지를 가합니다."));
                if (armorPiercingRatio != null)
                    EditorGUILayout.Slider(armorPiercingRatio, 0.0f, 1.0f, new GUIContent("방어력 관통 비율", "방어력을 무시하고 들어가는 데미지 비율입니다. (기본: 0.25 = 25% 관통)"));
                if (armorShredAmount != null)
                    EditorGUILayout.IntSlider(armorShredAmount, 0, 5000, new GUIContent("적 방어력 삭감 수치", "적중 시 적의 방어력을 영구/일시 삭감하는 수치입니다. (만분율 단위)"));
                if (rangedKnockbackPower != null)
                    EditorGUILayout.Slider(rangedKnockbackPower, 0.0f, 3.0f, new GUIContent("원거리 피격 넉백 세기", "화살 적중 시 피격된 유닛이 뒤로 밀려나는 물리 속도입니다. (단위: m/s, 기본: 0.35m/s)"));
                if (meleeSwitchDistance != null)
                    EditorGUILayout.PropertyField(meleeSwitchDistance, new GUIContent("근접 백병전 강제 전환 거리", "적이 이 거리 안으로 파고들면 즉시 활을 거두고 칼을 뽑아 백병전을 수행합니다. (단위: m, 기본: 5.0m)"));

                EditorGUILayout.Space(4);
                // F. 3축 공간 및 사선 클리어런스
                EditorGUILayout.LabelField("3축 공간 및 사선 클리어런스 (밀집도 & 언덕 사면)", EditorStyles.miniBoldLabel);
                if (minRearSpacing != null)
                    EditorGUILayout.PropertyField(minRearSpacing, new GUIContent("후방 최소 필요 거리", "백스윙/와인드업을 위한 뒷사람과의 최소 간격입니다. (단위: m, 활 0.8m, 총/쇠뇌 0.0m)"));
                if (minLateralSpacing != null)
                    EditorGUILayout.PropertyField(minLateralSpacing, new GUIContent("측면 최소 필요 거리", "활 시위 전개 및 견착 조준을 위한 좌우 최소 대형 간격입니다. (단위: m, 기본: 0.75m)"));
                if (headClearanceMargin != null)
                    EditorGUILayout.PropertyField(headClearanceMargin, new GUIContent("전열 머리 위 안전 여유고", "앞열 아군 머리 위를 넘길 수 있는 최소 사선 여유 높이입니다. (단위: m, 기본: 0.45m)"));
                if (allowStaggeredRank2DirectFire != null)
                    EditorGUILayout.PropertyField(allowStaggeredRank2DirectFire, new GUIContent("체커보드 2열 틈새 직사 허용", "체크 시 엇갈린(체커보드) 대형에서 2열 사수가 1열 병사 사이의 틈새로 직사 사격을 수행합니다."));

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        // 권장 소속 부대 진형 및 사격 전술 설정
        showSquadFormationDefaults = EditorGUILayout.Foldout(showSquadFormationDefaults, "권장 소속 부대 진형 및 사격 전술 설정", true);
        if (showSquadFormationDefaults)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (overrideSquadFormationDefaults != null)
                EditorGUILayout.PropertyField(overrideSquadFormationDefaults, new GUIContent("부대 진형 자동 동기화 활성화", "체크 시 이 유닛이 배속된 부대(Squad)의 대형 간격, 체커보드, 순차사격 수치를 아래 설정값으로 자동 동기화합니다."));

            if (overrideSquadFormationDefaults != null && overrideSquadFormationDefaults.boolValue)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("권장 대형 간격", EditorStyles.miniBoldLabel);
                if (recommendedSquadSpacingX != null)
                    EditorGUILayout.PropertyField(recommendedSquadSpacingX, new GUIContent("권장 가로(좌우) 간격", "부대원들 간의 기본 가로 간격입니다. (단위: m, 보병 1.0m, 궁병 1.1m 권장)"));
                if (recommendedSquadSpacingZ != null)
                    EditorGUILayout.PropertyField(recommendedSquadSpacingZ, new GUIContent("권장 세로(앞뒤) 간격", "부대 열(Row) 간의 기본 세로 간격입니다. (단위: m, 보병 1.0m, 장궁병 1.3m, 쇠뇌 0.8m)"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("권장 전술 및 사격 설정", EditorStyles.miniBoldLabel);
                if (recommendedStaggeredFormation != null)
                    EditorGUILayout.PropertyField(recommendedStaggeredFormation, new GUIContent("엇갈린 대형 권장", "체크 시 부대에 엇갈린 대형(2열 직사 사격 태세) 사용 권한이 부여됩니다. (원거리 유닛: 체크 권장)"));
                if (recommendedSequentialFire != null)
                    EditorGUILayout.PropertyField(recommendedSequentialFire, new GUIContent("순차 사격(Rolling Volley) 권장", "체크 시 부대원들이 제자리에서 전열부터 행별로 시간차를 두고 교대 사격합니다. (원거리 유닛: 체크 권장)"));
                if (recommendedSequentialRowDelay != null)
                    EditorGUILayout.PropertyField(recommendedSequentialRowDelay, new GUIContent("순차 사격 행간 지연 시간", "각 행(열) 사이의 발사 시간차입니다. (단위: 초, 기본: 0.4초)"));
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 1. 생존력 및 방어력
        showSurvival = EditorGUILayout.Foldout(showSurvival, "1. 생존력 및 방어력", true);
        if (showSurvival)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isPlayer != null)
                EditorGUILayout.PropertyField(isPlayer, new GUIContent("진영 소속 (플레이어/아군 여부)", "체크 시 아군(Player 진영), 해제 시 적군(Enemy 진영)으로 자동 분류됩니다."));

            if (maxHp != null)
                EditorGUILayout.PropertyField(maxHp, new GUIContent("최대 체력", "유닛의 최대 생명력입니다. (기본 보병 권장: 100)"));

            if (currentHp != null)
            {
                EditorGUILayout.PropertyField(currentHp, new GUIContent("현재 체력", "유닛의 실시간 생명력입니다. 0 이하가 되면 사망 처리됩니다."));
                float hpRatio = (maxHp != null && maxHp.floatValue > 0f) ? (currentHp.floatValue / maxHp.floatValue) : 1.0f;
                Rect r = EditorGUILayout.GetControlRect(false, 6);
                EditorGUI.ProgressBar(r, Mathf.Clamp01(hpRatio), "");
            }

            if (armor != null)
            {
                EditorGUILayout.IntSlider(armor, 0, 10000, new GUIContent("기본 방어력", "유닛 원본의 기본 방어력 (0 ~ 10,000 만분율). 10000 = 100% 완전 방어. 예: 2812 입력 시 28.12% 피해 감쇄."));
                float pct = (float)armor.intValue / 100f;
                EditorGUILayout.HelpBox($"기본 피해 감소율: {pct:F2}%\n↳ 적의 물리 타격 및 돌격 피해의 {100f - pct:F2}%만 체력에 적용됩니다.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 2. 주무기 및 교전 사거리
        showPrimaryWeapon = EditorGUILayout.Foldout(showPrimaryWeapon, "2. 주무기 및 교전 사거리", true);
        if (showPrimaryWeapon)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (damage != null)
                EditorGUILayout.PropertyField(damage, new GUIContent("주무기 공격력", "일반 백병전 근접 공격 1회당 기본 피해량입니다. (검병 권장: 10.0, 장창병 권장: 14.0)"));

            if (attackCooldown != null)
            {
                EditorGUILayout.PropertyField(attackCooldown, new GUIContent("공격 주기", "다음 공격을 휘두르기까지의 쿨다운 대기 시간입니다. (단위: 초, 기본: 1.0초)"));
                float cd = Mathf.Max(0.01f, attackCooldown.floatValue);
                float aps = 1.0f / cd;
                float dps = (damage != null ? damage.floatValue : 10f) * aps;
                EditorGUILayout.LabelField($"   ↳ 타격 속도: 초당 {aps:F2}회 타격 | 지속 화력(DPS): 초당 {dps:F1} 데미지", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("사거리 및 스위트스팟 (거리별 감쇠)", EditorStyles.miniBoldLabel);

            if (attackRange != null)
                EditorGUILayout.PropertyField(attackRange, new GUIContent("최대 타격 사거리", "주무기로 적을 타격할 수 있는 최대 유효 사거리입니다. (단위: m, 검병: 1.45m, 장창병: 3.0m+)"));

            if (minAttackRange != null)
                EditorGUILayout.PropertyField(minAttackRange, new GUIContent("최소 사거리", "주무기 최소 사거리입니다. 이보다 가까운 적은 주무기로 타격할 수 없습니다. (단위: m, 검병: 0m, 장창병: 1.2m)"));

            if (optimalRangeMin != null)
                EditorGUILayout.PropertyField(optimalRangeMin, new GUIContent("최적 사거리 최소 기준", "이 거리 이상에서 100% 정상 데미지를 가합니다. (단위: m, 검병: 0m, 장창병: 1.8m)"));

            if (closeRangeDamageRatio != null)
                EditorGUILayout.Slider(closeRangeDamageRatio, 0.1f, 1.0f, new GUIContent("근접 파고듦 피해 감쇠 비율", "적에게 최적 사거리 미만(품 안)으로 접근을 허용했을 때의 피해 비율입니다. (검병: 1.0, 장창병: 0.35 권장)"));

            if (knockbackPower != null)
                EditorGUILayout.Slider(knockbackPower, 0.0f, 5.0f, new GUIContent("주무기 넉백 세기 배율", "타격 시 적을 뒤로 밀쳐내는 넉백 충격량 배율입니다. (단위: 배, 검병: 1.0, 장창병: 2.2 권장)"));

            if (baseMeleeKnockback != null)
                EditorGUILayout.PropertyField(baseMeleeKnockback, new GUIContent("평타 넉백 기본 속도", "돌격이 아닌 일반 백병전 평타 시 발생하는 기본 저지 넉백 속도입니다. (단위: m/s, 기본: 0.45m/s)"));

            if (maxMeleeKnockbackCap != null)
                EditorGUILayout.PropertyField(maxMeleeKnockbackCap, new GUIContent("다대일 평타 넉백 상한 속도", "여러 아군이 동시에 집중 공격할 때 누적될 수 있는 평타 넉백 속도 최대 상한선입니다. (단위: m/s, 기본: 1.2m/s)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("백병전 대형 정지 및 교전 간격", EditorStyles.miniBoldLabel);

            if (combatStoppingDistance != null)
                EditorGUILayout.PropertyField(combatStoppingDistance, new GUIContent("교전 정지 거리", "적과 백병전 시 발을 완전히 멈추고 제자리에서 공격하는 거리입니다. (단위: m, 검병: 1.05m, 장창병: 2.5m)"));

            if (engagementOffset != null)
                EditorGUILayout.PropertyField(engagementOffset, new GUIContent("교전 목표 간격", "적을 향해 전진할 때 적의 중심으로부터 유지하려는 목표 간격입니다. (단위: m, 검병: 0.40m, 장창병: 2.0m)"));

            EditorGUILayout.HelpBox("[병과 세팅 팁]\n• 검병: 사거리 1.45m / 최소사거리 0m / 근접감쇠 1.0 / 넉백 1.0 / 정지거리 1.05m\n• 장창병: 사거리 3.0m / 최소사거리 1.2m / 최적 1.8m / 근접감쇠 0.35 / 넉백 2.2 / 정지거리 2.5m", MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 3. 보조무기 자동 전환
        showSidearm = EditorGUILayout.Foldout(showSidearm, "3. 보조무기 자동 전환", true);
        if (showSidearm)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (useSidearm != null)
                EditorGUILayout.PropertyField(useSidearm, new GUIContent("보조무기 자동 전환 활성화", "적이 일정 거리 이내로 밀착 시 주무기 대신 보조무기로 자동 전환하여 대응할지 여부입니다."));

            if (useSidearm != null && useSidearm.boolValue)
            {
                if (sidearmSwitchDistance != null)
                    EditorGUILayout.PropertyField(sidearmSwitchDistance, new GUIContent("보조무기 전환 거리", "적과의 거리가 이 거리 이내로 좁혀지면 즉시 보조무기로 교체합니다. (단위: m, 권장: 1.2m)"));

                if (sidearmAttackRange != null)
                    EditorGUILayout.PropertyField(sidearmAttackRange, new GUIContent("보조무기 공격 사거리", "보조무기의 유효 타격 사거리입니다. (단위: m, 권장: 1.0m)"));

                if (sidearmDamage != null)
                    EditorGUILayout.PropertyField(sidearmDamage, new GUIContent("보조무기 타격 공격력", "보조무기 1회 타격 시 입히는 피해량입니다. (권장: 4.0)"));

                if (sidearmAttackCooldown != null)
                {
                    EditorGUILayout.PropertyField(sidearmAttackCooldown, new GUIContent("보조무기 공격 주기", "보조무기의 공격 쿨다운입니다. (단위: 초, 권장: 0.8초)"));
                    float sCd = Mathf.Max(0.01f, sidearmAttackCooldown.floatValue);
                    float sAps = 1.0f / sCd;
                    float sDps = (sidearmDamage != null ? sidearmDamage.floatValue : 4f) * sAps;
                    EditorGUILayout.LabelField($"   ↳ 보조무기 연타 속도: 초당 {sAps:F2}회 | 지속 화력: 초당 {sDps:F1} 데미지", EditorStyles.miniLabel);
                }

                if (sidearmKnockbackPower != null)
                    EditorGUILayout.Slider(sidearmKnockbackPower, 0.0f, 1.0f, new GUIContent("보조무기 넉백 배율", "보조무기 타격 시 적을 밀어내는 힘입니다. (단위: 배, 권장: 0.1)"));

                if (sidearmBaseKnockback != null)
                    EditorGUILayout.PropertyField(sidearmBaseKnockback, new GUIContent("보조무기 넉백 기본 속도", "보조무기 평타 타격 시 발생하는 기본 저지 넉백 속도입니다. (단위: m/s, 기본: 0.2m/s)"));

                if (sidearmMaxKnockbackCap != null)
                    EditorGUILayout.PropertyField(sidearmMaxKnockbackCap, new GUIContent("다대일 보조무기 넉백 상한", "집중 공격 시 누적될 수 있는 넉백 속도의 최대 상한선입니다. (단위: m/s, 기본: 0.8m/s)"));

                EditorGUILayout.HelpBox("장창병 등 긴 주무기를 사용하는 유닛은 적이 품 안(1.2m 이내)으로 파고들면 레버리지를 쓰지 못하므로, 보조무기로 자동 전환하여 빠른 연타로 호신합니다.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("※ 보조무기가 비활성화되어 있습니다. 주무기만을 사용하여 교전합니다.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 4. 기동력 및 이동 속도
        showMobility = EditorGUILayout.Foldout(showMobility, "4. 기동력 및 이동 속도", true);
        if (showMobility)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isRunning != null)
                EditorGUILayout.PropertyField(isRunning, new GUIContent("현재 달리기(구보) 상태", "현재 유닛이 달리는 중인지 여부입니다. (R키로 부대 단위 토글)"));

            if (walkSpeed != null)
                EditorGUILayout.PropertyField(walkSpeed, new GUIContent("제식 보행 속도", "대형을 유지하며 걷는 제식 보행 속도입니다. (단위: m/s, 기본: 1.2m/s)"));

            if (runSpeed != null)
                EditorGUILayout.PropertyField(runSpeed, new GUIContent("전술 구보 속도", "신속한 기동 및 재배치를 위한 전술 달리기 속도입니다. (단위: m/s, 기본: 2.8m/s)"));

            if (chargeSpeed != null)
                EditorGUILayout.PropertyField(chargeSpeed, new GUIContent("돌격 쇄도 속도", "적 진형을 향해 전력으로 질주하는 돌격 속도입니다. (단위: m/s, 기본: 4.8m/s)"));

            if (unitMaxSpeed != null)
                EditorGUILayout.PropertyField(unitMaxSpeed, new GUIContent("물리 최고 한계 속도", "넉백이나 슬롯 추격 가속 시 유닛이 낼 수 있는 물리적 최고 속도 상한선입니다. (단위: m/s, 기본: 5.5m/s)"));

            float w = walkSpeed != null ? walkSpeed.floatValue : 1.2f;
            float r = runSpeed != null ? runSpeed.floatValue : 2.8f;
            float c = chargeSpeed != null ? chargeSpeed.floatValue : 4.8f;
            EditorGUILayout.HelpBox($"[기동 배율 비교]\n• 제식 보행: {w:F1} m/s (1.0x)\n• 전술 구보: {r:F1} m/s ({(r / Mathf.Max(0.1f, w)):F1}배 빠름)\n• 돌격 쇄도: {c:F1} m/s ({(c / Mathf.Max(0.1f, w)):F1}배 전력질주)", MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 5. 지형 굴곡 및 경사각 설정
        showSlope = EditorGUILayout.Foldout(showSlope, "5. 지형 굴곡 및 경사각 설정", true);
        if (showSlope)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (alignToSlope != null)
            {
                EditorGUILayout.PropertyField(alignToSlope, new GUIContent("경사면 몸체 기울기 반영 (옵션 B)", 
                    "• 체크(옵션 B): 언덕이나 경사면의 기울기(Normal)에 맞춰 유닛의 몸체가 앞뒤/좌우로 기울어집니다. (전차, 공성 병기 권장)\n• 해제(옵션 A): 지면 고도(Y)는 완벽히 밀착하되 몸체는 중력 수직(하늘)을 유지합니다. (보병/궁병 군단 표준 및 최고 성능 권장)"));
            }

            if (groundYOffset != null)
            {
                EditorGUILayout.PropertyField(groundYOffset, new GUIContent("지면 밀착 높이 오프셋", 
                    "지형 표면으로부터 유닛 중심의 Y축 띄움 거리입니다. (단위: m, 기본: 0.39m = 높이 0.78m 큐브의 절반)"));
            }

            bool isB = alignToSlope != null && alignToSlope.boolValue;
            if (isB)
            {
                EditorGUILayout.HelpBox("[현재 모드: 옵션 B - 지형 경사각 반영]\n지형의 표면 법선(Normal)에 맞춰 몸체가 기울어집니다.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("[현재 모드: 옵션 A - 수직 자세 유지]\n지면 높이를 따라 이동하되, 몸체는 중력 반대 방향으로 똑바로 서 있습니다. (군단 RTS 표준 & 초고속 성능)", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 6. 돌격 충격량 및 물리 질량
        showImpact = EditorGUILayout.Foldout(showImpact, "6. 돌격 충격량 및 물리 질량", true);
        if (showImpact)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (canReflectCharge != null)
                EditorGUILayout.PropertyField(canReflectCharge, new GUIContent("돌격 반사 능력 보유 (창벽 저지)", "체크 시 제자리에서 적의 돌격을 맞이할 때 적의 돌격 추가 피해를 적에게 고스란히 되돌려주고 돌격을 분쇄합니다.\n• 검병: 체크 해제 (돌격 반사 불가)\n• 장창병: 체크 권장 (돌격 반사 가능)"));

            if (mass != null)
                EditorGUILayout.PropertyField(mass, new GUIContent("유닛 질량 / 무게", "유닛의 기본 물리 질량입니다. 넉백 저항력과 적을 밀쳐내는 물리 충격량의 핵심 분모/분자가 됩니다. (단위: kg, 기본: 100.0kg)"));

            if (chargeBonus != null)
                EditorGUILayout.PropertyField(chargeBonus, new GUIContent("돌격 충격 보너스 계수", "돌격 속도로 첫 충돌 시 기본 공격력에 가산되는 물리 운동에너지 피해 계수입니다. (기본: 15.0)"));

            if (maxChargeDamage != null)
                EditorGUILayout.PropertyField(maxChargeDamage, new GUIContent("최대 돌격 충돌 피해 상한", "돌격 첫 충돌 시 가해질 수 있는 1회 최대 데미지 한계치입니다. (기본: 35.0)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("넘어짐 및 무력화 (Knockdown) 설정", EditorStyles.boldLabel);

            if (isImmuneToKnockdown != null)
                EditorGUILayout.PropertyField(isImmuneToKnockdown, new GUIContent("넘어짐/무력화 면역 (불굴 특수능력)", "체크 시 아무리 강력한 넉백 충격을 받아도 넘어지거나 무력화되지 않고 굳건히 버팁니다."));

            if (knockdownSpeedThreshold != null)
                EditorGUILayout.PropertyField(knockdownSpeedThreshold, new GUIContent("넘어짐 판정 넉백 속도 임계값", "피격 시 넉백 속도가 이 기준치 이상이면 충격을 이기지 못하고 그 자리에 넘어져 무력화됩니다. (단위: m/s, 기본: 2.0m/s)"));

            if (knockdownDuration != null)
                EditorGUILayout.PropertyField(knockdownDuration, new GUIContent("넘어짐 무력화 지속 시간", "넘어져 무력화되었을 때 다시 일어나서 전열로 복귀하기까지 걸리는 시간입니다. (단위: 초, 기본: 3.0초)"));

            EditorGUILayout.Space(2);

            // 실시간 돌격 물리 시뮬레이션 수치 예측 박스
            float curMass = mass != null ? mass.floatValue : 100f;
            float curDmg = damage != null ? damage.floatValue : 10f;
            float curBonus = chargeBonus != null ? chargeBonus.floatValue : 15f;
            float maxLimit = maxChargeDamage != null ? maxChargeDamage.floatValue : 35f;
            float curChargeSpeed = chargeSpeed != null ? chargeSpeed.floatValue : 4.8f;
            float curKnockback = knockbackPower != null ? knockbackPower.floatValue : 1.0f;
            bool isReflect = canReflectCharge != null && canReflectCharge.boolValue;
            bool isImmune = isImmuneToKnockdown != null && isImmuneToKnockdown.boolValue;
            float curKnockdownThresh = knockdownSpeedThreshold != null ? knockdownSpeedThreshold.floatValue : 2.0f;
            float curKnockdownDur = knockdownDuration != null ? knockdownDuration.floatValue : 3.0f;

            float speedRatio = curChargeSpeed / 4.8f;
            float actualChargeBonus = curBonus * speedRatio;
            float estTotalDamage = Mathf.Min(curDmg + actualChargeBonus, maxLimit);
            float estReflectedDamage = curDmg + 15f;
            float curBaseKnock = baseMeleeKnockback != null ? baseMeleeKnockback.floatValue : 0.45f;
            float curMaxCap = maxMeleeKnockbackCap != null ? maxMeleeKnockbackCap.floatValue : 1.2f;
            float estKnockbackSpeed = Mathf.Min(curBaseKnock * (curMass / 100f) * curKnockback, curMaxCap);
            float estChargeKnockbackSpeed = Mathf.Min(1.8f * (curMass / 100f) * curKnockback, 2.2f);
            float estKnockbackDist = estKnockbackSpeed / 8.0f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("[속도 비례 돌격 및 물리 예측]", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"• 돌격 쇄도 속도: {curChargeSpeed:F1} m/s (기준 속도 4.8m/s 대비 {speedRatio:F2}배)");
            EditorGUILayout.LabelField($"• 속도 비례 돌격 추가 피해: +{actualChargeBonus:F1} (공식: 보너스 {curBonus:F1} × ({curChargeSpeed:F1} ÷ 4.8))");
            EditorGUILayout.LabelField($"• 정면 돌격 시 총 피해량: {estTotalDamage:F1} (기본 {curDmg:F1} + 추가피해 {actualChargeBonus:F1} / 상한 {maxLimit:F1})");

            if (isReflect)
                EditorGUILayout.LabelField($"• 창벽 돌격 반사: 보유 [V] → 적 돌격 피해 15.0 반사 시 약 {estReflectedDamage:F1} 피해를 적에게 역주입");
            else
                EditorGUILayout.LabelField($"• 창벽 돌격 반사: 미보유 [ ] → 돌격 반사 불가 (적 돌격을 몸으로 수용)");

            EditorGUILayout.LabelField($"• 평타 교전 넉백 속도: 초당 {estKnockbackSpeed:F2} m/s (약 {estKnockbackDist * 100f:F1}cm 미세 주춤 저지)");
            EditorGUILayout.LabelField($"• 전력 돌격 충돌 넉백: 초당 {estChargeKnockbackSpeed:F1} m/s (약 {(estChargeKnockbackSpeed / 8.0f) * 100f:F1}cm 충격 밀림)");

            if (isImmune)
                EditorGUILayout.LabelField("• 넘어짐 무력화 판정: 불굴 면역 (전열 유지)", EditorStyles.boldLabel);
            else if (estChargeKnockbackSpeed >= curKnockdownThresh)
                EditorGUILayout.LabelField($"• 돌격 피격 시 넘어짐 판정: 돌격 충돌 시 넘어짐 발동 ({curKnockdownDur:F1}초간 무력화)", EditorStyles.miniBoldLabel);
            else
                EditorGUILayout.LabelField("• 돌격 피격 시 넘어짐 판정: 착지 유지", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 7. 지휘 상태 및 전술 태세
        showCommand = EditorGUILayout.Foldout(showCommand, "7. 지휘 상태 및 전술 태세", true);
        if (showCommand)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isSelected != null)
                EditorGUILayout.PropertyField(isSelected, new GUIContent("플레이어 선택 여부", "현재 플레이어가 마우스 드래그/클릭으로 선택한 유닛인지 여부입니다."));

            if (currentState != null)
                EditorGUILayout.PropertyField(currentState, new GUIContent("현재 작전 명령 상태", "유닛의 현재 전투 행동 상태입니다. (대기, 이동, 공격이동, 백병전)"));

            if (currentStance != null)
                EditorGUILayout.PropertyField(currentStance, new GUIContent("전술 공격 태세", "공세(적극적 타겟팅) 또는 방어(진형 사수) 태세입니다."));

            if (autoAttackEnabled != null)
                EditorGUILayout.PropertyField(autoAttackEnabled, new GUIContent("선제 요격 활성화 (V키)", "체크 시 적 발견 시 자동 돌격 요격, 해제 시 무기를 땅에 박고 버티는 접촉 방어 모드로 동작합니다."));

            if (detectRange != null)
                EditorGUILayout.PropertyField(detectRange, new GUIContent("적 탐색 시야 반경", "자유 유닛 상태에서 주변의 적을 스스로 탐지하고 인지하는 시야 반경입니다. (단위: m, 기본: 5.0m)"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 8. 대형 복귀 및 겹침 방지
        showFormation = EditorGUILayout.Foldout(showFormation, "8. 대형 복귀 및 겹침 방지", true);
        if (showFormation)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("대형 이탈 복귀 설정", EditorStyles.miniBoldLabel);
            if (reformThreshold != null)
                EditorGUILayout.PropertyField(reformThreshold, new GUIContent("대형 이탈 허용 오차", "대형 슬롯 위치에서 몇 미터 이상 벗어났을 때 복귀 타이머를 가동할지 결정합니다. (단위: m, 기본: 0.5m)"));

            if (reformDelay != null)
                EditorGUILayout.PropertyField(reformDelay, new GUIContent("복귀 대기 지연 시간", "대형에서 벗어난 뒤 복귀 기동을 시작하기까지 대기하는 시간입니다. (단위: 초, 기본: 2.0초)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("개인 공간 및 물리 척력", EditorStyles.miniBoldLabel);

            if (separationRadius != null)
                EditorGUILayout.PropertyField(separationRadius, new GUIContent("개인 공간 반경", "유닛 간 물리적 겹침을 방지하기 위한 개인 방어 반경입니다. (단위: m, 기본: 0.45m)"));

            if (separationForce != null)
                EditorGUILayout.PropertyField(separationForce, new GUIContent("물리 척력 세기", "유닛끼리 겹쳤을 때 서로를 밀어내는 물리 반발력의 세기입니다. (단위: 배, 기본: 1.2)"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 9. 소속 부대 및 런타임 정보
        showRuntime = EditorGUILayout.Foldout(showRuntime, "9. 소속 부대 및 런타임 정보 (참조용)", true);
        if (showRuntime)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (mySquad != null)
                EditorGUILayout.PropertyField(mySquad, new GUIContent("소속 부대", "이 유닛이 배속되어 있는 지휘 부대 오브젝트입니다."));

            if (fixedTargetPos != null)
                EditorGUILayout.PropertyField(fixedTargetPos, new GUIContent("지정 슬롯 월드 좌표", "부대 대형 내에서 이 유닛이 점유해야 할 목표 월드 좌표입니다."));

            EditorGUILayout.LabelField($"• 부대 내 격자 위치: 열(Col) {unit.Col}, 행(Row) {unit.Row} | 시뮬레이션 ID: {unit.simulationIndex}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 유닛의 실시간 전투 스탯과 성능을 요약 박스로 시각화합니다.
    /// </summary>
    private void DrawLiveCombatSummary(Unit unit)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.2f, 0.8f, 1.0f) }
        };

        float hpRatio = (unit.maxHp > 0f) ? (unit.currentHp / unit.maxHp * 100f) : 100f;
        float armorReduction = unit.armor / 100f;
        float mainCd = Mathf.Max(0.01f, unit.attackCooldown);
        float mainAps = 1.0f / mainCd;
        float mainDps = unit.damage * mainAps;

        string faction = unit.isPlayer ? "플레이어(아군)" : "적군(Enemy)";
        string squadName = (unit.mySquad != null) ? $"{unit.mySquad.name} [열 {unit.Col}, 행 {unit.Row}]" : "단독 자유 유닛";

        EditorGUILayout.LabelField("[유닛 실시간 전투 스탯 요약]", headerStyle);
        EditorGUILayout.LabelField($"• 소속: {faction} | 부대: {squadName}");
        EditorGUILayout.LabelField($"• 상태: 명령({unit.currentState}) / 태세({unit.currentStance}) / 선제요격({(unit.autoAttackEnabled ? "ON" : "OFF-접촉방어")})");

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"생명력: {unit.currentHp:F1} / {unit.maxHp:F1} ({hpRatio:F0}%) | 방어력: {unit.armor} (피해 감쇄 {armorReduction:F2}%)");
        EditorGUILayout.LabelField($"주무기 화력: 공격력 {unit.damage:F1} | 주기 {mainCd:F2}초 (초당 {mainAps:F2}회) | 지속 화력: {mainDps:F1} DPS");
        EditorGUILayout.LabelField($"교전 사거리: 타격 {unit.attackRange:F2}m (최소 {unit.minAttackRange:F2}m) | 정지거리 {unit.combatStoppingDistance:F2}m | 넉백 {unit.knockbackPower:F1}배");

        if (unit.useSidearm)
        {
            float sideCd = Mathf.Max(0.01f, unit.sidearmAttackCooldown);
            float sideAps = 1.0f / sideCd;
            float sideDps = unit.sidearmDamage * sideAps;
            EditorGUILayout.LabelField($"보조무기: {unit.sidearmSwitchDistance:F1}m 밀착 시 자동 전환 | 공격력 {unit.sidearmDamage:F1} | {sideDps:F1} DPS (초당 {sideAps:F2}회)");
        }

        EditorGUILayout.LabelField($"기동성: 제식 {unit.walkSpeed:F1}m/s | 구보 {unit.runSpeed:F1}m/s | 돌격 {unit.chargeSpeed:F1}m/s | 질량: {unit.mass:F0}kg");

        EditorGUILayout.EndVertical();
    }

    private void CreateOrAttachLaunchPoint(Unit unit)
    {
        Transform existing = unit.transform.Find("LaunchPoint");
        if (existing != null)
        {
            Undo.RecordObject(unit, "Bind Existing LaunchPoint");
            unit.launchPoint = existing;
            if (launchPoint != null) launchPoint.objectReferenceValue = existing;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(unit);
            return;
        }

        if (EditorUtility.IsPersistent(unit.gameObject))
        {
            string assetPath = AssetDatabase.GetAssetPath(unit.gameObject);
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
            Unit rootUnit = contentsRoot.GetComponent<Unit>();

            Transform p = contentsRoot.transform.Find("LaunchPoint");
            if (p == null)
            {
                GameObject newPoint = new GameObject("LaunchPoint");
                newPoint.transform.SetParent(contentsRoot.transform, false);
                newPoint.transform.localPosition = rootUnit.launchOffset;
                newPoint.transform.localRotation = Quaternion.identity;
                newPoint.transform.localScale = Vector3.one;
                rootUnit.launchPoint = newPoint.transform;
            }
            else
            {
                rootUnit.launchPoint = p;
            }

            PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            GameObject newPoint = new GameObject("LaunchPoint");
            Undo.RegisterCreatedObjectUndo(newPoint, "Create LaunchPoint Object");
            Undo.SetTransformParent(newPoint.transform, unit.transform, "Set LaunchPoint Parent");
            newPoint.transform.localPosition = unit.launchOffset;
            newPoint.transform.localRotation = Quaternion.identity;
            newPoint.transform.localScale = Vector3.one;

            Undo.RecordObject(unit, "Assign LaunchPoint");
            unit.launchPoint = newPoint.transform;
            if (launchPoint != null) launchPoint.objectReferenceValue = newPoint.transform;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(unit);

            UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
            }
        }
    }

    private void ResetLaunchPointToDefault(Unit unit)
    {
        Vector3 defaultOffset = new Vector3(0.25f, 0.15f, 0.45f);
        Undo.RecordObject(unit, "Reset Launch Offset to 0.5 Scale Default");
        unit.launchOffset = defaultOffset;
        if (launchOffset != null) launchOffset.vector3Value = defaultOffset;

        if (unit.launchPoint != null)
        {
            Undo.RecordObject(unit.launchPoint, "Reset Launch Point Transform");
            unit.launchPoint.localPosition = defaultOffset;
            unit.launchPoint.localRotation = Quaternion.identity;
            unit.launchPoint.localScale = Vector3.one;
            EditorUtility.SetDirty(unit.launchPoint);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(unit);

        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
        }
    }

    private void OnSceneGUI()
    {
        Unit unit = (Unit)target;
        if (unit == null || !unit.isRangedUnit) return;

        Vector3 worldLaunchPos = unit.GetWorldLaunchPosition();

        Handles.color = new Color(0f, 0.9f, 1f, 0.9f);
        Handles.SphereHandleCap(0, worldLaunchPos, Quaternion.identity, 0.08f, EventType.Repaint);
        Handles.color = new Color(0f, 0.7f, 1f, 0.5f);
        Handles.DrawDottedLine(unit.transform.position, worldLaunchPos, 2.5f);
        Handles.Label(worldLaunchPos + Vector3.up * 0.06f, "발사점 (Muzzle)");

        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPos = Handles.PositionHandle(worldLaunchPos, unit.transform.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(unit, "Move Launch Point Handle");
            Vector3 diff = Quaternion.Inverse(unit.transform.rotation) * (newWorldPos - unit.transform.position);
            Vector3 unitScale = unit.transform.localScale;
            Vector3 newLocalOffset = new Vector3(
                Mathf.Abs(unitScale.x) > 0.001f ? diff.x / unitScale.x : diff.x,
                Mathf.Abs(unitScale.y) > 0.001f ? diff.y / unitScale.y : diff.y,
                Mathf.Abs(unitScale.z) > 0.001f ? diff.z / unitScale.z : diff.z
            );

            unit.launchOffset = newLocalOffset;
            if (launchOffset != null) launchOffset.vector3Value = newLocalOffset;

            if (unit.launchPoint != null)
            {
                Undo.RecordObject(unit.launchPoint, "Move Launch Point Handle");
                unit.launchPoint.localPosition = newLocalOffset;
                unit.launchPoint.localScale = Vector3.one;
                EditorUtility.SetDirty(unit.launchPoint);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(unit);

            UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
            }
        }
    }
}
