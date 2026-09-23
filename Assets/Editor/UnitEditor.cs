using UnityEditor;
using UnityEngine;

/// <summary>
/// Unit 컴포넌트의 유니티 인스펙터를 한국어 라벨, 전술 설명 툴팁, 실시간 전투 스탯 요약 박스로 친절하게 시각화하는 커스텀 에디터입니다.
/// </summary>
[CustomEditor(typeof(Unit))]
[CanEditMultipleObjects]
public class UnitEditor : Editor
{
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

    private SerializedProperty combatStoppingDistance;
    private SerializedProperty engagementOffset;
    private SerializedProperty isRunning;
    private SerializedProperty walkSpeed;
    private SerializedProperty runSpeed;
    private SerializedProperty chargeSpeed;
    private SerializedProperty unitMaxSpeed;

    private SerializedProperty mass;
    private SerializedProperty chargeBonus;
    private SerializedProperty maxChargeDamage;
    private SerializedProperty canReflectCharge;
    private SerializedProperty knockdownSpeedThreshold;
    private SerializedProperty knockdownDuration;
    private SerializedProperty isImmuneToKnockdown;

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

    // 카테고리 접기/펼치기 상태 플래그
    private static bool showSurvival = true;
    private static bool showPrimaryWeapon = true;
    private static bool showSidearm = true;
    private static bool showMobility = true;
    private static bool showImpact = true;
    private static bool showCommand = true;
    private static bool showFormation = false;
    private static bool showRuntime = false;

    private void OnEnable()
    {
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

        combatStoppingDistance = serializedObject.FindProperty("combatStoppingDistance");
        engagementOffset = serializedObject.FindProperty("engagementOffset");
        isRunning = serializedObject.FindProperty("isRunning");
        walkSpeed = serializedObject.FindProperty("walkSpeed");
        runSpeed = serializedObject.FindProperty("runSpeed");
        chargeSpeed = serializedObject.FindProperty("chargeSpeed");
        unitMaxSpeed = serializedObject.FindProperty("unitMaxSpeed");

        mass = serializedObject.FindProperty("mass");
        chargeBonus = serializedObject.FindProperty("chargeBonus");
        maxChargeDamage = serializedObject.FindProperty("maxChargeDamage");
        canReflectCharge = serializedObject.FindProperty("canReflectCharge");
        knockdownSpeedThreshold = serializedObject.FindProperty("knockdownSpeedThreshold");
        knockdownDuration = serializedObject.FindProperty("knockdownDuration");
        isImmuneToKnockdown = serializedObject.FindProperty("isImmuneToKnockdown");

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
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Unit unit = (Unit)target;

        // 📊 0. 상단 실시간 전투 스탯 요약 박스 (단일 오브젝트 선택 시)
        if (!serializedObject.isEditingMultipleObjects && unit != null)
        {
            DrawLiveCombatSummary(unit);
            EditorGUILayout.Space(6);
        }

        // 🩺 1. 생존력 및 방어력 (Health & Armor)
        showSurvival = EditorGUILayout.Foldout(showSurvival, "🩺 1. 생존력 및 방어력 (Health & Armor)", true);
        if (showSurvival)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isPlayer != null)
                EditorGUILayout.PropertyField(isPlayer, new GUIContent("진영 소속 (플레이어/아군 여부)", "체크 시 아군(Player 진영), 해제 시 적군(Enemy 진영)으로 자동 분류됩니다."));

            if (maxHp != null)
                EditorGUILayout.PropertyField(maxHp, new GUIContent("최대 체력 (Max HP)", "유닛의 최대 생명력입니다. (기본 보병 권장: 100)"));

            if (currentHp != null)
            {
                EditorGUILayout.PropertyField(currentHp, new GUIContent("현재 체력 (Current HP)", "유닛의 실시간 생명력입니다. 0 이하가 되면 사망 처리됩니다."));
                float hpRatio = (maxHp != null && maxHp.floatValue > 0f) ? (currentHp.floatValue / maxHp.floatValue) : 1.0f;
                Rect r = EditorGUILayout.GetControlRect(false, 6);
                EditorGUI.ProgressBar(r, Mathf.Clamp01(hpRatio), "");
            }

