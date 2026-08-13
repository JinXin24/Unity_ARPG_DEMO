using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI 单个状态的速度剖面配置 — 敌人移动专用（参照玩家 StateMotionSO / PhysicsConfig 思路）。
/// 曲线语义：横轴 = 该状态动画的归一化时间(0~1)，纵轴 = 速度倍率(0 停 → 1 该状态全速)。
/// 关键帧直接控制"什么时候动"（曲线从 0 升起）/"什么时候停"（曲线降到 0）。
/// 参数来源：FBX RootT 曲线实测（见生成工具 AIMotionGenerator）。
/// </summary>
[System.Serializable]
public class AIMotionData
{
    public int EnemyId;

    /// <summary>状态 StateId（对应 AIConfig.ai_state 表）</summary>
    public int StateId;

    /// <summary>该状态全速基准 (m/s) — 各状态独立，来自 clip 峰值速度实测</summary>
    [Header("全速基准 (m/s)")]
    public float moveSpeed = 1.0f;

    [Header("速度剖面曲线：横轴=动画归一化时间(0~1)，纵轴=速度倍率(0停→1全速)")]
    public AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1f));

    [Header("距目标多近上报到达/切停步动画 (米)")]
    public float stopDist = 1.2f;

    [Header("到位判定距离 (米)")]
    public float arriveDist = 0.5f;
}

/// <summary>
/// AI 位移曲线配置容器 — 一个 SO 装多个状态的曲线（仿 StateMotionSO 容器模式）。
/// 用 Create → 配置 → AI位移配置 创建，EnemyController 按 StateId 取对应条目。
/// </summary>
[CreateAssetMenu(menuName = "配置/AI位移配置")]
public class AIMotionSO : ScriptableObject
{
    public List<AIMotionData> motions = new();
}
