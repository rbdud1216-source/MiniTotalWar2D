using UnityEditor;
using UnityEngine;

/// <summary>
/// Unit 컴포넌트의 유니티 인스펙터를 한국어 라벨과 친절한 툴팁 설명으로 표시하는 커스텀 에디터입니다.
/// </summary>
[CustomEditor(typeof(Unit))]
[CanEditMultipleObjects]
public class UnitEditor : Editor
{
    private SerializedProperty isPlayer;
    private SerializedProperty maxHp;
    private SerializedProperty currentHp;
    private SerializedProperty damage;
    private SerializedProperty attackCooldown;
    private SerializedProperty attackRange;
    private SerializedProperty minAttackRange;
    private SerializedProperty optimalRangeMin;
    private SerializedProperty closeRangeDamageRatio;
    private SerializedProperty knockbackPower;

    private SerializedProperty useSidearm;
    private SerializedProperty sidearmSwitchDistance;
    private SerializedProperty sidearmAttackRange;
    private SerializedProperty sidearmDamage;
    private SerializedProperty sidearmAttackCooldown;
    private SerializedProperty sidearmKnockbackPower;

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

    private void OnEnable()
    {
        isPlayer = serializedObject.FindProperty("isPlayer");
        maxHp = serializedObject.FindProperty("maxHp");
        currentHp = serializedObject.FindProperty("currentHp");
        damage = serializedObject.FindProperty("damage");
        attackCooldown = serializedObject.FindProperty("attackCooldown");
        attackRange = serializedObject.FindProperty("attackRange");
        minAttackRange = serializedObject.FindProperty("minAttackRange");
        optimalRangeMin = serializedObject.FindProperty("optimalRangeMin");
        closeRangeDamageRatio = serializedObject.FindProperty("closeRangeDamageRatio");
        knockbackPower = serializedObject.FindProperty("knockbackPower");

        useSidearm = serializedObject.FindProperty("useSidearm");
        sidearmSwitchDistance = serializedObject.FindProperty("sidearmSwitchDistance");
        sidearmAttackRange = serializedObject.FindProperty("sidearmAttackRange");
        sidearmDamage = serializedObject.FindProperty("sidearmDamage");
        sidearmAttackCooldown = serializedObject.FindProperty("sidearmAttackCooldown");
        sidearmKnockbackPower = serializedObject.FindProperty("sidearmKnockbackPower");

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

        // 1. 유닛 기본 설정
        EditorGUILayout.LabelField("유닛 기본 설정", EditorStyles.boldLabel);
        if (isPlayer != null)
            EditorGUILayout.PropertyField(isPlayer, new GUIContent("플레이어 진영 여부", "체크 시 아군(Player), 체크 해제 시 적군(Enemy) 진영으로 분류됩니다."));
        if (maxHp != null)
            EditorGUILayout.PropertyField(maxHp, new GUIContent("최대 체력 (Max HP)", "유닛의 최대 생명력입니다. (기본: 100)"));
        if (currentHp != null)
            EditorGUILayout.PropertyField(currentHp, new GUIContent("현재 체력 (Current HP)", "유닛의 실시간 현재 생명력입니다. 게임 시작 시 최대 체력으로 자동 초기화됩니다."));
        if (damage != null)
            EditorGUILayout.PropertyField(damage, new GUIContent("기본 공격력 (Damage)", "일반 백병전 근접 공격 시 1회당 입히는 피해량입니다."));
        if (attackCooldown != null)
            EditorGUILayout.PropertyField(attackCooldown, new GUIContent("공격 주기 (Attack Cooldown)", "다음 공격까지의 대기 시간(초)입니다. 수치가 낮을수록 더 자주 공격합니다."));

        EditorGUILayout.Space(6);

        // 2. 주무기 및 교전 사거리 설정
        EditorGUILayout.LabelField("주무기 및 교전 사거리 설정", EditorStyles.boldLabel);
        if (attackRange != null)
            EditorGUILayout.PropertyField(attackRange, new GUIContent("공격 사거리 (Attack Range)", "유닛이 적을 타격할 수 있는 유효 사거리(m)입니다. (검병 권장: 1.45m, 장창병 권장: 3.0m)"));
        if (minAttackRange != null)
            EditorGUILayout.PropertyField(minAttackRange, new GUIContent("최소 사거리 (Min Attack Range)", "주무기 최소 사거리(m)입니다. 이보다 가까운 적은 주무기로 공격할 수 없습니다. 0이면 사각지대 없음. (검병: 0m, 장창병: 1.2m~1.5m 권장)"));
        if (optimalRangeMin != null)
            EditorGUILayout.PropertyField(optimalRangeMin, new GUIContent("최적 사거리 기준 (Optimal Min)", "주무기 최적 사거리(스위트스팟) 최소 기준거리(m)입니다. 이 거리 이상에서 100% 정상 피해를 줍니다. (검병: 0m, 장창병: 1.8m 권장)"));
        if (closeRangeDamageRatio != null)
            EditorGUILayout.PropertyField(closeRangeDamageRatio, new GUIContent("근접 피해 감쇠 비율 (Close Falloff)", "최적 사거리 미만(품 안)으로 파고든 적에게 가하는 주무기 피해량 비율(0.1~1.0)입니다. (검병: 1.0=감쇠 없음, 장창병: 0.35 권장)"));
        if (knockbackPower != null)
            EditorGUILayout.PropertyField(knockbackPower, new GUIContent("주무기 넉백 배율 (Knockback Power)", "타격 시 적을 밀쳐내는 넉백 세기 배율입니다. (검병: 1.0, 장창병: 2.0~2.5 권장)"));
        if (combatStoppingDistance != null)
            EditorGUILayout.PropertyField(combatStoppingDistance, new GUIContent("교전 정지 거리 (Stopping Dist)", "적과 교전(백병전) 시 완전히 발을 멈추고 제자리에서 공격하는 거리(m)입니다. (검병: 1.05m, 장창병: 2.5~3.0m)"));
        if (engagementOffset != null)
            EditorGUILayout.PropertyField(engagementOffset, new GUIContent("교전 목표 간격 (Engage Offset)", "적을 향해 이동할 때 적의 중심으로부터 유지할 목표 교전 간격(m)입니다. (검병: 0.4m, 장창병: 2.0~2.5m)"));

        EditorGUILayout.Space(6);

        // 3. 보조무기 (단검 / Sidearm) 설정
        EditorGUILayout.LabelField("보조무기 (단검 / Sidearm) 설정", EditorStyles.boldLabel);
        if (useSidearm != null)
            EditorGUILayout.PropertyField(useSidearm, new GUIContent("보조무기 자동 전환 (Use Sidearm)", "적이 일정 거리 이내로 밀착 시 단검으로 자동 전환할지 여부입니다. (검병: 체크 해제, 장창병: 체크 권장)"));
        if (sidearmSwitchDistance != null)
            EditorGUILayout.PropertyField(sidearmSwitchDistance, new GUIContent("단검 전환 거리 (Switch Dist)", "단검으로 무기를 전환하는 적과의 거리 기준(m)입니다. (장창병: 1.2m 권장)"));
        if (sidearmAttackRange != null)
            EditorGUILayout.PropertyField(sidearmAttackRange, new GUIContent("단검 공격 사거리 (Attack Range)", "단검의 유효 타격 사거리(m)입니다. (단검 권장: 1.0m)"));
        if (sidearmDamage != null)
            EditorGUILayout.PropertyField(sidearmDamage, new GUIContent("단검 공격력 (Damage)", "단검 1회 타격 피해량입니다. (장창병 단검: 3.5~4.0 권장)"));
        if (sidearmAttackCooldown != null)
            EditorGUILayout.PropertyField(sidearmAttackCooldown, new GUIContent("단검 공격 주기 (Cooldown)", "단검 공격 쿨다운(초)입니다. (단검 연타 권장: 0.8s)"));
        if (sidearmKnockbackPower != null)
            EditorGUILayout.PropertyField(sidearmKnockbackPower, new GUIContent("단검 넉백 배율 (Knockback)", "단검 타격 시 적을 밀쳐내는 넉백 배율입니다. 단검은 밀치는 힘이 거의 없으므로 0.1 권장."));

        EditorGUILayout.Space(6);

        // 4. 기동 및 속도 설정
        EditorGUILayout.LabelField("기동 및 속도 설정", EditorStyles.boldLabel);
        if (isRunning != null)
            EditorGUILayout.PropertyField(isRunning, new GUIContent("달리기 모드 여부", "현재 구보(달리기) 상태인지 여부입니다."));
        if (walkSpeed != null)
            EditorGUILayout.PropertyField(walkSpeed, new GUIContent("걷기 속도 (Walk Speed)", "부대 기본 제식 보행 속도(m/s)입니다."));
        if (runSpeed != null)
            EditorGUILayout.PropertyField(runSpeed, new GUIContent("달리기 속도 (Run Speed)", "전술 구보 달리기 속도(m/s)입니다."));
        if (chargeSpeed != null)
            EditorGUILayout.PropertyField(chargeSpeed, new GUIContent("돌격 속도 (Charge Speed)", "적을 향해 돌격할 때의 최대 이동 속도(m/s)입니다."));
        if (unitMaxSpeed != null)
            EditorGUILayout.PropertyField(unitMaxSpeed, new GUIContent("유닛 최고 한계 속도", "물리 가속이나 넉백 시 유닛이 낼 수 있는 물리적 최고 속도 제한(m/s)입니다."));

        EditorGUILayout.Space(8);

        // 2. 돌격 및 물리 충격 설정
        EditorGUILayout.LabelField("돌격 및 물리 충격 설정", EditorStyles.boldLabel);
        if (mass != null)
            EditorGUILayout.PropertyField(mass, new GUIContent("유닛 질량 (Mass, kg)", "유닛의 무게입니다. 충돌 시 상대방을 밀쳐내는 넉백 거리와 저항력에 영향을 줍니다."));
        if (chargeBonus != null)
            EditorGUILayout.PropertyField(chargeBonus, new GUIContent("돌격 충격 보너스", "돌격 속도로 첫 충돌 시 기본 공격력에 추가되는 충격량 피해 계수입니다."));
        if (maxChargeDamage != null)
            EditorGUILayout.PropertyField(maxChargeDamage, new GUIContent("최대 돌격 충돌 피해", "돌격 첫 충돌 시 가해질 수 있는 최대 피해량 상한선입니다."));

        EditorGUILayout.Space(8);

        // 3. 지휘 및 상태 설정
        EditorGUILayout.LabelField("지휘 및 상태 설정", EditorStyles.boldLabel);
        if (isSelected != null)
            EditorGUILayout.PropertyField(isSelected, new GUIContent("선택 상태 여부", "플레이어가 마우스로 이 유닛을 선택했는지 여부입니다."));
        if (currentState != null)
            EditorGUILayout.PropertyField(currentState, new GUIContent("현재 작전 상태", "유닛의 현재 작전 명령 상태입니다 (Idle: 대기, Move: 이동, AttackMove: 공격이동, MeleeEngaged: 백병전)."));
        if (currentStance != null)
            EditorGUILayout.PropertyField(currentStance, new GUIContent("전술 태세", "유닛의 공격 성향입니다 (Aggressive: 공세, Defensive: 방어)."));
        if (autoAttackEnabled != null)
            EditorGUILayout.PropertyField(autoAttackEnabled, new GUIContent("선제 요격 활성화 (V키)", "적 접근 시 자동으로 선제 돌격할지 여부입니다. 해제 시 제자리 방어선 유지(접촉 방어) 모드로 동작합니다."));
        if (detectRange != null)
            EditorGUILayout.PropertyField(detectRange, new GUIContent("적 감지 시야 반경", "자유 유닛 상태에서 주변 적을 스스로 탐색하는 반경(미터)입니다."));

        EditorGUILayout.Space(8);

        // 4. 개별 복귀 설정
        EditorGUILayout.LabelField("개별 복귀 설정", EditorStyles.boldLabel);
        if (reformThreshold != null)
            EditorGUILayout.PropertyField(reformThreshold, new GUIContent("대형 이탈 허용 오차", "대형 슬롯 위치에서 몇 미터 이상 벗어났을 때 복귀 카운트다운을 시작할지 결정합니다."));
        if (reformDelay != null)
            EditorGUILayout.PropertyField(reformDelay, new GUIContent("복귀 대기 지연 시간", "대형에서 벗어난 뒤 복귀를 시작하기까지의 대기 시간(초)입니다."));

        EditorGUILayout.Space(8);

        // 5. 겹침 방지 (Separation) 설정
        EditorGUILayout.LabelField("겹침 방지 (Separation) 설정", EditorStyles.boldLabel);
        if (separationRadius != null)
            EditorGUILayout.PropertyField(separationRadius, new GUIContent("개인 공간 반경 (Radius)", "유닛 간 물리적 겹침을 방지하기 위한 개인 방어 반경(m)입니다."));
        if (separationForce != null)
            EditorGUILayout.PropertyField(separationForce, new GUIContent("척력 반발력 (Force)", "유닛끼리 겹쳤을 때 서로를 밀어내는 물리 척력의 세기입니다."));

        EditorGUILayout.Space(8);

        // 6. 소속 및 좌표 (참조용)
        if (mySquad != null || fixedTargetPos != null)
        {
            EditorGUILayout.LabelField("부대 연동 좌표 (런타임)", EditorStyles.boldLabel);
            if (mySquad != null)
                EditorGUILayout.PropertyField(mySquad, new GUIContent("소속 부대 (Squad)", "이 유닛이 소속된 부대 오브젝트입니다."));
            if (fixedTargetPos != null)
                EditorGUILayout.PropertyField(fixedTargetPos, new GUIContent("지정 목표 슬롯 좌표", "부대 대형 내에서 이 유닛이 이동해야 할 월드 좌표입니다."));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
