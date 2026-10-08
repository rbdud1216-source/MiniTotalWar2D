using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BattleManager 컴포넌트의 유니티 인스펙터를 100% 직관적인 한국어 라벨, 
/// 친절한 전술 툴팁, 군단 편성 목록 요약 박스 및 런타임 제어 버튼으로 시각화하는 커스텀 에디터입니다.
/// </summary>
[CustomEditor(typeof(BattleManager))]
[CanEditMultipleObjects]
public class BattleManagerEditor : Editor
{
    // 1. 시뮬레이션 모드
    private SerializedProperty usePureECS;

    // 2. 기본 프리팹
    private SerializedProperty playerUnitPrefab;
    private SerializedProperty playerMissilePrefab;
    private SerializedProperty enemyUnitPrefab;
    private SerializedProperty enemyMissilePrefab;
    private SerializedProperty squadPrefab;

    // 3. 진영 기본 위치 및 방향
    private SerializedProperty playerSpawnCenter;
    private SerializedProperty playerFacingAngle;
    private SerializedProperty enemySpawnCenter;
    private SerializedProperty enemyFacingAngle;
    private SerializedProperty squadSpacing;

    // 4. 전역 방진 스탯 튜닝
    private SerializedProperty overrideSquadFormationSettings;
    private SerializedProperty globalLooseSpacingThreshold;
    private SerializedProperty globalTightSpacingThreshold;
    private SerializedProperty globalNormalBonus;
    private SerializedProperty globalWedgeBonus;
    private SerializedProperty globalSquareBonus;
    private SerializedProperty globalCircleBonus;
    private SerializedProperty globalDiamondBonus;

    // 5. 군단 편성 목록
    private SerializedProperty playerArmyConfigs;
    private SerializedProperty enemyArmyConfigs;

    // 폴드아웃 상태 플래그
    private static bool showSimulationMode = true;
    private static bool showPrefabs = true;
    private static bool showSpawns = true;
    private static bool showFormationStats = false;
    private static bool showPlayerArmy = true;
    private static bool showEnemyArmy = true;

