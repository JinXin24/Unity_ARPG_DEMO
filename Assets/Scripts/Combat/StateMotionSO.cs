using UnityEngine;
using System.Collections.Generic;

/// <summary>单段位移配置 — 参照 Demo_3D_RPG_ PhysicsConfig</summary>
[System.Serializable]
public class PhysicsConfig
{
    [Header("触发帧")]
    public int triggerFrame;
    [Header("结束帧")]
    public int endFrame;
    [Header("位移向量 (米)：窗口内总位移量")]
    public Vector3 force;
    [Header("速度曲线：横轴=时间进度，纵轴=速度倍率")]
    public AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2f),   // 开头满速，快速下降
        new Keyframe(1f, 0f, -2f, 0f)    // 末尾归零，自然停住
    );
    [Header("是否忽略重力")]
    public bool ignoreGravity;
    [Header("前方检测到单位后停下")]
    public float stopDst;
}

/// <summary>单个状态的位移配置列表</summary>
[System.Serializable]
public class StateMotionData
{
    public int StateId;
    [Header("你的动画帧率（Timeline 上显示的）")]
    public float frameRate = 30f;
    [Header("物理位移配置")]
    public List<PhysicsConfig> physicsConfigs = new();
}

/// <summary>ScriptableObject：状态位移曲线配置</summary>
[CreateAssetMenu(menuName = "配置/状态位移配置")]
public class StateMotionSO : ScriptableObject
{
    public List<StateMotionData> motions = new();
}
