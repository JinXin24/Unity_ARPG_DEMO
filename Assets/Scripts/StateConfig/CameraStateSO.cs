using UnityEngine;
using System.Collections.Generic;

/// <summary>单段镜头变化</summary>
[System.Serializable]
public class CameraKeyframe
{
    [Header("触发时间 (秒)")]
    public float triggerSec;
    [Header("持续时间 (秒)")]
    public float duration = 0.3f;

    [Header("距离 (相对 FollowTarget)")]
    [Tooltip("0=不改变")] public float targetDistance;

    [Header("目标角度")]
    [Tooltip("锁定目标偏航角（绕角色转多少度），-999=不改变")]
    public float targetYaw = -999f;
    [Tooltip("锁定目标俯仰角（抬高/压低），-999=不改变")]
    public float targetPitch = -999f;

    [Header("禁用输入")]
    [Tooltip("期间禁用鼠标旋转输入")]
    public bool lockInput;

    [Header("相机臂挂点")]
    [Tooltip("枢轴挂点的子物体路径，如 Bip001/Bip001Pelvis。空=目标自身")]
    public string pivotPath;
}

/// <summary>单个状态的镜头时间线</summary>
[System.Serializable]
public class CameraStateData
{
    public int StateId;
    public List<CameraKeyframe> timeline = new();
}

/// <summary>ScriptableObject：状态镜头时间线配置</summary>
[CreateAssetMenu(menuName = "配置/状态镜头配置")]
public class CameraStateSO : ScriptableObject
{
    public List<CameraStateData> states = new();
}
