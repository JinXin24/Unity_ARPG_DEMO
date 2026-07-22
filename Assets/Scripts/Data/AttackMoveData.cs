using UnityEngine;

/// <summary>
/// 攻击位移配置 — 策划可手动填的 ScriptableObject。
/// 右键 → Create → Combat → Attack Move Data 创建。
/// </summary>
[CreateAssetMenu(menuName = "Combat/Attack Move Data", fileName = "AttackMove_xxx")]
public class AttackMoveData : ScriptableObject
{
    [Header("动画")]
    [Tooltip("Animator 里的 State 名称")]
    public string stateName;

    [Tooltip("攻击动画总时长（秒）")]
    public float duration = 0.5f;

    [Header("位移")]
    [Tooltip("往前冲多远（米）")]
    public float moveDistance = 3f;

    [Tooltip("位移曲线：横轴=归一化时间(0~1)，纵轴=位移百分比(0~1)")]
    public AnimationCurve moveCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("位移方向")]
    public MoveDirection direction = MoveDirection.Forward;

    [Header("追踪")]
    [Tooltip("是否锁定目标方向")]
    public bool faceTarget = true;

    [Tooltip("是否过程中追踪目标位置")]
    public bool trackTarget;

    [Tooltip("停止时距离目标多远")]
    public float stopDistance = 0.5f;

    [Header("判定")]
    [Tooltip("判定开始时间（归一化 0~1）")]
    public float hitCheckStart = 0.2f;

    [Tooltip("判定结束时间（归一化 0~1）")]
    public float hitCheckEnd = 0.6f;
}

public enum MoveDirection
{
    Forward,    // 角色前方
    ToTarget,   // 朝目标方向
    Custom      // 自定义向量
}
