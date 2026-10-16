using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토탈워 전투 엔진의 전체 수명주기(배치 -> 전투 개시 -> 실시간 교전 -> 군대 손실/승패 -> 사후 결과)를 총괄하는 상태 머신 관리자입니다.
/// </summary>
[DisallowMultipleComponent]
public class BattlePhaseManager : MonoBehaviour
{
    public static BattlePhaseManager Instance { get; private set; }

    [Header("1. 전투 수명주기 상태")]
    [KoreanLabel("현재 전투 단계", "현재 진행 중인 전투 상태(배치, 교전, 승패 등)입니다.")]
    [SerializeField] private BattlePhase currentPhase = BattlePhase.NotStarted;

    [Header("2. 타이머 및 연출 옵션")]
    [KoreanLabel("전투 개시 연출 대기 시간", "전투 시작 버튼을 누른 후 나팔/카운트다운 연출 시간(단위: 초)입니다.")]
    [SerializeField] private float battleStartCountdown = 1.0f;

    [KoreanLabel("전투 제한 시간 (초)", "전투 최대 제한 시간(단위: 초, 0이면 무제한)입니다.")]
    [SerializeField] private float maxBattleDuration = 0f;

    // 이벤트 델리게이트
    public event Action<BattlePhase> OnPhaseChanged;
    public event Action<bool> OnBattleEnded; // true: 플레이어 승리, false: 플레이어 패배

    // 시간 측정
    private float battleElapsedTime = 0f;
    private bool isTimerRunning = false;

    // 승패 판정 캐시
    private BattleManager battleManager;

