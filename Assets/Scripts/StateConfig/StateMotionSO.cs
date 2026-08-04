using UnityEngine;
using System.Collections.Generic;

/// <summary>单段位移配置 — 参照 Demo_3D_RPG_ PhysicsConfig</summary>
[System.Serializable]
public class PhysicsConfig
{
    [Header("启用位移")]
    public bool enabled = true;
    [Header("触发时间 (秒)")]
    public float triggerSec;
    [Header("结束时间 (秒)")]
    public float endSec;
    [Header("位移强度 (不是最终米数，配合曲线调)")]
    public Vector3 force;
    [Header("X 轴速度曲线 (1=全速, 0=停)")]
    public AnimationCurve curveX = EaseOutCurve();
    [Header("Y 轴速度曲线 (1=全速, 0=停)")]
    public AnimationCurve curveY = EaseOutCurve();
    [Header("Z 轴速度曲线 (1=全速, 0=停)")]
    public AnimationCurve curveZ = EaseOutCurve();
    [Header("忽略重力")]
    public bool ignoreGravity;
    [Header("前方碰到单位后停下 (米)")]
    public float stopDst;

    [Header("移动子节点（而不是根节点）")]
    public bool moveChild;
    [Header("子节点路径")]
    public string childPath;
    [Header("状态结束时子节点归零")]
    public bool resetChildOnEnd;

    static AnimationCurve EaseOutCurve() => new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2f),
        new Keyframe(1f, 0f, -2f, 0f)
    );
}

/// <summary>单个状态的位移配置列表</summary>
[System.Serializable]
public class StateMotionData
{
    public int StateId;
    [Header("物理位移配置")]
    public List<PhysicsConfig> physicsConfigs = new();
}

/// <summary>ScriptableObject：状态位移曲线配置</summary>
[CreateAssetMenu(menuName = "配置/状态位移配置")]
public class StateMotionSO : ScriptableObject
{
    public List<StateMotionData> motions = new();
}
