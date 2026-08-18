using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 物理剑命中检测（和虚拟扫描 HitDetectorService 并存，二选一或混用）。
/// 挂在剑刃节点上：Collider 提供形状，StateHitSO 提供数值（伤害/层/哪些状态算攻击）。
/// 挂上后 Reset() 自动配好 Trigger Collider + Kinematic Rigidbody。
///
/// 工作原理：
///  - 订阅 StateChangedEvent，进入攻击状态 → BeginSwing，离开 → EndSwing
///  - 挥砍中每帧用碰撞体形状手动 OverlapCapsule 检测（不依赖 OnTriggerEnter：
///    快速挥动的 kinematic trigger 是离散检测，远距离线速度大容易穿透漏检）
///  - OnTriggerEnter/Stay 作兜底（swingHit 去重，不双倍伤害）
///  - 去重：按 IDamageable 实例（一个敌人多个 Collider 也只结算一次）
///    Swing 内去重，下次挥砍 Clear 重新可打
///  - 数值全从 hitSO 读，不在脚本里重复配
/// </summary>
public class WeaponHit : MonoBehaviour, IEventReceiver<StateChangedEvent>
{
    [Header("配置")]
    [Tooltip("数据源：伤害/层/哪些状态算攻击。留空则只做物理检测不打伤害")]
    [SerializeField] private StateHitSO hitSO;
    [Tooltip("哪些状态算挥砍。空 = 从 hitSO 推导（有启用段的状态就是攻击）")]
    [SerializeField] private List<int> attackStateIds = new();

    /// <summary>命中回调（做顿帧/特效用）</summary>
    public event System.Action<DamageInfo> OnHit;

    // 运行时状态
    private bool swinging;
    private int currentDamage;
    private LayerMask currentMask;
    private CapsuleCollider swordCol;               // 物理剑碰撞体（挂载时配好，Update 手动检测用）
    private readonly HashSet<IDamageable> swingHit = new();

    void Reset()
    {
        ConfigureSelf();
    }

    void Awake()
    {
        ConfigureSelf();
        EventBus.Subscribe(this);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe(this);
    }

    /// <summary>挂上组件自动配好物理三件套：Trigger Collider + Kinematic Rigidbody</summary>
    void ConfigureSelf()
    {
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.direction = 2; // 沿 Z 轴（剑身方向）
        }
        else
        {
            GetComponent<Collider>().isTrigger = true;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;  // 剑是动画驱动，物理不能抢
            rb.useGravity = false;
        }

        swordCol = GetComponent<CapsuleCollider>();
    }

    // ═══════ 状态订阅 ═══════

    public void OnEvent(StateChangedEvent evt)
    {
        if (IsAttackState(evt.StateId))
            BeginSwing(evt.StateId);
        else
            EndSwing();
    }

    bool IsAttackState(int stateId)
    {
        if (attackStateIds.Count > 0)
            return attackStateIds.Contains(stateId);
        var data = FindState(stateId);
        // 只有"物理剑"段的状态才算挥砍：Sector/Line/Sphere 虚拟段由 HitDetectorService 处理，
        // 物理剑不抢，否则 swingHit 按整个状态去重，会把虚拟段的第二段伤害也吃掉
        return data != null && data.segments.Any(s => s.enabled && s.shape == HitShape.Physical);
    }

    void BeginSwing(int stateId)
    {
        var seg = FirstEnabledSegment(stateId);
        if (seg == null) return;

        currentDamage = seg.damage;
        currentMask = seg.hitMask;
        swinging = true;
        swingHit.Clear();
    }

    void EndSwing()
    {
        swinging = false;
        swingHit.Clear();
    }

    StateHitData FindState(int stateId)
    {
        if (hitSO == null) return null;
        for (int i = 0; i < hitSO.states.Count; i++)
            if (hitSO.states[i].StateId == stateId) return hitSO.states[i];
        return null;
    }

    HitSegment FirstEnabledSegment(int stateId)
    {
        var data = FindState(stateId);
        if (data == null) return null;
        // 只找 Physical 段：虚拟扫描段（Sector/Line/Sphere）由 HitDetectorService 处理，物理剑不碰
        for (int i = 0; i < data.segments.Count; i++)
        {
            var seg = data.segments[i];
            if (seg.enabled && seg.shape == HitShape.Physical) return seg;
        }
        return null;   // 没有物理剑段 → 不挥砍
    }

    // ═══════ 检测 ═══════

    /// <summary>
    /// 挥砍期间每帧用剑碰撞体的实际形状手动重叠检测。
    /// 比 OnTriggerEnter 可靠：物理引擎对快速挥动的 kinematic trigger 是离散检测，
    /// 两步之间可能直接穿过去（远距离剑刃线速度最大最易漏），且 Enter 只在重叠"开始"触发一次，
    /// 重叠早于挥砍开始/晚于结束就会被丢弃。这里每帧取当前动画位置检测，距离无关。
    /// </summary>
    void Update()
    {
        if (!swinging || swordCol == null) return;

        // 把本地胶囊参数换算成世界空间（支持非均匀缩放的父级）
        Transform t = swordCol.transform;
        Vector3 localScale = t.lossyScale;
        Vector3 worldCenter = t.TransformPoint(swordCol.center);
        Vector3 axis = (swordCol.direction == 0 ? t.right : swordCol.direction == 1 ? t.up : t.forward).normalized;

        float worldRadius, straightHalf;   // straightHalf = 直段半长（去掉两端半球）
        if (swordCol.direction == 0)
        {
            worldRadius = swordCol.radius * Mathf.Max(localScale.y, localScale.z);
            straightHalf = (swordCol.height * 0.5f - swordCol.radius) * localScale.x;
        }
        else if (swordCol.direction == 1)
        {
            worldRadius = swordCol.radius * Mathf.Max(localScale.x, localScale.z);
            straightHalf = (swordCol.height * 0.5f - swordCol.radius) * localScale.y;
        }
        else
        {
            worldRadius = swordCol.radius * Mathf.Max(localScale.x, localScale.y);
            straightHalf = (swordCol.height * 0.5f - swordCol.radius) * localScale.z;
        }

        Vector3 top = worldCenter + axis * straightHalf;
        Vector3 bottom = worldCenter - axis * straightHalf;

        foreach (var c in Physics.OverlapCapsule(top, bottom, worldRadius, currentMask))
            DealDamage(c, c.ClosestPointOnBounds(transform.position), -transform.forward);
    }

    // 兜底：物理引擎能抓住重叠时也走一遍（swingHit 去重，不会双倍伤害）
    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);   // Enter 只在重叠开始时触发，Stay 每帧都能补上

    void TryHit(Collider other)
    {
        if (!swinging) return;
        DealDamage(other, other.ClosestPointOnBounds(transform.position), -transform.forward);
    }

    void DealDamage(Collider target, Vector3 hitPoint, Vector3 hitDir)
    {
        // 层过滤（Trigger 路径没有射线 mask，手动再查一次）
        if (((1 << target.gameObject.layer) & currentMask) == 0) return;

        var dmgable = target.GetComponentInParent<IDamageable>();
        if (dmgable == null) return;
        if (!swingHit.Add(dmgable)) return; // 这一挥打过了，去重

        var info = new DamageInfo
        {
            Damage = currentDamage,
            HitPoint = hitPoint,
            HitDir = hitDir,
            Source = transform.root.gameObject,
        };
        dmgable.TakeDamage(info);
        OnHit?.Invoke(info);
    }

}
