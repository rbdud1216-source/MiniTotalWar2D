using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;
using MiniTotalWar.ECS;

/// <summary>
/// 📜 [삼국지 토탈워 스타일] 우측 상단 부대 및 유닛 상세 정보 패널 (Unit Detail Info Panel)
/// 플레이어가 아군 단일 부대/유닛을 선택하거나, 적 부대/유닛을 클릭(조사) 또는 마우스 오버(호버)했을 때
/// 화면 우측 상단에 고품격 수묵/골드 테마(적군은 흑홍/루비 테마)의 정보창을 띄워 생명력, 체력(피로도), 사기, 전투 스탯을 실시간 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class UnitDetailInfoPanel : MonoBehaviour
{
    private static UnitDetailInfoPanel _instance;
    public static UnitDetailInfoPanel Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<UnitDetailInfoPanel>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UnitDetailInfoPanel");
                    _instance = go.AddComponent<UnitDetailInfoPanel>();
                }
            }
            return _instance;
        }
    }

    [Header("UI 표시 설정")]
    [Tooltip("정보창 패널 표시 활성화 여부")]
    public bool isEnabled = true;

    [Header("패널 위치 및 크기 (우측 상단)")]
    public float panelWidth = 350f;
    public float panelHeight = 360f;
    public float marginRight = 20f;
    public float marginTop = 35f;

    // 실시간 페이드 인/아웃 알파 (0.0f ~ 1.0f)
    private float displayAlpha = 0f;
    private const float FADE_SPEED = 8.0f;

    // 캐시된 참조
    private PlayerController playerController;

    // GUI 텍스처 및 스타일 캐시 (아군: 칠흑 수묵 & 앤틱 골드)
    private Texture2D bgTex;
    private Texture2D borderTex;
    private Texture2D goldBorderTex;
    private Texture2D dividerTex;

    // GUI 텍스처 및 스타일 캐시 (적군: 흑홍빛 수묵 & 루비 골드)
    private Texture2D enemyBgTex;
    private Texture2D enemyBorderTex;
    private Texture2D enemyGoldBorderTex;
    private Texture2D enemyDividerTex;

    // 게이지 공용 텍스처
    private Texture2D barBgTex;
    private Texture2D healthBarTex;
    private Texture2D enemyHealthBarTex;
    private Texture2D moraleBarTex;
    private Texture2D staminaBarTex;
    private Texture2D ammoBarTex;
    private Texture2D statBarTex;

    // 스타일 캐시
    private GUIStyle titleStyle;
    private GUIStyle enemyTitleStyle;
    private GUIStyle badgeStyle;
    private GUIStyle enemyBadgeStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle footerStyle;
    private GUIStyle killStyle;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
            if (playerController == null) return;
        }

        // 유효한 대상이 존재하는지 판정 (적군 호버/검사 또는 아군 단일 선택)
        bool hasTarget = GetActiveTarget(out _, out _, out _, out _);

        float targetAlpha = (isEnabled && hasTarget) ? 1.0f : 0.0f;
        displayAlpha = Mathf.MoveTowards(displayAlpha, targetAlpha, Time.unscaledDeltaTime * FADE_SPEED);
    }

    /// <summary>
    /// 현재 정보창에 표시할 최우선 대상을 식별합니다.
    /// 우선순위:
    /// 1순위: 마우스 오버 중인 발각된 적 부대/유닛 (호버 프리뷰)
    /// 2순위: 플레이어가 클릭하여 조사 중인 발각된 적 부대/유닛 (고정 검사 모드)
    /// 3순위: 플레이어가 단일 선택한 아군 부대/유닛 (기존 선택 모드)
    /// </summary>
    private bool GetActiveTarget(out Squad targetSquad, out Unit targetUnit, out Entity targetEntity, out bool isEnemy)
    {
        targetSquad = null;
        targetUnit = null;
        targetEntity = Entity.Null;
        isEnemy = false;

        if (playerController == null) return false;

        // 1순위: 마우스 커서 아래에 위치한 발각된 적군 (호버 프리뷰)
        if (playerController.hoveredEnemySquad != null && playerController.hoveredEnemySquad.MemberCount > 0 && playerController.hoveredEnemySquad.isSpotted)
        {
            targetSquad = playerController.hoveredEnemySquad;
            isEnemy = true;
            return true;
        }
        if (playerController.hoveredEnemyUnit != null && playerController.hoveredEnemyUnit.IsAlive && playerController.hoveredEnemyUnit.isSpotted)
        {
            targetUnit = playerController.hoveredEnemyUnit;
            isEnemy = true;
            return true;
        }
        if (playerController.hoveredEnemyEntity != Entity.Null)
        {
            targetEntity = playerController.hoveredEnemyEntity;
            isEnemy = true;
            return true;
        }

        // 2순위: 플레이어가 클릭하여 조사(Inspect) 중인 발각된 적군
        if (playerController.inspectedEnemySquad != null && playerController.inspectedEnemySquad.MemberCount > 0 && playerController.inspectedEnemySquad.isSpotted)
        {
            targetSquad = playerController.inspectedEnemySquad;
            isEnemy = true;
            return true;
        }
        if (playerController.inspectedEnemyUnit != null && playerController.inspectedEnemyUnit.IsAlive && playerController.inspectedEnemyUnit.isSpotted)
        {
            targetUnit = playerController.inspectedEnemyUnit;
            isEnemy = true;
            return true;
        }
        if (playerController.inspectedEnemyEntity != Entity.Null)
        {
            targetEntity = playerController.inspectedEnemyEntity;
            isEnemy = true;
            return true;
        }

        // 3순위: 플레이어가 단일 선택한 아군 부대 / 유닛 / 순수 ECS 엔티티
        int squadCount = playerController.SelectedSquads != null ? playerController.SelectedSquads.Count : 0;
        int unitCount = playerController.selectedUnits != null ? playerController.selectedUnits.Count : 0;
        int ecsCount = playerController.SelectedECSEntities != null ? playerController.SelectedECSEntities.Count : 0;

        if (squadCount == 1 && unitCount == 0 && ecsCount == 0)
        {
            targetSquad = playerController.SelectedSquads[0];
            isEnemy = false;
            return targetSquad != null && targetSquad.MemberCount > 0;
        }
        if (squadCount == 0 && unitCount == 1 && ecsCount == 0)
        {
            targetUnit = playerController.selectedUnits[0];
            isEnemy = false;
            return targetUnit != null && targetUnit.IsAlive;
        }
        if (squadCount == 0 && unitCount == 0 && ecsCount == 1)
        {
            targetEntity = playerController.SelectedECSEntities[0];
            isEnemy = false;
            return targetEntity != Entity.Null;
        }

        return false;
    }

    private void OnGUI()
    {
        if (displayAlpha <= 0.005f) return;

        InitStylesIfNeeded();

        if (!GetActiveTarget(out Squad squad, out Unit unit, out Entity entity, out bool isEnemy)) return;

        Color originalGuiColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, displayAlpha);

        // 📍 화면 우측 상단(Top-Right) 배치 좌표 계산
        float x = Screen.width - panelWidth - marginRight;
        float y = marginTop;
        Rect panelRect = new Rect(x, y, panelWidth, panelHeight);

        // 1. 삼국지 토탈워 수묵 먹물 배경 & 앤틱 골드/루비 프레임 렌더링
        DrawAntiquePanel(panelRect, isEnemy);

        // 2. 단일 부대 또는 유닛 데이터 렌더링
        Rect contentRect = new Rect(panelRect.x + 14f, panelRect.y + 12f, panelRect.width - 28f, panelRect.height - 24f);

        if (squad != null)
        {
            DrawSquadDetails(contentRect, squad, isEnemy);
        }
        else if (unit != null)
        {
            DrawUnitDetails(contentRect, unit, isEnemy);
        }
        else if (entity != Entity.Null)
        {
            DrawECSEntityDetails(contentRect, entity, isEnemy);
        }

        GUI.color = originalGuiColor;
    }

    #region 삼국지 토탈워 수묵 & 앤틱 프레임 렌더링 (아군 골드 / 적군 루비)

    private void DrawAntiquePanel(Rect rect, bool isEnemy)
    {
        Texture2D currentBg = isEnemy ? enemyBgTex : bgTex;
        Texture2D currentGoldBorder = isEnemy ? enemyGoldBorderTex : goldBorderTex;
        Texture2D currentInnerBorder = isEnemy ? enemyBorderTex : borderTex;

        // 칠흑색(아군) / 흑홍색(적군) 수묵 배경
        GUI.DrawTexture(rect, currentBg);

        // 외곽 앤틱 골드 / 루비 테두리 (1.5px)
        DrawBorder(rect, currentGoldBorder, 1);

        // 안쪽 미세 테두리 (입체 음영)
        Rect innerRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
        DrawBorder(innerRect, currentInnerBorder, 1);

        // 네 모서리 앤틱 장식 포인트
        float cornerSize = 8f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, cornerSize, 2), currentGoldBorder);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, cornerSize), currentGoldBorder);

        GUI.DrawTexture(new Rect(rect.xMax - cornerSize, rect.y, cornerSize, 2), currentGoldBorder);
        GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, cornerSize), currentGoldBorder);

        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, cornerSize, 2), currentGoldBorder);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - cornerSize, 2, cornerSize), currentGoldBorder);

        GUI.DrawTexture(new Rect(rect.xMax - cornerSize, rect.yMax - 2, cornerSize, 2), currentGoldBorder);
        GUI.DrawTexture(new Rect(rect.xMax - 2, rect.yMax - cornerSize, 2, cornerSize), currentGoldBorder);
    }

    private void DrawBorder(Rect rect, Texture2D tex, int thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), tex);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), tex);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), tex);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), tex);
    }

    #endregion

    #region 부대(Squad) 상세 정보 렌더링

    private void DrawSquadDetails(Rect area, Squad squad, bool isEnemy)
    {
        float curY = area.y;

        // 1. 헤더: 부대명, 병종 뱃지, 킬 수
        string nameText = string.IsNullOrEmpty(squad.squadName) ? squad.gameObject.name : squad.squadName;
        string typeBadge = GetUnitTypeBadge(squad.unitType);
        string factionTag = isEnemy ? "<color=#FF6B6B>[적군]</color>" : "<color=#5DADE2>[아군]</color>";
        string headerIcon = isEnemy ? "⚔️" : "🛡️";

        GUIStyle activeTitleStyle = isEnemy ? enemyTitleStyle : titleStyle;
        GUIStyle activeBadgeStyle = isEnemy ? enemyBadgeStyle : badgeStyle;
        Texture2D activeDivider = isEnemy ? enemyDividerTex : dividerTex;
        Texture2D activeHpBar = isEnemy ? enemyHealthBarTex : healthBarTex;

        GUI.Label(new Rect(area.x, curY, area.width - 90f, 22f), $"{headerIcon} {nameText}", activeTitleStyle);
        GUI.Label(new Rect(area.xMax - 85f, curY, 85f, 20f), $"⚔️ {squad.totalKills} 처치", killStyle);
        curY += 24f;

        GUI.Label(new Rect(area.x, curY, 180f, 18f), $"{factionTag} [{typeBadge}]", activeBadgeStyle);
        curY += 20f;

        // 구분선
        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        // 2. 생명력(Health) 및 병력 수치 바
        int aliveCount = squad.MemberCount;
        int maxCount = (squad.initialUnitCount > 0) ? squad.initialUnitCount : aliveCount;
        float countRatio = (maxCount > 0) ? Mathf.Clamp01((float)aliveCount / maxCount) : 0f;

        DrawStatGaugeRow(area.x, ref curY, area.width, "👥 병력 수", $"{aliveCount} / {maxCount} ({countRatio * 100f:F0}%)", countRatio, activeHpBar);

        // 3. 사기(Morale / Leadership) 바
        float curMorale = squad.currentMorale > 0 ? squad.currentMorale : 75f;
        float maxMorale = 100f;
        float moraleRatio = Mathf.Clamp01(curMorale / maxMorale);
        string moraleStateName = GetMoraleStateName(curMorale);

        DrawStatGaugeRow(area.x, ref curY, area.width, "🧠 사기(통솔)", $"{curMorale:F0} / {maxMorale:F0} [{moraleStateName}]", moraleRatio, moraleBarTex);

        // 4. 체력 및 활력(Stamina / Fatigue) 바
        float curStamina = squad.currentStamina > 0 ? squad.currentStamina : 82f;
        float staminaRatio = Mathf.Clamp01(curStamina / 100f);
        string fatigueName = GetFatigueStageName(curStamina);

        DrawStatGaugeRow(area.x, ref curY, area.width, "⚡ 체력(활력)", $"{curStamina:F0}% [{fatigueName}]", staminaRatio, staminaBarTex);

        // 구분선
        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        // 5. 핵심 전투 능력치 격자 (2열 구조)
        float colWidth = (area.width - 10f) * 0.5f;
        float leftX = area.x;
        float rightX = area.x + colWidth + 10f;

        // 기본 방어력 및 방진 밀집도 실효 방어력 계산
        float tightness = squad.GetFormationTightness();
        float armorBonus = tightness * 12f;
        float displayArmor = Mathf.Clamp((squad.baseArmor / 100f) + armorBonus, 0f, 100f);

        DrawStatItem(leftX, curY, colWidth, "근접 공격", $"{10f + (tightness * 2f):F0} (AP: 4)");
        DrawStatItem(rightX, curY, colWidth, "장갑/방어", $"{displayArmor:F1}% (+{armorBonus:F1}%)");
        curY += 20f;

        DrawStatItem(leftX, curY, colWidth, "근접 방어", $"{35 + Mathf.RoundToInt(tightness * 8f)}");
        DrawStatItem(rightX, curY, colWidth, "돌격 보너스", $"+{12 + Mathf.RoundToInt(tightness * 4f)}");
        curY += 20f;

        if (squad.IsRangedSquad)
        {
            int curAmmo = squad.GetCurrentTotalAmmo();
            int maxAmmo = squad.GetMaxTotalAmmo();
            DrawStatItem(leftX, curY, colWidth, "사격 사거리", $"{squad.rangedAttackRange:F0}m");
            DrawStatItem(rightX, curY, colWidth, "잔여 탄약", $"{curAmmo} / {maxAmmo} 발");
            curY += 20f;
        }

        string speedMode = squad.isRunning ? "구보" : "보행";
        float spd = squad.isRunning ? squad.runSpeed : squad.walkSpeed;
        DrawStatItem(leftX, curY, colWidth, "이동 속도", $"{spd:F1} m/s ({speedMode})");
        DrawStatItem(rightX, curY, colWidth, "물리 질량", "100 (보병)");
        curY += 24f;

        // 구분선
        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        // 6. 하단 푸터: 현재 행동 상태 및 전술 대형
        string stateDesc = GetCommandStateDescription(squad.currentCommandState);
        string formDesc = GetFormationDescription(squad.currentFormationType, squad.GetFormationTightness());

        GUI.Label(new Rect(area.x, curY, area.width, 18f), $"🚩 행동: <color=#F5D77F>{stateDesc}</color>", footerStyle);
        curY += 18f;
        GUI.Label(new Rect(area.x, curY, area.width, 18f), $"📐 진형: <color=#AEE5D8>{formDesc}</color>", footerStyle);
    }

    #endregion

    #region 자유 유닛(Unit / ECS) 상세 정보 렌더링

    private void DrawUnitDetails(Rect area, Unit unit, bool isEnemy)
    {
        float curY = area.y;

        string nameText = unit.gameObject.name;
        string typeBadge = GetUnitTypeBadge(unit.unitType);
        string factionTag = isEnemy ? "<color=#FF6B6B>[적군 자유유닛]</color>" : "<color=#5DADE2>[아군 자유유닛]</color>";
        string headerIcon = isEnemy ? "⚔️" : "👤";

        GUIStyle activeTitleStyle = isEnemy ? enemyTitleStyle : titleStyle;
        GUIStyle activeBadgeStyle = isEnemy ? enemyBadgeStyle : badgeStyle;
        Texture2D activeDivider = isEnemy ? enemyDividerTex : dividerTex;
        Texture2D activeHpBar = isEnemy ? enemyHealthBarTex : healthBarTex;

        GUI.Label(new Rect(area.x, curY, area.width - 90f, 22f), $"{headerIcon} {nameText}", activeTitleStyle);
        GUI.Label(new Rect(area.xMax - 85f, curY, 85f, 20f), "자유 유닛", activeBadgeStyle);
        curY += 24f;

        GUI.Label(new Rect(area.x, curY, 180f, 18f), $"{factionTag} [{typeBadge}]", activeBadgeStyle);
        curY += 20f;

        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        // 생명력 (HP)
        float hpRatio = (unit.maxHp > 0) ? Mathf.Clamp01(unit.currentHp / unit.maxHp) : 0f;
        DrawStatGaugeRow(area.x, ref curY, area.width, "❤️ 생명력(HP)", $"{unit.currentHp:F0} / {unit.maxHp:F0}", hpRatio, activeHpBar);

        // 체력 (피로도)
        DrawStatGaugeRow(area.x, ref curY, area.width, "⚡ 체력(활력)", "85% [신선함]", 0.85f, staminaBarTex);

        // 사기
        DrawStatGaugeRow(area.x, ref curY, area.width, "🧠 사기", "70 / 100 [안정]", 0.70f, moraleBarTex);

        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        float colWidth = (area.width - 10f) * 0.5f;
        float leftX = area.x;
        float rightX = area.x + colWidth + 10f;

        DrawStatItem(leftX, curY, colWidth, "근접 공격력", $"{unit.damage:F1}");
        DrawStatItem(rightX, curY, colWidth, "장갑/방어력", $"{unit.armor / 100f:F1}%");
        curY += 20f;

        if (unit.isRangedUnit)
        {
            DrawStatItem(leftX, curY, colWidth, "사격 사거리", $"{unit.rangedAttackRange:F0}m");
            DrawStatItem(rightX, curY, colWidth, "잔여 화살", $"{unit.currentAmmo} / {unit.maxAmmo} 발");
            curY += 20f;
        }

        string spdStr = unit.isRunning ? $"{unit.runSpeed:F1} m/s (구보)" : $"{unit.walkSpeed:F1} m/s (보행)";
        DrawStatItem(leftX, curY, colWidth, "이동 속도", spdStr);
        DrawStatItem(rightX, curY, colWidth, "물리 질량", "100");
        curY += 24f;

        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        string stateDesc = GetCommandStateDescription(unit.currentState);
        GUI.Label(new Rect(area.x, curY, area.width, 18f), $"🚩 행동: <color=#F5D77F>{stateDesc}</color>", footerStyle);
    }

    private void DrawECSEntityDetails(Rect area, Entity entity, bool isEnemy)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var em = world.EntityManager;
        if (!em.Exists(entity)) return;

        float curY = area.y;

        var tag = em.GetComponentData<UnitEntityTag>(entity);
        var combat = em.GetComponentData<UnitCombatData>(entity);

        GUIStyle activeTitleStyle = isEnemy ? enemyTitleStyle : titleStyle;
        GUIStyle activeBadgeStyle = isEnemy ? enemyBadgeStyle : badgeStyle;
        Texture2D activeDivider = isEnemy ? enemyDividerTex : dividerTex;
        Texture2D activeHpBar = isEnemy ? enemyHealthBarTex : healthBarTex;

        string headerIcon = isEnemy ? "⚔️" : "⚡";
        GUI.Label(new Rect(area.x, curY, area.width - 90f, 22f), $"{headerIcon} 순수 ECS 유닛 #{entity.Index}", activeTitleStyle);
        GUI.Label(new Rect(area.xMax - 85f, curY, 85f, 20f), tag.IsFreeUnit == 1 ? "자유 유닛" : "제대 유닛", activeBadgeStyle);
        curY += 24f;

        string factionStr = tag.Faction == 1 ? "<color=#5DADE2>[아군 제대]</color>" : "<color=#FF6B6B>[적군 제대]</color>";
        GUI.Label(new Rect(area.x, curY, 180f, 18f), factionStr, activeBadgeStyle);
        curY += 20f;

        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        float hpRatio = (combat.MaxHp > 0) ? Mathf.Clamp01(combat.CurrentHp / combat.MaxHp) : 0f;
        DrawStatGaugeRow(area.x, ref curY, area.width, "❤️ 생명력(HP)", $"{combat.CurrentHp:F0} / {combat.MaxHp:F0}", hpRatio, activeHpBar);
        DrawStatGaugeRow(area.x, ref curY, area.width, "⚡ 체력(활력)", "80% [활기참]", 0.80f, staminaBarTex);
        DrawStatGaugeRow(area.x, ref curY, area.width, "🧠 사기", "75 / 100 [안정]", 0.75f, moraleBarTex);

        GUI.DrawTexture(new Rect(area.x, curY, area.width, 1), activeDivider);
        curY += 6f;

        float colWidth = (area.width - 10f) * 0.5f;
        DrawStatItem(area.x, curY, colWidth, "근접 공격력", $"{combat.Damage:F0}");
        DrawStatItem(area.x + colWidth + 10f, curY, colWidth, "장갑/방어력", $"{combat.Armor / 100f:F1}%");
        curY += 20f;

        string stateDesc = GetCommandStateDescription((UnitCommandState)combat.CurrentState);
        GUI.Label(new Rect(area.x, curY, area.width, 18f), $"🚩 행동: <color=#F5D77F>{stateDesc}</color>", footerStyle);
    }

    #endregion

    #region GUI 헬퍼 및 스타일 초기화

    private void DrawStatGaugeRow(float x, ref float y, float width, string label, string valueStr, float ratio, Texture2D fillTex)
    {
        GUI.Label(new Rect(x, y, 100f, 16f), label, labelStyle);
        GUI.Label(new Rect(x + 100f, y, width - 100f, 16f), valueStr, valueStyle);
        y += 16f;

        // 가로 게이지 바 (배경 트랙 + 채우기)
        Rect barRect = new Rect(x, y, width, 6f);
        GUI.DrawTexture(barRect, barBgTex);

        float fillW = Mathf.Clamp(width * ratio, 0f, width);
        if (fillW > 0.5f)
        {
            GUI.DrawTexture(new Rect(x, y, fillW, 6f), fillTex);
        }
        y += 10f;
    }

    private void DrawStatItem(float x, float y, float width, string label, string valueStr)
    {
        GUI.Label(new Rect(x, y, 75f, 18f), label, labelStyle);
        GUI.Label(new Rect(x + 75f, y, width - 75f, 18f), valueStr, valueStyle);
    }

    private string GetUnitTypeBadge(UnitType type)
    {
        switch (type)
        {
            case UnitType.MeleeInfantry: return "검/도끼 보병";
            case UnitType.SpearInfantry: return "장창/창병";
            case UnitType.Archer: return "궁병/사격";
            case UnitType.Gunner: return "화승총/사격";
            case UnitType.Cavalry: return "기병";
            default: return "정규군";
        }
    }

    private string GetMoraleStateName(float morale)
    {
        if (morale >= 60f) return "<color=#2ECC71>안정</color>";
        if (morale >= 35f) return "<color=#F1C40F>동요</color>";
        if (morale >= 15f) return "<color=#E67E22>붕괴 위기</color>";
        return "<color=#E74C3C>패주 중</color>";
    }

    private string GetFatigueStageName(float stamina)
    {
        if (stamina >= 85f) return "<color=#1ABC9C>신선함</color>";
        if (stamina >= 70f) return "<color=#2ECC71>활기참</color>";
        if (stamina >= 50f) return "<color=#A6E22E>약간 지침</color>";
        if (stamina >= 30f) return "<color=#F1C40F>지침</color>";
        if (stamina >= 15f) return "<color=#E67E22>매우 지침</color>";
        return "<color=#E74C3C>탈진</color>";
    }

    private string GetCommandStateDescription(UnitCommandState state)
    {
        switch (state)
        {
            case UnitCommandState.Idle: return "대기 중 (전열 유지)";
            case UnitCommandState.Move: return "행진 기동 중";
            case UnitCommandState.AttackMove: return "적진 쇄도 돌격 (어택땅)";
            case UnitCommandState.MeleeEngaged: return "백병전 격투 난전 중";
            default: return "작전 수행 중";
        }
    }

    private string GetFormationDescription(SquadFormationType form, float tightness)
    {
        string baseName = "일자진";
        switch (form)
        {
            case SquadFormationType.Square: baseName = "사각방진"; break;
            case SquadFormationType.Wedge: baseName = "쐐기진"; break;
            case SquadFormationType.Diamond: baseName = "마름모진"; break;
            case SquadFormationType.Loose: baseName = "산개진"; break;
            case SquadFormationType.Circle: baseName = "원형진"; break;
        }

        int percent = Mathf.RoundToInt(tightness * 100f);
        return $"{baseName} (밀집도: {percent}%)";
    }

    private void InitStylesIfNeeded()
    {
        if (bgTex != null) return;

        // 🛡️ 아군 테마: 삼국지 수묵 먹물 질감 배경 & 앤틱 골드
        bgTex = MakeTex(2, 2, new Color(0.08f, 0.09f, 0.11f, 0.94f));
        borderTex = MakeTex(2, 2, new Color(0.45f, 0.38f, 0.28f, 0.45f));
        goldBorderTex = MakeTex(2, 2, new Color(0.82f, 0.68f, 0.38f, 0.90f));
        dividerTex = MakeTex(2, 2, new Color(0.65f, 0.54f, 0.32f, 0.35f));

        // ⚔️ 적군 테마: 흑홍빛 수묵 먹물 질감 배경 & 루비 골드
        enemyBgTex = MakeTex(2, 2, new Color(0.14f, 0.05f, 0.05f, 0.95f));
        enemyBorderTex = MakeTex(2, 2, new Color(0.55f, 0.20f, 0.20f, 0.50f));
        enemyGoldBorderTex = MakeTex(2, 2, new Color(0.88f, 0.30f, 0.25f, 0.95f));
        enemyDividerTex = MakeTex(2, 2, new Color(0.70f, 0.25f, 0.25f, 0.40f));

        // 게이지 배경 및 색상 텍스처
        barBgTex = MakeTex(2, 2, new Color(0.04f, 0.05f, 0.06f, 0.85f));
        healthBarTex = MakeTex(2, 2, new Color(0.18f, 0.80f, 0.44f, 0.95f)); // 에메랄드 그린
        enemyHealthBarTex = MakeTex(2, 2, new Color(0.92f, 0.25f, 0.25f, 0.95f)); // 적군 크림슨 레드
        moraleBarTex = MakeTex(2, 2, new Color(0.16f, 0.73f, 0.61f, 0.95f)); // 청록 시안
        staminaBarTex = MakeTex(2, 2, new Color(0.95f, 0.77f, 0.18f, 0.95f)); // 호박색 골드
        ammoBarTex = MakeTex(2, 2, new Color(0.85f, 0.45f, 0.15f, 0.95f));   // 브라운 오렌지
        statBarTex = MakeTex(2, 2, new Color(0.70f, 0.60f, 0.45f, 0.90f));   // 앤틱 스탯 바

        // 스타일 정의 (아군)
        titleStyle = new GUIStyle
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = new Color(0.96f, 0.90f, 0.78f); // 앤틱 상아 골드

        badgeStyle = new GUIStyle
        {
            fontSize = 11,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            richText = true
        };
        badgeStyle.normal.textColor = new Color(0.65f, 0.75f, 0.85f);

        // 스타일 정의 (적군)
        enemyTitleStyle = new GUIStyle(titleStyle);
        enemyTitleStyle.normal.textColor = new Color(0.98f, 0.82f, 0.80f); // 루비 로즈 골드

        enemyBadgeStyle = new GUIStyle(badgeStyle);
        enemyBadgeStyle.normal.textColor = new Color(0.95f, 0.55f, 0.55f);

        killStyle = new GUIStyle
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        killStyle.normal.textColor = new Color(0.95f, 0.78f, 0.35f);

        labelStyle = new GUIStyle
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = new Color(0.70f, 0.72f, 0.75f);

        valueStyle = new GUIStyle
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        valueStyle.normal.textColor = new Color(0.92f, 0.91f, 0.88f);
        valueStyle.richText = true;

        footerStyle = new GUIStyle
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            richText = true
        };
        footerStyle.normal.textColor = new Color(0.80f, 0.82f, 0.85f);
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    #endregion
}
