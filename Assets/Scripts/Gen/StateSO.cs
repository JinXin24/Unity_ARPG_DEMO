using UnityEngine;
public class StateSO : ScriptableObject
{
    public int CharacterId;
    public bool UseCommon;
    public int StateId;
    public string Info;
    public string AnimName;
    public int OnAnimEnd;
    public float[] OnMove;
    public float[] OnAtk;
    public float[] OnSkill;
    public float[] OnEnhanceSkill;
    public bool UnlockEnhance;
    public float[] OnSprint;
    public float[] OnJump;
    public float[] OnFalling;
    public float[] OnLand;
}
