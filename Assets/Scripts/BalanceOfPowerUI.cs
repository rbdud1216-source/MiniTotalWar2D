using UnityEngine;

/// <summary>
/// 화면 상단에 실시간 양군 전황 게이지(Balance of Power Bar),
/// 배치 단계의 [전투 시작] 버튼 및 전투 승패 결과 배너를 표시하는 UI 매니저입니다.
/// </summary>
[DisallowMultipleComponent]
public class BalanceOfPowerUI : MonoBehaviour
{
    public static BalanceOfPowerUI Instance { get; private set; }

    [Header("전황 게이지 설정")]
    [KoreanLabel("UI 활성화", "상단 전황 바 및 전투 시작 버튼의 표출 여부입니다.")]
    public bool showUI = true;

    [Header("색상 테마")]
    public Color playerPowerColor = new Color(0.15f, 0.65f, 1.0f, 0.95f); // 아군 청록/블루
    public Color enemyPowerColor = new Color(0.95f, 0.25f, 0.2f, 0.95f);   // 적군 레드
    public Color frameColor = new Color(0.1f, 0.12f, 0.15f, 0.9f);         // 프레임 다크

    private BattlePhaseManager phaseManager;
    private BattleManager battleManager;

    private float smoothPowerRatio = 0.5f; // 0.0 (적군 100%) ~ 1.0 (아군 100%)
    private int playerTotalAlive = 0;
    private int enemyTotalAlive = 0;

