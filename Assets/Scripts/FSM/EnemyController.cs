using System.Collections.Generic;
using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 敌人控制器 — 数据驱动状态机（读 AIConfig 的 ai_state + ai_transition 表）。
///
/// 配置来源：AIConfig.xlsx 导出的 List SO。
///   ai_state 表：状态定义（StateId → AnimName）
///   ai_transition 表：状态迁移（From/To/Condition/Param/Order，From=0 全局）
/// 移动方式：AC 里 Moving 状态内置 1D Blend Tree（参数 Speed：0=待机、1=走、2=跑），
///   代码用 Mathf.SmoothDamp 平滑驱动 Speed，起步/停步自然加减速。
/// 待机设计：独立 Idle 状态 = 大脑决策节点；Walking 混合树保留 Speed=0 待机节点 = 视觉减速锚点。
/// 停止方式（玩家同款）：到位只翻 targetSpeed，身体始终跟着衰减的 Speed 滑行减速，
///   Speed≈0 时 Arrive 迁移切 Idle —— 身体和动画同步减速、脚不离地（允许偏离巡逻点一段刹车距离）。
/// 巡逻流程：Moving(blendSpeed=1 走速) 走向 patrolPoints[patrolIndex] → 到位 → 滑行刹车 → Speed≈0 → Arrive 迁移 → Idle
///   → Timer 停留 N 秒（Param[0]）→ 迁移回 Walking → 下一个巡逻点。
/// </summary>
public class EnemyController : MonoBehaviour, IEventReceiver<DamageEvent>, IEventReceiver<DeathEvent>
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
    [SerializeField] private int chaseStateId = 2003;     // 战斗追击（移动目标=玩家）
    [SerializeField] private int attackStateId = 2004;    // 攻击（进入时一次性转正，攻击期间锁定方向）
    [SerializeField] private int hitStateId = 2005;       // 受击（被打进入，播完按 HasTarget 切追击/待机）
    [SerializeField] private int deathStateId = 2006;     // 死亡（全局迁移 Dead 条件进入，终态不再切走）

    [Header("调试：运行时强制切换状态")]
    [SerializeField] private int debugForceStateId;       // 填 StateId，数值一改就强切（不经过迁移表）
    private int lastForceStateId;                         // 记录上次值，只在变化时强切

    [Header("感知")]
    [SerializeField] private EnemyPerception perception;  // 感知组件（自动检测玩家，替代手动拖目标）

    [Header("巡逻（父物体下子物体按顺序 = 巡逻点）")]
    [SerializeField] private Transform patrolRoot;        // 巡逻路径父物体（子物体顺序 = 起点→…→终点）
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
    private Transform chaseTarget;                         // 运行时追击目标（从 perception.CurrentTarget 同步）
    private Transform[] patrolPoints;                      // 运行时缓存：patrolRoot 子物体（Start 收集）
    private int patrolIndex;
    private int patrolDir = 1;                             // 巡逻方向：+1 正向（起点→终点），-1 反向（终点→起点）
    private bool arrived;                                  // 已到位（刹车中），等 Speed≈0 切待机
    private bool isDead;                                   // 死亡标记（DeathEvent 置位，Dead 条件驱动切死亡态）
    private float idleEnterTime;                           // 进入待机的时间（Timer 条件基准）
    private int stateEnterFrame;                           // 进入状态时的帧号（OnAnimEnd 防读上一状态的进度）

    AiStateSO CurrentState => stateData.TryGetValue(currentStateId, out var s) ? s : null;

    void OnEnable()
    {
        EventBus.Subscribe<DamageEvent>(this);
        EventBus.Subscribe<DeathEvent>(this);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<DamageEvent>(this);
        EventBus.Unsubscribe<DeathEvent>(this);
    }

    /// <summary>被打中 → 硬切受击状态（打断当前任何状态），每次命中都重新播受击动画。已死则跳过（致命一击直接走死亡迁移）。</summary>
    public void OnEvent(DamageEvent evt)
    {
        if (evt.Target == null || evt.Target.gameObject != gameObject) return;   // 只响应自己身上的伤害
        if (evt.Target.IsDead) return;   // 已死 → 不播受击，让全局 Dead 迁移接管切死亡态
        Debug.Log($"[受击] {name} 命中，当前状态={currentStateId} → 切受击 {hitStateId}");
        EnterState(hitStateId);
    }

    /// <summary>死亡事件（血量归零首次触发）→ 置死亡标记，下一帧由全局 Dead 迁移切死亡态。</summary>
    public void OnEvent(DeathEvent evt)
    {
        if (evt.Target == null || evt.Target.gameObject != gameObject) return;   // 只响应自己
        isDead = true;
        Debug.Log($"[死亡] {name} 血量归零，置死亡标记（迁移表 Dead 条件接管）");
    }

    void Start()
    {
        if (perception == null) perception = GetComponent<EnemyPerception>();   // 兜底：同物体上挂了感知组件就自动找到，不用手动拖
        BuildPatrolPoints();
        BuildStateData();
        if (stateData.Count == 0)
        {
            Debug.LogWarning($"[Enemy] {name} 没有状态配置（EnemyId={enemyId}），检查 AIConfig 导出");
            return;
        }
        EnterState(stateData.ContainsKey(initialStateId) ? initialStateId : FirstStateId());
    }

    /// <summary>从 patrolRoot 子物体按顺序收集巡逻点（起点→…→终点）</summary>
    void BuildPatrolPoints()
    {
        if (patrolRoot == null) { patrolPoints = new Transform[0]; return; }
        patrolPoints = new Transform[patrolRoot.childCount];
        for (int i = 0; i < patrolRoot.childCount; i++)
            patrolPoints[i] = patrolRoot.GetChild(i);
    }

    void Update()
    {
        DebugForceSwitch();   // 调试：Inspector 强切（不经过迁移表）
        if (CurrentState == null || animator == null) return;
        UpdatePerception();   // 感知同步：chaseTarget 跟随感知结果
        UpdateMovement();
        CheckTransitions();
    }

    /// <summary>把感知结果同步到 chaseTarget（感知到玩家 → 追击目标；丢失 → null）</summary>
    void UpdatePerception()
    {
        chaseTarget = perception != null ? perception.CurrentTarget : null;
    }

    /// <summary>
    /// 调试：Inspector 里改 debugForceStateId → 强切到该状态（绕过迁移表）。
    /// 用法：Play 模式下在 Inspector 填要测试的 StateId，数值一改立即生效；填回 0 恢复由迁移表驱动。
    /// </summary>
    void DebugForceSwitch()
    {
        if (debugForceStateId == lastForceStateId) return;
        lastForceStateId = debugForceStateId;
        if (debugForceStateId > 0)
        {
            Debug.Log($"[Enemy] 调试强切 → StateId={debugForceStateId}");
            EnterState(debugForceStateId);
        }
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
        if (!stateData.TryGetValue(stateId, out var s)) { Debug.LogWarning($"[Enemy] {name} 状态 {stateId} 不在 stateData（表没导出？）"); return; }
        int prevId = currentStateId;
        currentStateId = stateId;
        stateEnterFrame = Time.frameCount;   // OnAnimEnd 基准：进入当帧 CrossFade 还没生效，禁止判动画结束
        Debug.Log($"[Enemy] {name} 进入状态 {stateId}（{s.AnimName}）");

        // 进入攻击 → 一次性转正面对玩家（攻击期间锁定方向，让玩家能走位躲技能）
        if (stateId == attackStateId && chaseTarget != null)
        {
            var dir = chaseTarget.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        // 待机结束 → 走向下一个巡逻点（乒乓折返：走到终点反向、走到起点再反向）
        if (stateId == walkStateId && prevId == idleStateId && patrolPoints != null && patrolPoints.Length > 1)
        {
            int next = patrolIndex + patrolDir;
            if (next >= patrolPoints.Length || next < 0)   // 撞到终点或起点 → 反转方向
            {
                patrolDir = -patrolDir;
                next = patrolIndex + patrolDir;
            }
            patrolIndex = next;
        }

        animator.CrossFade(s.AnimName, crossFadeTime, 0, 0f);   // normalizedTimeOffset=0：强制从头播（否则同状态重进会从当前位置继续，不重播）

        if (stateId == idleStateId)
            idleEnterTime = Time.time;   // Timer 条件基准
        else
            arrived = false;             // 非待机 → 重新开始到位判定
    }

    // ═══════ 移动（按当前状态是否有位移配置决定走/停） ═══════

    /// <summary>移动目标：追逐状态追玩家，否则走巡逻点；没有目标返回 null（原地停）。</summary>
    Transform GetMoveTarget()
    {
        if (currentStateId == chaseStateId && chaseTarget != null)
            return chaseTarget;                                     // 追击 → 追玩家（持续转身跟踪）
        if (currentStateId == attackStateId)
            return null;                                            // 攻击 → 不追踪（进入时已转正，攻击期间锁定方向）
        if (patrolPoints != null && patrolPoints.Length > 0)
            return patrolPoints[patrolIndex];                        // 巡逻 → 走点
        return null;                                                 // 无目标 → 原地停
    }

    void UpdateMovement()
    {
        var motion = GetMotion();
        if (motion == null)
        {
            targetSpeed = 0f;   // 无位移配置（待机/攻击等）→ 原地停（攻击态进入时已转正，这里锁死方向）
            ApplySpeed();
            return;
        }

        var target = GetMoveTarget();
        if (target == null)
        {
            targetSpeed = 0f;   // 没有移动目标 → 原地停
            ApplySpeed();
            return;
        }

        var dir = target.position - transform.position;
        dir.y = 0;
        float dist = dir.magnitude;

        // 到位判定：只翻 targetSpeed（走→停），不冻结身体。
        // 追逐：动态追踪（玩家离远重新追、够近才停）；巡逻：一次性到位（停下后靠迁移切走）。
        if (currentStateId == chaseStateId)
            arrived = dist <= motion.chaseStopDist;
        else if (!arrived && dist <= motion.arriveDist)
            arrived = true;

        targetSpeed = arrived ? 0f : motion.blendSpeed;   // Moving 混合树档位：0=停 1=走 2=跑

        // 身体位移 = 全速基准 × 当前 Speed —— 玩家同款：身体跟着平滑衰减的 Speed 滑行减速，
        // 脚不离地。允许偏离巡逻点一段刹车距离（不需要精确到位）。
        float speed = motion.moveSpeed * animator.GetFloat(SpeedHash);
        if (speed > 0.0001f)
        {
            transform.position += dir.normalized * speed * Time.deltaTime;

            // 转身：只在移动时转向目标，停下就锁定朝向（玩家围着转圈时不跟着原地打转）
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
    /// 迁移条件（对应 ai_transition 表 Condition 列，多个用分号分隔表示 AND）：
    ///   Timer = 停留 Param[0] 秒（在待机状态计时）
    ///   Arrive = 到位且停稳（等 Speed 归零，避免走路姿势直接跳待机）
    ///   OnAnimEnd = 动画播完
    ///   HasTarget = 已锁定目标（追击/攻击玩家）
    ///   NoTarget = 无锁定目标（走巡逻）
    ///   Dead = 已死亡（DeathEvent 置位，配合 From=0 全局迁移：任意状态血归零立即切死亡态）
    /// 例："Timer;HasTarget" = 停留够 5 秒 且 有玩家目标才追击。
    /// </summary>
    bool CheckCondition(AiTransitionSO t)
    {
        if (string.IsNullOrEmpty(t.Condition)) return false;
        foreach (var cond in t.Condition.Split(';'))
        {
            if (!CheckSingleCondition(cond.Trim(), t)) return false;
        }
        return true;
    }

    /// <summary>单个迁移条件判断（Condition 拆分后的每一项）</summary>
    bool CheckSingleCondition(string cond, AiTransitionSO t)
    {
        switch (cond)
        {
            case "Timer":
                float wait = t.Param != null && t.Param.Length > 0 ? t.Param[0] : 2f;
                return Time.time - idleEnterTime >= wait;

            case "Arrive":
                return arrived && animator.GetFloat(SpeedHash) <= SpeedZeroEpsilon;

            case "OnAnimEnd":
                if (animator.IsInTransition(0)) return false;
                // 刚进入的前几帧 CrossFade 未生效，GetCurrentAnimatorStateInfo 读到的还是上一状态
                // （循环状态 normalizedTime 恒 ≥1），会把 OnAnimEnd 当帧误判成真，受击/攻击动画被跳过
                if (Time.frameCount - stateEnterFrame < 2) return false;
                return animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;

            case "HasTarget":
                return chaseTarget != null;

            case "NoTarget":
                return chaseTarget == null;

            case "Dead":
                // 已死 → 切死亡态；已在死亡态 → 排除（防止全局迁移每帧重复重播死亡动画）
                return isDead && currentStateId != deathStateId;

            default:
                return false;
        }
    }
}
