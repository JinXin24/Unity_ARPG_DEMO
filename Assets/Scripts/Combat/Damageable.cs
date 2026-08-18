using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 统一受击方 — 敌人 / 木桩 / 可破坏物挂它接收伤害。
/// 扣血 + 发布 DamageEvent / DeathEvent，表现层（血条 / 飘字 / 死亡动画）订阅事件，与逻辑解耦。
/// 替代旧的 TestDummy。
/// </summary>
public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHp = 100;

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    public bool IsDead => Hp <= 0;
    public CharacterStats Stats { get; set; }   // 运行时赋值，可空（空 = 按 0 防算）

    void Awake()
    {
        Hp = maxHp;
    }

    public void TakeDamage(DamageInfo info)
    {
        bool wasDead = IsDead;
        Hp = Mathf.Max(0, Hp - info.Damage);
        EventBus.Publish(new DamageEvent(this, info));   // 无条件发：受击动画每次命中都播（含死亡后）
        if (!wasDead && IsDead)                           // 死亡事件只在首次归零时发一次，避免重复
            EventBus.Publish(new DeathEvent(this));
    }
}
