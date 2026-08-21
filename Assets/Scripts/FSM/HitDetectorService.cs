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
    // (stateId, 段索引) → 飞行剑气波头当前距离（从 triggerSec 起推进，跨帧保留）
    private Dictionary<(int, int), float> wavefront = new();

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
        var staleWave = wavefront.Keys.Where(k => k.Item1 == id).ToList();
        foreach (var k in staleWave) wavefront.Remove(k);
    }

    public override void OnUpdate()
    {
        if (hitDict == null) return;
        int id = Owner.CurrentState.Id;
        if (!hitDict.TryGetValue(id, out var data)) return;

        // 用「状态进入后的真实流逝秒」判定窗口，不读 animator 的 normalizedTime。
        // 原因：进入状态的当帧 CrossFade 未生效，GetNormalizedTime() 读到的是上一状态
        // 的进度（旧状态若是长 clip，t 很大），会把段窗口一次打穿标成已结束 → 命中检测永不触发。
        float elapsed = Owner.GetStateElapsed();
        if (!executed.TryGetValue(id, out var exe))
            executed[id] = exe = new HashSet<int>();

        for (int i = 0; i < data.segments.Count; i++)
        {
            var seg = data.segments[i];
            if (!seg.enabled) continue;
            if (exe.Contains(i)) continue; // 窗口已结束，本段不再检测

            //Debug.Log($"elapsed={elapsed:F3} seg={i}");   // 调试：保留你的日志，改看流逝秒

            if (elapsed < seg.triggerSec) continue; // 未到触发时刻（秒）

            

            // 飞行剑气：线形 + lineTravelSpeed>0，波头自己推进到 lineLength 才结束（忽略 duration）
            if (seg.shape == HitShape.Line && seg.lineTravelSpeed > 0f)
            {
                if (DoWaveHit(id, i, seg)) exe.Add(i); // 波头到头 → 段结束
                continue;
            }

            

            // 持续窗口：triggerSec → triggerSec + duration 内每帧检测（秒直接比较）

            


            if (elapsed >= seg.triggerSec + seg.duration)
            {
                exe.Add(i); // 窗口结束，标记完成
                continue;
            }

            // 扇形扫掠：sectorSlices>1 时在窗口内逐片展开（剑挥到哪、那片才结算）
            if (seg.shape == HitShape.Sector && seg.sectorSlices > 1)
            {
                //Debug.Log("检测开始");
                float progress = Mathf.InverseLerp(seg.triggerSec, seg.triggerSec + seg.duration, elapsed);
                DoSectorSweep(id, i, seg, progress);

                continue;
            }
            
            
            

            DoHit(id, i, seg); // 窗口内持续判定（段内 alreadyHit 去重，目标只结算一次）
        }
    }

    void DoHit(int stateId, int segIdx, HitSegment seg)
    {
        if (seg.shape == HitShape.Physical) return; // 物理碰撞体由场景 WeaponHit 处理，虚拟扫描不检测
        Vector3 center = Owner.transform.position + Owner.transform.rotation * seg.offset;
        if (!alreadyHit.TryGetValue((stateId, segIdx), out var set))
            alreadyHit[(stateId, segIdx)] = set = new HashSet<Collider>();

        if (seg.shape == HitShape.Sector)
            DoSectorHit(stateId, segIdx, seg, center, set);
        else if (seg.shape == HitShape.Line)
            DoLineHit(stateId, segIdx, seg, center, set);
        else if (seg.shape == HitShape.Box)
            DoBoxHit(stateId, segIdx, seg, center, set);
        else
            DoSphereHit(stateId, segIdx, seg, center, set);
    }

    /// <summary>
    /// 飞行剑气：波头从 triggerSec 起以 lineTravelSpeed 从近往远推进，每帧只检测
    /// 波头扫过的那一小段（SphereCast），扫到才结算——剑气有真实飞行时序，不是整段瞬发。
    /// 返回 true = 波头已到达 lineLength，本段结束。
    /// </summary>
    bool DoWaveHit(int stateId, int segIdx, HitSegment seg)
    {
        Vector3 dir = Owner.transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;
        Vector3 center = Owner.transform.position + Owner.transform.rotation * seg.offset;

        if (!wavefront.TryGetValue((stateId, segIdx), out float prev))
            wavefront[(stateId, segIdx)] = prev = 0f;
        if (prev >= seg.lineLength) return true; // 已到头（兜底，正常由 exe 挡住）

        float next = Mathf.Min(seg.lineLength, prev + seg.lineTravelSpeed * Time.deltaTime);
        wavefront[(stateId, segIdx)] = next;

        if (!alreadyHit.TryGetValue((stateId, segIdx), out var set))
            alreadyHit[(stateId, segIdx)] = set = new HashSet<Collider>();

        // 波头扫掠 = 半径 lineWidth 的球沿 dir 从 prev 推进到 next（平头圆柱），只结算这段新扫过的区域
        float sweep = next - prev;
        if (sweep > 0.0001f)
        {
            Vector3 origin = center + dir * prev;
            foreach (var hit in Physics.SphereCastAll(origin, seg.lineWidth, dir, sweep, seg.hitMask))
                TryHit(stateId, segIdx, seg, hit.collider, center, set);
        }

        return next >= seg.lineLength;
    }

    void DoSphereHit(int stateId, int segIdx, HitSegment seg, Vector3 center, HashSet<Collider> set)
    {
        foreach (var c in Physics.OverlapSphere(center, seg.radius, seg.hitMask))
            TryHit(stateId, segIdx, seg, c, center, set);
    }

    /// <summary>盒形：OverlapBox 检测，朝向跟随角色 + yaw/pitch 偏转，halfExtents = boxSize/2。</summary>
    void DoBoxHit(int stateId, int segIdx, HitSegment seg, Vector3 center, HashSet<Collider> set)
    {
        Quaternion orientation = Owner.transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f);
        Vector3 half = seg.boxSize * 0.5f;
        foreach (var c in Physics.OverlapBox(center, half, orientation, seg.hitMask))
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

    /// <summary>
    /// 扇形扫掠：扇形在 duration 内从一侧逐渐展开到另一侧（剑挥到哪片、那片才结算），
    /// 默认从左往右（-halfAngle → +halfAngle），sweepFromRight=true 时从右往左。
    /// 不是整片瞬发。每帧一次 OverlapSphere 粗筛，用带符号角度判目标是否落在"已扫过"的角度区间，
    /// alreadyHit 去重 → 越靠挥砍起始侧的敌人越早被扫到、越早结算。
    /// </summary>
    void DoSectorSweep(int stateId, int segIdx, HitSegment seg, float progress)
    {
        Vector3 center = Owner.transform.position + Owner.transform.rotation * seg.offset;
        Vector3 dir = Owner.transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;

        if (!alreadyHit.TryGetValue((stateId, segIdx), out var set))
            alreadyHit[(stateId, segIdx)] = set = new HashSet<Collider>();

        // 挥砍进度 → 当前扫到的边界角。默认从左往右（-halfAngle → +halfAngle），
        // sweepFromRight=true 时反向（+halfAngle → -halfAngle）。
        float from = seg.sweepFromRight ? seg.halfAngle : -seg.halfAngle;
        float to   = seg.sweepFromRight ? -seg.halfAngle : seg.halfAngle;
        float currentAngle = Mathf.Lerp(from, to, Mathf.Clamp01(progress));

        foreach (var c in Physics.OverlapSphere(center, seg.radius, seg.hitMask))
        {
            Vector3 flat = c.transform.position - center;
            flat.y = 0f; // 投影到水平面，只判水平夹角（绕 Y 轴的带符号角）
            if (flat.sqrMagnitude <= 0.0001f) continue;

            float signedAngle = Vector3.SignedAngle(dir, flat, Vector3.up);
            // 已扫过的角度区间 = [min(from,currentAngle), max(from,currentAngle)]，左右两个方向统一用区间判
            float lo = Mathf.Min(from, currentAngle);
            float hi = Mathf.Max(from, currentAngle);
            if (signedAngle >= lo && signedAngle <= hi)
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

        Vector3 hitPoint = c.ClosestPointOnBounds(center);   // 包围盒最近点：所有碰撞体类型都支持（ClosestPoint 只支持球/盒/胶囊/凸网格）
        Vector3 hitDir = (c.transform.position - center).normalized;

        // 攻防结算：攻击方数值从 Owner 拿，防御方数值从目标 Damageable 拿（没有则按 0 防）
        var attacker = Owner != null ? Owner.GetStats() : null;
        // GetComponentInParent：碰撞体可能挂在子物体（模型自带的 MeshCollider），
        // Damageable 在根物体上，沿父链向上找，避免扫到子碰撞体时取不到受击组件。
        var target = c.GetComponentInParent<Damageable>();
        var defender = target != null ? target.Stats : null;
        var info = DamageCalculator.Calculate(attacker, defender, seg.damage, hitPoint, hitDir,
            Owner != null ? Owner.gameObject : null);

        var dmgable = c.GetComponentInParent<IDamageable>();
        if (dmgable != null) dmgable.TakeDamage(info);

        Debug.Log($"[命中] State={stateId} 段{segIdx} 目标={c.name} 伤害={info.Damage}{(info.IsCrit ? " 暴击" : "")}");
    }
}
