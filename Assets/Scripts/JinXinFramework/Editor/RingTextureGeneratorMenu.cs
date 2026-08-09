using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器菜单：一键生成环形 PNG 资产。
/// 菜单：Tools → 生成环形贴图 → 生成到 Assets/UI 目录
/// 生成两个版本：灰环（底层）+ 亮环（充能层），尺寸 100×100，环宽 2px。
/// </summary>
public static class RingTextureGeneratorMenu
{
    [MenuItem("Tools/生成环形贴图/生成充能圆环 (100×100, 宽3px)")]
    public static void GenerateChargeRing()
    {
        // 底层灰环：暗色，表示未充能部分
        Texture2D gray = RingTextureGenerator.Generate(100, 3, new Color(0.25f, 0.25f, 0.28f, 1f));
        SavePngWithDialog(gray, "Ring_Charge_Bg");

        // 充能层亮环：红色，fillAmount 驱动
        Texture2D charge = RingTextureGenerator.Generate(100, 3, new Color(0.95f, 0.2f, 0.15f, 1f));
        SavePngWithDialog(charge, "Ring_Charge_Fill");

        AssetDatabase.Refresh();
        Debug.Log("[Ring] 两张圆环已生成");
    }

    [MenuItem("Tools/生成环形贴图/自定义尺寸")]
    public static void GenerateCustom()
    {
        var tex = RingTextureGenerator.Generate(128, 3, new Color(1f, 1f, 1f, 1f));
        SavePngWithDialog(tex, "Ring_Custom");
        AssetDatabase.Refresh();
    }

    /// <summary>弹保存面板，让用户自己选位置和文件名</summary>
    static void SavePngWithDialog(Texture2D tex, string defaultName)
    {
        string path = EditorUtility.SaveFilePanel(
            "保存环形贴图",                    // 面板标题
            "Assets",                          // 默认目录
            defaultName,                       // 默认文件名
            "png");                            // 扩展名

        if (string.IsNullOrEmpty(path))
            return;                            // 用户取消了

        // 确保保存到项目 Assets 内，并转成相对路径
        if (!path.StartsWith(Application.dataPath))
        {
            Debug.LogError("[Ring] 必须保存到项目 Assets 目录内");
            return;
        }

        string relative = "Assets" + path.Substring(Application.dataPath.Length);
        File.WriteAllBytes(relative, tex.EncodeToPNG());
    }
}
