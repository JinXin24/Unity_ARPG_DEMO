using UnityEngine;

/// <summary>
/// 输入控制器 — Singleton，基于旧版 Input Manager。
/// </summary>
public class InputSystemController : Singleton<InputSystemController>
{
    public Vector2 GetMoveInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        return new Vector2(h, v);
    }
    

    public bool GetAttackPressed() => Input.GetMouseButtonDown(0);
    public bool GetSprintToggled() => Input.GetKeyDown(KeyCode.LeftShift);
    public bool GetSkillPressed() => Input.GetKeyDown(KeyCode.E);



}



