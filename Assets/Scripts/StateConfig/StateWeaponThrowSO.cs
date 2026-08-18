using UnityEngine;
using System.Collections.Generic;

/// <summary>单个武器投掷配置（把武器丢出去 → 悬停自转 → 收回的一整套参数）</summary>
[System.Serializable]
public class WeaponThrowConfig
{
    [Header("启用")]
    public bool enabled = true;

    [Header("触发")]
    [Tooltip("进入状态后第几秒触发投掷（秒，相对动画开头）")]
    public float triggerSec = 0f;

    [Header("武器")]
    [Tooltip("要丢出去的武器节点路径 (Tools→复制Transform路径)")]
    public string weaponPath;

    [Header("悬停点")]
    [Tooltip("武器悬停点相对角色根节点的本地偏移（X右 / Y上 / Z前，米）。编辑模式可拖 Scene 手柄摆")]
    public Vector3 hoverOffset = new Vector3(0f, 1.2f, 6f);

    [Header("飞出")]
    [Tooltip("飞出耗时（秒）")]
    public float flyOutDuration = 0.25f;
    [Tooltip("飞出速度曲线：横轴 0~1 进度，纵轴 0~1 位移比例")]
    public AnimationCurve flyOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("自转")]
    [Tooltip("自转圈数")]
    public int spinLaps = 3;
    [Tooltip("自转总耗时（秒），越小转越快")]
    public float spinDuration = 1.2f;
    [Tooltip("自转轴（本地空间，相对武器自身朝向）")]
    public Vector3 spinAxis = Vector3.up;

    [Header("收回")]
    [Tooltip("飞回耗时（秒）")]
    public float flyBackDuration = 0.25f;
    [Tooltip("飞回速度曲线")]
    public AnimationCurve flyBackCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("命中")]
    [Tooltip("开启后自转期每帧用球扫一圈结算伤害")]
    public bool detectHit = true;
    [Tooltip("命中半径（米）")]
    public float hitRadius = 1.2f;
    [Tooltip("伤害倍率：100 = 100% 攻击力")]
    public int damage = 100;
    [Tooltip("命中层")]
    public LayerMask hitMask = -1;
}

/// <summary>单个状态的武器投掷配置</summary>
[System.Serializable]
public class StateWeaponThrowData
{
    public int StateId;
    public List<WeaponThrowConfig> throws = new();
}

/// <summary>ScriptableObject：武器投掷配置（按状态组织，和 StateWeaponSO 同一套结构）</summary>
[CreateAssetMenu(menuName = "配置/武器投掷配置")]
public class StateWeaponThrowSO : ScriptableObject
{
    public List<StateWeaponThrowData> states = new();
}
