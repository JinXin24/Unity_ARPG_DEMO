using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 武器投掷服务：按 StateWeaponThrowSO 配置，状态内每段在 triggerSec 触发一次，
/// 把武器丢出去 → 飞到悬停点自转 N 圈 → 飞回手中挂回。触发点用 GetStateElapsed()
/// 匹配（和其他服务一致），飞出去后按自身时序（飞出/自转/飞回时长）推进，与动画时长无关。
/// 命中检测可选：自转期每帧 OverlapSphere 粗测 + 伤害结算，单次投掷内去重。
/// </summary>
public class WeaponThrowService : FSMServiceBase
{
    private StateWeaponThrowSO throwSO;
    private Dictionary<int, StateWeaponThrowData> throwDict;

    // stateId → 已触发的投掷段索引（一段只触发一次）
    private readonly Dictionary<int, HashSet<int>> executed = new();

    // 单次投掷的运行时小状态机
    private enum Phase { Idle, FlyingOut, Spinning, FlyingBack }
    private class Runtime
    {
        public Phase phase = Phase.Idle;
        public Transform weapon;
        public Transform hand;          // 原挂点（父级）
        public Vector3 handLocalPos;
        public Quaternion handLocalRot;
        public Vector3 handLocalScale; // SetParent(null,true) 会改 localScale，回来必须恢复
        public Vector3 flyFrom;
        public Vector3 flyTo;
        public float phaseTimer;
        public float spinTotalDeg;
        public readonly HashSet<Collider> hitSet = new();

        // 配置副本（推进时用，避免每次查 SO）
        public Vector3 hoverOffset;
        public float flyOutDuration;
        public AnimationCurve flyOutCurve;
        public float spinDuration;
        public Vector3 spinAxis;
        public float flyBackDuration;
        public AnimationCurve flyBackCurve;
        public bool detectHit;
        public float hitRadius;
        public int damage;
        public LayerMask hitMask;
    }
    private readonly Dictionary<(int, int), Runtime> runtimes = new(); // (stateId, 段索引) → 运行时

    public WeaponThrowService(StateWeaponThrowSO so)
    {
        throwSO = so;
        if (throwSO != null)
            throwDict = throwSO.states.Where(s => s.throws.Count > 0)
                .ToDictionary(s => s.StateId);
    }

    public override void Init()
    {
        if (throwDict == null)
        {
            Debug.Log("[WeaponThrowService] 未拖入 StateWeaponThrowSO，武器投掷不生效");
            return;
        }
        Debug.Log($"[WeaponThrowService] 已加载 {throwDict.Count} 个武器投掷配置: {string.Join(", ", throwDict.Keys)}");
    }

    public override void OnBegin()
    {
        int id = Owner.CurrentState.Id;
        executed[id] = new HashSet<int>();
        // 状态中断时，把该状态所有未完成的投掷复位（武器挂回手，清理运行时）
        var stale = runtimes.Keys.Where(k => k.Item1 == id).ToList();
        foreach (var k in stale) ReattachAndClear(k);
    }

    public override void OnUpdate()
    {
        if (throwDict == null) return;
        int id = Owner.CurrentState.Id;
        if (!throwDict.TryGetValue(id, out var data)) return;
        if (!executed.TryGetValue(id, out var exe))
            executed[id] = exe = new HashSet<int>();

        float elapsed = Owner.GetStateElapsed(); // 进入状态后的流逝秒，和其他服务统一

        for (int i = 0; i < data.throws.Count; i++)
        {
            var cfg = data.throws[i];
            if (!cfg.enabled) continue;
            if (exe.Contains(i)) continue; // 已触发过，只推进其运行时

            if (elapsed < cfg.triggerSec) continue; // 未到触发时刻

            // 触发：启动投掷小状态机
            StartThrow(id, i, cfg);
            exe.Add(i);
        }

        // 推进所有已触发的投掷（飞出/自转/飞回）
        var active = runtimes.Where(kv => kv.Key.Item1 == id).ToList();
        foreach (var kv in active)
            TickPhase(kv.Key.Item1, kv.Key.Item2, kv.Value);
    }

    public override void OnEnd()
    {
        int id = Owner.CurrentState.Id;
        var stale = runtimes.Keys.Where(k => k.Item1 == id).ToList();
        foreach (var k in stale) ReattachAndClear(k);
    }

