using UnityEngine;

/// <summary>
/// 巡逻路径可视化 — 挂在「路径父物体」上，Scene 视图画线（不参与巡逻逻辑）。
/// 绿 = 起点（第 0 个子物体）、红 = 终点（最后子物体）、黄 = 中间结点。
/// 巡逻逻辑在 EnemyController.patrolRoot 读同一个父物体的子物体，两者配套使用。
/// </summary>
public class PatrolPath : MonoBehaviour
{
    [SerializeField] private float nodeRadius = 0.25f;
    [SerializeField] private Color lineColor = Color.yellow;

    void OnDrawGizmos()
    {
        Transform path = transform;   // 自身就是路径父物体
        if (path.childCount == 0) return;

        for (int i = 0; i < path.childCount; i++)
        {
            Vector3 p = path.GetChild(i).position;
            Gizmos.color = i == 0 ? Color.green
                          : i == path.childCount - 1 ? Color.red
                          : lineColor;
            Gizmos.DrawWireSphere(p, nodeRadius);

            if (i > 0)
            {
                Gizmos.color = lineColor;
                Gizmos.DrawLine(path.GetChild(i - 1).position, p);
            }
        }
    }
}
