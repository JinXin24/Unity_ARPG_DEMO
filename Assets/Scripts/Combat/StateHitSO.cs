using UnityEngine;
using System.Collections.Generic;

/// <summary>命中检测形状：球形 / 扇形 / 线形（虚拟扫描） / Physical（场景碰撞体，由 WeaponHit 处理）</summary>
// 注意：Box 加在末尾，避免打乱已有 asset 里 Physical=3 的序列化映射（枚举中间插入会整体后移）
public enum HitShape { Sphere, Sector, Line, Physical, Box }

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
    public int damage = 100;   // 技能倍率：100 = 100% 攻击力（DamageCalculator 乘攻击方 Atk）
    public float radius = 2f;
    public float halfAngle = 60f;
    [Tooltip("扇形分段数：>1 时扇形在 duration 内逐片展开（剑挥到哪、那片才结算）；1 或 0 = 瞬发整片")]
    public int sectorSlices = 1;
    [Tooltip("扇形扫掠方向（仅 sectorSlices>1 时生效）：false=从左往右（默认），true=从右往左")]
    public bool sweepFromRight;
    public float yawOffset;
    public float pitchOffset;
    public float lineLength = 3f;
    public float lineWidth = 0.3f;
    [Tooltip("剑气飞行速度 (米/秒)。>0 时线形变为飞行剑气：从 triggerSec 起波头以该速度从近往远扫掠，扫到才结算（不是整段瞬发）；0 或负 = 瞬发整段")]
    public float lineTravelSpeed;
    [Tooltip("盒形全尺寸（长X、高Y、宽Z），检测用 halfExtents = 尺寸/2")]
    public Vector3 boxSize = new Vector3(2f, 1.5f, 2f);
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
