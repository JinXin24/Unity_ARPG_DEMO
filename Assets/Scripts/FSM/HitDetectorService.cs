using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 命中检测服务：按 StateHitSO 配置，每个状态的命中段在 triggerSec 开始、
/// 持续 duration 秒的窗口内持续检测（球形 / 扇形=刀剑挥砍 / 线形=戳刺激光），
/// 每段独立去重结算。后续可扩展盒形/SphereCast 等形状。
/// </summary>
public class HitDetectorService : FSMServiceBase
{
    private StateHitSO hitSO;
    private Dictionary<int, StateHitData> hitDict;

    // stateId → 已触发的段索引（一段只触发一次）
    private Dictionary<int, HashSet<int>> executed = new();
    // (stateId, 段索引) → 已命中的碰撞体（段内去重，段间互不影响）
    private Dictionary<(int, int), HashSet<Collider>> alreadyHit = new();

    /// <summary>传入命中配置 SO（Inspector 里 CharacterState 拖的那个）</summary>
    public HitDetectorService(StateHitSO so)
    {
        hitSO = so;
        if (hitSO != null)
            hitDict = hitSO.states.Where(s => s.segments.Count > 0)
                .ToDictionary(s => s.StateId);
    }

    public override void Init()
    {
        if (hitDict == null)
        {
            Debug.Log("[HitDetectorService] 未拖入 StateHitSO，命中检测不生效");
            return;
        }
        Debug.Log($"[HitDetectorService] 已加载 {hitDict.Count} 个命中配置: {string.Join(", ", hitDict.Keys)}");
    }

    public override void OnBegin()
    {
        int id = Owner.CurrentState.Id;
        executed[id] = new HashSet<int>();
        // 清空该状态所有段的命中记录（重新计算，但上次残留的引用要清掉）
        var stale = alreadyHit.Keys.Where(k => k.Item1 == id).ToList();
        foreach (var k in stale) alreadyHit.Remove(k);
    }

    public override void OnUpdate()
    {
        if (hitDict == null) return;
        int id = Owner.CurrentState.Id;
        if (!hitDict.TryGetValue(id, out var data)) return;

        float t = Owner.GetNormalizedTime();
        float clipLen = Owner.GetClipLength();
        if (!executed.TryGetValue(id, out var exe))
            executed[id] = exe = new HashSet<int>();

        for (int i = 0; i < data.segments.Count; i++)
        {
            var seg = data.segments[i];
            if (!seg.enabled) continue;
            if (exe.Contains(i)) continue; // 窗口已结束，本段不再检测

            float startNorm = seg.triggerSec / clipLen;
            if (t < startNorm) continue; // 未到触发时刻

            // 持续窗口：triggerSec → triggerSec + duration 内每帧检测
            float endNorm = (seg.triggerSec + seg.duration) / clipLen;
            if (t >= endNorm)
            {
                exe.Add(i); // 窗口结束，标记完成
                continue;
            }
            DoHit(id, i, seg); // 窗口内持续判定（段内 alreadyHit 去重，目标只结算一次）
        }
    }

    void DoHit(int stateId, int segIdx, HitSegment seg)
    {
        Vector3 center = Owner.transform.position + Owner.transform.rotation * seg.offset;
        if (!alreadyHit.TryGetValue((stateId, segIdx), out var set))
            alreadyHit[(stateId, segIdx)] = set = new HashSet<Collider>();

        if (seg.shape == HitShape.Sector)
            DoSectorHit(stateId, segIdx, seg, center, set);
        else if (seg.shape == HitShape.Line)
            DoLineHit(stateId, segIdx, seg, center, set);
        else
            DoSphereHit(stateId, segIdx, seg, center, set);
    }

    void DoSphereHit(int stateId, int segIdx, HitSegment seg, Vector3 center, HashSet<Collider> set)
    {
        foreach (var c in Physics.OverlapSphere(center, seg.radius, seg.hitMask))
            TryHit(stateId, segIdx, seg, c, center, set);
    }

    /// <summary>扇形：大球预筛（半径=最大距离），再按水平角度过滤。挥砍高度不限，只比水平夹角。</summary>
    void DoSectorHit(int stateId, int segIdx, HitSegment seg, Vector3 center, HashSet<Collider> set)
    {
        Vector3 dir = Owner.transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;
        foreach (var c in Physics.OverlapSphere(center, seg.radius, seg.hitMask))
        {
            Vector3 flat = c.transform.position - center;
            flat.y = 0f; // 投影到水平面，只判水平角度
            if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(dir, flat) <= seg.halfAngle)
                TryHit(stateId, segIdx, seg, c, center, set);
        }
    }

    /// <summary>线形：lineWidth=0 → 纯射线，lineWidth>0 → 平头圆柱（胶囊粗筛 + 过滤两端半球）</summary>
    void DoLineHit(int stateId, int segIdx, HitSegment seg, Vector3 center, HashSet<Collider> set)
    {
        Vector3 dir = Owner.transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;

        if (seg.lineWidth <= 0f)
        {
            // 纯直线射线
            foreach (var hit in Physics.RaycastAll(center, dir, seg.lineLength, seg.hitMask))
                TryHit(stateId, segIdx, seg, hit.collider, center, set);
            return;
        }

        // 平头圆柱
        Vector3 end = center + dir * seg.lineLength;
        foreach (var c in Physics.OverlapCapsule(center, end, seg.lineWidth, seg.hitMask))
        {
            // 过滤胶囊两端半球：碰撞体投影必须在线段 [0, lineLength] 内 → 平头圆柱
            Vector3 toTarget = c.bounds.center - center;
            float t = Vector3.Dot(toTarget, dir);
            if (t < 0f || t > seg.lineLength) continue;
            TryHit(stateId, segIdx, seg, c, center, set);
        }
    }

    void TryHit(int stateId, int segIdx, HitSegment seg, Collider c, Vector3 center, HashSet<Collider> set)
    {
        if (set.Contains(c)) return; // 本段已命中过，去重
        set.Add(c);

        var info = new DamageInfo
        {
            Damage = seg.damage,
            HitPoint = c.ClosestPoint(center),
            HitDir = (c.transform.position - center).normalized
        };

        var dmgable = c.GetComponent<IDamageable>();
        if (dmgable != null) dmgable.TakeDamage(info);

        Debug.Log($"[命中] State={stateId} 段{segIdx} 目标={c.name} 伤害={seg.damage}");
    }
}