    public BattlePhase CurrentPhase => currentPhase;
    public bool IsInDeployment => currentPhase == BattlePhase.Deployment;
    public bool IsCombatRunning => currentPhase == BattlePhase.CombatRunning;
    public float BattleElapsedTime => battleElapsedTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentPhase = BattlePhase.Deployment;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        battleManager = BattleManager.Instance;
        // 씬 시작 시 즉시 '배치 단계'로 진입
        SetPhase(BattlePhase.Deployment);
    }

    private void Update()
    {
        if (currentPhase == BattlePhase.CombatRunning)
        {
            if (isTimerRunning)
            {
                battleElapsedTime += Time.deltaTime;
                if (maxBattleDuration > 0f && battleElapsedTime >= maxBattleDuration)
                {
                    // 시간 초과 판정 (수비측 승리 등)
                    EvaluateTimeOverResult();
                }
            }

            // 실시간 승패 및 군대 손실(Army Losses) 판정 주기적 검사
            CheckBattleEndConditions();
        }
    }

    /// <summary>
    /// 전투 수명주기 상태를 전이하고 등록된 리스너들에게 알립니다.
    /// </summary>
    public void SetPhase(BattlePhase newPhase)
    {
        if (currentPhase == newPhase) return;

        BattlePhase prevPhase = currentPhase;
        currentPhase = newPhase;

        Debug.Log($"<color=#00FFCC><b>[BattlePhaseManager] 🏛️ 전투 단계 전이: {prevPhase} -> {newPhase}</b></color>");

        switch (newPhase)
        {
            case BattlePhase.Deployment:
                isTimerRunning = false;
                battleElapsedTime = 0f;
                break;

            case BattlePhase.BattleStarting:
                isTimerRunning = false;
                StartCoroutine(BattleStartingSequence());
                break;

            case BattlePhase.CombatRunning:
                isTimerRunning = true;
                // 전투 개시 시 적군 AI 공격 명령 발동
                if (battleManager != null)
                {
                    battleManager.StartCombatEngagement();
                }
                break;

            case BattlePhase.ArmyRouting:
                // 한쪽 군대가 완전히 패주함
                break;

            case BattlePhase.PostBattle:
                isTimerRunning = false;
                break;
        }

        OnPhaseChanged?.Invoke(newPhase);
    }

    /// <summary>
    /// [전투 시작] 버튼 클릭 시 호출되어 배치 단계를 종료하고 교전 상태로 진입합니다.
    /// </summary>
    public void StartBattle()
    {
        if (currentPhase != BattlePhase.Deployment)
        {
            Debug.LogWarning($"[BattlePhaseManager] 현재 상태({currentPhase})에서는 전투를 시작할 수 없습니다.");
            return;
        }

        // 🏛️ [배치 구역 검증]: 아군 유닛이 배치 구역 바깥에 위치해 있으면 전투 개시 차단
        if (DeploymentZoneManager.Instance != null && !DeploymentZoneManager.Instance.AreAllPlayerUnitsInsideZone(out int outsideCount))
        {
            Debug.LogWarning($"<color=#FF5555><b>[BattlePhaseManager] ⚠️ 전투 시작 불가: 아직 배치 구역 바깥에 {outsideCount}명의 아군 유닛이 있습니다!</b></color>");
            return;
        }

        Debug.Log("<color=#FFD700><b>[BattlePhaseManager] 🎺 나팔 소리와 함께 전투가 개시됩니다! (Start Battle)</b></color>");
        SetPhase(BattlePhase.BattleStarting);
    }

    private IEnumerator BattleStartingSequence()
    {
        // 1초간 카운트다운/나팔 연출 후 실시간 전투 돌입
        yield return new WaitForSeconds(battleStartCountdown);
        SetPhase(BattlePhase.CombatRunning);
    }

    /// <summary>
    /// 양군의 생존 병력 및 사기 상태를 검사하여 승패를 판정합니다.
    /// </summary>
    private void CheckBattleEndConditions()
    {
        if (battleManager == null) battleManager = BattleManager.Instance;
        if (battleManager == null) return;

        var allSquads = battleManager.GetAllSquads();
        if (allSquads == null || allSquads.Count == 0) return;

        int playerAliveSquads = 0;
        int enemyAliveSquads = 0;

        for (int i = 0; i < allSquads.Count; i++)
        {
            var squad = allSquads[i];
            if (squad == null) continue;

            // 생존 병력이 0보다 크고 패주(Routing) 상태가 아니면 생존 부대로 카운트
            if (squad.MemberCount > 0)
            {
                if (squad.isPlayer) playerAliveSquads++;
                else enemyAliveSquads++;
            }
        }

        // 적군 전멸 또는 총패주 -> 플레이어 승리
        if (enemyAliveSquads == 0 && playerAliveSquads > 0)
        {
            EndBattle(true);
        }
        // 플레이어 전멸 또는 총패주 -> 플레이어 패배
        else if (playerAliveSquads == 0 && enemyAliveSquads > 0)
        {
            EndBattle(false);
        }
        // 양군 동시 전멸 -> 무승부/패배
        else if (playerAliveSquads == 0 && enemyAliveSquads == 0)
        {
            EndBattle(false);
        }
    }

    private void EvaluateTimeOverResult()
    {
        // 시간 초과 시 생존 병력 비율 비교
        EndBattle(false);
    }

    /// <summary>
    /// 전투를 공식 종료하고 결과 화면 페이즈로 전환합니다.
    /// </summary>
    public void EndBattle(bool isPlayerVictory)
    {
        if (currentPhase == BattlePhase.ArmyRouting || currentPhase == BattlePhase.PostBattle) return;

        SetPhase(BattlePhase.ArmyRouting);

        string resultText = isPlayerVictory ? "<color=#00FF00>🏆 [결정적 승리! (Decisive Victory)]</color>" : "<color=#FF0000>💀 [비참한 패배... (Crushing Defeat)]</color>";
        Debug.Log($"<b>[BattlePhaseManager] {resultText} (경과 시간: {battleElapsedTime:F1}초)</b>");

        OnBattleEnded?.Invoke(isPlayerVictory);

        // 2초 후 포스트 배틀(결과 창) 단계로 전이
        StartCoroutine(PostBattleSequence());
    }

    private IEnumerator PostBattleSequence()
    {
        yield return new WaitForSeconds(2.0f);
        SetPhase(BattlePhase.PostBattle);
    }
}
