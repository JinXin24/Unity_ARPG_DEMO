using UnityEngine;
using JinXinFramework.Event;

/// <summary>输入模式：Explore=探索（只有主界面，鼠标锁定）/ UI=UI模式（其他界面打开，鼠标全程显示）</summary>
public enum InputMode
{
    Explore,
    UI
}

/// <summary>
/// 输入控制器 — Singleton，基于旧版 Input Manager。
/// </summary>
public class InputSystemController : Singleton<InputSystemController>, IEventReceiver<UIPanelChangedEvent>
{
    [Header("输入模式配置")]
    [Tooltip("初始模式。运行中由 UIPanelChangedEvent 驱动：非主界面打开→UI，全部关闭→Explore")]
    [SerializeField] private InputMode mode = InputMode.Explore;

    /// <summary>当前输入模式。切换时立即应用光标状态。</summary>
    public InputMode Mode
    {
        get => mode;
        private set
        {
            if (mode == value) return;
            mode = value;
            ApplyCursorState();
        }
    }

    public bool IsExploreMode => mode == InputMode.Explore;
    public bool IsUIMode => mode == InputMode.UI;

    protected override void Awake()
    {
        base.Awake();
        ApplyCursorState();
    }

    void OnEnable()
    {
        EventBus.Subscribe<UIPanelChangedEvent>(this);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<UIPanelChangedEvent>(this);
    }

    public void OnEvent(UIPanelChangedEvent evt)
    {
        // 有任何主界面以外的界面打开 → UI 模式；全部关闭 → 探索模式
        Mode = evt.AnyPanelOpen ? InputMode.UI : InputMode.Explore;
    }

    void Update()
    {
        if (mode == InputMode.UI)
        {
            // UI 模式：鼠标全程显示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // 探索模式：按住左 Alt → 呼出鼠标；松开 → 锁定
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>按当前模式立即设置光标</summary>
    void ApplyCursorState()
    {
        if (mode == InputMode.UI)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public Vector2 GetMoveInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        return new Vector2(h, v);
    }

    public float GetScrollInput() => Input.GetAxis("Mouse ScrollWheel");

    public bool GetAttackPressed() => !Cursor.visible && Input.GetMouseButtonDown(0);
    public bool GetRunModeToggled() => Input.GetKeyDown(KeyCode.LeftControl);
    public bool GetSprintHeld() => Input.GetKey(KeyCode.LeftShift);   // 左 Shift：冲刺（按住进入，松开退出）
    public bool GetSkillPressed() => Input.GetKeyDown(KeyCode.E);
    public bool GetLockOnPressed() => !Cursor.visible && Input.GetMouseButtonDown(2);   // 中键：锁定/解除锁定
    public bool GetJumpPressed() => Input.GetKeyDown(KeyCode.Space);   // 空格：跳跃

    /// <summary>是否正在呼出鼠标（UI 模式或按住左 Alt 期间）</summary>
    public bool IsCursorVisible() => Cursor.visible;
}
