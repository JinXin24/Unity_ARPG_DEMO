using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人控制器 — 数据驱动状态机（读 AIConfig 的 ai_state + ai_transition 表）。
///
/// 配置来源：AIConfig.xlsx 导出的 List SO。
///   ai_state 表：状态定义（StateId → AnimName）
///   ai_transition 表：状态迁移（From/To/Condition/Param/Order，From=0 全局）
/// 移动方式：AC 里 Walking 状态内置 1D Blend Tree（参数 Speed：0=待机、1=走），
///   代码用 Mathf.SmoothDamp 平滑驱动 Speed，起步/停步自然加减速。
/// 待机设计：独立 Idle 状态 = 大脑决策节点；Walking 混合树保留 Speed=0 待机节点 = 视觉减速锚点。
/// 停止方式（玩家同款）：到位只翻 targetSpeed，身体始终跟着衰减的 Speed 滑行减速，
///   Speed≈0 时 Arrive 迁移切 Idle —— 身体和动画同步减速、脚不离地（允许偏离巡逻点一段刹车距离）。
/// 巡逻流程：Walking 走向 patrolPoints[patrolIndex] → 到位 → 滑行刹车 → Speed≈0 → Arrive 迁移 → Idle
///   → Timer 停留 N 秒（Param[0]）→ 迁移回 Walking → 下一个巡逻点。
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Animator animator;                    // 敌人 Animator（挂子物体）
    [SerializeField] private AiStateSOList stateList;              // ai_state 表 List 容器
    [SerializeField] private AiTransitionSOList transitionList;    // ai_transition 表 List 容器

    [Header("配置")]
    [SerializeField] private int enemyId = 2001;          // 对应表的 EnemyId
    [SerializeField] private int initialStateId = 2002;   // 出生时的状态
    [SerializeField] private float crossFadeTime = 0.016f; // 进入状态过渡（1 逻辑帧，同玩家）

    [Header("状态ID（对应 ai_state 表 StateId）")]
    [SerializeField] private int idleStateId = 2001;      // 待机（Timer 基准 / 巡逻循环锚点）
    [SerializeField] private int walkStateId = 2002;      // 巡逻/走路

    [Header("巡逻（占位，待寻路）")]
    [SerializeField] private Transform[] patrolPoints;    // 巡逻点
    [SerializeField] private AIMotionSO motionSO;         // 各状态位移配置（取 moveSpeed 作全速基准）
    [SerializeField] private float turnSpeed = 360f;      // 转身速度(度/秒)

    // Blend Tree Speed 驱动（参照玩家 CharacterState：SmoothDamp 平滑加速/减速）
    private float speedVelocity;                           // SmoothDamp 缓存，跨帧保留
    private float targetSpeed;                             // 目标 Speed：1 走 / 0 停
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private const float AccelTime = 0.2f;                  // 起步加速时间
    private const float DecelTime = 0.3f;                  // 停步减速时间
    private const float SpeedZeroEpsilon = 0.02f;          // 判定"停稳"的 Speed 阈值

    // 运行时数据
    private readonly Dictionary<int, AiStateSO> stateData = new();          // StateId → 状态配置
    private readonly Dictionary<int, AIMotionData> motionDict = new();     // StateId → 位移配置
    private readonly Dictionary<int, List<AiTransitionSO>> transitionMap = new(); // From → 按 Order 排序的迁移
    private int currentStateId;
    private int patrolIndex;
    private bool arrived;                                  // 已到位（刹车中），等 Speed≈0 切待机
    private float idleEnterTime;                           // 进入待机的时间（Timer 条件基准）

    AiStateSO CurrentState => stateData.TryGetValue(currentStateId, out var s) ? s : null;

    void Start()
    {
        BuildStateData();
        if (stateData.Count == 0)
        {
            Debug.LogWarning($"[Enemy] {name} 没有状态配置（EnemyId={enemyId}），检查 AIConfig 导出");
            return;
        }
        EnterState(stateData.ContainsKey(initialStateId) ? initialStateId : FirstStateId());
    }

    void Update()
    {
        if (CurrentState == null || animator == null) return;
        UpdateMovement();
        CheckTransitions();
    }

    /// <summary>按 EnemyId 构建 state / motion / transition 数据</summary>
    void BuildStateData()
    {
        stateData.Clear();
        if (stateList != null)
            foreach (var s in stateList.list)
                if (s.EnemyId == enemyId && s.StateId > 0)
                    stateData[s.StateId] = s;

        motionDict.Clear();
        if (motionSO != null)
            foreach (var m in motionSO.motions)
                if (m.EnemyId == enemyId)
                    motionDict[m.StateId] = m;

        // 迁移表：按 Order 升序排序后分组；From=0 全局迁移挂到所有状态
        transitionMap.Clear();
        var allTransitions = new List<AiTransitionSO>();
        if (transitionList != null)
            foreach (var t in transitionList.list)
                if (t.EnemyId == enemyId)
                    allTransitions.Add(t);
        allTransitions.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var t in allTransitions)
        {
            if (t.From == 0) // 全局迁移：任意状态可触发
            {
                foreach (var stateId in stateData.Keys)
                    AddTransition(stateId, t);
            }
            else
            {
                AddTransition(t.From, t);
            }
        }

        Debug.Log($"[Enemy] {name} 加载 {stateData.Count} 状态, {motionDict.Count} 位移配置, {allTransitions.Count} 条迁移");
    }

    void AddTransition(int from, AiTransitionSO t)
    {
        if (!transitionMap.TryGetValue(from, out var list))
            transitionMap[from] = list = new List<AiTransitionSO>();
        list.Add(t);
    }

    int FirstStateId()
    {
        foreach (var kv in stateData) return kv.Key;
        return 0;
    }

    /// <summary>取当前状态的位移配置；没有就回退空占位（静止）</summary>
    AIMotionData GetMotion() => motionDict.TryGetValue(currentStateId, out var m) ? m : null;

    /// <summary>进入状态：CrossFade + 各状态专属进入逻辑</summary>
    void EnterState(int stateId)
    {
        if (!stateData.TryGetValue(stateId, out var s)) return;
        int prevId = currentStateId;
        currentStateId = stateId;

        // 待机结束 → 走向下一个巡逻点
        if (stateId == walkStateId && prevId == idleStateId && patrolPoints != null && patrolPoints.Length > 0)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;

        animator.CrossFade(s.AnimName, crossFadeTime);

        if (stateId == idleStateId)
            idleEnterTime = Time.time;   // Timer 条件基准
        else
            arrived = false;             // 非待机 → 重新开始到位判定
    }

    // ═══════ 移动（按当前状态是否有位移配置决定走/停） ═══════

    void UpdateMovement()
    {
        var motion = GetMotion();
        if (motion == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            targetSpeed = 0f;   // 无位移配置（待机/攻击等）或没巡逻点 → 原地停
            ApplySpeed();
            return;
        }

        var target = patrolPoints[patrolIndex];
        var dir = target.position - transform.position;
        dir.y = 0;
        float dist = dir.magnitude;

        // 到位判定：只翻 targetSpeed（走→停），不冻结身体
        if (!arrived && dist <= motion.arriveDist)
            arrived = true;

        targetSpeed = arrived ? 0f : 1f;

        // 身体位移 = 全速基准 × 当前 Speed —— 玩家同款：身体跟着平滑衰减的 Speed 滑行减速，
        // 脚不离地。允许偏离巡逻点一段刹车距离（不需要精确到位）。
        float speed = motion.moveSpeed * animator.GetFloat(SpeedHash);
        if (speed > 0.0001f)
        {
            transform.position += dir.normalized * speed * Time.deltaTime;

            // 转身：匀速向目标朝向旋转（RotateTowards 固定角速度）
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }

        ApplySpeed();
    }

    /// <summary>Blend Tree Speed 平滑驱动：起步 0.2s 加速、停步 0.3s 减速，玩家同款参数</summary>
    void ApplySpeed()
    {
        float currentSpeed = animator.GetFloat(SpeedHash);
        float smoothTime = targetSpeed < currentSpeed ? DecelTime : AccelTime;
        animator.SetFloat(SpeedHash, Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, smoothTime));
    }

    // ═══════ 数据驱动状态迁移 ═══════

    void CheckTransitions()
    {
        if (!transitionMap.TryGetValue(currentStateId, out var list)) return;
        foreach (var t in list)   // 已按 Order 升序，同一帧只执行优先级最高的迁移
        {
            if (CheckCondition(t))
            {
                EnterState(t.To);
                return;
            }
        }
    }

    /// <summary>
    /// 迁移条件（对应 ai_transition 表 Condition 列）：
    ///   Timer = 停留 Param[0] 秒（在待机状态计时）
    ///   Arrive = 到位且停稳（等 Speed 归零，避免走路姿势直接跳待机）
    ///   OnAnimEnd = 动画播完
    /// </summary>
    bool CheckCondition(AiTransitionSO t)
    {
        switch (t.Condition)
        {
            case "Timer":
                float wait = t.Param != null && t.Param.Length > 0 ? t.Param[0] : 2f;
                return Time.time - idleEnterTime >= wait;

            case "Arrive":
                return arrived && animator.GetFloat(SpeedHash) <= SpeedZeroEpsilon;

            case "OnAnimEnd":
                if (animator.IsInTransition(0)) return false;
                return animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;

            default:
                return false;
        }
    }
}
