using UnityEngine;
using UnityEditor;

/// <summary>
/// 选中 Hierarchy 里的 GameObject → 菜单 Tools → 复制 Transform 路径
/// 把路径复制到剪贴板，直接贴到 SO 配置里。
/// </summary>
public class CopyTransformPath : EditorWindow
{
    [MenuItem("Tools/复制 Transform 路径")]
    static void CopyPath()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 里选中一个 GameObject", "好的");
            return;
        }

        var root = go.transform.root;
        if (root == go.transform)
        {
            // 根节点不要包含自己的名字，直接空字符串表示自身
            GUIUtility.systemCopyBuffer = "";
            Debug.Log($"[路径] 根节点: (空)");
            return;
        }

        string path = go.name;
        var parent = go.transform.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        GUIUtility.systemCopyBuffer = path;
        Debug.Log($"[路径] 已复制: {path}");
        EditorUtility.DisplayDialog("复制成功", $"路径:\n{path}\n\n已复制到剪贴板", "好的");
    }
}
