using UnityEngine;
public class AiTransitionSO : ScriptableObject
{
    public int EnemyId;
    public int From;
    public int To;
    public string Condition;
    public float[] Param;
    public int Order;
}
