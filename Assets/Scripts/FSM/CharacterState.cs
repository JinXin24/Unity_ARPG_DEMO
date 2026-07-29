using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private Animator animator;
    [SerializeField] private StateSO[] stateConfigs;
    [SerializeField] private StateMotionSO motionSO;

    public PlayerState CurrentState { get; private set; }
    private Dictionary<int, PlayerState> stateData = new();
    private CharacterController characterController;

    // Blend Tree — 1D, Speed 参数 (0=待机, 0.5=走, 1=跑)
    private float speedVelocity;
    private bool runMode;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // 旋转 — 参照 Demo_3D_RPG_ DORotate，面向输入方向（相对相机）
    private float targetRotation;
    private float rotationVelocity;
    private const float RotationSmoothTime = 0.025f;

    // 位移 — 参照 Demo_3D_RPG_ PhysicsService
    private Dictionary<int, StateMotionData> motionDict;
    private Dictionary<int, HashSet<int>> physicsExecuted = new(); // stateId → executed config indices
    private PhysicsConfig activePhysics;
    private Vector3 activePhysicsVelocity;
    private float activePhysicsTriggerNorm;  // 归一化后的 trigger
    private float activePhysicsTimeNorm;     // 归一化后的 time
    private bool animEndFired;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
            Debug.LogWarning($"[CharacterState] {name} 没有 CharacterController，位移不会生效");

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

        if (stateConfigs.Length == 0) return;
        foreach (var cfg in stateConfigs)
        {
            var ps = new PlayerState { Id = cfg.StateId, Config = cfg };
            stateData[cfg.StateId] = ps;

            // 注册攻击检测：有 OnAtk 配置时，每帧检测攻击输入
            if (cfg.OnAtk != null && cfg.OnAtk.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnAtk);

            // 注册移动检测：有 OnMove 配置时，每帧检测移动输入
            if (cfg.OnMove != null && cfg.OnMove.Length >= 3)
                AddListener(cfg.StateId, StateEventType.Update, OnMove);

            // 注册位移：SO 里有该状态的位移配置时
            if (motionDict != null && motionDict.ContainsKey(cfg.StateId))
            {
                AddListener(cfg.StateId, StateEventType.Begin, OnPhysicsBegin);
                AddListener(cfg.StateId, StateEventType.Update, OnPhysicsUpdate);
                AddListener(cfg.StateId, StateEventType.End, OnPhysicsEnd);
            }
        }
        CurrentState = stateData[stateConfigs[0].StateId];
        CurrentState.SetBeginTime();
    }

    void Update()
    {
        if (CurrentState == null || animator == null) return;

        // Shift 切换 走/跑 模式
        if (InputSystemController.Instance.GetSprintToggled())
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
    }

    // ═══════ 攻击检测（参照 Demo_3D_RPG_ OnAtk） ═══════

    /// <summary>归一化时间窗口检查：t ≤ config[0] 或 t ≥ config[1]</summary>
    bool CheckConfig(float[] config)
    {
        if (config == null || config.Length < 2) return false;
        float t = GetNormalizedTime();
        return (t >= 0f && t <= config[0]) || t >= config[1];
    }

    float GetNormalizedTime()
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

    void OnAtk()
    {
        if (InputSystemController.Instance.GetAttackPressed())
        {
            if (CheckConfig(CurrentState.Config.OnAtk))
            {
                ToNext((int)CurrentState.Config.OnAtk[2]);
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

    // ═══════ 位移（参照 Demo_3D_RPG_ PhysicsService） ═══════

    void OnPhysicsBegin()
    {
        int id = CurrentState.Id;
        if (!physicsExecuted.ContainsKey(id)) physicsExecuted[id] = new HashSet<int>();
        else physicsExecuted[id].Clear();
        activePhysics = null;
        activePhysicsTriggerNorm = 0f;
        activePhysicsTimeNorm = 0f;
    }

    void OnPhysicsUpdate()
    {
        if (motionDict == null || characterController == null) return;
        if (!motionDict.TryGetValue(CurrentState.Id, out var motionData)) return;

        float t = GetNormalizedTime();
        var executed = physicsExecuted[CurrentState.Id];

        bool justTriggered = false;

        // 帧数 → 归一化时间 转换
        bool inTransition = animator.IsInTransition(0);
        var stateInfo = inTransition ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        var clipInfos = inTransition ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
        // Clip 真实长度和帧率（Timeline 上能看到），用于帧→归一化换算
        float clipLen = clipInfos.Length > 0 ? clipInfos[0].clip.length : stateInfo.length;
        float clipFps = clipInfos.Length > 0 ? clipInfos[0].clip.frameRate : motionData.frameRate;
        float totalFrames = clipLen * clipFps;
        // 实际播放时长（可能被 Animator 加速/减速），用于速度计算
        float realPlayLen = stateInfo.length;

        // 检查新触发的位移配置
        for (int i = 0; i < motionData.physicsConfigs.Count; i++)
        {
            if (executed.Contains(i)) continue;
            var cfg = motionData.physicsConfigs[i];
            float triggerNorm = cfg.triggerFrame / totalFrames;
            float timeNorm = cfg.endFrame / totalFrames;

            if (t >= triggerNorm)
            {
                executed.Add(i);
                activePhysics = cfg;
                justTriggered = true;
                float duration = realPlayLen * (timeNorm - triggerNorm);
                activePhysicsVelocity = duration > 0.001f ? cfg.force / duration : cfg.force;
                // 存归一化 time 用于后续帧检查
                activePhysicsTriggerNorm = triggerNorm;
                activePhysicsTimeNorm = timeNorm;
                Debug.Log($"[位移] StateId={CurrentState.Id} 触发! force={cfg.force} frames=[{cfg.triggerFrame},{cfg.endFrame}] totalFrames={totalFrames:F0} realPlay={realPlayLen:F3}s duration={duration:F3}s velocity={activePhysicsVelocity}");
                break;
            }
        }

        // 应用当前位移（刚触发的同一帧不做过期检查）
        if (activePhysics == null) return;
        if (!justTriggered && t >= activePhysicsTimeNorm)
        {
            activePhysics = null;
            return;
        }

        float progress = (t - activePhysicsTriggerNorm) / Mathf.Max(0.0001f, activePhysicsTimeNorm - activePhysicsTriggerNorm);
        Vector3 localMove = activePhysicsVelocity * Time.deltaTime * activePhysics.curve.Evaluate(progress);
        Vector3 move = transform.TransformDirection(localMove); // 相对坐标 → 世界坐标

        if (!activePhysics.ignoreGravity)
            move.y += Physics.gravity.y * Time.deltaTime;

        Vector3 posBefore = transform.position;
        characterController.Move(move);
        Debug.Log($"[位移帧] move={move:F4} posDelta={Vector3.Distance(posBefore, transform.position):F4} t={t:F4} progress={progress:F2}");

        // 前方检测到单位则停下
        if (activePhysics.stopDst > 0f)
        {
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, activePhysics.stopDst))
                activePhysics = null;
        }
        if (activePhysics.stopDst > 0f)
        {
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, activePhysics.stopDst))
                activePhysics = null;
        }
    }

    void OnPhysicsEnd()
    {
        activePhysics = null;
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

    // ═══════ 状态切换 ═══════

    public bool ToNext(int stateId)
    {
        if (!stateData.TryGetValue(stateId, out var next)) return false;
        if (CurrentState != null) DOStateEvent(CurrentState.Id, StateEventType.End);

        CurrentState = next;
        CurrentState.SetBeginTime();
        animEndFired = false;
        animator.CrossFade(CurrentState.Config.AnimName, 0.016f);
        DOStateEvent(CurrentState.Id, StateEventType.Begin);
        return true;
    }

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
}
