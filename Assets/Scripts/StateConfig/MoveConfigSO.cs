using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>单个状态的移动速度配置</summary>
[Serializable]
public class StateMoveData
{
    public int StateId;
    [Header("走路速度 (Speed=0.5)")]
    public float walkSpeed = 2.5f;
    [Header("跑步速度 (Speed=1)")]
    public float runSpeed = 6f;
}

/// <summary>Blend Tree Speed → 世界位移速度，按 StateId 映射</summary>
[CreateAssetMenu(menuName = "配置/移动速度配置")]
public class MoveConfigSO : ScriptableObject
{
    public List<StateMoveData> states = new();

    /// <summary>根据 StateId + Speed 参数 Lerp 出位移速度（没配的状态返回 0）</summary>
    public float GetMoveSpeed(int stateId, float speedParam)
    {
        float walk = 0f, run = 0f;

        foreach (var s in states)
        {
            if (s.StateId == stateId)
            {
                walk = s.walkSpeed;
                run = s.runSpeed;
                break;
            }
        }

        if (speedParam <= 0.5f)
            return Mathf.Lerp(0f, walk, speedParam / 0.5f);
        else
            return Mathf.Lerp(walk, run, (speedParam - 0.5f) / 0.5f);
    }
}
