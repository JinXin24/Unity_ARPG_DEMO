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

    [Header("运行时状态")]
    [SerializeField] private string currentForm = "人类";

    void Awake()
    {
        SwitchToHuman();
    }

    protected override void OnSkillTriggered(int targetStateId)
    {
        // ID 首位：1=人类，2=机甲
        if (targetStateId.ToString()[0] == '2') SwitchToMech();
        else SwitchToHuman();
        ToNext(targetStateId);
    }

    public void SwitchToHuman()
    {
        IsMechForm = false;
        currentForm = "人类";
        animator = humanAnimator;
        ApplyCollider(humanCollider);
        if (humanModel != null) humanModel.SetActive(true);
        if (mechModel != null) mechModel.SetActive(false);
    }

    public void SwitchToMech()
    {
        IsMechForm = true;
        currentForm = "机甲";
        animator = mechAnimator;
        ApplyCollider(mechCollider);
        if (humanModel != null) humanModel.SetActive(false);
        if (mechModel != null) mechModel.SetActive(true);
    }

    void ApplyCollider(FormCollider c)
    {
        if (characterController == null) return;
        characterController.height = c.height;
        characterController.radius = c.radius;
        characterController.center = c.center;
    }
}
