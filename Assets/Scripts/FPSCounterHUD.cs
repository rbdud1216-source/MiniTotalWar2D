using UnityEngine;
using Unity.Entities;
using MiniTotalWar.ECS;

/// <summary>
/// 실시간 FPS, 프레임 타임(ms), 전장 내 총 유닛 수, 부대 수, 메모리를 깔끔하게 표시하는 개발용 성능 모니터링 HUD
/// 단축키: F3 (표시/숨김 토글)
/// </summary>
public class FPSCounterHUD : MonoBehaviour
{
    public static FPSCounterHUD Instance { get; private set; }

    [Header("성능 모니터링 HUD 설정")]
    [KoreanLabel("HUD 표시 활성화", "게임 화면 상단에 FPS, 프레임타임, 유닛 수를 표시할지 여부입니다.")]
    public bool showHUD = false;

    [KoreanLabel("HUD 표시 토글 단축키", "HUD를 켜거나 끌 수 있는 단축키입니다. (기본: F12)")]
    public KeyCode toggleKey = KeyCode.F12;

    [KoreanLabel("수치 갱신 주기", "FPS 수치를 다시 계산하여 화면을 갱신하는 주기입니다. (단위: 초, 기본: 0.25초)")]
    public float updateInterval = 0.25f;

    // 측정 변수
    private float accum = 0f;
    private int frames = 0;
    private float timeLeft;
    private float currentFps = 60f;
    private float frameTimeMs = 16.6f;
    private float minFps = 999f;
    private float maxFps = 0f;

    // 유닛 및 부대 통계
    private int alivePlayerUnits = 0;
    private int aliveEnemyUnits = 0;
    private int totalSquads = 0;
    private float nextUnitCountTime = 0f;

