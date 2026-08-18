using UnityEngine;

/// <summary>
/// 敌人感知组件 — 只负责「看/听」，把结果暴露给状态机，不碰状态逻辑。
/// 视觉 = 距离 + 视野角度(FOV) + 无遮挡，三者都满足才算「看到」。
/// 听觉 = 距离 < hearDistance（一期预留，暂未接噪音事件）。
///
/// 输出（给 EnemyController 当迁移条件）：
///   CurrentTarget      当前感知目标（null = 没看到）
///   IsAlerted          是否锁定目标
///   LastKnownPosition  丢失前最后看到的位置（给 search 状态）
///   HasLastKnown       是否曾看到过目标（判断 LastKnownPosition 是否有效）
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [Header("视觉")]
    [SerializeField] private float fovAngle = 120f;       // 视野角度（全角，度）
    [SerializeField] private float viewDistance = 12f;    // 视野距离（米）

    [Header("听觉（预留）")]
    [SerializeField] private float hearDistance = 5f;     // 听力半径（米）

    [Header("检测")]
    [SerializeField] private LayerMask targetMask = ~0;   // 目标层（玩家所在层，必须设对）
    [SerializeField] private LayerMask obstacleMask = ~0; // 遮挡层（墙等；别含地面和目标，否则误判被挡）
    [SerializeField] private float checkInterval = 0.15f; // 检测间隔（秒），不用每帧

    public Transform CurrentTarget { get; private set; }      // 当前感知目标，null = 没看到
    public bool IsAlerted => CurrentTarget != null;           // 是否锁定目标
    public Vector3 LastKnownPosition { get; private set; }    // 丢失前最后看到的位置
    public bool HasLastKnown { get; private set; }            // 是否曾看到过目标

    private float nextCheckTime;
    private readonly Collider[] candidates = new Collider[16]; // OverlapSphere 复用缓冲

    void Update()
    {
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;
        Tick();
    }

    void Tick()
    {
        // 1. 视野半径内粗筛目标（物理层）
        int count = Physics.OverlapSphereNonAlloc(transform.position, viewDistance, candidates, targetMask);

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var c = candidates[i];
            if (c.transform == transform) continue;   // 排除自己

            Vector3 toTarget = c.transform.position - transform.position;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) continue;

            // 2. 视野角度（超出半角 FOV/2 就看不到背后）
            float angle = Vector3.Angle(transform.forward, toTarget.normalized);
            if (angle > fovAngle * 0.5f) continue;

            // 3. 遮挡判定（射线打到「非目标本身」= 中间有墙挡）
            if (IsBlocked(toTarget, dist, c)) continue;

            if (dist < bestDist) { bestDist = dist; best = c.transform; }
        }

        if (best != null)
        {
            CurrentTarget = best;
            LastKnownPosition = best.position;
            HasLastKnown = true;
        }
        else
        {
            CurrentTarget = null;   // 丢失：保留 LastKnownPosition 给 search 用
        }
    }

    bool IsBlocked(Vector3 toTarget, float dist, Collider targetCol)
    {
        var hits = Physics.RaycastAll(transform.position, toTarget.normalized, dist, obstacleMask);
        foreach (var h in hits)
        {
            if (h.collider == targetCol) continue;   // 打到目标本身不算遮挡
            return true;                              // 打到别的（墙） = 被挡
        }
        return false;
    }

    void OnDrawGizmos()
    {
        // 视野扇形（挂了 EnemyPerception 就画，方便调 FOV/距离）
        Gizmos.color = IsAlerted ? Color.red : Color.yellow;
        Vector3 f = transform.forward;
        Vector3 left = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * f;
        Vector3 right = Quaternion.Euler(0, fovAngle * 0.5f, 0) * f;
        Gizmos.DrawLine(transform.position, transform.position + left * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + right * viewDistance);

        // 弧线（扇形外沿），把视野锥画完整
        const int arcSegments = 24;
        Vector3 prev = transform.position + left * viewDistance;
        for (int i = 1; i <= arcSegments; i++)
        {
            float a = -fovAngle * 0.5f + fovAngle * i / arcSegments;
            Vector3 p = transform.position + (Quaternion.Euler(0, a, 0) * f) * viewDistance;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // 听力半径（预留）
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hearDistance);

        // 锁定目标连线
        if (CurrentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, CurrentTarget.position);
        }
    }
}