    private void OnEnable()
    {
        usePureECS = serializedObject.FindProperty("usePureECS");

        playerUnitPrefab = serializedObject.FindProperty("playerUnitPrefab");
        playerMissilePrefab = serializedObject.FindProperty("playerMissilePrefab");
        enemyUnitPrefab = serializedObject.FindProperty("enemyUnitPrefab");
        enemyMissilePrefab = serializedObject.FindProperty("enemyMissilePrefab");
        squadPrefab = serializedObject.FindProperty("squadPrefab");

        playerSpawnCenter = serializedObject.FindProperty("playerSpawnCenter");
        playerFacingAngle = serializedObject.FindProperty("playerFacingAngle");
        enemySpawnCenter = serializedObject.FindProperty("enemySpawnCenter");
        enemyFacingAngle = serializedObject.FindProperty("enemyFacingAngle");
        squadSpacing = serializedObject.FindProperty("squadSpacing");

        overrideSquadFormationSettings = serializedObject.FindProperty("overrideSquadFormationSettings");
        globalLooseSpacingThreshold = serializedObject.FindProperty("globalLooseSpacingThreshold");
        globalTightSpacingThreshold = serializedObject.FindProperty("globalTightSpacingThreshold");
        globalNormalBonus = serializedObject.FindProperty("globalNormalBonus");
        globalWedgeBonus = serializedObject.FindProperty("globalWedgeBonus");
        globalSquareBonus = serializedObject.FindProperty("globalSquareBonus");
        globalCircleBonus = serializedObject.FindProperty("globalCircleBonus");
        globalDiamondBonus = serializedObject.FindProperty("globalDiamondBonus");

        playerArmyConfigs = serializedObject.FindProperty("playerArmyConfigs");
        enemyArmyConfigs = serializedObject.FindProperty("enemyArmyConfigs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BattleManager bm = (BattleManager)target;

        // 상단 타이틀 요약 배너
        DrawHeaderBanner(bm);

        EditorGUILayout.Space(6);

        // 1. 시뮬레이션 모드 섹션
        showSimulationMode = EditorGUILayout.Foldout(showSimulationMode, "⚡ 1. 시뮬레이션 엔진 모드", true, EditorStyles.foldoutHeader);
        if (showSimulationMode)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (usePureECS != null)
            {
                EditorGUILayout.PropertyField(usePureECS, new GUIContent("순수 ECS 모드 (Zero-GameObject)", 
                    "체크 시 개별 유닛 GameObject 생성을 완전히 배제하고, 순수 GPU 인스턴싱 ECS 엔티티로 5만 기 이상의 대규모 전투를 60FPS로 구동합니다."));
                if (usePureECS.boolValue)
                {
                    EditorGUILayout.HelpBox("⚡ [순수 ECS 모드 활성화됨] GameObject가 생성되지 않고 PureECSRenderer를 통해 직접 GPU 렌더링됩니다.", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // 2. 기본 프리팹 섹션
        showPrefabs = EditorGUILayout.Foldout(showPrefabs, "📦 2. 기본 유닛 및 부대 프리팹 설정", true, EditorStyles.foldoutHeader);
        if (showPrefabs)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔵 아군(Player) 기본 프리팹", EditorStyles.miniBoldLabel);
            if (playerUnitPrefab != null)
                EditorGUILayout.PropertyField(playerUnitPrefab, new GUIContent("  아군 보병 유닛 프리팹", "플레이어 진영의 기본 근접 보병(검병/창병) 프리팹입니다."));
            if (playerMissilePrefab != null)
                EditorGUILayout.PropertyField(playerMissilePrefab, new GUIContent("  아군 궁병 유닛 프리팹", "플레이어 진영의 기본 원거리 궁병 프리팹입니다."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🔴 적군(Enemy) 기본 프리팹", EditorStyles.miniBoldLabel);
            if (enemyUnitPrefab != null)
                EditorGUILayout.PropertyField(enemyUnitPrefab, new GUIContent("  적군 보병 유닛 프리팹", "적군 진영의 기본 근접 보병 프리팹입니다."));
            if (enemyMissilePrefab != null)
                EditorGUILayout.PropertyField(enemyMissilePrefab, new GUIContent("  적군 궁병 유닛 프리팹", "적군 진영의 기본 원거리 궁병 프리팹입니다."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🚩 부대 컨트롤러 프리팹", EditorStyles.miniBoldLabel);
            if (squadPrefab != null)
                EditorGUILayout.PropertyField(squadPrefab, new GUIContent("  부대 관리자(Squad) 프리팹", "부대 진형, 이동, 명령 등을 총괄하는 Squad 루트 프리팹입니다."));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // 3. 진영 기본 스폰 위치 및 방향 섹션
        showSpawns = EditorGUILayout.Foldout(showSpawns, "📍 3. 진영 기본 스폰 위치 및 정면 방향", true, EditorStyles.foldoutHeader);
        if (showSpawns)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔵 아군(Player) 군단 스폰 기준", EditorStyles.miniBoldLabel);
            if (playerSpawnCenter != null)
                EditorGUILayout.PropertyField(playerSpawnCenter, new GUIContent("  아군 스폰 중심 좌표", "아군 군단이 배치될 중심 월드 좌표입니다. (X, Y, Z)"));
            if (playerFacingAngle != null)
                EditorGUILayout.Slider(playerFacingAngle, 0f, 360f, new GUIContent("  아군 정면 회전 각도", "아군 군단의 기본 정면 각도입니다. (0도 = 북쪽 정면)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🔴 적군(Enemy) 군단 스폰 기준", EditorStyles.miniBoldLabel);
            if (enemySpawnCenter != null)
                EditorGUILayout.PropertyField(enemySpawnCenter, new GUIContent("  적군 스폰 중심 좌표", "적군 군단이 배치될 중심 월드 좌표입니다. (X, Y, Z)"));
            if (enemyFacingAngle != null)
                EditorGUILayout.Slider(enemyFacingAngle, 0f, 360f, new GUIContent("  적군 정면 회전 각도", "적군 군단의 기본 정면 각도입니다. (180도 = 남쪽 정면)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("↔️ 군단 횡대 자동 정렬 간격", EditorStyles.miniBoldLabel);
            if (squadSpacing != null)
                EditorGUILayout.Slider(squadSpacing, 1.0f, 15.0f, new GUIContent("  부대 간 가로 여유 간격 (m)", "군단 횡대 자동 배치 시 인접 부대 사이의 가로 여백 거리입니다."));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // 4. 전역 방진 및 산개도 스탯 튜닝 섹션
        showFormationStats = EditorGUILayout.Foldout(showFormationStats, "🛡️ 4. 전역 방진 및 산개도 스탯 튜닝 (공통 적용)", true, EditorStyles.foldoutHeader);
        if (showFormationStats)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (overrideSquadFormationSettings != null)
                EditorGUILayout.PropertyField(overrideSquadFormationSettings, new GUIContent("전역 방진 스탯 덮어쓰기", "체크 시 부대 생성 시 아래의 스탯 계수를 전체 부대에 일괄 적용합니다."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("📏 산개도(밀집도) 판정 임계 간격", EditorStyles.miniBoldLabel);
            if (globalLooseSpacingThreshold != null)
                EditorGUILayout.Slider(globalLooseSpacingThreshold, 1.5f, 5.0f, new GUIContent("  완전 산개 기준 간격 (m)", "병사 간격이 이 거리 이상으로 넓어지면 밀집 보너스가 0%가 됩니다. (기본: 2.2m)"));
            if (globalTightSpacingThreshold != null)
                EditorGUILayout.Slider(globalTightSpacingThreshold, 0.4f, 1.2f, new GUIContent("  최대 밀집 기준 간격 (m)", "병사 간격이 이 거리 이하로 밀착되면 밀집 보너스가 100% 최대로 적용됩니다. (기본: 0.7m)"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎛️ 방진별 2단계 전역 스탯 계수", EditorStyles.miniBoldLabel);
            if (globalNormalBonus != null)
                DrawFormationModifier(globalNormalBonus, "일반 방진 (Normal)", "표준적인 보병 방진으로 균형 잡힌 기본 방어력과 저지력을 제공합니다.");
            if (globalWedgeBonus != null)
                DrawFormationModifier(globalWedgeBonus, "쐐기진 (Wedge)", "돌격 특화 진형으로 전두부 무게를 집중시켜 적진을 돌파합니다.");
            if (globalSquareBonus != null)
                DrawFormationModifier(globalSquareBonus, "사각방진 (Square)", "사방 방어 특화 진형으로 기병 돌격을 저지하고 높은 방어력을 발휘합니다.");
            if (globalCircleBonus != null)
                DrawFormationModifier(globalCircleBonus, "원형진 (Circle)", "포위 상황 결사항전 진형으로 극도의 방어력을 발휘하지만 공속이 둔화됩니다.");
            if (globalDiamondBonus != null)
                DrawFormationModifier(globalDiamondBonus, "마름모진 (Diamond)", "기동 돌파형 진형으로 기동성과 전선 분할 돌파에 최적화되어 있습니다.");

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // 5. 플레이어 군단 편성 목록
        showPlayerArmy = EditorGUILayout.Foldout(showPlayerArmy, $"🚩 5. 플레이어 군단 편성 목록 ({playerArmyConfigs.arraySize}개 부대)", true, EditorStyles.foldoutHeader);
        if (showPlayerArmy)
        {
            DrawArmyConfigList(playerArmyConfigs, "플레이어 부대", new Color(0.2f, 0.6f, 1.0f, 0.15f));
        }

        EditorGUILayout.Space(6);

        // 6. 적군 군단 편성 목록
        showEnemyArmy = EditorGUILayout.Foldout(showEnemyArmy, $"⚔️ 6. 적군 군단 편성 목록 ({enemyArmyConfigs.arraySize}개 부대)", true, EditorStyles.foldoutHeader);
        if (showEnemyArmy)
        {
            DrawArmyConfigList(enemyArmyConfigs, "적군 부대", new Color(1.0f, 0.35f, 0.2f, 0.15f));
        }

        EditorGUILayout.Space(10);

        // 7. 런타임 디버그 제어 버튼
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 런타임 실시간 제어", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚔️ 전투 시나리오 재스폰", GUILayout.Height(30)))
            {
                bm.SpawnBattleScenario();
            }
            if (GUILayout.Button("🔄 전체 유닛 물리 리셋", GUILayout.Height(30)))
            {
                bm.ResetAllUnitsPhysics();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 상단 상태 및 배치 현황 요약 배너를 렌더링합니다.
    /// </summary>
    private void DrawHeaderBanner(BattleManager bm)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚔️ 토탈워 전장 총괄 관리자 (BattleManager)", EditorStyles.boldLabel);
        string modeText = bm.usePureECS ? "순수 ECS 모드 (Zero-GameObject)" : "하이브리드 모드 (GameObject + Job)";
        int pCount = bm.PlayerArmyConfigs != null ? bm.PlayerArmyConfigs.Count : 0;
        int eCount = bm.EnemyArmyConfigs != null ? bm.EnemyArmyConfigs.Count : 0;
        EditorGUILayout.LabelField($"현재 엔진: {modeText}  |  아군 부대: {pCount}개  |  적군 부대: {eCount}개", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 군단 편성 리스트(List&lt;SquadSpawnConfig&gt;)를 직관적인 한국어 카드로 렌더링합니다.
    /// </summary>
    private void DrawArmyConfigList(SerializedProperty listProp, string defaultLabelPrefix, Color tintColor)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        int count = listProp.arraySize;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"등록된 부대 수: {count}개", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("➕ 새 부대 추가", GUILayout.Width(110)))
        {
            listProp.InsertArrayElementAtIndex(count);
            SerializedProperty newElem = listProp.GetArrayElementAtIndex(count);
            newElem.FindPropertyRelative("squadName").stringValue = $"{defaultLabelPrefix} {count + 1}";
            newElem.FindPropertyRelative("unitCount").intValue = 60;
            newElem.FindPropertyRelative("columns").intValue = 15;
            newElem.FindPropertyRelative("formationType").enumValueIndex = 0;
            newElem.FindPropertyRelative("useCustomPosition").boolValue = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        for (int i = 0; i < count; i++)
        {
            SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
            SerializedProperty sName = elem.FindPropertyRelative("squadName");
            SerializedProperty uType = elem.FindPropertyRelative("unitType");
            SerializedProperty uCount = elem.FindPropertyRelative("unitCount");
            SerializedProperty cols = elem.FindPropertyRelative("columns");
            SerializedProperty fType = elem.FindPropertyRelative("formationType");
            SerializedProperty useCustom = elem.FindPropertyRelative("useCustomPosition");
            SerializedProperty customPos = elem.FindPropertyRelative("customPosition");
            SerializedProperty customRot = elem.FindPropertyRelative("customRotationY");
            SerializedProperty customPrefab = elem.FindPropertyRelative("customUnitPrefab");

            string displayName = sName != null && !string.IsNullOrEmpty(sName.stringValue) ? sName.stringValue : $"{defaultLabelPrefix} {i + 1}";
            string unitTypeStr = uType != null ? uType.enumDisplayNames[uType.enumValueIndex] : "보병";
            int units = uCount != null ? uCount.intValue : 60;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더 줄 (요약 표시 및 삭제 버튼)
            EditorGUILayout.BeginHorizontal();
            elem.isExpanded = EditorGUILayout.Foldout(elem.isExpanded, $"#{i + 1} [{displayName}] ({unitTypeStr}, {units}명)", true, EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕ 삭제", GUILayout.Width(55)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (elem.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);

                if (sName != null)
                    EditorGUILayout.PropertyField(sName, new GUIContent("부대 명칭", "UI 및 전황 로그에 표시되는 부대의 고유 이름입니다."));
                if (uType != null)
                    EditorGUILayout.PropertyField(uType, new GUIContent("병과 종류", "근접 보병, 창병, 궁병, 기병 등 병과의 역할을 지정합니다."));
                if (uCount != null)
                    EditorGUILayout.IntSlider(uCount, 1, 200, new GUIContent("부대 총 인원 수", "이 부대에 소속될 병사의 총 인원 수입니다."));
                if (cols != null)
                    EditorGUILayout.IntSlider(cols, 1, 50, new GUIContent("가로 열(Columns) 수", "대형의 가로 폭(한 열에 서는 병사 수)입니다."));
                if (fType != null)
                    EditorGUILayout.PropertyField(fType, new GUIContent("초기 진형 형태", "스폰 시 초기에 형성할 기본 진형입니다. (일반, 쐐기진, 사각방진 등)"));

                EditorGUILayout.Space(2);
                if (useCustom != null)
                    EditorGUILayout.PropertyField(useCustom, new GUIContent("수동 좌표 스폰 사용", "체크 시 자동 횡대 배치를 무시하고 아래의 수동 월드 좌표에 스폰합니다."));

                if (useCustom != null && useCustom.boolValue)
                {
                    EditorGUI.indentLevel++;
                    if (customPos != null)
                        EditorGUILayout.PropertyField(customPos, new GUIContent("수동 스폰 좌표 (World)", "부대 중심이 스폰될 월드 좌표 (X, Y, Z)입니다."));
                    if (customRot != null)
                        EditorGUILayout.Slider(customRot, 0f, 360f, new GUIContent("수동 회전 각도 (Y축)", "부대가 바라볼 정면 각도입니다."));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(2);
                if (customPrefab != null)
                    EditorGUILayout.PropertyField(customPrefab, new GUIContent("개별 유닛 프리팹 (선택)", "지정하지 않으면 진영 기본 프리팹을 사용합니다. 특정 특수 유닛을 배치할 때 사용합니다."));

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 방진별 2단계 전역 스탯 계수 프로퍼티를 렌더링합니다.
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
            EditorGUILayout.IntSlider(baseArmor, 0, 5000, new GUIContent("    기본 추가 방어력", "진형 형태 자체로 기본 부여되는 방어력 (+만분율). 500 = +5.00%"));
        if (baseMass != null)
            EditorGUILayout.Slider(baseMass, 1.0f, 3.0f, new GUIContent("    기본 무게 배율", "진형 전환 시 기본 적용되는 무게 배율. 1.2 = +20% 증가로 돌격 저지력 향상."));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("  [2단계: 산개도 비례 추가 효과 - 밀집할수록 점진적 적용]", EditorStyles.miniBoldLabel);
        if (densityArmor != null)
            EditorGUILayout.IntSlider(densityArmor, 0, 5000, new GUIContent("    최대 밀집 추가 방어력", "최대 밀집 시 추가 가산되는 방어력 (+만분율). 1000 = +10.00%"));
        if (densityMass != null)
            EditorGUILayout.Slider(densityMass, 1.0f, 3.0f, new GUIContent("    최대 밀집 추가 무게", "최대 밀집 시 추가 가산되는 무게 배율. 1.5 = +50% 추가"));
        if (densitySpeedRatio != null)
            EditorGUILayout.Slider(densitySpeedRatio, 0.3f, 1.0f, new GUIContent("    밀집 시 공격 속도 비율", "최대 밀집 시 공속 둔화율. 0.50 = 공속 50% 수준으로 반감."));

        EditorGUILayout.EndVertical();
    }
}
