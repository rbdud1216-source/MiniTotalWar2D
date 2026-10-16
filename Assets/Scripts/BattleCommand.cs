using System;
using UnityEngine;

/// <summary>
/// 전투에서 플레이어 또는 AI가 부대에 내릴 수 있는 전술 명령의 종류를 정의하는 열거형입니다.
/// </summary>
public enum BattleCommandType
{
    Move,               // 이동 및 진형 대형 전개
    AttackSquad,        // 특정 적 부대 타겟 공격 (원거리 사격 또는 근접 돌격)
    AttackPosition,     // 지정 좌표 공격 이동 (어택땅)
    Stop,               // 정지 (모든 이동 및 공격 즉시 중단)
    SetStance,          // 부대 전투 태세 변경 (자유 추격 / 위치 사수)
    ToggleRun,          // 달리기 / 걷기 이동 모드 토글
    ToggleAutoAttack,   // 선제 돌격 요격 / 접촉 방어 모드 토글
    ToggleFireAtWill,   // 자유 사격 / 사격 금지 토글 (원거리 전용)
    SetFormation,       // 진형 형태 변경 (일자진, 마름모, 쐐기진, 방진, 원형진 등)
    ToggleLoose,        // 산개 대형 / 밀집 방진 토글
    ToggleStaggered,    // 엇갈린 대형 / 직렬 대형 토글
    AddWaypoint,        // 경유지 추가 (Shift 이동)
    ClearWaypoints      // 경유지 초기화
}

/// <summary>
/// 단일 전술 명령의 모든 파라미터를 담는 직렬화 가능한 명령 데이터 구조체입니다.
/// 싱글플레이에서는 즉시 실행되며, 향후 멀티플레이 락스텝(Lockstep) 네트워크 전송 시 패킷으로 직렬화됩니다.
/// </summary>
[Serializable]
public struct BattleCommand
{
    [Tooltip("명령 고유 식별 번호 (시퀀스 ID)")]
    public uint commandId;

    [Tooltip("명령을 발행한 플레이어 ID (싱글플레이: 0, 멀티플레이: 0 또는 1)")]
    public int playerId;

    [Tooltip("명령이 실행될 목표 시뮬레이션 틱 번호 (싱글플레이: 현재 틱)")]
    public uint scheduledTick;

    [Tooltip("전술 명령 종류")]
    public BattleCommandType commandType;

    [Tooltip("명령 대상 아군 부대들의 고유 InstanceID 목록")]
    public int[] targetSquadIds;

    [Tooltip("타겟 적 부대의 고유 InstanceID (-1이면 지정 타겟 없음)")]
    public int targetEnemySquadId;

    [Tooltip("목표 이동/공격 월드 좌표 (X, Y, Z)")]
    public Vector3 targetPosition;

    [Tooltip("목표 회전 방향 (대형 진형의 지향각)")]
    public Quaternion targetRotation;

    [Tooltip("대형 횡대 열 수 (Columns)")]
    public int columns;

    [Tooltip("보조 정수 파라미터 (진형 타입, 태세 등 열거형 값 저장용)")]
    public int intParam;

    [Tooltip("보조 불리언 1 (Shift 경유지 여부, Alt 근접 강제 돌격 여부 등)")]
    public bool boolParam1;

    [Tooltip("보조 불리언 2 (강제 정렬 여부 등)")]
    public bool boolParam2;

    /// <summary>
    /// 디버그 및 로깅용 문자열 표현을 반환합니다.
    /// </summary>
    public override string ToString()
    {
        int squadCount = targetSquadIds != null ? targetSquadIds.Length : 0;
        return $"[Cmd #{commandId} | 틱 #{scheduledTick} | P{playerId}] {commandType} -> 부대 {squadCount}개, 타겟 적: {targetEnemySquadId}, 위치: {targetPosition}";
    }
}
