using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JinXinFramework.Event;

public enum StateEventType { Begin, Update, End, OnAnmEnd }

public class PlayerState
{
    public int Id;
    public StateSO Config;
    public float BeginTime;
    public void SetBeginTime() => BeginTime = Time.time;
}

public class CharacterState : MonoBehaviour
{
    [SerializeField] protected Animator animator;
    [SerializeField] private StateSOList stateConfigList;
    [SerializeField] private StateMotionSO motionSO;
    [SerializeField] private StateEffectSO effectSO;
    [SerializeField] private StateWeaponSO weaponSO;
    [SerializeField] private StateWeaponThrowSO weaponThrowSO;
    [SerializeField] private StateHitSO hitSO;
    [SerializeField] private AnimTransitionSO animTransitionSO;   // 过渡配置（站稳区间 + 过渡对齐 + 过渡时长）
    [SerializeField] private int characterId = 1001;   // 对应 CharacterManager 角色ID，攻击结算取攻击方数值
    [SerializeField] private MoveConfigSO moveConfig;
    [SerializeField] protected CameraStateSO cameraSO;
    [SerializeField] private LockOnController lockOnController;   // 锁敌组件（同物体自动找，可拖可自动）

    // 调试：Scene 视图预览命中扇形/球形用的状态ID
    [Header("调试")]
    [SerializeField] private int gizmoPreviewStateId = 10021;
    [SerializeField] private int weaponThrowPreviewStateId = 20021;   // Scene 视图预览武器投掷悬停点用的状态ID

    public PlayerState CurrentState { get; private set; }
    private Dictionary<int, PlayerState> stateData = new();
    protected CharacterController characterController;

    // 空中/地面状态判断 — 供其他逻辑读取
    [Header("状态判断")]
    [SerializeField, Tooltip("角色当前是否站在地面上（只读，每帧更新）")]
    private bool grounded;
    /// <summary>角色是否站在地面上（由 CharacterController 判定）</summary>
    public bool IsGrounded => grounded;
    /// <summary>角色是否在空中（取反）</summary>
    public bool IsInAir => !grounded;

    // 跳跃惯性 — 进入空中前记录的水平速度，空中时保持（方案B）
    private Vector3 jumpInertia;   // 进入空中前的水平速度

    // Blend Tree — 1D, Speed 参数 (0=待机, 0.5=走, 1=跑)
    private float speedVelocity;
    private bool runMode;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // 旋转 — 参照 Demo_3D_RPG_ DORotate，面向输入方向（相对相机）
    private float targetRotation;
    private float rotationVelocity;
    private const float RotationSmoothTime = 0.025f;

    // 锁定攻击转向 — 攻击起手朝锁定敌人快速转身，转到位自动清除
    private Transform faceTarget;
    private const float FaceTurnSmoothTime = 0.05f;

    // 位移 — 参照 Demo_3D_RPG_ PhysicsService
    private Dictionary<int, StateMotionData> motionDict;
    private Dictionary<int, HashSet<int>> physicsExecuted = new(); // stateId → executed config indices
    private PhysicsConfig activePhysics;
    private Vector3 activePhysicsVelocity;
    private float activePhysicsTriggerSec;
    private float activePhysicsTimeSec;
    private bool animEndFired;

    // 统一重力缩放 — 默认 1 用 Physics.gravity(-9.81)，调小则下落变轻
    [SerializeField, Tooltip("重力缩放系数，1=物理默认(-9.81)，调小则下落更慢")]
    private float gravityScale = 1f;

    // 特效
    private Dictionary<int, StateEffectData> effectDict;
    private Dictionary<int, HashSet<int>> effectSpawned = new();
    private Dictionary<int, List<GameObject>> activeEffects = new();

    // 服务层
    private readonly List<FSMServiceBase> services = new();
    private WeaponVisibleService weaponService;
    private HitDetectorService hitService;
    private WeaponThrowService weaponThrowService;

    // 相机镜头
    private Dictionary<int, CameraStateData> cameraDict;
    private Dictionary<int, int> cameraKeyframeTrack = new(); // stateId → 已触发的 keyframe 索引

