using UnityEditor;
using UnityEngine;

/// <summary>
/// HitSegment 的自定义 Inspector：中文标签 + 按 shape 整行隐藏无关参数。
/// 选线形时，球形半径/扇形半角连标题一起消失，只留线形相关字段。
/// 注意：放在 Combat/Editor 而不是 Plugins —— Plugins 编译进 firstpass 程序集，
/// 无法引用 Assets/Scripts 里的 HitSegment。
/// </summary>
[CustomPropertyDrawer(typeof(HitSegment))]
public class HitSegmentDrawer : PropertyDrawer
{
    // 字段路径 / 中文显示名 / 显示条件(shape 枚举值，null=始终显示)
    static readonly (string path, string label, int[] showFor)[] Fields =
    {
        ("enabled",     "启用",                        null),
        ("shape",       "检测形状",                     null),
        ("triggerSec",  "触发时刻 (秒)",               null),
        ("duration",    "持续时间 (秒)",               null),
        ("damage",      "伤害",                         null),
        ("radius",      "范围 (球半径/扇形距离)",      new[] { (int)HitShape.Sphere, (int)HitShape.Sector }),
        ("halfAngle",   "扇形半角 (度)",                new[] { (int)HitShape.Sector }),
        ("yawOffset",   "水平偏转 (度)",                new[] { (int)HitShape.Sector, (int)HitShape.Line }),
        ("pitchOffset", "俯仰偏转 (度)",                new[] { (int)HitShape.Sector, (int)HitShape.Line }),
        ("lineLength",  "线段长度",                    new[] { (int)HitShape.Line }),
        ("lineWidth",   "线段粗细",                    new[] { (int)HitShape.Line }),
        ("offset",      "中心偏移",                    null),
        ("hitMask",     "命中层",                      null),
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 0;
        foreach (var f in Fields)
            if (IsVisible(property, f.showFor))
                lines++;
        return lines * (EditorGUIUtility.singleLineHeight + 2f) + 6f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float y = position.y;
        foreach (var f in Fields)
        {
            if (!IsVisible(property, f.showFor)) continue;
            var prop = property.FindPropertyRelative(f.path);
            if (prop == null) continue;

            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                prop, new GUIContent(f.label));
            y += EditorGUIUtility.singleLineHeight + 2f;
        }
    }

    static bool IsVisible(SerializedProperty property, int[] showFor)
    {
        if (showFor == null) return true;
        int shape = property.FindPropertyRelative("shape").enumValueIndex;
        return System.Array.IndexOf(showFor, shape) >= 0;
    }
}
