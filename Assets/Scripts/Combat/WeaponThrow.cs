using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器投掷（临时触发脚本，验证手感用）：
/// 按投掷键把武器从手里扔出去 → 飞到前方某位置悬停自转 N 圈 → 飞回手中重新挂回。
/// 运动走一条 Idle→飞出→自转→飞回 的简单状态机；命中检测可选（OverlapSphere 粗测 + 伤害结算）。
///
/// 后续接入状态机时：把 StartThrow / 各阶段时序换成 OnSkillTriggered + 状态流逝时间驱动即可，
/// 这套飞出/自转/飞回的插值逻辑直接搬进一个新的 FSMServiceBase 服务。
///
/// 挂到角色上，weaponPath 填武器节点路径（Tools→复制Transform路径，和 WeaponVisibleService 一致）。
/// </summary>
public class WeaponThrow : MonoBehaviour
{
    [Header("武器")]
    [Tooltip("武器节点路径，同 WeaponVisibleService（Tools→复制Transform路径）")]
    [SerializeField] private string weaponPath;
    [Tooltip("触发投掷的键（临时测试用，后续接入 FSM 走技能）")]
    [SerializeField] private KeyCode throwKey = KeyCode.G;

    [Header("扔出去")]
    [Tooltip("武器悬停点相对角色根节点的本地偏移（X右 / Y上 / Z前，单位米）。编辑模式选中角色后可拖 Scene 手柄直接摆")]
    [SerializeField] private Vector3 hoverOffset = new Vector3(0f, 1.2f, 6f);
    [Tooltip("飞出耗时（秒）")]
    [SerializeField] private float flyOutDuration = 0.25f;
    [Tooltip("飞出速度曲线：横轴 0~1 进度，纵轴 0~1 位移比例")]
    [SerializeField] private AnimationCurve flyOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("自转")]
    [Tooltip("自转圈数")]
    [SerializeField] private int spinLaps = 3;
    [Tooltip("自转总耗时（秒），越小转越快")]
    [SerializeField] private float spinDuration = 1.2f;
    [Tooltip("自转轴（本地空间，相对武器自身朝向）。默认绕本地 Y 轴转；想竖着滚就改 X/Z 轴")]
    [SerializeField] private Vector3 spinAxis = Vector3.up;

    [Header("收回来")]
    [Tooltip("飞回耗时（秒）")]
    [SerializeField] private float flyBackDuration = 0.25f;
    [Tooltip("飞回速度曲线")]
    [SerializeField] private AnimationCurve flyBackCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("命中（可选）")]
    [Tooltip("开启后自转期每帧用球扫一圈结算伤害")]
    [SerializeField] private bool detectHit = true;
    [Tooltip("命中半径（米）")]
    [SerializeField] private float hitRadius = 1.2f;
    [Tooltip("伤害倍率：100 = 100% 攻击力")]
    [SerializeField] private int damage = 100;
    [SerializeField] private LayerMask hitMask = -1;

    [Header("运行时状态（观察用）")]
    [SerializeField] private string state = "待机";

    [SerializeField] private CharacterState characterState;   // 攻击方数值来源（可手动拖，同物体自动找）

    private enum Phase { Idle, FlyingOut, Spinning, FlyingBack }

    private Phase phase = Phase.Idle;
    private Transform weapon;          // 武器节点
    private Transform hand;            // 原挂点（手/父级），回来时挂回
    private Vector3 handLocalPos;
    private Quaternion handLocalRot;
    private Vector3 handLocalScale;   // 关键：SetParent(null,true) 会改 localScale，回来必须恢复
    private Vector3 flyFrom;           // 当前段起点（世界坐标）
    private Vector3 flyTo;             // 当前段终点（世界坐标）
    private float phaseTimer;          // 当前段已过时间
    private float spinTotalDeg;        // 自转总角度（圈数 × 360）
    private readonly HashSet<Collider> hitSet = new();   // 单次投掷内命中去重

    void Awake()
    {
        if (characterState == null)
            characterState = GetComponent<CharacterState>();
        ResolveWeapon();
    }

    void ResolveWeapon()
    {
        weapon = string.IsNullOrEmpty(weaponPath) ? null : transform.Find(weaponPath);
        if (weapon == null)
            Debug.LogWarning($"[WeaponThrow] 找不到武器节点 '{weaponPath}'，请检查路径（Tools→复制Transform路径）");
    }

    void Update()
    {
        if (weapon == null) return;   // 找不到武器就不跑

        if (Input.GetKeyDown(throwKey) && phase == Phase.Idle)
            StartThrow();

        TickPhase();
    }

    void StartThrow()
    {
        hand = weapon.parent;
        handLocalPos = weapon.localPosition;
        handLocalRot = weapon.localRotation;
        handLocalScale = weapon.localScale;

        flyFrom = weapon.position;
        // 悬停点：角色根本地偏移转世界（Z=前方距离，Y=高度，X=左右），自动带角色缩放
        flyTo = transform.TransformPoint(hoverOffset);

        weapon.SetParent(null, true);   // 脱离手部，保持当前世界姿态飞到世界空间

        spinTotalDeg = spinLaps * 360f;
        hitSet.Clear();
        phase = Phase.FlyingOut;
        phaseTimer = 0f;
        state = "飞出";
    }

    void TickPhase()
    {
        phaseTimer += Time.deltaTime;

        switch (phase)
        {
            case Phase.FlyingOut:
                {
                    float t = Mathf.Clamp01(phaseTimer / flyOutDuration);
                    weapon.position = Vector3.Lerp(flyFrom, flyTo, Eval(flyOutCurve, t));
                    if (t >= 1f) { phase = Phase.Spinning; phaseTimer = 0f; state = "自转"; }
                    break;
                }

            case Phase.Spinning:
                {
                    weapon.Rotate(spinAxis, spinTotalDeg / spinDuration * Time.deltaTime, Space.Self);
                    if (detectHit) ScanHit();
                    if (phaseTimer >= spinDuration)
                    {
                        flyFrom = weapon.position;                               // 飞回起点 = 当前悬停位置
                        flyTo = hand != null ? hand.position : transform.position; // 飞回终点 = 手部世界位置
                        phase = Phase.FlyingBack;
                        phaseTimer = 0f;
                        state = "飞回";
                    }
                    break;
                }

            case Phase.FlyingBack:
                {
                    float t = Mathf.Clamp01(phaseTimer / flyBackDuration);
                    weapon.position = Vector3.Lerp(flyFrom, flyTo, Eval(flyBackCurve, t));
                    if (t >= 1f)
                    {
                        ReattachWeapon();
                        phase = Phase.Idle;
                        phaseTimer = 0f;
                        state = "待机";
                    }
                    break;
                }
        }
    }

    /// <summary>曲线取值，曲线为空/被清空时退化成线性 t</summary>
    static float Eval(AnimationCurve c, float t) => (c != null && c.length > 0) ? c.Evaluate(t) : t;

    void ReattachWeapon()
    {
        if (hand == null) return;
        weapon.SetParent(hand, false);
        weapon.localPosition = handLocalPos;
        weapon.localRotation = handLocalRot;
        weapon.localScale = handLocalScale;   // 恢复原始缩放，抵消 detach 时 SetParent(null,true) 的 localScale 重算
    }

    void ScanHit()
    {
        foreach (var c in Physics.OverlapSphere(weapon.position, hitRadius, hitMask))
        {
            if (!hitSet.Add(c)) continue;   // 本次投掷已打过，去重

            var attacker = characterState != null ? characterState.GetStats() : null;
            var target = c.GetComponentInParent<Damageable>();
            var defender = target != null ? target.Stats : null;
            Vector3 hitPoint = c.ClosestPointOnBounds(weapon.position);
            Vector3 hitDir = (c.transform.position - weapon.position).normalized;
            var info = DamageCalculator.Calculate(attacker, defender, damage, hitPoint, hitDir, gameObject);

            var dmgable = c.GetComponentInParent<IDamageable>();
            if (dmgable != null) dmgable.TakeDamage(info);

            Debug.Log($"[WeaponThrow] 命中 {c.name} 伤害={info.Damage}{(info.IsCrit ? " 暴击" : "")}");
        }
    }
}
