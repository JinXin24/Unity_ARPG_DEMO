using UnityEngine;
using System.Collections.Generic;

/// <summary>命中检测形状</summary>
public enum HitShape { Sphere, Sector, Line }

/// <summary>
/// 单个命中段：一个状态内可配多段，每段独立结算（伤害/范围/触发时刻各自配置）。
/// 形状：球形 / 扇形（刀剑挥砍）/ 线形（戳刺、激光）。
/// </summary>
[System.Serializable]
public class HitSegment
{
    // Inspector 绘制由 HitSegmentDrawer 完成：中文标签 + 按 shape 整行隐藏无关参数
    public bool enabled = true;
    public HitShape shape = HitShape.Sector;
    public float triggerSec;
    public float duration = 0.2f;
    public int damage = 100;
    public float radius = 2f;
    public float halfAngle = 60f;
    public float yawOffset;
    public float pitchOffset;
    public float lineLength = 3f;
    public float lineWidth = 0.3f;
    public Vector3 offset = Vector3.zero;
    public LayerMask hitMask = -1;
}

/// <summary>单个状态的命中段配置</summary>
[System.Serializable]
public class StateHitData
{
    public int StateId;
    public List<HitSegment> segments = new();
}

/// <summary>ScriptableObject：攻击状态命中检测配置</summary>
[CreateAssetMenu(menuName = "配置/命中检测配置")]
public class StateHitSO : ScriptableObject
{
    public List<StateHitData> states = new();
}