    void Start()
    {
        if (characterController == null)
            characterController = GetComponentInChildren<CharacterController>();
        if (characterController == null)
            Debug.LogWarning($"[CharacterState] {name} 没有 CharacterController，位移不会生效");

        if (lockOnController == null)
            lockOnController = GetComponent<LockOnController>();   // 同物体上挂了锁敌组件就自动找到，不用手动拖

        // 构建位移配置字典（StateId → StateMotionData）
        if (motionSO != null)
        {
            motionDict = motionSO.motions.Where(m => m.physicsConfigs.Count > 0)
                .ToDictionary(m => m.StateId);
            Debug.Log($"[CharacterState] 已加载 {motionDict.Count} 个位移配置: {string.Join(", ", motionDict.Keys)}");
        }
        else
        {
            Debug.LogWarning($"[CharacterState] {name} 未拖入 StateMotionSO");
        }

        // 构建特效配置字典
        if (effectSO != null)
        {
            effectDict = effectSO.states.Where(s => s.effects.Count > 0)
                .ToDictionary(s => s.StateId);
            Debug.Log($"[CharacterState] 已加载 {effectDict.Count} 个特效配置: {string.Join(", ", effectDict.Keys)}");
        }

        // 服务层：武器显隐服务（配置方式和原来一样，逻辑挪到独立服务）
        weaponService = new WeaponVisibleService(weaponSO);
        weaponService.SetOwner(this);
        weaponService.Init();
        services.Add(weaponService);

        // 服务层：命中检测服务（当前只支持球形检测，段内独立去重结算）
        hitService = new HitDetectorService(hitSO);
        hitService.SetOwner(this);
        hitService.Init();
        services.Add(hitService);

        // 服务层：武器投掷服务（triggerSec 触发，飞出→自转→飞回，命中可选）
        weaponThrowService = new WeaponThrowService(weaponThrowSO);
        weaponThrowService.SetOwner(this);
        weaponThrowService.Init();
        services.Add(weaponThrowService);

        // 构建相机镜头字典
        if (cameraSO != null)
        {
            cameraDict = cameraSO.states.Where(s => s.timeline.Count > 0)
                .ToDictionary(s => s.StateId);
            Debug.Log($"[CharacterState] 已加载 {cameraDict.Count} 个镜头配置: {string.Join(", ", cameraDict.Keys)}");
        }

        if (stateConfigList == null || stateConfigList.list.Count == 0) return;
        var cfgs = stateConfigList.list;
        foreach (var cfg in cfgs)
        {
            var ps = new PlayerState { Id = cfg.StateId, Config = cfg };
            stateData[cfg.StateId] = ps;

            // 注册攻击检测：有 OnAtk 配置时，每帧检测攻击输入
            if (cfg.OnAtk != null && cfg.OnAtk.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnAtk);

            // 注册移动检测：有 OnMove 配置时，每帧检测移动输入
            if (cfg.OnMove != null && cfg.OnMove.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnMove);

            // 注册技能检测：有 OnSkill 配置时，每帧检测技能输入
            if (cfg.OnSkill != null && cfg.OnSkill.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnSkillCheck);

            // 注册冲刺检测：有 OnSprint 配置时，每帧检测左 Shift
            if (cfg.OnSprint != null && cfg.OnSprint.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnSprintCheck);

            // 注册跳跃检测：有 OnJump 配置时，每帧检测跳键
            if (cfg.OnJump != null && cfg.OnJump.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnJumpCheck);

            // 注册空中下落检测：有 OnFalling 配置时，空中在该窗口切到对应状态
            if (cfg.OnFalling != null && cfg.OnFalling.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnFallingCheck);

            // 注册落地检测：有 OnLand 配置时，地面在该窗口切到对应状态
            if (cfg.OnLand != null && cfg.OnLand.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnLandCheck);

            // 注册强化技能检测：有 OnEnhanceSkill 配置时，每帧检测
            if (cfg.OnEnhanceSkill != null && cfg.OnEnhanceSkill.Length >= 2)
                AddListener(cfg.StateId, StateEventType.Update, OnEnhanceSkillCheck);

            // 注册位移：SO 里有该状态的位移配置时
            if (motionDict != null && motionDict.ContainsKey(cfg.StateId))
            {
                AddListener(cfg.StateId, StateEventType.Begin, OnPhysicsBegin);
                AddListener(cfg.StateId, StateEventType.Update, OnPhysicsUpdate);
                AddListener(cfg.StateId, StateEventType.End, OnPhysicsEnd);
            }

            // 注册特效：SO 里有该状态的特效配置时
            if (effectDict != null && effectDict.ContainsKey(cfg.StateId))
            {
                AddListener(cfg.StateId, StateEventType.Begin, OnEffectBegin);
                AddListener(cfg.StateId, StateEventType.Update, OnEffectUpdate);
            }

            // 注册相机镜头：SO 里有该状态的镜头配置时
            if (cameraDict != null && cameraDict.ContainsKey(cfg.StateId))
            {
                AddListener(cfg.StateId, StateEventType.Begin, OnCameraBegin);
                AddListener(cfg.StateId, StateEventType.Update, OnCameraUpdate);
                AddListener(cfg.StateId, StateEventType.End, OnCameraEnd);
            }
        }
        CurrentState = stateData[cfgs[0].StateId];
        CurrentState.SetBeginTime();
    }

    protected virtual void Update()
    {
        if (CurrentState == null || animator == null) return;

        // Shift 切换 走/跑 模式
        if (InputSystemController.Instance.GetRunModeToggled())
            runMode = !runMode;

        // 1D Blend Tree: 输入 Magnitude → Speed (走:0~0.5, 跑:0~1)
        float rawSpeed = InputSystemController.Instance.GetMoveInput().magnitude;
        float maxSpeed = runMode ? 1f : 0.5f;
        float targetSpeed = Mathf.Min(rawSpeed, maxSpeed);
        float currentSpeed = animator.GetFloat(SpeedHash);

        // 加速统一 0.2s，减速分走/跑：走 0.3s，跑 0.5s
        float accelTime = 0.2f;
        float decelTime = runMode ? 0.5f : 0.3f;
        float smoothTime = targetSpeed < currentSpeed ? decelTime : accelTime;

        animator.SetFloat(SpeedHash, Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, smoothTime));

