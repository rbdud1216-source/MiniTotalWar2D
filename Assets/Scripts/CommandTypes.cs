using UnityEngine;
using System;

public enum UnitCommandState
{
    Idle,           // 대기 상태 (명령 없음)
    Move,           // 강제 이동 (적을 무시하고 돌파)
    AttackMove,     // 어택땅 (이동 중 사거리 내 적 발견 시 요격)
    MeleeEngaged    // 근접 교전 중 (스스로 멈춰서 싸움)
}

public enum UnitStance
{
    Aggressive,     // 자유 추격 (적을 발견하면 쫓아감)
    HoldPosition    // 위치 사수 (대형 이탈 금지, 제자리 공격만)
}

public enum SquadFormationType
{
    Normal,         // 일자진 (표준 사각 진형)
    Loose,          // 산개 대형 (행렬 간격 2배 확장)
    Diamond,        // 마름모 대형
    Wedge,          // 쐐기진 (삼각 대형)
    Square,         // 사각방진 (중공 4면 전방위 방진)
    Circle,         // 원형진 (중공 360도 방사형 원형 방진)
    Line            // 횡대 진형 (호환성 유지)
}

/// <summary>
/// 유닛의 전술적 역할 및 병종 분류를 정의하는 열거형입니다.
/// </summary>
public enum UnitType
{
    MeleeInfantry,  // 일반 검/도끼 보병 (근접 돌격 및 방진 유지)
    SpearInfantry,  // 장창병/창병 (대기병 돌격 반사 특화)
    Archer,         // 궁병/사격병 (원거리 곡사 사격 + 보조무기 백병전)
    Gunner,         // 총병/화승총병 (원거리 초고속 직사 사격 + 사선확보 기동 특화)
    Cavalry         // 기병 (고속 기동 및 충격 돌격)
}

/// <summary>
/// 원거리 투사체의 탄도학 비행 궤적 형태를 정의하는 열거형입니다.
/// </summary>
public enum TrajectoryMode
{
    HighArc,        // 곡사 포물선 (활/장궁 - 아군 머리 위를 넘겨 쏘는 곡사)
    Flat            // 직선 평사 (쇠뇌/총기 - 수평에 가깝게 빠르고 낮게 비행)
}

[Serializable]
public class CommandButtonData
{
    public string commandId;
    public string buttonName;
    public string hotkeyText;
    public Sprite icon;
    public Action onClickAction;
}

/// <summary>
/// 토탈워식 전투 수명주기 상태를 정의하는 열거형입니다.
/// </summary>
public enum BattlePhase
{
    NotStarted = 0,     // 씬 초기화 중
    Deployment = 1,     // 부대 배치 단계 (시뮬레이션 동결, 구역 내 자유 이동/회전)
    BattleStarting = 2, // 전투 개시 카운트다운 및 나팔/팡파레 연출 (1초)
    CombatRunning = 3,  // 정규 실시간 교전 진행 중
    ArmyRouting = 4,    // 한쪽 군대가 완전히 붕괴되어 승패가 확정된 상태
    PostBattle = 5      // 결과 화면 (사살수/손실수 통계 표시, 재시작/메뉴 버튼)
}

/// <summary>
/// 토탈워식 사기(Morale / Leadership) 상태를 정의하는 열거형입니다.
/// </summary>
public enum MoraleState
{
    Firm = 0,       // 안정 (60~100): 정상 지휘 및 안정적인 전선 유지
    Shaken = 1,     // 동요 (35~59): 심리적 압박 상태, 추가 충격 시 붕괴 위험
    Wavering = 2,   // 붕괴 위기 (15~34): 사기 게이지 깜빡임, 1회 충돌로 즉각 패주 위험
    Broken = 3,     // 패주 (0~14): 제어 불능 상태로 후방 도주 (재집결 가능)
    Shattered = 4   // 영구 궤멸: 도주 중 지속 피격 등으로 재집결 불가 완전 이탈
}

/// <summary>
/// 토탈워식 체력 및 피로도(Fatigue / Stamina) 6단계 활력 단계를 정의하는 열거형입니다.
/// </summary>
public enum FatigueStage
{
    Fresh = 0,      // 신선함 (85~100%): 페널티 일절 없음 (최대 전투력)
    Active = 1,     // 활기참 (70~84%): 페널티 없음 (가벼운 보행 및 대기)
    Winded = 2,     // 약간 지침 (50~69%): 이속 -5%, 돌격 -10%, 장전시간 +5%
    Tired = 3,      // 지침 (30~49%): 이속 -15%, 돌격 -20%, 근접공격/방어 -10%, 사기 -4
    VeryTired = 4,  // 매우 지침 (15~29%): 이속 -25%, 근접공격/방어 -20%, 돌격 -35%, 사기 -8
    Exhausted = 5   // 탈진 (0~14%): 이속 -35%, 근접공격/방어 -30%, 돌격 -50%, 장전 +35%, 사기 -15
}
