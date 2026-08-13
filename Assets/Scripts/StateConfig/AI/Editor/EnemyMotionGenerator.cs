using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器菜单：一键生成 AI 巡逻位移 SO（AIMotionSO）。
/// 菜单：Tools → AI巡逻位移 → 生成默认配置 (2001)
/// 生成一个容器 SO，只含一个 Walking 状态（2002）。
/// 移动方式：Blend Tree Speed 参数（0~1）作速度倍率，moveSpeed 为全速基准。
/// 参数来源：FBX RootT.z（Humanoid 根位移）实测 Walking 1.75m/1.03s ≈ 1.68 m/s。
/// 注意坐标系陷阱：本 FBX 模型 Z 轴朝前，前进位移在 RootT.z（不是 Y）。
/// </summary>
public static class EnemyMotionGenerator
{
    const string DefaultDir = "Assets/Resources/StateConfig/AI";

    [MenuItem("Tools/AI巡逻位移/生成默认配置 (2001)")]
    public static void GenerateDefault()
    {
        Directory.CreateDirectory(DefaultDir);
        string path = $"{DefaultDir}/AIMotion.asset";

        // 已存在则直接选中返回，避免覆盖用户调好的曲线
        var existing = AssetDatabase.LoadAssetAtPath<AIMotionSO>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            Debug.Log($"[AIMotion] 已存在 {path}，选中它（不覆盖）");
            return;
        }

        var so = ScriptableObject.CreateInstance<AIMotionSO>();

        // Walking：匀速全速 1.68 m/s（循环动画），Blend Tree 由 Speed 参数控制加减速
        so.motions.Add(new AIMotionData
        {
            EnemyId = 2001,
            StateId = 2002,
            moveSpeed = 1.68f,
            stopDist = 1.2f,
            arriveDist = 0.5f,
            speedCurve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f))
        });

        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = so;
        Debug.Log($"[AIMotion] 已生成 {path}，共 {so.motions.Count} 个状态曲线");
    }
}
