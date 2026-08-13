using System.Linq;
using UnityEngine;

/// <summary>
/// 条件显示属性：当 ConditionField 字段的值等于任一 EqualValues 时，该字段才在 Inspector 显示。
/// 用于命中段等配置里"按形状隐藏无关参数"。
/// 用法：[ShowIf("shape", HitShape.Sector)]  或  [ShowIf("shape", HitShape.Sphere, HitShape.Sector)]
/// </summary>
public class ShowIfAttribute : PropertyAttribute
{
    public readonly string ConditionField;
    public readonly int[] EqualValues;

    public ShowIfAttribute(string conditionField, params object[] equalValues)
    {
        ConditionField = conditionField;
        EqualValues = equalValues.Select(v => (int)v).ToArray();
    }
}