    // GUI 스타일
    private GUIStyle boxStyle;
    private GUIStyle headerStyle;
    private GUIStyle fpsStyle;
    private GUIStyle statStyle;
    private bool stylesInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        timeLeft = updateInterval;
    }

    private void Start()
    {
        // 👁️ 시작 시 시야 디버그 모드와 HUD 표시 상태를 1:1 완벽 동기화 (기본: 둘 다 비활성화)
        if (VisionDetectionManager.Instance != null)
        {
            VisionDetectionManager.Instance.isFullVisionDebug = showHUD;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance == null && FindFirstObjectByType<FPSCounterHUD>() == null)
        {
            GameObject hudObj = new GameObject("[FPSCounterHUD]");
            hudObj.AddComponent<FPSCounterHUD>();
        }
    }

    private void Update()
    {
        // F12 키로 성능 HUD 및 전술 시야 디버그 모드 동시 토글
        if (Input.GetKeyDown(toggleKey))
        {
            showHUD = !showHUD;
            if (VisionDetectionManager.Instance != null)
            {
                VisionDetectionManager.Instance.isFullVisionDebug = showHUD;
                Debug.Log($"<color=#00FFFF><b>[FPSCounterHUD] 👁️ [F12] 디버그 모드 전환: {(showHUD ? "활성화(HUD On, 모든 적/경로 강제 노출)" : "비활성화(HUD Off, 정상 전술 시야 규칙 적용)")}</b></color>");
            }
        }

        if (!showHUD) return;

        // FPS 측정
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;

        if (timeLeft <= 0.0f)
        {
            currentFps = (frames / accum);
            frameTimeMs = (accum / frames) * 1000.0f;

            if (currentFps < minFps && currentFps > 5f) minFps = currentFps;
            if (currentFps > maxFps) maxFps = currentFps;

            timeLeft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }

        // 유닛 수 실시간 집계 (0.5초마다)
        if (Time.unscaledTime >= nextUnitCountTime)
        {
            nextUnitCountTime = Time.unscaledTime + 0.5f;
            UpdateUnitCounts();
        }
    }

    private void UpdateUnitCounts()
    {
        alivePlayerUnits = 0;
        aliveEnemyUnits = 0;

        if (BattleManager.Instance != null && BattleManager.Instance.usePureECS)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(typeof(UnitEntityTag), typeof(UnitCombatData));
                using (var tags = query.ToComponentDataArray<UnitEntityTag>(Unity.Collections.Allocator.Temp))
                using (var combats = query.ToComponentDataArray<UnitCombatData>(Unity.Collections.Allocator.Temp))
                {
                    for (int i = 0; i < tags.Length; i++)
                    {
                        if (tags[i].IsAlive == 1 && combats[i].CurrentHp > 0)
                        {
                            if (tags[i].Faction == 1) alivePlayerUnits++;
                            else aliveEnemyUnits++;
                        }
                    }
                }
            }
        }
        else if (UnitJobSimulationManager.HasInstance)
        {
            var units = UnitJobSimulationManager.Instance.registeredUnits;
            int count = units.Count;
            for (int i = 0; i < count; i++)
            {
                Unit u = units[i];
                if (u != null && u.currentHp > 0)
                {
                    if (u.isPlayer) alivePlayerUnits++;
                    else aliveEnemyUnits++;
                }
            }
        }

        if (BattleManager.Instance != null)
        {
            var squads = BattleManager.Instance.GetAllSquads();
            totalSquads = squads != null ? squads.Count : 0;
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.88f));
        bgTexture.Apply();

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTexture },
            padding = new RectOffset(12, 12, 10, 10),
            border = new RectOffset(0, 0, 0, 0)
        };

        headerStyle = new GUIStyle
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.9f, 1.0f) }
        };

        fpsStyle = new GUIStyle
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        statStyle = new GUIStyle
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        InitStyles();

        // FPS 수치에 따른 색상 분기
        Color fpsColor;
        if (currentFps >= 55f) fpsColor = new Color(0.3f, 1.0f, 0.4f); // 초록색
        else if (currentFps >= 30f) fpsColor = new Color(1.0f, 0.85f, 0.2f); // 노란색
        else fpsColor = new Color(1.0f, 0.35f, 0.35f); // 빨간색

        fpsStyle.normal.textColor = fpsColor;

        float width = 230f;
        float height = 145f;
        float margin = 12f;

        // 좌측 상단 HUD 박스
        Rect rect = new Rect(margin, margin, width, height);
        GUILayout.BeginArea(rect, boxStyle);
        GUILayout.BeginVertical();

        // 헤더
        GUILayout.Label($"⚡ PERFORMANCE HUD ({toggleKey})", headerStyle);
        GUILayout.Space(4);

        // FPS & 프레임 타임
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{currentFps:F0} FPS", fpsStyle, GUILayout.Width(110));
        GUILayout.Label($"({frameTimeMs:F1} ms)", statStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Min / Max FPS
        GUILayout.Label($"Min: {minFps:F0} | Max: {maxFps:F0}", statStyle);

        // 전장 유닛 수 집계
        int totalUnits = alivePlayerUnits + aliveEnemyUnits;
        GUILayout.Label($"⚔️ 생존 유닛: {totalUnits:N0} 명 (아군 {alivePlayerUnits:N0} / 적 {aliveEnemyUnits:N0})", statStyle);

        // 부대 수 및 메모리
        long memoryMB = System.GC.GetTotalMemory(false) / (1024 * 1024);
        GUILayout.Label($"🚩 부대: {totalSquads} 개 | 메모리: {memoryMB} MB", statStyle);

        // 👁️ 시야 디버그 상태 표기
        bool isFullVision = (VisionDetectionManager.Instance != null && VisionDetectionManager.Instance.isFullVisionDebug);
        string visionStatus = isFullVision ? "<color=#00FFFF>전체 시야(맵핵)</color>" : "<color=#AAAAAA>정상 시야(은폐 적용)</color>";
        GUILayout.Label($"👁️ 전술 시야: {visionStatus}", statStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
