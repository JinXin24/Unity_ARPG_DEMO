using UnityEngine;

/// <summary>
/// 一次命中结算的伤害信息。由命中检测方算出，传给被击方的 IDamageable。
/// 可扩展：暴击、来源、击退力等。
/// </summary>
public struct DamageInfo
{
    public int Damage;        // 最终伤害
    public bool IsCrit;       // 是否暴击
    public Vector3 HitPoint;  // 命中点
    public Vector3 HitDir;    // 命中方向（攻击方→目标）
    public GameObject Source; // 攻击来源（谁打的）
}
