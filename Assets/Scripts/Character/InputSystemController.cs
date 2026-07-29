using UnityEngine;

/// <summary>
/// 输入控制器 — Singleton，基于 CharacterInputActions.inputactions。
/// </summary>
public class InputSystemController : Singleton<InputSystemController>
{
    private CharacterInputActions input;

    protected override void Awake()
    {
        base.Awake();
        input = new CharacterInputActions();
    }

    void OnEnable() => input?.Player.Enable();
    void OnDisable() => input?.Player.Disable();
    void OnDestroy() => input?.Dispose();

    public bool GetAttackPressed() => input?.Player.Attack.WasPressedThisFrame() ?? false;
    public bool GetSprintToggled() => input?.Player.LShift.WasPressedThisFrame() ?? false;
    public Vector2 GetMoveInput() => input?.Player.Move.ReadValue<Vector2>() ?? Vector2.zero;
}
