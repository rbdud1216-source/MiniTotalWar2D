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
