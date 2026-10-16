using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멀티플레이 네트워크 릴레이/P2P 전송을 위한 명령 발송 인터페이스입니다.
/// 싱글플레이에서는 사용되지 않으며, 멀티플레이 활성화 시 패킷 송신 모듈이 바인딩됩니다.
/// </summary>
public interface INetworkCommandDispatcher
{
    void SendCommand(BattleCommand command);
}

/// <summary>
/// 플레이어의 마우스 조작/UI 입력을 부대 실행 로직과 분리하여 큐(Queue)로 중계하는 전술 명령 큐 매니저입니다.
/// - 싱글플레이: 0ms 즉시 실행하여 쾌적한 조작감 유지.
/// - 향후 멀티플레이: 결정론적 락스텝(Lockstep) 틱에 맞춰 패킷 교환 및 정밀 동기화 실행 지원.
/// </summary>
[DisallowMultipleComponent]
public class BattleCommandQueue : MonoBehaviour
{
    private static BattleCommandQueue _instance;
    public static BattleCommandQueue Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BattleCommandQueue>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[BattleCommandQueue]");
                    _instance = go.AddComponent<BattleCommandQueue>();
                }
            }
            return _instance;
        }
    }

    [Header("시뮬레이션 틱 및 모드 설정")]
    [KoreanLabel("네트워크 멀티플레이 모드", "체크 시 로컬 즉시 실행 대신 네트워크 디스패처를 통해 명령을 동기화합니다.")]
    [SerializeField] private bool isNetworkMode = false;

    [KoreanLabel("초당 틱 빈도 (Hz)", "락스텝 시뮬레이션의 초당 고정 틱 수입니다. (기본 20Hz = 50ms마다 1틱)")]
    [SerializeField] private float tickRate = 20f;

    [Header("실시간 통계 & 디버그 모니터")]
    [KoreanLabel("현재 시뮬레이션 틱", "게임 시작 후 누적된 고정 시뮬레이션 틱 번호입니다.")]
    [SerializeField] private uint currentTick = 0;

    [KoreanLabel("누적 처리 명령 수", "지금까지 큐를 거쳐 실행된 총 전술 명령 개수입니다.")]
    [SerializeField] private uint totalCommandsProcessed = 0;

    // 명령 큐 및 버퍼
    private readonly Queue<BattleCommand> pendingCommands = new Queue<BattleCommand>();
    private readonly List<BattleCommand> recentCommandHistory = new List<BattleCommand>();
    private const int MaxHistoryCount = 100;

    // 시퀀스 ID 발급기
    private uint nextCommandId = 1;

    // 틱 타이머
    private float tickTimer = 0f;

    // 멀티플레이 네트워크 송신 인터페이스 (옵션)
    private INetworkCommandDispatcher networkDispatcher;

    // 부대 빠른 조회를 위한 캐시 맵
    private readonly Dictionary<int, Squad> squadCache = new Dictionary<int, Squad>();

    public bool IsNetworkMode => isNetworkMode;
    public uint CurrentTick => currentTick;
    public uint TotalCommandsProcessed => totalCommandsProcessed;
    public IReadOnlyList<BattleCommand> RecentCommandHistory => recentCommandHistory;

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

    private void Update()
    {
        // 틱 카운터 갱신 (초당 tickRate 회)
        float tickInterval = 1f / Mathf.Max(1f, tickRate);
        tickTimer += Time.deltaTime;

        while (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            currentTick++;
        }

        // 대기 중인 명령 실행
        ProcessPendingCommands();
    }

    /// <summary>
    /// 멀티플레이 네트워크 송신기를 바인딩합니다.
    /// </summary>
    public void SetNetworkDispatcher(INetworkCommandDispatcher dispatcher)
    {
        networkDispatcher = dispatcher;
        isNetworkMode = (dispatcher != null);
        Debug.Log($"[BattleCommandQueue] 네트워크 디스패처 설정 완료 (멀티플레이 모드: {isNetworkMode})");
    }

    /// <summary>
    /// 외부(플레이어 컨트롤러, UI, AI 등)에서 전술 명령을 큐에 인큐합니다.
    /// </summary>
    public void EnqueueCommand(BattleCommand command)
    {
        command.commandId = nextCommandId++;
        command.scheduledTick = currentTick;

        // 멀티플레이 모드인 경우 네트워크로 먼저 패킷 발송
        if (isNetworkMode && networkDispatcher != null)
        {
            networkDispatcher.SendCommand(command);
            return;
        }

        // 싱글플레이 모드: 로컬 큐에 등록 후 즉시 처리 대기
        pendingCommands.Enqueue(command);

        // 싱글플레이에서는 입력 지연(0ms)을 방지하기 위해 즉시 처리 시도
        ProcessPendingCommands();
    }

    /// <summary>
    /// 멀티플레이 수신 모듈에서 원격 플레이어의 명령 패킷을 받았을 때 호출합니다.
    /// </summary>
    public void ReceiveRemoteCommand(BattleCommand command)
    {
        pendingCommands.Enqueue(command);
    }

    /// <summary>
    /// 큐에 누적된 명령들을 순서대로 꺼내어 실행합니다.
    /// </summary>
    private void ProcessPendingCommands()
    {
        if (pendingCommands.Count == 0) return;

        RefreshSquadCache();

        while (pendingCommands.Count > 0)
        {
            BattleCommand cmd = pendingCommands.Dequeue();

            // 실행
            ExecuteCommand(cmd);

            // 통계 및 히스토리 보관
            totalCommandsProcessed++;
            recentCommandHistory.Add(cmd);
            if (recentCommandHistory.Count > MaxHistoryCount)
            {
                recentCommandHistory.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 단일 명령을 해석하여 대상 부대(Squad)에 적절한 전술 메서드를 호출합니다.
    /// </summary>
    private void ExecuteCommand(BattleCommand cmd)
    {
        if (cmd.targetSquadIds == null || cmd.targetSquadIds.Length == 0) return;

        // 대상 아군 부대 수집
        List<Squad> squads = new List<Squad>();
        for (int i = 0; i < cmd.targetSquadIds.Length; i++)
        {
            if (squadCache.TryGetValue(cmd.targetSquadIds[i], out Squad squad) && squad != null)
            {
                squads.Add(squad);
            }
        }

        if (squads.Count == 0) return;

        // 타겟 적 부대 조회 (필요 시)
        Squad targetEnemy = null;
        if (cmd.targetEnemySquadId != -1)
        {
            squadCache.TryGetValue(cmd.targetEnemySquadId, out targetEnemy);
        }

        switch (cmd.commandType)
        {
            case BattleCommandType.Move:
                ExecuteMoveInternal(squads, cmd.targetPosition, cmd.targetRotation, cmd.columns, cmd.boolParam1, cmd.boolParam2);
                break;

            case BattleCommandType.AttackSquad:
                if (targetEnemy != null)
                {
                    bool isAlt = cmd.boolParam1;
                    foreach (var s in squads)
                    {
                        if (isAlt || s.isMeleeMode || s.isForcedMeleeCharge)
                        {
                            s.CommandMeleeChargeSquad(targetEnemy);
                        }
                        else
                        {
                            s.CommandAttackSquad(targetEnemy);
                        }
                    }
                }
                break;

            case BattleCommandType.AttackPosition:
                bool isAltAttack = cmd.boolParam1;
                Vector3 targetWorldPos = cmd.targetPosition;
                foreach (var s in squads)
                {
                    if (s == null) continue;
                    bool isMeleeCharge = isAltAttack || s.isMeleeMode || s.isForcedMeleeCharge;

                    if (!isMeleeCharge && s.IsRangedSquad)
                    {
                        Vector3 myPos = s.GetVisualCenter();
                        Vector3 dir = targetWorldPos - myPos;
                        dir.y = 0f;
                        float dist = dir.magnitude;
                        float maxRange = 150f;
                        if (s.members != null && s.members.Count > 0 && s.members[0] != null && s.members[0].rangedAttackRange > 10f)
                        {
                            maxRange = s.members[0].rangedAttackRange;
                        }
                        Quaternion faceRot = (dir.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(dir.normalized) : s.transform.rotation;
                        if (dist <= maxRange * 0.90f)
                        {
                            s.CommandRangedHaltAndFire(faceRot, null);
                        }
                        else
                        {
                            Vector3 standPos = targetWorldPos - (dir.normalized * (maxRange * 0.70f));
                            s.ClearWaypoints();
                            s.CommandMoveWithFormation(standPos, faceRot, s.currentColumns, false, UnitCommandState.Move);
                        }
                    }
                    else
                    {
                        if (isMeleeCharge && s.IsRangedSquad)
                        {
                            s.CommandMeleeChargePosition(targetWorldPos);
                        }
                        else
                        {
                            s.ClearWaypoints();
                            s.CommandMoveWithFormation(targetWorldPos, s.transform.rotation, s.currentColumns, false, UnitCommandState.AttackMove);
                        }
                    }
                }
                break;

            case BattleCommandType.Stop:
                foreach (var s in squads)
                {
                    s.CommandStop();
                }
                break;

            case BattleCommandType.SetStance:
                UnitStance stance = (UnitStance)cmd.intParam;
                foreach (var s in squads)
                {
                    s.SetStance(stance);
                }
                break;

            case BattleCommandType.ToggleRun:
                foreach (var s in squads)
                {
                    s.ToggleRunMode();
                }
                break;

            case BattleCommandType.ToggleAutoAttack:
                foreach (var s in squads)
                {
                    s.SetAutoAttack(!s.autoAttackEnabled);
                }
                break;

            case BattleCommandType.ToggleFireAtWill:
                foreach (var s in squads)
                {
                    s.ToggleFireAtWill();
                }
                break;

            case BattleCommandType.SetFormation:
                SquadFormationType formationType = (SquadFormationType)cmd.intParam;
                foreach (var s in squads)
                {
                    s.SetFormationType(formationType);
                }
                break;

            case BattleCommandType.ToggleLoose:
                foreach (var s in squads)
                {
                    s.ToggleLooseFormation();
                }
                break;

            case BattleCommandType.ToggleStaggered:
                foreach (var s in squads)
                {
                    s.ToggleStaggeredFormation();
                }
                break;

            case BattleCommandType.AddWaypoint:
                foreach (var s in squads)
                {
                    s.AddWaypoint(cmd.targetPosition, cmd.targetRotation, cmd.columns > 0 ? cmd.columns : s.currentColumns);
                }
                break;

            case BattleCommandType.ClearWaypoints:
                foreach (var s in squads)
                {
                    s.ClearWaypoints();
                }
                break;
        }
    }

    /// <summary>
    /// 단일/복수 부대의 진형 이동 명령을 수행합니다.
    /// </summary>
    private void ExecuteMoveInternal(List<Squad> squads, Vector3 centerPos, Quaternion targetRot, int cols, bool isShift, bool isLockedGroup)
    {
        if (squads.Count == 1)
        {
            Squad squad = squads[0];
            if (squad != null)
            {
                if (isShift)
                {
                    squad.AddWaypoint(centerPos, targetRot, cols > 0 ? cols : squad.currentColumns);
                }
                else
                {
                    squad.ClearWaypoints();
                    squad.CommandMoveWithFormation(centerPos, targetRot, cols > 0 ? cols : squad.currentColumns, forceSort: false);
                }
            }
        }
        else if (squads.Count > 1)
        {
            Vector3 centroid = Vector3.zero;
            int validCount = 0;
            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                {
                    centroid += squads[i].transform.position;
                    validCount++;
                }
            }
            if (validCount > 0) centroid /= validCount;

            Quaternion deltaRot = Quaternion.identity;
            if (targetRot != Quaternion.identity && squads[0] != null)
            {
                deltaRot = targetRot * Quaternion.Inverse(squads[0].transform.rotation);
            }

            foreach (Squad squad in squads)
            {
                if (squad == null) continue;

                Vector3 relOffset = squad.transform.position - centroid;
                Vector3 finalOffset = (isLockedGroup && targetRot != Quaternion.identity) ? (deltaRot * relOffset) : relOffset;
                Vector3 targetSquadPos = centerPos + finalOffset;
                Quaternion finalSquadRot = (isLockedGroup && targetRot != Quaternion.identity) ? (deltaRot * squad.transform.rotation) : squad.transform.rotation;

                if (isShift)
                {
                    squad.AddWaypoint(targetSquadPos, finalSquadRot, squad.currentColumns);
                }
                else
                {
                    squad.ClearWaypoints();
                    squad.CommandMoveWithFormation(targetSquadPos, finalSquadRot, squad.currentColumns, forceSort: false);
                }
            }
        }
    }

    /// <summary>
    /// 전장 내 모든 활성 부대를 캐시 맵에 갱신합니다.
    /// </summary>
    private void RefreshSquadCache()
    {
        if (BattleManager.Instance == null) return;
        var allSquads = BattleManager.Instance.GetAllSquads();
        if (allSquads == null) return;

        squadCache.Clear();
        for (int i = 0; i < allSquads.Count; i++)
        {
            Squad s = allSquads[i];
            if (s != null)
            {
                squadCache[s.GetInstanceID()] = s;
            }
        }
    }

    // -------------------------------------------------------------
    // 외부 편의 발행 메서드 (Convenience Issue Methods)
    // -------------------------------------------------------------

    private static int[] ExtractSquadIds(IReadOnlyList<Squad> squads)
    {
        if (squads == null || squads.Count == 0) return Array.Empty<int>();
        List<int> ids = new List<int>(squads.Count);
        for (int i = 0; i < squads.Count; i++)
        {
            if (squads[i] != null) ids.Add(squads[i].GetInstanceID());
        }
        return ids.ToArray();
    }

    public void IssueMoveCommand(IReadOnlyList<Squad> squads, Vector3 centerPos, Quaternion targetRot, int cols, bool isShift, bool isLockedGroup = false)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.Move,
            targetSquadIds = ids,
            targetEnemySquadId = -1,
            targetPosition = centerPos,
            targetRotation = targetRot,
            columns = cols,
            boolParam1 = isShift,
            boolParam2 = isLockedGroup
        });
    }

    public void IssueAttackSquadCommand(IReadOnlyList<Squad> squads, Squad enemySquad, bool isAltPressed = false)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0 || enemySquad == null) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.AttackSquad,
            targetSquadIds = ids,
            targetEnemySquadId = enemySquad.GetInstanceID(),
            targetPosition = enemySquad.transform.position,
            targetRotation = Quaternion.identity,
            boolParam1 = isAltPressed
        });
    }

    public void IssueAttackPositionCommand(IReadOnlyList<Squad> squads, Vector3 targetWorldPos, bool isAltPressed = false)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.AttackPosition,
            targetSquadIds = ids,
            targetEnemySquadId = -1,
            targetPosition = targetWorldPos,
            targetRotation = Quaternion.identity,
            boolParam1 = isAltPressed
        });
    }

    public void IssueStopCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.Stop,
            targetSquadIds = ids,
            targetEnemySquadId = -1
        });
    }

    public void IssueStanceCommand(IReadOnlyList<Squad> squads, UnitStance stance)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.SetStance,
            targetSquadIds = ids,
            intParam = (int)stance
        });
    }

    public void IssueRunToggleCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.ToggleRun,
            targetSquadIds = ids
        });
    }

    public void IssueAutoAttackToggleCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.ToggleAutoAttack,
            targetSquadIds = ids
        });
    }

    public void IssueFireAtWillToggleCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.ToggleFireAtWill,
            targetSquadIds = ids
        });
    }

    public void IssueFormationCommand(IReadOnlyList<Squad> squads, SquadFormationType formationType)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.SetFormation,
            targetSquadIds = ids,
            intParam = (int)formationType
        });
    }

    public void IssueLooseToggleCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.ToggleLoose,
            targetSquadIds = ids
        });
    }

    public void IssueStaggeredToggleCommand(IReadOnlyList<Squad> squads)
    {
        int[] ids = ExtractSquadIds(squads);
        if (ids.Length == 0) return;

        EnqueueCommand(new BattleCommand
        {
            playerId = 0,
            commandType = BattleCommandType.ToggleStaggered,
            targetSquadIds = ids
        });
    }
}
