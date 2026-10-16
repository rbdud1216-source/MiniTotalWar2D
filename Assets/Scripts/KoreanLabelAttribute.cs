using System;
using UnityEngine;

/// <summary>
/// 유니티 인스펙터에 표시되는 필드 라벨을 한국어로 변경하고 상세 툴팁을 제공하는 전용 애트리뷰트입니다.
/// 기존 C# 영문 변수명을 그대로 유지하므로 씬(Scene) 및 프리팹(Prefab)의 직렬화 데이터를 100% 안전하게 보존합니다.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class KoreanLabelAttribute : PropertyAttribute
{
    public readonly string label;
    public readonly string tooltip;

    public KoreanLabelAttribute(string label, string tooltip = "")
    {
        this.label = label;
        this.tooltip = tooltip;
    }
}