        // 旋转 — 参照 Demo_3D_RPG_ DORotate：面向输入方向（相对相机）
        DORotate();
        UpdateLockFace();   // 锁定攻击转向（优先级高于走路转身，覆盖上一行结果）

        // 移动：Blend Tree Speed → 世界位移速度（按状态区分）
        // 地面：正常移动并记录实际水平速度（供跳跃惯性）；空中：保持进入空中前的水平惯性
        if (characterController != null)
        {
            if (IsInAir)
            {
                // 空中：保持跳跃前水平惯性（跳跃位移配置多为纯Y轴，横向不冲突）
                Vector3 inertia = new Vector3(jumpInertia.x, 0, jumpInertia.z);
                if (inertia.sqrMagnitude > 0.0001f)
                    characterController.Move(inertia * Time.deltaTime);
            }
            else
            {
                // 地面：正常移动（若配置了），并记录实际水平速度供空中保持惯性
                bool hasMoveInput = InputSystemController.Instance.GetMoveInput().magnitude > 0.01f;
                if (hasMoveInput && moveConfig != null && currentSpeed > 0.01f)
                {
                    float worldSpeed = moveConfig.GetMoveSpeed(CurrentState.Id, currentSpeed);
                    characterController.Move(transform.forward * worldSpeed * Time.deltaTime);
                    // 只有真正产生水平移动时才更新惯性速度；否则保留旧值（避免 Move(0) 把 velocity 清零而覆盖 jumpInertia）
                    if (worldSpeed > 0.01f)
                        jumpInertia = new Vector3(characterController.velocity.x, 0, characterController.velocity.z);
                }
                else if (!hasMoveInput)
                {
                    // 没按移动键 → 静止，清空惯性（防止静止起跳还带着旧惯性）
                    jumpInertia = Vector3.zero;
                }
                // 有移动输入但 worldSpeed=0（如起跳状态 10051）→ 保留 jumpInertia（起跳惯性意图仍在）
            }
        }


        // 统一重力：仅当当前没有位移配置接管时，才叠加重力下落（地面被碰撞挡住，空中自然下落）
        ApplyGravity();

        // 每帧更新地面/空中状态（放在 Move 之后，isGrounded 反映最新碰撞结果）
        grounded = characterController != null && characterController.isGrounded;

        // 动画播完检测（用原始 normalizedTime，不用 %1f 的版本）
        if (!animEndFired && !animator.IsInTransition(0))
        {
            float rawTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (rawTime >= 1f)
            {
                animEndFired = true;
                int next = CurrentState.Config.OnAnimEnd;
                if (next > 0)
                {
                    DOStateEvent(CurrentState.Id, StateEventType.OnAnmEnd);
                    ToNext(next);
                }
            }
        }

