using UnityEngine;

/// <summary>
/// 挂在动画状态上：播放期间禁用 Animator 的 Root Motion，角色不位移。
/// 添加到 Animator State 的 "Add Behaviour" 即可。
/// </summary>
public class LockRootMotion : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.applyRootMotion = false;
    }


    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.applyRootMotion = true;
    }
    
}
