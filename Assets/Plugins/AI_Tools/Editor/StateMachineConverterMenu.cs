using UnityEngine;
using UnityEditor;
using System.IO;
using System.ComponentModel;

/// <summary>
/// 状态机表转换菜单：选中单表 .xlsx → 转双表 → 生成 <名>_Double.xlsx（state + transition 两张表）。
/// 底层调用 state_machine_convert.py（Python 3 + openpyxl）。
/// 原则：输入单表不动，转换结果写到新文件。自带 round-trip 校验，失败不落盘。
/// </summary>
public static class StateMachineConverterMenu
{
    private const string ContextPath = "Assets/Excel/转双表";   // 右键 .xlsx 的菜单
    private const string ToolsPath = "Tools/状态机转双表";       // 顶部 Tools 菜单

    [MenuItem(ToolsPath, false, 30)]
    public static void ConvertFromTools()
    {
        var sel = Selection.activeObject;
        if (sel == null)
        {
            EditorUtility.DisplayDialog("转双表", "请先在 Project 窗口选中一个单表 .xlsx", "好的");
            return;
        }
        Convert(AssetDatabase.GetAssetPath(sel));
    }

    [MenuItem(ContextPath, false, 51)]
    public static void ConvertFromContext()
    {
        var sel = Selection.activeObject;
        if (sel == null) return;
        Convert(AssetDatabase.GetAssetPath(sel));
    }

    // 两个菜单共用一个校验：选中且是 .xlsx 才可点
    [MenuItem(ContextPath, true)]
    [MenuItem(ToolsPath, true)]
    private static bool Validate()
    {
        var sel = Selection.activeObject;
        if (sel == null) return false;
        var path = AssetDatabase.GetAssetPath(sel);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".xlsx");
    }

    private static void Convert(string assetPath)
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("请等待", "Unity 正在编译，编译完再点。", "好的");
            return;
        }

        string inputFull = AssetPathToFull(assetPath);
        string outputAsset = assetPath.Substring(0, assetPath.Length - 5) + "_Double.xlsx"; // .xlsx → _Double.xlsx
        string outputFull = AssetPathToFull(outputAsset);
        string scriptFull = AssetPathToFull("Assets/Plugins/AI_Tools/state_machine_convert.py");

        if (File.Exists(outputFull)
            && !EditorUtility.DisplayDialog("转双表", $"输出文件已存在，覆盖？\n{outputFull}", "覆盖", "取消"))
            return;

        Debug.Log($"[转双表] {inputFull}\n  → {outputFull}");
        var (text, exitCode) = RunPython($"{Quote(scriptFull)} {Quote(inputFull)} {Quote(outputFull)}");

        if (exitCode != 0)
        {
            Debug.LogError("[转双表] 转换失败:\n" + text);
            EditorUtility.DisplayDialog("转双表失败", text, "好的");
            return;
        }

        Debug.Log("[转双表] " + text.Replace("\n", "\n  "));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("转双表完成", $"已生成双表:\n{outputAsset}\n\n(输入单表未改动)", "好的");
    }

    /// <summary>跑 Python，优先 python，找不到再试 python3。返回 (输出, 退出码)。</summary>
    private static (string text, int exitCode) RunPython(string args)
    {
        foreach (var py in new[] { "python", "python3" })
        {
            try
            {
                var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = py;
                p.StartInfo.Arguments = args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                return (stdout + (string.IsNullOrEmpty(stderr) ? "" : "\n[stderr]\n" + stderr), p.ExitCode);
            }
            catch (Win32Exception) { continue; } // 该命令不存在，试下一个
        }
        return ("找不到 python 命令。请安装 Python 3 + openpyxl（pip install openpyxl）", -1);
    }

    private static string Quote(string p) => "\"" + p.Replace("\"", "\\\"") + "\"";

    private static string AssetPathToFull(string assetPath)
        => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
}
