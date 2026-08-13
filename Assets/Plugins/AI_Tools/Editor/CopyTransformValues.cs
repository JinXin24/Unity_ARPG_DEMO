using UnityEngine;
using UnityEditor;

/// <summary>
/// 选中 Hierarchy 里的 GameObject → 菜单 Tools → 复制 Transform 值
/// 把位置和旋转复制到剪贴板，直接贴到 SO 配置里。
/// </summary>
public class CopyTransformValues
{
    [MenuItem("Tools/复制 Transform 值")]
    static void Copy()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 里选中一个 GameObject", "好的");
            return;
        }

        var t = go.transform;
        Vector3 pos = t.position;
        Vector3 rot = t.eulerAngles;

        string result = $"Pos: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3})\nRot: ({rot.x:F3}, {rot.y:F3}, {rot.z:F3})";

        GUIUtility.systemCopyBuffer = result;
        Debug.Log($"[Transform] 已复制:\n{result}");
        EditorUtility.DisplayDialog("复制成功", result + "\n\n已复制到剪贴板", "好的");
    }
}
