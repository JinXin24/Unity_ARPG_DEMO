using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input Action 定义 — 可在 Unity 中手动创建 .inputactions 替换此类。
/// </summary>
public class PlayerInputActions
{
    public GameplayActions Gameplay { get; private set; }
    private InputActionMap map;

    public PlayerInputActions()
    {
        map = new InputActionMap("Gameplay");
        Gameplay = new GameplayActions(map);
    }

    public void Enable() => map.Enable();
    public void Disable() => map.Disable();
}

public class GameplayActions
{
    public InputAction Move { get; private set; }
    public InputAction Attack { get; private set; }
    public InputAction Skill { get; private set; }
    public InputAction Dodge { get; private set; }

    public GameplayActions(InputActionMap map)
    {
        Move = map.AddAction("Move", binding: "<Gamepad>/leftStick");
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        Attack = map.AddButtonAction("Attack", binding: "<Mouse>/leftButton");
        Skill = map.AddButtonAction("Skill", binding: "<Keyboard>/e");
        Dodge = map.AddButtonAction("Dodge", binding: "<Keyboard>/space");
    }
}

public static class InputActionMapExtensions
{
    public static InputAction AddButtonAction(this InputActionMap map, string name, string binding = null)
    {
        var action = map.AddAction(name, InputActionType.Button);
        if (binding != null) action.AddBinding(binding);
        return action;
    }
}