    void StartThrow(int stateId, int segIdx, WeaponThrowConfig cfg)
    {
        var weapon = string.IsNullOrEmpty(cfg.weaponPath) ? null : Owner.transform.Find(cfg.weaponPath);
        if (weapon == null)
        {
            Debug.LogWarning($"[WeaponThrowService] 找不到武器节点 '{cfg.weaponPath}'，请检查路径");
            return;
        }

        var rt = new Runtime();
        rt.weapon = weapon;
        rt.hand = weapon.parent;
        rt.handLocalPos = weapon.localPosition;
        rt.handLocalRot = weapon.localRotation;
        rt.handLocalScale = weapon.localScale;

        rt.flyFrom = weapon.position;
        rt.flyTo = Owner.transform.TransformPoint(cfg.hoverOffset); // 悬停点：本地偏移转世界（带角色缩放）

        weapon.SetParent(null, true); // 脱离手部，保持当前世界姿态

        rt.spinTotalDeg = cfg.spinLaps * 360f;
        rt.hitSet.Clear();
        rt.phase = Phase.FlyingOut;
        rt.phaseTimer = 0f;

        // 保存配置引用供推进使用（把 cfg 也塞进 Runtime，避免 TickPhase 再查）
        rt.hoverOffset = cfg.hoverOffset;
        rt.flyOutDuration = cfg.flyOutDuration;
        rt.flyOutCurve = cfg.flyOutCurve;
        rt.spinDuration = cfg.spinDuration;
        rt.spinAxis = cfg.spinAxis;
        rt.flyBackDuration = cfg.flyBackDuration;
        rt.flyBackCurve = cfg.flyBackCurve;
        rt.detectHit = cfg.detectHit;
        rt.hitRadius = cfg.hitRadius;
        rt.damage = cfg.damage;
        rt.hitMask = cfg.hitMask;

        runtimes[(stateId, segIdx)] = rt;
    }

    void TickPhase(int stateId, int segIdx, Runtime rt)
    {
        if (rt.weapon == null) return;

        rt.phaseTimer += Time.deltaTime;

        switch (rt.phase)
        {
            case Phase.FlyingOut:
                {
                    float t = Mathf.Clamp01(rt.phaseTimer / rt.flyOutDuration);
                    rt.weapon.position = Vector3.Lerp(rt.flyFrom, rt.flyTo, Eval(rt.flyOutCurve, t));
                    if (t >= 1f) { rt.phase = Phase.Spinning; rt.phaseTimer = 0f; }
                    break;
                }

            case Phase.Spinning:
                {
                    rt.weapon.Rotate(rt.spinAxis, rt.spinTotalDeg / rt.spinDuration * Time.deltaTime, Space.Self);
                    if (rt.detectHit) ScanHit(rt);
                    if (rt.phaseTimer >= rt.spinDuration)
                    {
                        rt.flyFrom = rt.weapon.position;                              // 飞回起点 = 当前悬停位置
                        rt.flyTo = rt.hand != null ? rt.hand.position : Owner.transform.position; // 飞回终点 = 手部世界位置
                        rt.phase = Phase.FlyingBack;
                        rt.phaseTimer = 0f;
                    }
                    break;
                }

            case Phase.FlyingBack:
                {
                    float t = Mathf.Clamp01(rt.phaseTimer / rt.flyBackDuration);
                    rt.weapon.position = Vector3.Lerp(rt.flyFrom, rt.flyTo, Eval(rt.flyBackCurve, t));
                    if (t >= 1f)
                    {
                        Reattach(rt);
                        runtimes.Remove((stateId, segIdx)); // 整段完成，清理
                    }
                    break;
                }
        }
    }

    void Reattach(Runtime rt)
    {
        if (rt.hand == null) return;
        rt.weapon.SetParent(rt.hand, false);
        rt.weapon.localPosition = rt.handLocalPos;
        rt.weapon.localRotation = rt.handLocalRot;
        rt.weapon.localScale = rt.handLocalScale;
    }

    void ReattachAndClear((int, int) key)
    {
        if (runtimes.TryGetValue(key, out var rt) && rt != null)
            Reattach(rt);
        runtimes.Remove(key);
    }

    static float Eval(AnimationCurve c, float t) => (c != null && c.length > 0) ? c.Evaluate(t) : t;

    void ScanHit(Runtime rt)
    {
        foreach (var c in Physics.OverlapSphere(rt.weapon.position, rt.hitRadius, rt.hitMask))
        {
            if (!rt.hitSet.Add(c)) continue; // 本次投掷已打过，去重

            var attacker = Owner != null ? Owner.GetStats() : null;
            var target = c.GetComponentInParent<Damageable>();
            var defender = target != null ? target.Stats : null;
            Vector3 hitPoint = c.ClosestPointOnBounds(rt.weapon.position);
            Vector3 hitDir = (c.transform.position - rt.weapon.position).normalized;
            var info = DamageCalculator.Calculate(attacker, defender, rt.damage, hitPoint, hitDir,
                Owner != null ? Owner.gameObject : null);

            var dmgable = c.GetComponentInParent<IDamageable>();
            if (dmgable != null) dmgable.TakeDamage(info);

            Debug.Log($"[WeaponThrowService] 命中 {c.name} 伤害={info.Damage}{(info.IsCrit ? " 暴击" : "")}");
        }
    }
}