            if (armor != null)
            {
                EditorGUILayout.IntSlider(armor, 0, 10000, new GUIContent("기본 방어력 (Base Armor)", "유닛 원본의 기본 방어력 (0 ~ 10,000 만분율). 10000 = 100% 완전 방어. 예: 2812 입력 시 28.12% 피해 감쇄."));
                float pct = (float)armor.intValue / 100f;
                EditorGUILayout.HelpBox($"🛡️ 기본 피해 감소율: {pct:F2}%\n   ↳ 적의 물리 타격 및 돌격 피해의 {100f - pct:F2}%만 체력에 적용됩니다. (부대 방진 형성 시 추가 가산)", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ⚔️ 2. 주무기 및 교전 사거리 (Primary Weapon)
        showPrimaryWeapon = EditorGUILayout.Foldout(showPrimaryWeapon, "⚔️ 2. 주무기 및 교전 사거리 (Primary Weapon)", true);
        if (showPrimaryWeapon)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (damage != null)
                EditorGUILayout.PropertyField(damage, new GUIContent("주무기 공격력 (Damage)", "일반 백병전 근접 공격 1회당 기본 피해량입니다. (검병 권장: 10.0, 장창병 권장: 14.0)"));

            if (attackCooldown != null)
            {
                EditorGUILayout.PropertyField(attackCooldown, new GUIContent("공격 주기 (초)", "다음 공격을 휘두르기까지의 쿨다운 대기 시간입니다. (기본: 1.0초)"));
                float cd = Mathf.Max(0.01f, attackCooldown.floatValue);
                float aps = 1.0f / cd;
                float dps = (damage != null ? damage.floatValue : 10f) * aps;
                EditorGUILayout.LabelField($"   ↳ ⚡ 타격 속도: 초당 {aps:F2}회 타격 | 지속 화력(DPS): 초당 {dps:F1} 데미지", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("📐 사거리 및 스위트스팟 (거리별 감쇠)", EditorStyles.miniBoldLabel);

            if (attackRange != null)
                EditorGUILayout.PropertyField(attackRange, new GUIContent("최대 타격 사거리 (m)", "주무기로 적을 타격할 수 있는 최대 유효 사거리입니다. (검병: 1.45m, 장창병: 3.0m+)"));

            if (minAttackRange != null)
                EditorGUILayout.PropertyField(minAttackRange, new GUIContent("최소 사거리 (m)", "주무기 최소 사거리입니다. 이보다 가까운 적은 주무기로 타격할 수 없습니다. (검병: 0m, 장창병: 1.2m 권장)"));

            if (optimalRangeMin != null)
                EditorGUILayout.PropertyField(optimalRangeMin, new GUIContent("최적 사거리(스위트스팟) 최소 기준 (m)", "이 거리 이상에서 100% 정상 데미지를 가합니다. (검병: 0m, 장창병: 1.8m 권장)"));

            if (closeRangeDamageRatio != null)
                EditorGUILayout.Slider(closeRangeDamageRatio, 0.1f, 1.0f, new GUIContent("근접 파고듦 피해 감쇠 비율", "적에게 최적 사거리 미만(품 안)으로 접근을 허용했을 때 주무기 피해량 비율입니다. (검병: 1.0=감쇠 없음, 장창병: 0.35 권장)"));

            if (knockbackPower != null)
                EditorGUILayout.Slider(knockbackPower, 0.0f, 5.0f, new GUIContent("주무기 넉백 세기 배율", "타격 시 적을 뒤로 밀쳐내는 넉백 충격량 배율입니다. (검병: 1.0, 장창병: 2.2 권장)"));

            if (baseMeleeKnockback != null)
                EditorGUILayout.PropertyField(baseMeleeKnockback, new GUIContent("평타 넉백 기본 속도 (m/s)", "돌격이 아닌 일반 백병전 평타(창 찌르기, 칼질) 시 발생하는 기본 저지 넉백 속도입니다. 수치가 낮을수록 적이 덜 밀려나며 창벽을 파고들기 쉬워집니다. (기본: 0.45m/s)"));

            if (maxMeleeKnockbackCap != null)
                EditorGUILayout.PropertyField(maxMeleeKnockbackCap, new GUIContent("다대일 평타 넉백 상한 속도 (m/s)", "여러 아군이 동시에 한 명의 적을 집중 공격할 때, 피격 유닛에게 누적될 수 있는 평타 넉백 속도의 최대 상한선입니다. 수십 미터 날아가는 것을 원천 차단합니다. (기본: 1.2m/s, 밀림 거리 약 9~15cm)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("🛑 백병전 대형 정지 및 교전 간격", EditorStyles.miniBoldLabel);

            if (combatStoppingDistance != null)
                EditorGUILayout.PropertyField(combatStoppingDistance, new GUIContent("교전 정지 거리 (m)", "적과 백병전 시 발을 완전히 멈추고 제자리에서 공격하는 거리입니다. (검병: 1.05m, 장창병: 2.5m)"));

            if (engagementOffset != null)
                EditorGUILayout.PropertyField(engagementOffset, new GUIContent("교전 목표 간격 (m)", "적을 향해 전진할 때 적의 중심으로부터 유지하려는 목표 간격입니다. (검병: 0.40m, 장창병: 2.0m)"));

            EditorGUILayout.HelpBox("💡 [병과 세팅 팁]\n• 검병: 사거리 1.45m / 최소사거리 0m / 근접감쇠 1.0 / 넉백 1.0 / 정지거리 1.05m\n• 장창병: 사거리 3.0m / 최소사거리 1.2m / 최적 1.8m / 근접감쇠 0.35 / 넉백 2.2 / 정지거리 2.5m", MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 🗡️ 3. 보조무기 (Sidearm) 자동 전환
        showSidearm = EditorGUILayout.Foldout(showSidearm, "🗡️ 3. 보조무기 (Sidearm) 자동 전환", true);
        if (showSidearm)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (useSidearm != null)
                EditorGUILayout.PropertyField(useSidearm, new GUIContent("보조무기 자동 전환 활성화", "적이 일정 거리 이내로 밀착 시 주무기 대신 보조무기로 자동 전환하여 대응할지 여부입니다."));

            if (useSidearm != null && useSidearm.boolValue)
            {
                if (sidearmSwitchDistance != null)
                    EditorGUILayout.PropertyField(sidearmSwitchDistance, new GUIContent("  보조무기 전환 거리 (m)", "적과의 거리가 이 거리 이내로 좁혀지면 즉시 보조무기로 무기를 교체합니다. (권장: 1.2m)"));

                if (sidearmAttackRange != null)
                    EditorGUILayout.PropertyField(sidearmAttackRange, new GUIContent("  보조무기 공격 사거리 (m)", "보조무기의 유효 타격 사거리입니다. (권장: 1.0m)"));

                if (sidearmDamage != null)
                    EditorGUILayout.PropertyField(sidearmDamage, new GUIContent("  보조무기 타격 공격력", "보조무기 1회 타격 시 입히는 피해량입니다. (권장: 4.0)"));

                if (sidearmAttackCooldown != null)
                {
                    EditorGUILayout.PropertyField(sidearmAttackCooldown, new GUIContent("  보조무기 공격 주기 (초)", "보조무기의 공격 쿨다운입니다. (권장: 0.8초 빠른 연타)"));
                    float sCd = Mathf.Max(0.01f, sidearmAttackCooldown.floatValue);
                    float sAps = 1.0f / sCd;
                    float sDps = (sidearmDamage != null ? sidearmDamage.floatValue : 4f) * sAps;
                    EditorGUILayout.LabelField($"     ↳ ⚡ 보조무기 연타 속도: 초당 {sAps:F2}회 | 지속 화력(DPS): 초당 {sDps:F1} 데미지", EditorStyles.miniLabel);
                }

                if (sidearmKnockbackPower != null)
                    EditorGUILayout.Slider(sidearmKnockbackPower, 0.0f, 1.0f, new GUIContent("  보조무기 넉백 배율", "보조무기 타격 시 적을 밀어내는 힘입니다. 보조무기는 넉백이 거의 없으므로 0.1 권장."));

                if (sidearmBaseKnockback != null)
                    EditorGUILayout.PropertyField(sidearmBaseKnockback, new GUIContent("  보조무기 넉백 기본 속도 (m/s)", "보조무기 평타 타격 시 발생하는 기본 저지 넉백 속도입니다. 품 안 호신용이므로 매우 미약합니다. (기본: 0.2m/s)"));

                if (sidearmMaxKnockbackCap != null)
                    EditorGUILayout.PropertyField(sidearmMaxKnockbackCap, new GUIContent("  다대일 보조무기 넉백 상한 (m/s)", "여러 아군이 보조무기로 동시에 한 명의 적을 집중 공격할 때 누적될 수 있는 넉백 속도의 최대 상한선입니다. (기본: 0.8m/s)"));

                EditorGUILayout.HelpBox("💡 장창병 등 긴 주무기를 사용하는 유닛은 적이 품 안(1.2m 이내)으로 파고들면 레버리지를 쓰지 못하므로, 보조무기로 자동 전환하여 빠른 연타로 호신합니다.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("   ※ 보조무기가 비활성화되어 있습니다. 주무기만을 사용하여 교전합니다.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 🏃 4. 기동력 및 이동 속도 (Mobility)
        showMobility = EditorGUILayout.Foldout(showMobility, "🏃 4. 기동력 및 이동 속도 (Mobility)", true);
        if (showMobility)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isRunning != null)
                EditorGUILayout.PropertyField(isRunning, new GUIContent("현재 달리기(구보) 상태", "현재 유닛이 달리는 중인지 여부입니다. (R키로 부대 단위 토글)"));

            if (walkSpeed != null)
                EditorGUILayout.PropertyField(walkSpeed, new GUIContent("제식 보행 속도 (m/s)", "대형을 유지하며 질서정연하게 걷는 제식 보행 속도입니다. (기본: 1.2m/s)"));

            if (runSpeed != null)
                EditorGUILayout.PropertyField(runSpeed, new GUIContent("전술 구보 속도 (m/s)", "신속한 기동 및 재배치를 위한 전술 달리기 속도입니다. (기본: 2.8m/s)"));

            if (chargeSpeed != null)
                EditorGUILayout.PropertyField(chargeSpeed, new GUIContent("돌격 쇄도 속도 (m/s)", "적 진형을 향해 전력으로 질주하는 돌격 속도입니다. (기본: 4.8m/s)"));

            if (unitMaxSpeed != null)
                EditorGUILayout.PropertyField(unitMaxSpeed, new GUIContent("물리 최고 한계 속도 (m/s)", "넉백이나 슬롯 추격 가속 시 유닛이 낼 수 있는 물리적 최고 속도 상한선입니다. (기본: 5.5m/s)"));

            float w = walkSpeed != null ? walkSpeed.floatValue : 1.2f;
            float r = runSpeed != null ? runSpeed.floatValue : 2.8f;
            float c = chargeSpeed != null ? chargeSpeed.floatValue : 4.8f;
            EditorGUILayout.HelpBox($"⚡ [기동 배율 비교]\n• 제식 보행: {w:F1} m/s (1.0x)\n• 전술 구보: {r:F1} m/s ({(r / Mathf.Max(0.1f, w)):F1}배 빠름)\n• 돌격 쇄도: {c:F1} m/s ({(c / Mathf.Max(0.1f, w)):F1}배 전력질주)", MessageType.None);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 💥 5. 돌격 충격량 및 물리 질량 (Charge Impact & Mass)
        showImpact = EditorGUILayout.Foldout(showImpact, "💥 5. 돌격 충격량 및 물리 질량 (Charge Impact & Mass)", true);
        if (showImpact)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (canReflectCharge != null)
                EditorGUILayout.PropertyField(canReflectCharge, new GUIContent("돌격 반사 능력 보유 (창벽 저지)", "체크(V) 시 제자리에서 적의 돌격을 맞이할 때 적의 돌격 추가 피해를 적에게 고스란히 되돌려주고 돌격을 분쇄합니다.\n• 검병, 도끼병: 체크 해제 (돌격 반사 불가)\n• 장창병, 파이크병: 체크 권장 (돌격 반사 가능)"));

            if (mass != null)
                EditorGUILayout.PropertyField(mass, new GUIContent("유닛 질량 / 무게 (Mass, kg)", "유닛의 기본 물리 질량입니다. 넉백 저항력과 적을 밀쳐내는 물리 충격량의 핵심 분모/분자가 됩니다. (기본 보병: 100.0kg)"));

            if (chargeBonus != null)
                EditorGUILayout.PropertyField(chargeBonus, new GUIContent("돌격 충격 보너스 계수 (Charge Bonus)", "돌격 속도로 첫 충돌 시 기본 공격력에 가산되는 물리 운동에너지 피해 계수입니다. (기본: 15.0)\n속도가 빠를수록 피해가 증가합니다."));

            if (maxChargeDamage != null)
                EditorGUILayout.PropertyField(maxChargeDamage, new GUIContent("최대 돌격 충돌 피해 상한 (Max Cap)", "돌격 첫 충돌 시 가해질 수 있는 1회 최대 데미지 한계치입니다. (기본: 35.0)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("💫 넘어짐 및 무력화 (Knockdown) 설정", EditorStyles.boldLabel);

            if (isImmuneToKnockdown != null)
                EditorGUILayout.PropertyField(isImmuneToKnockdown, new GUIContent("넘어짐/무력화 면역 (불굴 특수능력)", "체크(V) 시 아무리 강력한 넉백 충격을 받아도 넘어지거나 무력화되지 않고 굳건히 버팁니다.\n(정예 중보병, 중장 기병, 괴수, 영웅 지휘관 등 특수능력 유닛용)"));

            if (knockdownSpeedThreshold != null)
                EditorGUILayout.PropertyField(knockdownSpeedThreshold, new GUIContent("넘어짐 판정 넉백 속도 임계값 (m/s)", "피격 시 넉백 속도가 이 기준치 이상이면 충격을 이기지 못하고 그 자리에 넘어져 무력화됩니다. (기본: 2.0 m/s)"));

            if (knockdownDuration != null)
                EditorGUILayout.PropertyField(knockdownDuration, new GUIContent("넘어짐 무력화 지속 시간 (초)", "넘어져 무력화되었을 때 다시 일어나서 전열로 복귀하기까지 걸리는 시간(초)입니다. 무력화 중에는 이동 및 공격이 정지됩니다. (기본: 3.0초)"));

            EditorGUILayout.Space(2);

            // 📊 실시간 돌격 물리 시뮬레이션 수치 예측 박스
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

            // 💥 속도 비례 돌격 추가 피해 공식: 돌격 보너스 × (돌격 쇄도 속도 ÷ 4.8m/s)
            float speedRatio = curChargeSpeed / 4.8f;
            float actualChargeBonus = curBonus * speedRatio;
            float estTotalDamage = Mathf.Min(curDmg + actualChargeBonus, maxLimit);
            float estReflectedDamage = curDmg + 15f; // 적의 기본 돌격 보너스 15.0을 반사할 시
            float curBaseKnock = baseMeleeKnockback != null ? baseMeleeKnockback.floatValue : 0.45f;
            float curMaxCap = maxMeleeKnockbackCap != null ? maxMeleeKnockbackCap.floatValue : 1.2f;
            float estKnockbackSpeed = Mathf.Min(curBaseKnock * (curMass / 100f) * curKnockback, curMaxCap); // 평타 창질 미세 저지 속도 (상한 캡 적용)
            float estChargeKnockbackSpeed = Mathf.Min(1.8f * (curMass / 100f) * curKnockback, 2.2f); // 전력 돌격 충돌 넉백 속도
            float estKnockbackDist = estKnockbackSpeed / 8.0f; // 감쇠율 8.0 기준 현실적인 밀림 거리 (약 0.05~0.15m)

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 [정직한 속도 비례 돌격 및 물리 예측]", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"• 🏃 돌격 쇄도 속도: {curChargeSpeed:F1} m/s (기준 속도 4.8m/s 대비 {speedRatio:F2}배)");
            EditorGUILayout.LabelField($"• 💥 속도 비례 돌격 추가 피해: +{actualChargeBonus:F1} (공식: 보너스 {curBonus:F1} × ({curChargeSpeed:F1} ÷ 4.8))");
            EditorGUILayout.LabelField($"• ⚔️ 정면 돌격 시 총 피해량: {estTotalDamage:F1} (기본 {curDmg:F1} + 추가피해 {actualChargeBonus:F1} / 상한 {maxLimit:F1})");

            if (isReflect)
            {
                EditorGUILayout.LabelField($"• 🛡️ 창벽 돌격 반사: 보유 [V] → 적 돌격 피해 15.0 반사 시 약 {estReflectedDamage:F1} 피해를 적에게 역주입!");
            }
            else
            {
                EditorGUILayout.LabelField($"• 🛡️ 창벽 돌격 반사: 미보유 [ ] → 돌격 반사 불가 (검병/보병 등 적 돌격을 몸으로 수용)");
            }

            EditorGUILayout.LabelField($"• 💨 평타 교전 넉백 속도: 초당 {estKnockbackSpeed:F2} m/s (약 {estKnockbackDist * 100f:F1}cm 미세 주춤 저지 / 다대일 집중 시에도 최대 1.2m/s 상한 캡)");
            EditorGUILayout.LabelField($"• 💥 전력 돌격 충돌 넉백: 초당 {estChargeKnockbackSpeed:F1} m/s (약 {(estChargeKnockbackSpeed / 8.0f) * 100f:F1}cm 충격 밀림 / 최대 2.2m/s 상한 캡)");

            if (isImmune)
            {
                EditorGUILayout.LabelField($"• 💫 넘어짐 무력화 판정: 🛡️ 불굴 면역 [V] (어떤 넉백 충격에도 넘어지지 않고 전열 유지)", EditorStyles.boldLabel);
            }
            else if (estChargeKnockbackSpeed >= curKnockdownThresh)
            {
                EditorGUILayout.LabelField($"• 💫 돌격 피격 시 넘어짐 판정: ⚠️ 돌격 충돌 시 넘어짐 발동! (돌격 넉백 {estChargeKnockbackSpeed:F1} >= 기준 {curKnockdownThresh:F1} m/s) → {curKnockdownDur:F1}초간 무력화\n  ※ 일반 평타(창 찌르기)로는 넘어지지 않고 적이 전진 돌파할 수 있습니다.", EditorStyles.miniBoldLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"• 💫 돌격 피격 시 넘어짐 판정: 착지 유지 (돌격 넉백 {estChargeKnockbackSpeed:F1} < 기준 {curKnockdownThresh:F1} m/s)");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "🔬 [돌격 및 물리 충격 핵심 공식 (한국어 표준 정리)]\n" +
                "1. 속도 비례 돌격 피해 공식:\n" +
                "   【돌격 추가 피해 = 돌격 보너스 × (돌격 쇄도 속도 ÷ 4.8 m/s)】\n" +
                "   【최종 충돌 피해 = 기본 공격력 + 돌격 추가 피해】 (상한선 제한 적용)\n" +
                "   • 보병 기본 속도(4.8 m/s)에서는 설정한 보너스가 100% 정직하게 들어갑니다.\n" +
                "   • 기병 등 속도가 빠른 유닛(예: 8.0 m/s)은 속도에 비례하여 데미지가 대폭 상승합니다.\n\n" +
                "2. 돌격 반사 (창벽 방어 태세) 공식:\n" +
                "   【창벽 반사 피해 = 내 기본 공격력 + 적의 돌격 추가 피해】\n" +
                "   • '돌격 반사 능력 보유'에 체크된 유닛(창병)만 제자리에서 적의 돌격을 역으로 되돌려줄 수 있습니다.\n" +
                "   • 검병 등 미체크 유닛은 돌격을 반사하지 못하고 적의 돌격 피해를 그대로 입습니다.\n\n" +
                "3. 넉백 폭증 방지 및 상한선 (수십 미터 날아감 원천 차단):\n" +
                "   • 평타 창 찌르기: 0.45 m/s 수준의 미세 주춤(Micro-Stagger 5~10cm)만 발생하여, 검병이 피해를 감내하며 전진 돌파할 수 있습니다.\n" +
                "   • 다대일 집중 타격 시에도 넉백 속도가 최대 1.2 m/s(돌격 시 2.2 m/s)로 엄격히 클램프되어 수십 미터 튕겨나가지 않습니다.\n" +
                "   • 검병이 창벽을 뚫고 1.2m 이내로 파고들면 창병은 긴 창을 쓰지 못하고 무력화되어 검병이 압도하게 됩니다.",
                MessageType.None
            );

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 🎯 6. 지휘 상태 및 전술 태세 (Command & AI)
        showCommand = EditorGUILayout.Foldout(showCommand, "🎯 6. 지휘 상태 및 전술 태세 (Command & AI)", true);
        if (showCommand)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (isSelected != null)
                EditorGUILayout.PropertyField(isSelected, new GUIContent("플레이어 선택 여부", "현재 플레이어가 마우스 드래그/클릭으로 선택한 유닛인지 여부입니다."));

            if (currentState != null)
                EditorGUILayout.PropertyField(currentState, new GUIContent("현재 작전 명령 상태", "유닛의 현재 전투 행동 상태입니다. (Idle: 대기, Move: 이동, AttackMove: 공격이동, MeleeEngaged: 백병전 교전)"));

            if (currentStance != null)
                EditorGUILayout.PropertyField(currentStance, new GUIContent("전술 공격 태세", "공세(Aggressive: 적극적 타겟팅) 또는 방어(Defensive: 진형 사수) 태세입니다."));

            if (autoAttackEnabled != null)
                EditorGUILayout.PropertyField(autoAttackEnabled, new GUIContent("선제 요격 활성화 (V키)", "체크 시 적 발견 시 자동 돌격 요격, 해제 시 무기를 땅에 박고 버티는 접촉 방어(Bracing) 모드로 동작합니다."));

            if (detectRange != null)
                EditorGUILayout.PropertyField(detectRange, new GUIContent("적 탐색 시야 반경 (m)", "자유 유닛 상태에서 주변의 적을 스스로 탐지하고 인지하는 시야 반경입니다. (기본: 5.0m)"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 🔄 7. 대형 복귀 및 겹침 방지 (Formation Reform & Separation)
        showFormation = EditorGUILayout.Foldout(showFormation, "🔄 7. 대형 복귀 및 겹침 방지 (Formation Reform & Separation)", true);
        if (showFormation)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("대형 이탈 복귀 설정", EditorStyles.miniBoldLabel);
            if (reformThreshold != null)
                EditorGUILayout.PropertyField(reformThreshold, new GUIContent("대형 이탈 허용 오차 (m)", "대형 슬롯 위치에서 몇 미터 이상 벗어났을 때 복귀 타이머를 가동할지 결정합니다. (기본: 0.5m)"));

            if (reformDelay != null)
                EditorGUILayout.PropertyField(reformDelay, new GUIContent("복귀 대기 지연 시간 (초)", "대형에서 벗어난 뒤 복귀 기동을 시작하기까지 대기하는 시간입니다. (기본: 2.0초)"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("개인 공간 및 물리 척력 (Separation)", EditorStyles.miniBoldLabel);

            if (separationRadius != null)
                EditorGUILayout.PropertyField(separationRadius, new GUIContent("개인 공간 반경 (m)", "유닛 간 물리적 겹침을 방지하기 위한 개인 방어 반경입니다. (기본: 0.45m)"));

            if (separationForce != null)
                EditorGUILayout.PropertyField(separationForce, new GUIContent("물리 척력 세기", "유닛끼리 겹쳤을 때 서로를 밀어내는 물리 반발력의 세기입니다. (기본: 1.2)"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // 📌 8. 소속 부대 및 런타임 좌표 (참조용)
        showRuntime = EditorGUILayout.Foldout(showRuntime, "📌 8. 소속 부대 및 런타임 좌표 (참조용)", true);
        if (showRuntime)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (mySquad != null)
                EditorGUILayout.PropertyField(mySquad, new GUIContent("소속 부대 (Squad)", "이 유닛이 배속되어 있는 지휘 부대 오브젝트입니다."));

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

        string faction = unit.isPlayer ? "🔵 플레이어(아군)" : "🔴 적군(Enemy)";
        string squadName = (unit.mySquad != null) ? $"{unit.mySquad.name} [열 {unit.Col}, 행 {unit.Row}]" : "단독 자유 유닛";

        EditorGUILayout.LabelField("📊 [유닛 실시간 전투 스탯 요약]", headerStyle);
        EditorGUILayout.LabelField($"• 소속: {faction} | 부대: {squadName}");
        EditorGUILayout.LabelField($"• 상태: 명령({unit.currentState}) / 태세({unit.currentStance}) / 선제요격({(unit.autoAttackEnabled ? "ON" : "OFF-접촉방어")})");

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"🩺 생명력: {unit.currentHp:F1} / {unit.maxHp:F1} ({hpRatio:F0}%) | 🛡️ 방어력: {unit.armor} (피해 감쇄 {armorReduction:F2}%)");
        EditorGUILayout.LabelField($"⚔️ 주무기 화력: 공격력 {unit.damage:F1} | 주기 {mainCd:F2}s (초당 {mainAps:F2}회) | 지속 화력: {mainDps:F1} DPS");
        EditorGUILayout.LabelField($"📐 교전 사거리: 타격 {unit.attackRange:F2}m (최소 {unit.minAttackRange:F2}m) | 정지거리 {unit.combatStoppingDistance:F2}m | 넉백 {unit.knockbackPower:F1}x");

        if (unit.useSidearm)
        {
            float sideCd = Mathf.Max(0.01f, unit.sidearmAttackCooldown);
            float sideAps = 1.0f / sideCd;
            float sideDps = unit.sidearmDamage * sideAps;
            EditorGUILayout.LabelField($"🗡️ 보조무기 (Sidearm): {unit.sidearmSwitchDistance:F1}m 밀착 시 자동 전환 | 공격력 {unit.sidearmDamage:F1} | {sideDps:F1} DPS (초당 {sideAps:F2}회)");
        }

        EditorGUILayout.LabelField($"🏃 기동성: 제식 {unit.walkSpeed:F1}m/s | 구보 {unit.runSpeed:F1}m/s | 돌격 {unit.chargeSpeed:F1}m/s | ⚖️ 질량: {unit.mass:F0}kg");

        EditorGUILayout.EndVertical();
    }
}