    // GUI 스타일 캐시
    private GUIStyle startButtonStyle;
    private GUIStyle disabledButtonStyle;
    private GUIStyle warningSubStyle;
    private GUIStyle headerStyle;
    private GUIStyle countStyle;
    private GUIStyle victoryStyle;
    private GUIStyle defeatStyle;
    private Texture2D playerTex;
    private Texture2D enemyTex;
    private Texture2D frameTex;
    private Texture2D buttonTex;
    private Texture2D disabledButtonTex;
    private Texture2D resultBgTex;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        phaseManager = BattlePhaseManager.Instance;
        battleManager = BattleManager.Instance;
    }

    private void Update()
    {
        if (battleManager == null) battleManager = BattleManager.Instance;
        if (battleManager == null) return;

        var squads = battleManager.GetAllSquads();
        if (squads == null) return;

        int pCount = 0;
        int eCount = 0;

        for (int i = 0; i < squads.Count; i++)
        {
            var s = squads[i];
            if (s != null && s.MemberCount > 0)
            {
                if (s.isPlayer) pCount += s.MemberCount;
                else eCount += s.MemberCount;
            }
        }

        playerTotalAlive = pCount;
        enemyTotalAlive = eCount;

        float targetRatio = 0.5f;
        int sum = pCount + eCount;
        if (sum > 0)
        {
            targetRatio = (float)pCount / sum;
        }

        smoothPowerRatio = Mathf.Lerp(smoothPowerRatio, targetRatio, Time.deltaTime * 3.5f);
    }

    private void InitTextures()
    {
        if (playerTex == null) playerTex = MakeColorTex(playerPowerColor);
        if (enemyTex == null) enemyTex = MakeColorTex(enemyPowerColor);
        if (frameTex == null) frameTex = MakeColorTex(frameColor);
        if (buttonTex == null) buttonTex = MakeColorTex(new Color(0.78f, 0.58f, 0.15f, 0.95f));
        if (disabledButtonTex == null) disabledButtonTex = MakeColorTex(new Color(0.22f, 0.22f, 0.26f, 0.92f));
        if (resultBgTex == null) resultBgTex = MakeColorTex(new Color(0.05f, 0.05f, 0.08f, 0.85f));

        if (startButtonStyle == null)
        {
            startButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            startButtonStyle.normal.textColor = Color.white;
            startButtonStyle.normal.background = buttonTex;
            startButtonStyle.hover.textColor = Color.yellow;
        }

        if (disabledButtonStyle == null)
        {
            disabledButtonStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            disabledButtonStyle.normal.textColor = new Color(1.0f, 0.45f, 0.45f, 1f); // 연한 주홍/빨강 경고 텍스트
            disabledButtonStyle.normal.background = disabledButtonTex;
        }

        if (warningSubStyle == null)
        {
            warningSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            warningSubStyle.normal.textColor = new Color(1.0f, 0.8f, 0.3f, 0.95f); // 황색 경고 안내 텍스트
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = Color.white;
        }

        if (countStyle == null)
        {
            countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            countStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        }

        if (victoryStyle == null)
        {
            victoryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            victoryStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f); // Gold
        }

        if (defeatStyle == null)
        {
            defeatStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            defeatStyle.normal.textColor = new Color(1.0f, 0.25f, 0.25f); // Red
        }
    }

    private Texture2D MakeColorTex(Color col)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (!showUI) return;
        InitTextures();

        if (phaseManager == null) phaseManager = BattlePhaseManager.Instance;
        if (phaseManager == null) return;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. 🏛️ 상단 전황 바 (Balance of Power Bar)
        float barW = 380f;
        float barH = 14f;
        float barX = (screenW - barW) * 0.5f;
        float barY = 12f;

        // 바 배경 프레임
        GUI.DrawTexture(new Rect(barX - 2, barY - 2, barW + 4, barH + 4), frameTex);

        // 아군 바 & 적군 바 분할 렌더링
        float playerW = barW * Mathf.Clamp01(smoothPowerRatio);
        float enemyW = barW - playerW;

        GUI.DrawTexture(new Rect(barX, barY, playerW, barH), playerTex);
        GUI.DrawTexture(new Rect(barX + playerW, barY, enemyW, barH), enemyTex);

        // 중앙 분할선 (골드 마커)
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(barX + (barW * 0.5f) - 1, barY - 3, 2, barH + 6), Texture2D.whiteTexture);

        // 병력 숫자 표시
        GUI.Label(new Rect(barX - 90, barY - 2, 80, 20), $"{playerTotalAlive}명", countStyle);
        GUI.Label(new Rect(barX + barW + 10, barY - 2, 80, 20), $"{enemyTotalAlive}명", countStyle);

        // 전황 텍스트
        GUI.Label(new Rect(barX, barY + barH + 2, barW, 18), "전황 (Balance of Power)", headerStyle);

        // 2. ⚔️ [전투 시작] 버튼 (배치 단계에서만 상단 중앙에 표출)
        if (phaseManager.IsInDeployment)
        {
            int outsideCount = 0;
            bool allInside = (DeploymentZoneManager.Instance == null) || DeploymentZoneManager.Instance.AreAllPlayerUnitsInsideZone(out outsideCount);

            float btnW = allInside ? 240f : 350f;
            float btnH = 46f;
            float btnX = (screenW - btnW) * 0.5f;
            float btnY = barY + barH + 26f;

            if (allInside)
            {
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "⚔️ 전투 시작 (START)", startButtonStyle))
                {
                    phaseManager.StartBattle();
                }
            }
            else
            {
                // 🛑 유닛이 선 바깥에 있으면 버튼 클릭 차단 (GUI.Box 표출)
                GUI.Box(new Rect(btnX, btnY, btnW, btnH), $"⚠️ 배치 구역 밖 유닛 있음 ({outsideCount}명)", disabledButtonStyle);

                // 하단 보조 안내 메시지
                GUI.Label(new Rect(btnX - 50, btnY + btnH + 4, btnW + 100, 20), "모든 아군 유닛을 하늘색 테두리 구역 내에 배치해야 전투를 시작할 수 있습니다.", warningSubStyle);
            }
        }

        // 3. 🏆 승패 판정 배너 (ArmyRouting / PostBattle)
        if (phaseManager.CurrentPhase == BattlePhase.ArmyRouting || phaseManager.CurrentPhase == BattlePhase.PostBattle)
        {
            bool isVictory = (enemyTotalAlive == 0 && playerTotalAlive > 0);
            float bannerW = 500f;
            float bannerH = 140f;
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = screenH * 0.28f;

            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), resultBgTex);

            string title = isVictory ? "🏆 결정적 승리 (VICTORY)" : "💀 비참한 패배 (DEFEAT)";
            GUIStyle textStyle = isVictory ? victoryStyle : defeatStyle;
            GUI.Label(new Rect(bannerX, bannerY + 20, bannerW, 50), title, textStyle);

            string sub = $"경과 시간: {phaseManager.BattleElapsedTime:F1}초  |  아군 잔존: {playerTotalAlive}명  |  적군 잔존: {enemyTotalAlive}명";
            GUI.Label(new Rect(bannerX, bannerY + 80, bannerW, 30), sub, headerStyle);
        }
    }
}
