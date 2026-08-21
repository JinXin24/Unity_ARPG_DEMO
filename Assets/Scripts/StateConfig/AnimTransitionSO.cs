using UnityEngine;
using System.Collections.Generic;

/// <summary>站稳类型：判断当前动画处于哪种脚部支撑状态</summary>
public enum FootStance
{
    None,       // 不限制 / 无法判断
    LeftFoot,   // 左脚站稳（单脚支撑）
    RightFoot,  // 右脚站稳（单脚支撑）
    BothFeet    // 双脚站稳（双脚支撑）
}


/// <summary>单个状态的站稳区间标定（归一化时间 0~1）</summary>
[System.Serializable]
public class FootPhaseData
{
    [Header("状态ID")]
    public int StateId;

    [Header("左脚站稳区间（归一化时间）")]
    public float leftStart, leftEnd;
    [Header("右脚站稳区间（归一化时间）")]
    public float rightStart, rightEnd;
    [Header("双脚站稳区间（归一化时间）")]
    public float bothStart, bothEnd;
}

/// <summary>
/// 过渡对配置：从 fromState 切到 toState 时，对齐到哪种站稳，并指定过渡时长。
/// 这样来源不同（冲刺/攻击）切到同一目标，可用不同站稳锚点。
/// </summary>
[System.Serializable]
public class TransitionPhaseData
{
    public int fromState;              // 源状态ID
    public int toState;                // 目标状态ID
    public FootStance alignStance;     // 对齐到哪种站稳（目标动画里）
    public float crossFadeDur = 0.12f; // 过渡时长（秒）
}

/// <summary>
/// 过渡配置：各动画的站稳区间 + 各过渡对的对齐规则与过渡时长。
/// 挂到角色上即可让所有状态切换走 CrossFadeInFixedTime 过渡分支；
/// 配了过渡对的用配置值，没配的用默认过渡时长。
/// </summary>
[CreateAssetMenu(menuName = "配置/动画过渡配置")]
public class AnimTransitionSO : ScriptableObject
{
    public List<FootPhaseData> stances = new();
    public List<TransitionPhaseData> transitions = new();

    /// <summary>按 StateId 查站稳区间，查不到返回 null</summary>
    public FootPhaseData GetStance(int stateId)
    {
        for (int i = 0; i < stances.Count; i++)
            if (stances[i].StateId == stateId) return stances[i];
        return null;
    }

    /// <summary>按 (from, to) 查过渡对，查不到返回 null</summary>
    public TransitionPhaseData GetTransition(int fromId, int toId)
    {
        for (int i = 0; i < transitions.Count; i++)
        {
            var t = transitions[i];
            if (t.fromState == fromId && t.toState == toId) return t;
        }
        return null;
    }
}
