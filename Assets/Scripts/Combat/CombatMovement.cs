using UnityEngine;
using System.Collections;

/// <summary>
/// 战斗位移控制器 — 挂在角色上，负责所有攻击/受击的位移驱动。
/// </summary>
public class CombatMovement : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    [Header("移动参数")]
    [SerializeField] private float rotationSpeed = 720f;    // 转向速度（度/秒）

    // 内部状态
    private Transform target;
    private Coroutine currentMove;
    private bool isMoving;

    public bool IsMoving => isMoving;

    void Reset()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 执行一次攻击位移。Animator 通过 Animation Event 或 StateMachineBehaviour 调用。
    /// </summary>
    public void ExecuteAttackMove(AttackMoveData data)
    {
        if (data == null) return;
        if (currentMove != null) StopCoroutine(currentMove);
        currentMove = StartCoroutine(AttackMoveRoutine(data));
    }

    /// <summary>
    /// 直接参数调用版，不需要 ScriptableObject。
    /// </summary>
    public void MoveForward(float distance, float duration, AnimationCurve curve = null)
    {
        if (currentMove != null) StopCoroutine(currentMove);

        var dir = transform.forward;
        var effectiveCurve = curve ?? AnimationCurve.Linear(0, 0, 1, 1);
        currentMove = StartCoroutine(MoveRoutine(dir, distance, duration, effectiveCurve));
    }

    /// <summary>
    /// 设置当前目标（锁定系统调用）。
    /// </summary>
    public void SetTarget(Transform newTarget) => target = newTarget;
    public void ClearTarget() => target = null;
    public Transform GetTarget() => target;

    /// <summary>
    /// 立刻停止所有位移。
    /// </summary>
    public void StopMove()
    {
        if (currentMove != null)
        {
            StopCoroutine(currentMove);
            currentMove = null;
        }
        isMoving = false;
    }

    // ════════════════════════════════════════

    IEnumerator AttackMoveRoutine(AttackMoveData data)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        Vector3 moveDir = GetMoveDirection(data);

        // 开始时朝向目标
        if (data.faceTarget && target != null)
            yield return StartCoroutine(FaceTarget());

        while (elapsed < data.duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / data.duration;

            // 位移曲线
            float curveValue = data.moveCurve.Evaluate(t);
            Vector3 desiredPos = startPos + moveDir * (data.moveDistance * curveValue);

            // 途中追踪目标
            if (data.trackTarget && target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0;
                float finalDist = Mathf.Max(data.stopDistance, 0.01f);
                desiredPos = target.position - toTarget.normalized * finalDist;
                desiredPos.y = startPos.y;
            }

            // 应用移动
            Vector3 delta = desiredPos - transform.position;
            if (characterController != null)
                characterController.Move(delta);
            else
                transform.position = desiredPos;

            // 朝向
            if (data.faceTarget && target != null)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0;
                if (dir.magnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        isMoving = false;
        currentMove = null;
    }

    IEnumerator MoveRoutine(Vector3 direction, float distance, float duration, AnimationCurve curve)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 desiredPos = startPos + direction * (distance * curve.Evaluate(t));

            Vector3 delta = desiredPos - transform.position;
            if (characterController != null)
                characterController.Move(delta);
            else
                transform.position = desiredPos;

            yield return null;
        }

        isMoving = false;
        currentMove = null;
    }

    IEnumerator FaceTarget()
    {
        if (target == null) yield break;

        float elapsed = 0f;
        float maxTime = 0.15f;  // 最多转 150ms

        while (elapsed < maxTime)
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0;
            if (dir.magnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, look) < 1f) yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    Vector3 GetMoveDirection(AttackMoveData data)
    {
        switch (data.direction)
        {
            case MoveDirection.Forward:
                return transform.forward;
            case MoveDirection.ToTarget:
                if (target != null)
                {
                    Vector3 dir = target.position - transform.position;
                    dir.y = 0;
                    return dir.normalized;
                }
                return transform.forward;
            default:
                return transform.forward;
        }
    }
}
