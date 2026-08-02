using UnityEngine;

/// <summary>角色形态的碰撞体参数</summary>
[System.Serializable]
public struct FormCollider
{
    [Header("身高 (米)")]
    public float height;
    [Header("半径 (米)")]
    public float radius;
    [Header("中心偏移")]
    public Vector3 center;
}

/// <summary>
/// 爱弥斯专属：双 Animator + 形态切换 + 碰撞体适配。
/// </summary>
public class AimisiCharacter : CharacterState
{
    [Header("人类形态")]
    [SerializeField] private Animator humanAnimator;
    [SerializeField] private GameObject humanModel;
    [SerializeField] private FormCollider humanCollider = new FormCollider
    {
        height = 1.8f,
        radius = 0.4f,
        center = new Vector3(0, 0.9f, 0)
    };

    [Header("机甲形态")]
    [SerializeField] private Animator mechAnimator;
    [SerializeField] private GameObject mechModel;
    [SerializeField] private FormCollider mechCollider = new FormCollider
    {
        height = 2.5f,
        radius = 0.6f,
        center = new Vector3(0, 1.25f, 0)
    };

    public bool IsMechForm { get; private set; }

    [Header("强化 E 技能")]
    [SerializeField] private float enhanceDuration = 4f;  // 强化持续时间（秒）
    [SerializeField] private float enhanceTimeLeft;       // 剩余时间（Inspector 观察用）
    public bool IsEnhancing => enhanceTimeLeft > 0f;

    [Header("运行时状态")]
    [SerializeField] private string currentForm = "人类";

    // 双人同屏离场管理
    private Animator leavingAnimator;   // 正在离场的形态 Animator
    private GameObject leavingModel;    // 正在离场的形态模型

    void Awake()
    {
        SwitchToHuman();
    }

    protected override void Update()
    {
        base.Update(); // 必须调用父类 Update，否则状态切换逻辑全失效

        // 强化倒计时
        if (enhanceTimeLeft > 0f)
        {
            enhanceTimeLeft -= Time.deltaTime;
            if (enhanceTimeLeft < 0f) enhanceTimeLeft = 0f;
        }

        // 离场动画播完 → 关闭离场形态的模型
        if (leavingAnimator != null && !leavingAnimator.IsInTransition(0))
        {
            if (leavingAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                if (leavingModel != null) leavingModel.SetActive(false);
                leavingAnimator = null;
                leavingModel = null;
            }
        }
    }

    /// <summary>状态进入钩子：UnlockEnhance=true 的状态（普攻4）→ 解锁强化期</summary>
    protected override void OnStateBegin(PlayerState state)
    {
        if (state.Config != null && state.Config.UnlockEnhance)
            enhanceTimeLeft = enhanceDuration;
    }

    /// <summary>强化期倒计时内才可触发强化E</summary>
    protected override bool CanUseEnhanceSkill() => IsEnhancing;

    protected override void OnSkillTriggered(int targetStateId)
    {
        // ID 首位：1=人类，2=机甲
        if (targetStateId.ToString()[0] == '2') SwitchToMech();
        else SwitchToHuman();
        ToNext(targetStateId);
    }

    /// <summary>
    /// 强化 E：双形态同屏。当前形态播离场，目标形态同时显示播进场。
    /// OnEnhanceSkill = [离场状态, 进场状态]，按 ID 首位判断目标形态。
    /// </summary>
    protected override void OnEnhanceSkillTriggered(int leaveStateId, int enterStateId)
    {
        enhanceTimeLeft = 0f; // 强化E触发后消耗本次强化，等下次进普攻4再解锁
        bool goMech = enterStateId.ToString()[0] == '2';
        string leaveAnim = GetAnimName(leaveStateId);
        string enterAnim = GetAnimName(enterStateId);

        // 双人同屏：两个模型都显示，各自播各自的动画
        if (goMech)
        {
            // 人→机：人播离场，机播进场
            if (humanModel != null) humanModel.SetActive(true);
            if (mechModel != null) mechModel.SetActive(true);
            if (humanAnimator != null) humanAnimator.CrossFade(leaveAnim, 0.016f);
            if (mechAnimator != null) mechAnimator.CrossFade(enterAnim, 0.016f);

            // 离场的是人
            leavingAnimator = humanAnimator;
            leavingModel = humanModel;

            // 主状态机切到机甲进场状态，用机甲 Animator 驱动
            animator = mechAnimator;
            IsMechForm = true;
            currentForm = "机甲";
            ApplyCollider(mechCollider);
        }
        else
        {
            // 机→人：机播离场，人播进场
            if (mechModel != null) mechModel.SetActive(true);
            if (humanModel != null) humanModel.SetActive(true);
            if (mechAnimator != null) mechAnimator.CrossFade(leaveAnim, 0.016f);
            if (humanAnimator != null) humanAnimator.CrossFade(enterAnim, 0.016f);

            // 离场的是机甲
            leavingAnimator = mechAnimator;
            leavingModel = mechModel;

            animator = humanAnimator;
            IsMechForm = false;
            currentForm = "人类";
            ApplyCollider(humanCollider);
        }

        ToNext(enterStateId);
    }

    public void SwitchToHuman()
    {
        IsMechForm = false;
        currentForm = "人类";
        animator = humanAnimator;
        ApplyCollider(humanCollider);
        if (humanModel != null) humanModel.SetActive(true);
        if (mechModel != null) mechModel.SetActive(false);
        leavingAnimator = null;
        leavingModel = null;
    }

    public void SwitchToMech()
    {
        IsMechForm = true;
        currentForm = "机甲";
        animator = mechAnimator;
        ApplyCollider(mechCollider);
        if (humanModel != null) humanModel.SetActive(false);
        if (mechModel != null) mechModel.SetActive(true);
        leavingAnimator = null;
        leavingModel = null;
    }

    void ApplyCollider(FormCollider c)
    {
        if (characterController == null) return;
        characterController.height = c.height;
        characterController.radius = c.radius;
        characterController.center = c.center;
    }
}
