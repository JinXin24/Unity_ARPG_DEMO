using UnityEditor;
using UnityEngine;

/// <summary>
/// ShowIf 的编辑器绘制：判断条件字段的值，不满足就高度为 0（隐藏）。
/// 支持布尔和枚举两种判断字段。
/// </summary>
[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return IsVisible(property) ? EditorGUI.GetPropertyHeight(property, label, true) : 0f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!IsVisible(property)) return;
        EditorGUI.PropertyField(position, property, label, true);
    }

    bool IsVisible(SerializedProperty property)
    {
        var attr = (ShowIfAttribute)attribute;

        // 构造同对象下条件字段的完整路径：数组元素里也要能找到
        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');
        string condPath = lastDot >= 0 ? path.Substring(0, lastDot + 1) + attr.ConditionField : attr.ConditionField;
        var cond = property.serializedObject.FindProperty(condPath);
        if (cond == null) return true; // 找不到判断字段，保守显示

        if (cond.propertyType == SerializedPropertyType.Boolean)
            return System.Array.IndexOf(attr.EqualValues, cond.boolValue ? 1 : 0) >= 0;

        if (cond.propertyType == SerializedPropertyType.Enum)
            return System.Array.IndexOf(attr.EqualValues, cond.enumValueIndex) >= 0;

        return true;
    }
}
