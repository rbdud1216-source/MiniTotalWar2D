using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar;

/// <summary>
/// 👁️ [삼국지 토탈워식 시야 & 적군 탐지 관리자]
/// 지형은 100% 훤히 보이지만, 적 부대와 유닛은 아군의 시야선(Line of Sight) 및 언덕/숲 차폐 여부에 따라
/// 실시간으로 은폐(Invisible)되거나 발각(Spotted)되는 토탈워 고유의 전술 시야 엔진입니다.
/// 또한 언덕 뒤로 차폐된 아군에게 적 원거리 부대가 곡사(HighArc) 사격을 가하지 못하도록 통제합니다.
/// </summary>
public class VisionDetectionManager : MonoBehaviour
{
    private static VisionDetectionManager _instance;
    public static VisionDetectionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<VisionDetectionManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("VisionDetectionManager");
                    _instance = go.AddComponent<VisionDetectionManager>();
                }
            }
            return _instance;
        }
    }

    [Header("👁️ [시야 탐지 주기 및 옵션]")]
    [Tooltip("시야 및 사선 차폐 판정 갱신 주기 (초 단위, 기본: 0.1초 = 10Hz)")]
    public float updateInterval = 0.1f;

    [Tooltip("숲/수풀 속 정지 은폐 유닛의 최대 피탐지 거리 (m, 기본: 25.0m)")]
    public float forestDetectionDistance = 25.0f;

    [Tooltip("고지대 최대 시야 확장 보너스 비율 (기본: +40%)")]
    public float maxHillVisionBonus = 0.40f;

    [Header("🔧 [F12 통합 디버그 모드]")]
    [Tooltip("F12 키를 누르면 전체 시야 맵핵 디버그 모드가 켜져 모든 적 부대가 강제 노출됩니다.")]
    public bool isFullVisionDebug = false;

    [Header("🌫️ [페이드 인 / 페이드 아웃 연출]")]
    [Tooltip("적이 시야에 들어오거나 사라질 때의 부드러운 페이드 시간 (단위: 초, 기본: 0.6초)")]
    public float fadeDuration = 0.6f;

    // 부대 ID별 실시간 발각 여부 O(1) 캐싱 딕셔너리 (PureECSRenderer 및 시스템 연동용)
    private readonly Dictionary<int, bool> squadSpottedLookup = new Dictionary<int, bool>();

    // 부대 ID별 실시간 가시성 알파값 (0.0f = 완전 투명/은폐, 1.0f = 완전 노출)
    private readonly Dictionary<int, float> squadAlphaLookup = new Dictionary<int, float>();

    private Coroutine visionRoutine;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        visionRoutine = StartCoroutine(VisionUpdateLoop());
    }

    private void OnDestroy()
    {
        if (visionRoutine != null)
        {
            StopCoroutine(visionRoutine);
            visionRoutine = null;
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        // ⚡ [F12 키]: Performance HUD와 1:1 완벽 일원화된 전체 시야 디버그 토글
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (FPSCounterHUD.Instance != null)
            {
                isFullVisionDebug = FPSCounterHUD.Instance.showHUD;
            }
            else
            {
                isFullVisionDebug = !isFullVisionDebug;
            }
            Debug.Log($"<color=#00FFFF><b>[VisionDetectionManager] 👁️ [F12] 전체 시야 맵핵 디버그 모드: {(isFullVisionDebug ? "활성화(모든 적 강제 노출)" : "비활성화(정상 시야 규칙 적용)")}</b></color>");
        }

        // 🌫️ 실시간 부대별 가시성 알파 부드러운 페이드 인/페이드 아웃 보간
        UpdateSquadAlphaFading();
    }

    private IEnumerator VisionUpdateLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(updateInterval);

        while (true)
        {
            yield return wait;
            UpdateVisionAndLineOfSight();
        }
    }

    /// <summary>
    /// 아군 시야선(LoS) 및 적군 시야선을 0.1초 주기로 전면 갱신합니다.
    /// </summary>
    public void UpdateVisionAndLineOfSight()
    {
        var allSquads = BattleManager.Instance != null ? BattleManager.Instance.GetAllSquads() : null;
        if (allSquads == null || allSquads.Count == 0)
        {
            allSquads = new List<Squad>(FindObjectsByType<Squad>(FindObjectsSortMode.None));
        }

        List<Squad> playerSquads = new List<Squad>();
        List<Squad> enemySquads = new List<Squad>();

        for (int i = 0; i < allSquads.Count; i++)
        {
            Squad s = allSquads[i];
            if (s == null || s.MemberCount <= 0) continue;

            // 🌲 전술 숲 영역 내 진입 여부 실시간 갱신
            s.isHiddenInForest = TacticalForestZone.IsPositionInForest(s.GetVisualCenter());

            if (s.isPlayer)
            {
                playerSquads.Add(s);
                s.isSpotted = true; // 아군은 플레이어에게 항상 보임
            }
            else
            {
                enemySquads.Add(s);
            }
        }

        // =========================================================================
        // 1. 아군 시야 -> 적 부대 발각(isSpotted) 판정 (플레이어 화면/미니맵 노출)
        // =========================================================================
        squadSpottedLookup.Clear();

        for (int e = 0; e < enemySquads.Count; e++)
        {
            Squad enemy = enemySquads[e];
            int enemyId = enemy.GetInstanceID();

            // 디버그 모드 활성화 시 무조건 노출
            if (isFullVisionDebug)
            {
                enemy.SetSpotted(true);
                squadSpottedLookup[enemyId] = true;
                continue;
            }

            Vector3 enemyCenter = enemy.GetVisualCenter();
            Vector3 enemyEye = enemyCenter + Vector3.up * 1.6f;
            bool enemyInForest = enemy.isHiddenInForest;
            bool spotted = false;

            for (int p = 0; p < playerSquads.Count; p++)
            {
                Squad friend = playerSquads[p];
                Vector3 friendCenter = friend.GetVisualCenter();
                Vector3 friendEye = friendCenter + Vector3.up * 1.6f;

                float dist = Vector3.Distance(friendCenter, enemyCenter);

                // 고지대 시야 보너스 계산: 언덕 위에 올라선 아군은 시야 최대 +40% 확장
                float friendGroundY = TerrainHeightManager.SampleHeightFast(friendCenter.x, friendCenter.z);
                float hillBonus = Mathf.Clamp01((friendGroundY - 5.0f) / 25.0f) * maxHillVisionBonus;
                float baseRange = friend.visionRange > 10f ? friend.visionRange : (friend.IsRangedSquad ? 130f : 90f);
                float effectiveVisionRange = baseRange * (1.0f + hillBonus);

                // 🌲 숲 속 적 판정: 정지/보행 시 25m 초근접해야 발각, 전력질주(구보) 시 45m로 확장
                float forestRange = enemy.isRunning ? 45.0f : forestDetectionDistance;
                float maxDetectDist = enemyInForest ? forestRange : effectiveVisionRange;

                if (dist <= maxDetectDist)
                {
                    // 1) 언덕 능선 사선 차폐 검사 (TerrainHeightManager LoS)
                    bool isLoSClear = TerrainHeightManager.CheckLineOfSightFast(friendEye, enemyEye, 0.0f, 0.0f, 5);

                    // 2) 🌲 숲 지대 사선 차폐 검사 (TacticalForestZone LoS: 숲 너머의 적 차폐)
                    if (isLoSClear && !TacticalForestZone.IsLineBlockedByForest(friendCenter, enemyCenter))
                    {
                        spotted = true;
                        break; // 한 부대라도 적을 감지했으면 즉시 발각 확정
                    }
                }
            }

            enemy.SetSpotted(spotted);
            squadSpottedLookup[enemyId] = spotted;
        }

        // =========================================================================
        // 2. 적군 시야 -> 아군 부대 발각(isSpottedByEnemy) 판정 (적 원거리 곡사 사격 통제용)
        // =========================================================================
        for (int p = 0; p < playerSquads.Count; p++)
        {
            Squad friend = playerSquads[p];
            Vector3 friendCenter = friend.GetVisualCenter();
            Vector3 friendEye = friendCenter + Vector3.up * 1.6f;
            bool friendInForest = friend.isHiddenInForest;
            bool spottedByEnemy = false;

            for (int e = 0; e < enemySquads.Count; e++)
            {
                Squad enemy = enemySquads[e];
                Vector3 enemyCenter = enemy.GetVisualCenter();
                Vector3 enemyEye = enemyCenter + Vector3.up * 1.6f;

                float dist = Vector3.Distance(enemyCenter, friendCenter);

                float enemyGroundY = TerrainHeightManager.SampleHeightFast(enemyCenter.x, enemyCenter.z);
                float hillBonus = Mathf.Clamp01((enemyGroundY - 5.0f) / 25.0f) * maxHillVisionBonus;
                float baseRange = enemy.visionRange > 10f ? enemy.visionRange : (enemy.IsRangedSquad ? 130f : 90f);
                float effectiveVisionRange = baseRange * (1.0f + hillBonus);

                float forestRange = friend.isRunning ? 45.0f : forestDetectionDistance;
                float maxDetectDist = friendInForest ? forestRange : effectiveVisionRange;

                if (dist <= maxDetectDist)
                {
                    bool isLoSClear = TerrainHeightManager.CheckLineOfSightFast(enemyEye, friendEye, 0.0f, 0.0f, 5);
                    if (isLoSClear && !TacticalForestZone.IsLineBlockedByForest(enemyCenter, friendCenter))
                    {
                        spottedByEnemy = true;
                        break;
                    }
                }
            }

            // 차폐되어 적에게 보이지 않으면 false -> 적 궁병이 곡사로 공격할 수 없음!
            friend.isSpottedByEnemy = spottedByEnemy;
        }
    }

    /// <summary>
    /// PureECSRenderer 및 외부 시스템에서 특정 부대가 플레이어 시야에 발각되었는지 O(1)로 조회합니다.
    /// </summary>
    public bool IsSquadSpotted(int squadId)
    {
        if (isFullVisionDebug) return true;
        if (squadSpottedLookup.TryGetValue(squadId, out bool spotted))
        {
            return spotted;
        }
        return true; // 등록되지 않은 부대는 안전하게 표출 폴백
    }

    /// <summary>
    /// 특정 부대의 실시간 시각적 가시성 알파값(0.0f ~ 1.0f)을 O(1)로 조회합니다.
    /// 0.0f = 완전 은폐(안 보임), 1.0f = 완전 발각(선명함), 0.01f~0.99f = 서서히 페이드 인/아웃 중
    /// </summary>
    public float GetSquadAlpha(int squadId)
    {
        if (isFullVisionDebug) return 1.0f;
        if (squadAlphaLookup.TryGetValue(squadId, out float alpha))
        {
            return alpha;
        }
        return 1.0f; // 기본 아군 또는 미등록 부대
    }

    /// <summary>
    /// 매 프레임 모든 부대의 가시성 알파를 부드럽게 목표값(0.0f 또는 1.0f)으로 보간합니다.
    /// </summary>
    private void UpdateSquadAlphaFading()
    {
        var allSquads = BattleManager.Instance != null ? BattleManager.Instance.GetAllSquads() : null;
        if (allSquads == null) return;

        float step = (fadeDuration > 0.01f) ? (Time.deltaTime / fadeDuration) : 1.0f;

        for (int i = 0; i < allSquads.Count; i++)
        {
            Squad s = allSquads[i];
            if (s == null) continue;

            int id = s.GetInstanceID();

            // 아군은 플레이어에게 항상 완전 선명(1.0f)
            if (s.isPlayer)
            {
                squadAlphaLookup[id] = 1.0f;
                s.SetVisibilityAlpha(1.0f);
                continue;
            }

            // 적군은 발각 여부에 따라 목표 알파 결정 (발각 시 1.0f, 가려짐 시 0.0f)
            bool spotted = isFullVisionDebug || (squadSpottedLookup.TryGetValue(id, out bool sp) && sp);
            float targetAlpha = spotted ? 1.0f : 0.0f;

            if (!squadAlphaLookup.TryGetValue(id, out float currentAlpha))
            {
                // 초기값은 스폰 시의 발각 상태
                currentAlpha = spotted ? 1.0f : 0.0f;
            }

            // 부드러운 선형 보간 (Fade-In / Fade-Out)
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, step);
            squadAlphaLookup[id] = currentAlpha;
            s.SetVisibilityAlpha(currentAlpha);
        }
    }
}
