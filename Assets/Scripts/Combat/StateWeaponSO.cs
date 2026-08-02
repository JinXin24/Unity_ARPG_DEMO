using UnityEngine;
using System.Collections.Generic;

/// <summary>武器显隐时间点</summary>
[System.Serializable]
public class WeaponVisibleConfig
{
    [Header("启用")]
    public bool enabled = true;
    [Header("武器路径 (Tools→复制Transform路径)")]
    public string weaponPath;
    [Header("开始显示 (秒)")]
    public float showSec;
    [Header("结束显示 (秒)")]
    public float hideSec = 999f;
}

/// <summary>单个状态的武器配置</summary>
[System.Serializable]
public class StateWeaponData
{
    public int StateId;
    public List<WeaponVisibleConfig> weapons = new();
}

/// <summary>ScriptableObject：武器显隐配置</summary>
[CreateAssetMenu(menuName = "配置/武器显隐配置")]
public class StateWeaponSO : ScriptableObject
{
    public List<StateWeaponData> states = new();
}
