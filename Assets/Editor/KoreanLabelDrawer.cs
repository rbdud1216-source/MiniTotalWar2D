using UnityEditor;
using UnityEngine;

/// <summary>
/// [KoreanLabel] 애트리뷰트가 적용된 필드를 유니티 인스펙터에 한국어 라벨과 툴팁으로 렌더링하는 전용 프로퍼티 드로어입니다.
/// </summary>
[CustomPropertyDrawer(typeof(KoreanLabelAttribute))]
public class KoreanLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        KoreanLabelAttribute koreanAttr = (KoreanLabelAttribute)attribute;
        GUIContent guiContent = new GUIContent(koreanAttr.label, string.IsNullOrEmpty(koreanAttr.tooltip) ? label.tooltip : koreanAttr.tooltip);

        EditorGUI.BeginProperty(position, guiContent, property);
        EditorGUI.PropertyField(position, property, guiContent, true);
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
