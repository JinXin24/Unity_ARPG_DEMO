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
    [SerializeField] private MoveConfigSO moveConfig;
    [SerializeField] protected CameraStateSO cameraSO;

    public PlayerState CurrentState { get; private set; }
    private Dictionary<int, PlayerState> stateData = new();
    protected CharacterController characterController;

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
    private float activePhysicsTriggerNorm;
    private float activePhysicsTimeNorm;
    private bool animEndFired;

    // 特效
    private Dictionary<int, StateEffectData> effectDict;
    private Dictionary<int, HashSet<int>> effectSpawned = new();
    private Dictionary<int, List<GameObject>> activeEffects = new();

    // 武器显隐
    private Dictionary<int, StateWeaponData> weaponDict;

    // 相机镜头
    private Dictionary<int, CameraStateData> cameraDict;
    private Dictionary<int, int> cameraKeyframeTrack = new(); // stateId → 已触发的 keyframe 索引

    void Start()
    {
        if (characterController == null)
            characterController = GetComponentInChildren<CharacterController>();
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

        // 构建特效配置字典
        if (effectSO != null)
        {
            effectDict = effectSO.states.Where(s => s.effects.Count > 0)
                .ToDictionary(s => s.StateId);
            Debug.Log($"[CharacterState] 已加载 {effectDict.Count} 个特效配置: {string.Join(", ", effectDict.Keys)}");
        }

        // 构建武器显隐字典
        if (weaponSO != null)
        {
            weaponDict = weaponSO.states.Where(s => s.weapons.Count > 0)
                .ToDictionary(s => s.StateId);
            Debug.Log($"[CharacterState] 已加载 {weaponDict.Count} 个武器配置: {string.Join(", ", weaponDict.Keys)}");
        }

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

            // 注册武器显隐：SO 里有该状态的武器配置时
            if (weaponDict != null && weaponDict.ContainsKey(cfg.StateId))
            {
                AddListener(cfg.StateId, StateEventType.Begin, OnWeaponBegin);
                AddListener(cfg.StateId, StateEventType.Update, OnWeaponUpdate);
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

        // 移动：Blend Tree Speed → 世界位移速度（按状态区分）
        if (moveConfig != null && characterController != null && currentSpeed > 0.01f)
        {
            float worldSpeed = moveConfig.GetMoveSpeed(CurrentState.Id, currentSpeed);
            characterController.Move(transform.forward * worldSpeed * Time.deltaTime);
        }

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
    /// 强化技能检测：`OnEnhanceSkill` 格式 [离场状态, 进场状态]。
    /// 强化期是否可用由子类 `CanUseEnhanceSkill()` 决定。
    /// </summary>
    void OnEnhanceSkillCheck()
    {
        if (!InputSystemController.Instance.GetSkillPressed()) return;
        if (!CanUseEnhanceSkill()) return;
        var cfg = CurrentState.Config;
        if (cfg.OnEnhanceSkill == null || cfg.OnEnhanceSkill.Length < 2) return;
        if (!CheckConfig(cfg.OnEnhanceSkill)) return;

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

        // 秒 → 归一化时间 转换
        bool inTransition = animator.IsInTransition(0);
        var stateInfo = inTransition ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        float clipLen = stateInfo.length;
        if (clipLen <= 0.001f) clipLen = 1f;

        // 检查新触发的位移配置
        for (int i = 0; i < motionData.physicsConfigs.Count; i++)
        {
            if (executed.Contains(i)) continue;
            var cfg = motionData.physicsConfigs[i];
            if (!cfg.enabled) continue;
            float triggerNorm = cfg.triggerSec / clipLen;
            float timeNorm = cfg.endSec / clipLen;

            if (t >= triggerNorm)
            {
                executed.Add(i);
                activePhysics = cfg;
                justTriggered = true;
                float duration = cfg.endSec - cfg.triggerSec;
                activePhysicsVelocity = duration > 0.001f ? cfg.force / duration : cfg.force;
                activePhysicsTriggerNorm = triggerNorm;
                activePhysicsTimeNorm = timeNorm;
                Debug.Log($"[位移] StateId={CurrentState.Id} 触发! force={cfg.force} time=[{cfg.triggerSec},{cfg.endSec}]s clip={clipLen:F2}s duration={duration:F3}s velocity={activePhysicsVelocity}");
                break;
            }
        }

        // 运行中也可通过 Inspector 关闭位移
        if (activePhysics != null && !activePhysics.enabled)
            activePhysics = null;

        // 应用当前位移（刚触发的同一帧不做过期检查）
        if (activePhysics == null) return;
        if (!justTriggered && t >= activePhysicsTimeNorm)
        {
            activePhysics = null;
            return;
        }

        float progress = (t - activePhysicsTriggerNorm) / Mathf.Max(0.0001f, activePhysicsTimeNorm - activePhysicsTriggerNorm);
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
            characterController.Move(move);
        }

        // 前方检测到单位则停下
        if (activePhysics.stopDst > 0f)
        {
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, activePhysics.stopDst))
                activePhysics = null;
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

        float t = GetNormalizedTime();
        var spawned = effectSpawned[CurrentState.Id];
        float clipLen = animator.GetCurrentAnimatorStateInfo(0).length;
        if (clipLen <= 0.001f) clipLen = 1f;

        for (int i = 0; i < effectData.effects.Count; i++)
        {
            if (spawned.Contains(i)) continue;
            var cfg = effectData.effects[i];
            if (!cfg.enabled) continue;

            float triggerNorm = cfg.triggerSec / clipLen;
            if (t >= triggerNorm)
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

    // ═══════ 武器显隐 ═══════

    void OnWeaponBegin()
    {
        if (!weaponDict.TryGetValue(CurrentState.Id, out var data)) return;
        foreach (var w in data.weapons)
        {
            var t = string.IsNullOrEmpty(w.weaponPath) ? null : transform.Find(w.weaponPath);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    void OnWeaponUpdate()
    {
        if (!weaponDict.TryGetValue(CurrentState.Id, out var data)) return;
        float t = GetNormalizedTime();
        float clipLen = animator.GetCurrentAnimatorStateInfo(0).length;
        if (clipLen <= 0.001f) clipLen = 1f;

        foreach (var w in data.weapons)
        {
            if (!w.enabled) continue;
            var tr = string.IsNullOrEmpty(w.weaponPath) ? null : transform.Find(w.weaponPath);
            if (tr == null) continue;

            float showNorm = w.showSec / clipLen;
            float hideNorm = w.hideSec / clipLen;

            if (t >= showNorm && t < hideNorm)
                tr.gameObject.SetActive(true);
            else if (t >= hideNorm)
                tr.gameObject.SetActive(false);
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
        float t = GetNormalizedTime();
        float clipLen = animator.GetCurrentAnimatorStateInfo(0).length;
        if (clipLen <= 0.001f) clipLen = 1f;
        int lastIdx = cameraKeyframeTrack[CurrentState.Id];

        for (int i = 0; i < data.timeline.Count; i++)
        {
            if (i <= lastIdx) continue;
            var kf = data.timeline[i];
            if (t >= kf.triggerSec / clipLen)
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
            if (t >= (lastKf.triggerSec + lastKf.duration) / clipLen)
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
}
