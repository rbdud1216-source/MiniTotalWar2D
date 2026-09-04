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

[Serializable]
public class CommandButtonData
{
    public string commandId;
    public string buttonName;
    public string hotkeyText;
    public Sprite icon;
    public Action onClickAction;
}
