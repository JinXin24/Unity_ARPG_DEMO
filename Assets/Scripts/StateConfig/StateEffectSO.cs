using UnityEngine;
using System.Collections.Generic;

/// <summary>单段特效配置</summary>
[System.Serializable]
public class EffectConfig
{
    [Header("启用")]
    public bool enabled = true;
    [Header("触发时间 (秒)")]
    public float triggerSec;
    [Header("特效预制体")]
    public GameObject effectPrefab;
    [Header("挂点路径 (角色的子物体路径)")]
    public string bindPoint;
    [Header("持续时长 (秒, 0=跟随动画销毁)")]
    public float duration;
    [Header("位置偏移")]
    public Vector3 offset;
    [Header("旋转偏移")]
    public Vector3 rotation;
}

/// <summary>单个状态的特效配置列表</summary>
[System.Serializable]
public class StateEffectData
{
    public int StateId;
    [Header("特效配置")]
    public List<EffectConfig> effects = new();
}

/// <summary>ScriptableObject：状态特效播放配置</summary>
[CreateAssetMenu(menuName = "配置/状态特效配置")]
public class StateEffectSO : ScriptableObject
{
    public List<StateEffectData> states = new();
}
