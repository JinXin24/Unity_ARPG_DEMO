using UnityEngine;
using UnityEditor;
using System.Text;

public class SkeletonDiagnostic : EditorWindow
{
    [MenuItem("Tools/骨骼诊断/打印选中对象的骨骼层级")]
    public static void PrintSkeleton()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            // 也试试从 Project 窗口取
            if (Selection.activeObject is GameObject sgo)
                go = sgo;
        }

        if (go == null)
        {
            Debug.LogError("请先在 Hierarchy 中选中角色！");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"选中对象: {go.name}\n");

        // 方法 1：递归打骨骼树
        sb.AppendLine("── Transform 层级 ──");
        int count = 0;
        PrintHierarchy(go.transform, sb, 0, ref count);
        sb.AppendLine($"\n（共 {count} 个物体）");

        // 方法 2：如果有 Animator，打 Avatar 骨骼映射
        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            sb.AppendLine("\n── Avatar HumanBone 映射 ──");
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null)
                    sb.AppendLine($"  {(HumanBodyBones)i}: {bone.name}");
            }
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("完成", "骨骼层级已打印到 Console。", "好的");
    }

    private static void PrintHierarchy(Transform t, StringBuilder sb, int depth, ref int count)
    {
        count++;

        string indent = "";
        for (int i = 0; i < depth; i++)
        {
            indent += (i == depth - 1) ? "  │" : "   ";
        }
        if (depth > 0) indent = indent.Substring(0, indent.Length - 1) + (t.childCount > 0 ? "├─" : "└─");

        sb.AppendLine($"{indent}{t.name}");

        for (int i = 0; i < t.childCount; i++)
        {
            PrintHierarchy(t.GetChild(i), sb, depth + 1, ref count);
        }
    }
}