        DOStateEvent(CurrentState.Id, StateEventType.Update);
        for (int i = 0; i < services.Count; i++) services[i].OnUpdate();
    }

    // ═══════ 攻击检测（参照 Demo_3D_RPG_ OnAtk） ═══════

    /// <summary>窗口内检查：归一化时间落在 [start, end] 内才算命中（前摇不命中、命中/后摇区间可切）</summary>
    bool CheckWindow(float start, float end)
    {
        float t = GetNormalizedTime();
        return t >= start && t <= end;
    }

    /// <summary>单窗口配置检查：config = [窗口始, 窗口末, 目标StateId]</summary>
    bool CheckConfig(float[] config)
    {
        if (config == null || config.Length < 2) return false;
        return CheckWindow(config[0], config[1]);
    }

    public float GetNormalizedTime()
    {
        if (animator == null) return 0f;
        // CrossFade 过渡期间，当前状态还是旧动画，取下一个状态的时间
        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            return next.normalizedTime % 1f;
        }
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.normalizedTime % 1f;
    }

    /// <summary>当前动画片段长度（秒），CrossFade 期间取下一段。供各服务换算秒→归一化时间。</summary>
    public float GetClipLength()
    {
        if (animator == null) return 1f;
        var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        float len = info.length;
        return len <= 0.001f ? 1f : len;
    }

    /// <summary>
    /// 进入当前状态后的真实流逝秒（相对动画开头）。窗口/触发判定统一用它，别用 GetNormalizedTime：
    /// 进入状态的当帧 CrossFade 未生效，normalizedTime 读到的是上一状态的进度（旧状态长 clip 时值很大），
    /// 会把 triggerSec 窗口一次性打穿。BeginTime 在 ToNext 里 SetBeginTime() 刷新。
    /// </summary>
    public float GetStateElapsed()
    {
        return CurrentState != null ? Time.time - CurrentState.BeginTime : 0f;
    }

    // ═══════ 脚相位（站稳区间标定 + 过渡对齐） ═══════

    /// <summary>判断当前动画处于哪种站稳（左脚/右脚/双脚）。依赖 animTransitionSO 的区间标定。</summary>
    public FootStance GetCurrentStance()
    {
        if (animTransitionSO == null || CurrentState == null) return FootStance.None;
        var data = animTransitionSO.GetStance(CurrentState.Id);
        if (data == null) return FootStance.None;

        float norm = GetNormalizedTime();
        if (IsInRange(norm, data.leftStart, data.leftEnd)) return FootStance.LeftFoot;
        if (IsInRange(norm, data.rightStart, data.rightEnd)) return FootStance.RightFoot;
        if (IsInRange(norm, data.bothStart, data.bothEnd)) return FootStance.BothFeet;
        return FootStance.None;
    }

    /// <summary>
    /// 计算从 fromId 切到 toId 时，目标动画的切入点（归一化时间）。
    /// 规则：查过渡对 → 确定对齐到哪种站稳 → 返回目标动画该站稳区间的中点。
    /// 查不到过渡对 → 返回 0（从头切）。
    /// </summary>
    float GetEnterNorm(int fromId, int toId)
    {
        if (animTransitionSO == null) return 0f;
        var trans = animTransitionSO.GetTransition(fromId, toId);
        var toData = animTransitionSO.GetStance(toId);
        if (toData == null) return 0f;

        if (trans != null)
        {
            switch (trans.alignStance)
            {
                case FootStance.LeftFoot: return (toData.leftStart + toData.leftEnd) * 0.5f;
                case FootStance.RightFoot: return (toData.rightStart + toData.rightEnd) * 0.5f;
                case FootStance.BothFeet: return (toData.bothStart + toData.bothEnd) * 0.5f;
            }
        }
        return 0f;
    }

    /// <summary>从 fromId 切到 toId 的过渡时长（秒）。查不到用默认 0.12。</summary>
    float GetCrossFadeDur(int fromId, int toId)
    {
        if (animTransitionSO == null) return 0.12f;
        var trans = animTransitionSO.GetTransition(fromId, toId);
        return trans != null ? trans.crossFadeDur : 0.12f;
    }

    /// <summary>归一化时间是否落在区间内（处理跨 0 边界的循环动画）</summary>
    static bool IsInRange(float v, float a, float b)
    {
        if (a <= b) return v >= a && v <= b;
        return v >= a || v <= b;   // 跨 0：如 [0.9, 0.1]
    }

    /// <summary>攻击方数值快照（攻击结算用），CharacterManager 没挂则返回 null</summary>
    public CharacterStats GetStats()
    {
        if (CharacterManager.Instance == null) return null;
        return CharacterManager.Instance.GetStats(characterId);
    }

    void OnAtk()
    {
        if (!InputSystemController.Instance.GetAttackPressed()) return;
        var arr = CurrentState.Config.OnAtk;
        if (arr == null) return;

        // flat 数组，每 3 个一组 [窗口始, 窗口末, 目标StateId]，按顺序第一个命中的生效
        // 例：0.3;0.5;10022;0.6;1.0;10021 → 命中窗口接攻击2；错过窗口后摇再按 → 回攻击1
        for (int i = 0; i + 2 < arr.Length; i += 3)
        {
            if (CheckWindow(arr[i], arr[i + 1]))
            {
                // 锁定敌人时：攻击起手自动转向锁定目标（每次攻击都转）
                if (lockOnController != null && lockOnController.IsLocked)
                    faceTarget = lockOnController.LockedTarget;

                ToNext((int)arr[i + 2]);
                return;
            }
        }
    }

    void OnMove()
    {
        var move = InputSystemController.Instance.GetMoveInput();
        if (move.x == 0 && move.y == 0) return;

        if (CheckConfig(CurrentState.Config.OnMove))
        {
            ToNext((int)CurrentState.Config.OnMove[2]);
        }
    }

    /// <summary>
    /// 冲刺检测：按住左 Shift，且当前状态配置了 OnSprint = [窗口始, 窗口末, 目标StateId]。
    /// 相位落在窗口内才允许切（0;1;1004 = 全程可切到冲刺 1004）。
    /// </summary>
    void OnSprintCheck()
    {
        if (!InputSystemController.Instance.GetSprintHeld()) return;   // 松开左 Shift 不触发
        var arr = CurrentState.Config.OnSprint;
        if (arr == null || arr.Length < 3) return;

        for (int i = 0; i + 2 < arr.Length; i += 3)
        {
            if (CheckWindow(arr[i], arr[i + 1]))
            {
                ToNext((int)arr[i + 2]);
                return;
            }
        }
    }

    /// <summary>
    /// 跳跃检测：按空格，且当前状态配置了 OnJump = [窗口始, 窗口末, 目标StateId]。
    /// 相位落在窗口内才允许切（0;1;10051 = 全程可切到跳跃 10051）。
    /// </summary>
    void OnJumpCheck()
    {
        if (!InputSystemController.Instance.GetJumpPressed()) return;   // 没按空格不触发
        var arr = CurrentState.Config.OnJump;
        if (arr == null || arr.Length < 3) return;

        for (int i = 0; i + 2 < arr.Length; i += 3)
        {
            if (CheckWindow(arr[i], arr[i + 1]))
            {
                ToNext((int)arr[i + 2]);
                return;
            }
        }
    }

    /// <summary>
    /// 空中下落检测：`OnFalling` = [窗口始, 窗口末, 目标StateId]。
    /// 仅在角色处于空中（IsInAir）且动画相位落在窗口内时，切到对应状态。
    /// 例：10051 起跳在相位 0.68~1 切到 10053 滞空。
    /// </summary>
    void OnFallingCheck()
    {
        if (!IsInAir) return;   // 仅在空中触发
        var arr = CurrentState.Config.OnFalling;
        if (arr == null || arr.Length < 3) return;

        for (int i = 0; i + 2 < arr.Length; i += 3)
        {
            if (CheckWindow(arr[i], arr[i + 1]))
            {
                ToNext((int)arr[i + 2]);
                return;
            }
        }
    }

    /// <summary>
    /// 落地检测：`OnLand` = [窗口始, 窗口末, 目标StateId]。
    /// 仅在角色处于地面（IsGrounded）且动画相位落在窗口内时，切到对应状态。
    /// 例：10053 滞空在相位 0~1 切到 1001 待机（落地）。
    /// </summary>
    void OnLandCheck()
    {
        if (!IsGrounded) return;   // 仅在地面触发
        var arr = CurrentState.Config.OnLand;
        if (arr == null || arr.Length < 3) return;

        for (int i = 0; i + 2 < arr.Length; i += 3)
        {
            if (CheckWindow(arr[i], arr[i + 1]))
            {
                ToNext((int)arr[i + 2]);
                return;
            }
        }
    }

    void OnSkillCheck()
    {
        if (InputSystemController.Instance.GetSkillPressed())
        {
            if (CanUseEnhanceSkill()) return; // 强化期内，普通E让给强化E
            if (CheckConfig(CurrentState.Config.OnSkill))
            {
                int targetId = (int)CurrentState.Config.OnSkill[2];
                OnSkillTriggered(targetId);
            }
        }
    }

    /// <summary>技能触发。子类可重写做形态切换等前置逻辑。</summary>
    protected virtual void OnSkillTriggered(int targetStateId)
    {
        ToNext(targetStateId);
    }

    /// <summary>按 StateId 查动画名（供子类跨形态播动画用）</summary>
    protected string GetAnimName(int stateId)
    {
        if (stateData.TryGetValue(stateId, out var ps))
            return ps.Config.AnimName;
        return null;
    }

    /// <summary>
    /// 强化技能检测：`OnEnhanceSkill` 格式 [离场状态, 进场状态]（不是时间窗口，不套 CheckConfig）。
    /// 强化期是否可用由子类 `CanUseEnhanceSkill()` 决定。
    /// </summary>
    void OnEnhanceSkillCheck()
    {
        if (!InputSystemController.Instance.GetSkillPressed()) return;
        if (!CanUseEnhanceSkill()) return;
        var cfg = CurrentState.Config;
        if (cfg.OnEnhanceSkill == null || cfg.OnEnhanceSkill.Length < 2) return;

        int leaveState = (int)cfg.OnEnhanceSkill[0];
        int enterState = (int)cfg.OnEnhanceSkill[1];
        OnEnhanceSkillTriggered(leaveState, enterState);
    }

    /// <summary>强化技能是否可用，子类重写（默认不可用）</summary>
    protected virtual bool CanUseEnhanceSkill() => false;

    /// <summary>强化技能触发。子类可重写实现双形态同屏。</summary>
    protected virtual void OnEnhanceSkillTriggered(int leaveStateId, int enterStateId)
    {
        ToNext(enterStateId);
    }

    // ═══════ 位移（参照 Demo_3D_RPG_ PhysicsService） ═══════

    void OnPhysicsBegin()
    {
        int id = CurrentState.Id;
        if (!physicsExecuted.ContainsKey(id)) physicsExecuted[id] = new HashSet<int>();
        else physicsExecuted[id].Clear();
        activePhysics = null;
        activePhysicsTriggerSec = 0f;
        activePhysicsTimeSec = 0f;
    }

    /// <summary>
    /// 统一重力下落。仅当当前没有位移配置接管（activePhysics == null）时生效。
    /// - 配了 motion 且正在位移：位移逻辑自己处理重力（按 ignoreGravity），这里跳过。
    /// - 没配 motion / 位移结束：这里叠加重力，角色在空中自然下落；在地面被碰撞挡住则不动。
    /// </summary>
    void ApplyGravity()
    {
        if (characterController == null) return;
        if (activePhysics != null) return;   // 正在位移，由位移逻辑处理
        characterController.Move(Physics.gravity * gravityScale * Time.deltaTime);
    }

    void OnPhysicsUpdate()
    {
        if (motionDict == null || characterController == null) return;
        if (!motionDict.TryGetValue(CurrentState.Id, out var motionData)) return;

        float elapsed = GetStateElapsed();
        var executed = physicsExecuted[CurrentState.Id];

        bool justTriggered = false;

        // 检查新触发的位移配置（triggerSec/endSec 是秒，直接用流逝秒比较）
        for (int i = 0; i < motionData.physicsConfigs.Count; i++)
        {
            if (executed.Contains(i)) continue;
            var cfg = motionData.physicsConfigs[i];
            if (!cfg.enabled) continue;

            if (elapsed >= cfg.triggerSec)
            {
                executed.Add(i);
                activePhysics = cfg;
                justTriggered = true;
                float duration = cfg.endSec - cfg.triggerSec;
                activePhysicsVelocity = duration > 0.001f ? cfg.force / duration : cfg.force;
                activePhysicsTriggerSec = cfg.triggerSec;
                activePhysicsTimeSec = cfg.endSec;
                break;
            }
        }

        // 运行中也可通过 Inspector 关闭位移
        if (activePhysics != null && !activePhysics.enabled)
            activePhysics = null;

        // 应用当前位移（刚触发的同一帧不做过期检查）
        if (activePhysics == null) return;
        if (!justTriggered && elapsed >= activePhysicsTimeSec)
        {
            activePhysics = null;
            return;
        }

        float progress = (elapsed - activePhysicsTriggerSec) / Mathf.Max(0.0001f, activePhysicsTimeSec - activePhysicsTriggerSec);
        float cx = activePhysics.curveX.Evaluate(progress);
        float cy = activePhysics.curveY.Evaluate(progress);
        float cz = activePhysics.curveZ.Evaluate(progress);
        Vector3 localMove = Vector3.Scale(activePhysicsVelocity, new Vector3(cx, cy, cz)) * Time.deltaTime;

        if (activePhysics.moveChild)
        {
            // 只移动子节点，绕开 CharacterController
            var child = string.IsNullOrEmpty(activePhysics.childPath) ? null : transform.Find(activePhysics.childPath);
            if (child != null)
                child.localPosition += localMove;
        }
        else
        {
            Vector3 move = transform.TransformDirection(localMove); // 相对坐标 → 世界坐标
            if (!activePhysics.ignoreGravity)
                move.y += Physics.gravity.y * Time.deltaTime;

            // 停下/夹紧：前进=敌人进入 stopDst 内停；后退=退到敌人 stopDst 外停（防退过头）
            if (activePhysics.stopDst > 0f)
                StopAtDistance(ref move, localMove.z >= 0f);

            characterController.Move(move);
        }
    }

    /// <summary>
    /// 前后位移停下/夹紧：让角色正好停在离敌人 stopDst 米处。
    /// 前进：敌人进入 stopDst 内就停；后退：退到敌人 stopDst 外就停，本帧位移夹紧不冲过头。
    /// 按 localMove.z 正负判断前后（配置里的曲线方向）。
    /// </summary>
    void StopAtDistance(ref Vector3 move, bool movingForward)
    {
        float radius = characterController != null ? characterController.radius : 0.3f;
        Vector3 origin = transform.position + Vector3.up * (characterController != null ? characterController.height * 0.5f : 1f);

        bool hit = Physics.SphereCast(origin, radius, transform.forward, out var info, 50f, activePhysics.stopMask);
        float fwd = Vector3.Dot(move, transform.forward); // 本帧世界位移的前后分量（正=前进，负=后退）

        if (movingForward)
        {
            // 敌人已进入 stopDst 内 → 取消本帧前进量并停
            if (hit && info.distance <= activePhysics.stopDst)
            {
                if (fwd > 0f) move -= transform.forward * fwd;
                activePhysics = null;
            }
        }
        else
        {
            // 前方无敌人 → 不是"退到敌人安全距离"场景（如没锁定/前方没人），正常后退，不掐位移
            if (!hit) return;

            float remaining = activePhysics.stopDst - info.distance; // 还需退多少才到 stopDst
            if (remaining <= 0f)
            {
                activePhysics = null; // 已退到 stopDst
            }
            else if (fwd < 0f) // 本帧确实在后退
            {
                if (-fwd > remaining) // 这帧会退过头 → 夹到正好剩 remaining
                {
                    move -= transform.forward * (fwd + remaining);
                    activePhysics = null;
                }
            }
        }
    }

    void OnPhysicsEnd()
    {
        // 状态结束时子节点位置归零
        if (activePhysics != null && activePhysics.moveChild && activePhysics.resetChildOnEnd)
        {
            var child = string.IsNullOrEmpty(activePhysics.childPath) ? null : transform.Find(activePhysics.childPath);
            if (child != null) child.localPosition = Vector3.zero;
        }
        activePhysics = null;
    }

    // ═══════ 特效 ═══════

    void OnEffectBegin()
    {
        int id = CurrentState.Id;
        if (!effectSpawned.ContainsKey(id)) effectSpawned[id] = new HashSet<int>();
        else effectSpawned[id].Clear();
        // 清理上次残留的特效实例
        if (activeEffects.TryGetValue(id, out var list))
        {
            foreach (var go in list) if (go != null) Destroy(go);
            list.Clear();
        }
        else activeEffects[id] = new List<GameObject>();
    }

    void OnEffectUpdate()
    {
        if (effectDict == null) return;
        if (!effectDict.TryGetValue(CurrentState.Id, out var effectData)) return;

        float elapsed = GetStateElapsed();
        var spawned = effectSpawned[CurrentState.Id];

        for (int i = 0; i < effectData.effects.Count; i++)
        {
            if (spawned.Contains(i)) continue;
            var cfg = effectData.effects[i];
            if (!cfg.enabled) continue;

            if (elapsed >= cfg.triggerSec)
            {
                spawned.Add(i);
                Transform parent = string.IsNullOrEmpty(cfg.bindPoint)
                    ? transform : transform.Find(cfg.bindPoint);
                if (parent == null) parent = transform;

                var go = Instantiate(cfg.effectPrefab, parent);
                go.transform.localPosition = cfg.offset;
                go.transform.localRotation = Quaternion.Euler(cfg.rotation);

                activeEffects[CurrentState.Id].Add(go);

                if (cfg.duration > 0f)
                    Destroy(go, cfg.duration);

                Debug.Log($"[特效] StateId={CurrentState.Id} idx={i} trigger={cfg.triggerSec}s prefab={cfg.effectPrefab?.name}");
            }
        }
    }

    // ═══════ 相机镜头 ═══════

    void OnCameraBegin()
    {
        int id = CurrentState.Id;
        cameraKeyframeTrack[id] = -1;
    }

    void OnCameraUpdate()
    {
        if (!cameraDict.TryGetValue(CurrentState.Id, out var data)) return;
        float elapsed = GetStateElapsed();
        int lastIdx = cameraKeyframeTrack[CurrentState.Id];

        for (int i = 0; i < data.timeline.Count; i++)
        {
            if (i <= lastIdx) continue;
            var kf = data.timeline[i];
            if (elapsed >= kf.triggerSec)
            {
                cameraKeyframeTrack[CurrentState.Id] = i;
                // targetYaw 是相对角色的，叠上角色当前朝向转世界角度（哨兵 -999 保持不变）
                float worldYaw = kf.targetYaw;
                if (Mathf.Abs(kf.targetYaw + 999f) > 0.01f)
                    worldYaw = kf.targetYaw + transform.eulerAngles.y;

                // 看向目标：路径字符串直接传给 CameraController 解析
                bool hasLookAt = kf.lookAtOther && !string.IsNullOrEmpty(kf.lookAtPath);

                EventBus.Publish(new CameraParamEvent(kf.targetDistance, worldYaw, kf.targetPitch, kf.pivotPath, kf.lockInput,
                    hasLookAt, kf.lookAtPath));
            }
        }

        // 时间线播完 → 解锁（lastIdx 有效 && 已是最后一段）
        if (lastIdx >= 0 && lastIdx < data.timeline.Count && lastIdx + 1 >= data.timeline.Count)
        {
            var lastKf = data.timeline[lastIdx];
            if (elapsed >= lastKf.triggerSec + lastKf.duration)
            {
                cameraKeyframeTrack[CurrentState.Id] = lastIdx + 1; // 标记已解锁，不再重复发
                EventBus.Publish(CameraParamEvent.Release);
            }
        }
    }

    void OnCameraEnd()
    {
        // 状态被中断时释放相机锁定，防止 lockInput 泄漏
        EventBus.Publish(CameraParamEvent.Release);
    }

    // ═══════ 旋转（参照 Demo_3D_RPG_ DORotate） ═══════

    void DORotate()
    {
        var move = InputSystemController.Instance.GetMoveInput();
        if (move.x == 0 && move.y == 0) return;

        Vector3 inputDir = new Vector3(move.x, 0f, move.y).normalized;
        float cameraY = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;

        // 输入方向角度 + 相机 Y 轴 = 世界空间目标角度
        targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraY;

        float rotation = Mathf.SmoothDampAngle(
            transform.eulerAngles.y, targetRotation,
            ref rotationVelocity, RotationSmoothTime);

        transform.rotation = Quaternion.Euler(0f, rotation, 0f);
    }

    /// <summary>
    /// 锁定攻击转向：攻击起手设置的 faceTarget 生效，朝锁定敌人快速转身（0.05s），
    /// 转到位自动清除。只在有 faceTarget 时工作，走路转身（DORotate）之后调用可覆盖它。
    /// </summary>
    void UpdateLockFace()
    {
        if (faceTarget == null) return;

        Vector3 dir = faceTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) { faceTarget = null; return; }

        float desired = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y, desired,
            ref rotationVelocity, FaceTurnSmoothTime);

        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, desired)) < 0.5f)
            faceTarget = null;   // 已面对目标，转交给走路转身
    }

    // ═══════ 状态切换 ═══════

    public bool ToNext(int stateId)
    {
        if (!stateData.TryGetValue(stateId, out var next)) return false;
        int prevStateId = CurrentState != null ? CurrentState.Id : -1;   // 记录来源状态（切过渡锚点用）
        if (CurrentState != null)
        {
            DOStateEvent(CurrentState.Id, StateEventType.End);            // 旧状态 End 事件
            for (int i = 0; i < services.Count; i++) services[i].OnEnd(); // 旧状态服务清理
        }

        CurrentState = next;
        CurrentState.SetBeginTime();
        animEndFired = false;

        // 过渡：用脚相位锚点（过渡对配置）+ FixedTime 时长。查不到过渡对则用默认 0.12s 从头切。
        if (animTransitionSO != null)
        {
            float dur = GetCrossFadeDur(prevStateId, stateId);
            float enter = GetEnterNorm(prevStateId, stateId);
            animator.CrossFadeInFixedTime(CurrentState.Config.AnimName, dur, 0, enter);
        }
        else
        {
            animator.CrossFade(CurrentState.Config.AnimName, 0.016f); // 未配脚相位 → 保持原逻辑
        }

        DOStateEvent(CurrentState.Id, StateEventType.Begin);              // 新状态 Begin 事件
        for (int i = 0; i < services.Count; i++) services[i].OnBegin();   // 新状态服务初始化
        OnStateBegin(CurrentState);
        EventBus.Publish(new StateChangedEvent(CurrentState.Id, cameraSO)); // 通知相机切镜头
        return true;
    }

    /// <summary>状态进入钩子，子类可重写（如普攻4进入时解锁强化）</summary>
    protected virtual void OnStateBegin(PlayerState state) { }

    // ═══════ 事件系统 ═══════

    private Dictionary<int, Dictionary<StateEventType, List<System.Action>>> actions = new();

    public void AddListener(int stateId, StateEventType type, System.Action callback)
    {
        if (!actions.ContainsKey(stateId))
            actions[stateId] = new Dictionary<StateEventType, List<System.Action>>();
        if (!actions[stateId].ContainsKey(type))
            actions[stateId][type] = new List<System.Action>();
        actions[stateId][type].Add(callback);
    }

    public void RemoveListeners(int stateId) => actions.Remove(stateId);

    void DOStateEvent(int stateId, StateEventType type)
    {
        if (actions.TryGetValue(stateId, out var dict))
            if (dict.TryGetValue(type, out var list))
                for (int i = 0; i < list.Count; i++)
                    list[i].Invoke();
    }

    // ═══════ 命中范围可视化（Scene 视图） ═══════

    /// <summary>
    /// Scene 视图画出命中扇形/球形范围，方便调角度和距离。
    /// 编辑模式预览 gizmoPreviewStateId；Play 模式实时画当前状态的命中段。
    /// </summary>
    void OnDrawGizmos()
    {
        if (hitSO == null) return;

        // 运行时画当前状态，编辑时画预览状态
        int id = Application.isPlaying && CurrentState != null ? CurrentState.Id : gizmoPreviewStateId;

        StateHitData data = null;
        for (int i = 0; i < hitSO.states.Count; i++)
            if (hitSO.states[i].StateId == id) { data = hitSO.states[i]; break; }
        if (data == null) return;

        foreach (var seg in data.segments)
        {
            if (!seg.enabled) continue;
            if (seg.shape == HitShape.Physical) continue; // 物理碰撞体形状在场景里摆，不画虚拟 Gizmo
            // Play 模式只画"正在判定的窗口"，窗口结束框消失；编辑模式常亮预览
            if (Application.isPlaying && !IsHitWindowActive(seg)) continue;

            Vector3 center = transform.position + transform.rotation * seg.offset;
            Gizmos.color = Color.red;

            if (seg.shape == HitShape.Sphere)
            {
                Gizmos.DrawWireSphere(center, seg.radius);
                continue;
            }

            if (seg.shape == HitShape.Line)
            {
                // 线形：平头圆柱（中心线 + 两端平头圆 + 侧边线）
                Vector3 ldir = transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;
                Vector3 end = center + ldir * seg.lineLength;
                DrawWireCylinder(center, end, seg.lineWidth);
                continue;
            }

            if (seg.shape == HitShape.Box)
            {
                // 盒形：线框盒，跟随角色朝向 + yaw/pitch 偏转
                Quaternion rot = transform.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f);
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, seg.boxSize);
                Gizmos.matrix = old;
                continue;
            }

            // 扇形：两条边线 + 弧线（fwd 保持原始世界旋转，两条边线能正常旋转）
            Vector3 fwd = Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * transform.forward;
            Vector3 left = Quaternion.Euler(0f, -seg.halfAngle, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f, seg.halfAngle, 0f) * fwd;
            Gizmos.DrawLine(center, center + left * seg.radius);
            Gizmos.DrawLine(center, center + right * seg.radius);

            const int steps = 24;
            Vector3 prev = center + left * seg.radius;
            for (int i = 1; i <= steps; i++)
            {
                float ang = Mathf.Lerp(-seg.halfAngle, seg.halfAngle, (float)i / steps);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * fwd;   // 基于 fwd（去掉原重复的 yawOffset），与边线同基准
                Vector3 cur = center + dir * seg.radius;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }

    /// <summary>当前动画时间是否处于该段的命中窗口内（Play 模式控制检测框显隐）</summary>
    bool IsHitWindowActive(HitSegment seg)
    {
        float sec = GetNormalizedTime() * GetClipLength();
        return sec >= seg.triggerSec && sec < seg.triggerSec + seg.duration;
    }

    /// <summary>画平头圆柱：中心线 + 两端平头圆 + 4 条侧边线</summary>
    static void DrawWireCylinder(Vector3 start, Vector3 end, float radius)
    {
        Vector3 dir = (end - start).normalized;
        if (dir.sqrMagnitude < 0.0001f) return;

        // 垂直于 dir 的两个正交方向
        Vector3 perp = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        Vector3 perp2 = Vector3.Cross(dir, perp).normalized;

        // 中心线
        Gizmos.DrawLine(start, end);

        // 两端平头圆 + 侧边线
        const int segs = 24;
        Vector3 prevStart = start + perp * radius;
        Vector3 prevEnd = end + perp * radius;
        for (int i = 1; i <= segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            Vector3 offset = (Mathf.Cos(a) * perp + Mathf.Sin(a) * perp2) * radius;
            Vector3 curStart = start + offset;
            Vector3 curEnd = end + offset;
            Gizmos.DrawLine(prevStart, curStart);
            Gizmos.DrawLine(prevEnd, curEnd);
            // 每 90° 画一条侧边线
            if (i % (segs / 4) == 0)
                Gizmos.DrawLine(curStart, curEnd);
            prevStart = curStart;
            prevEnd = curEnd;
        }
    }
}
