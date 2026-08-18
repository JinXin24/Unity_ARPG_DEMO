using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 锁敌控制器 — 挂玩家根物体。中键锁定"正前方扇形内最近"的敌人，
/// 再按中键 / 目标死亡 → 解除。锁定结果供战斗系统读取（如攻击自动转向）。
///
/// 用法：
///   1. 挂玩家根物体（和 CharacterState 同物体）。
///   2. enemyMask 设敌人层（默认第 7 层 = 敌人）。
///   3. CharacterState 攻击时读 LockedTarget 做自动转向。
/// </summary>
public class LockOnController : MonoBehaviour, IEventReceiver<DeathEvent>
{
    [Header("索敌参数")]
    [SerializeField] private LayerMask enemyMask = 1 << 7;  // 敌人所在层（场景敌人 = 第 7 层）
    [SerializeField] private float lockRange = 12f;         // 索敌距离
    [SerializeField] private float lockHalfAngle = 60f;     // 正前方半角（度）：±60° = 120° 扇形

    [Header("锁定标记")]
    [SerializeField] private float markerRadius = 0.6f;     // 敌人脚下标记圈半径
    [SerializeField] private float markerHeight = 0.05f;    // 标记圈贴地高度（略抬离地面，防穿插）
    [SerializeField] private Color markerColor = Color.yellow; // 标记圈颜色

    private LineRenderer markerRing;                        // 地面标记圈（运行时创建，无资源依赖）
    private const int MarkerSegments = 33;                  // 圆圈分段数（含闭合点）

    /// <summary>当前锁定的敌人根物体（未锁定则 null）</summary>
    public Transform LockedTarget { get; private set; }

    public bool IsLocked => LockedTarget != null;

    void OnEnable()
    {
        EventBus.Subscribe<DeathEvent>(this);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<DeathEvent>(this);
        if (markerRing != null) markerRing.enabled = false;   // 组件失活时藏起标记
    }

    void Update()
    {
        if (InputSystemController.Instance.GetLockOnPressed())
        {
            if (LockedTarget != null)
                Unlock();                                  // 已锁定：再按中键解除
            else
                LockedTarget = FindNearestInFront();       // 未锁定：锁正前方最近的敌人
        }

        UpdateMarker();   // 每帧刷新锁定标记（锁定 → 画圈；无锁定 → 隐藏）
    }

    /// <summary>自己锁定的敌人死亡 → 自动解锁</summary>
    public void OnEvent(DeathEvent evt)
    {
        if (evt.Target != null && evt.Target.transform == LockedTarget)
            Unlock();
    }

    /// <summary>清除锁定</summary>
    public void Unlock() => LockedTarget = null;

    /// <summary>正前方扇形内最近的敌人（按距离取最近，带 Damageable 才算可锁对象）</summary>
    Transform FindNearestInFront()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, lockRange, enemyMask);
        float bestDist = float.MaxValue;
        Transform best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            // 必须带 Damageable（碰撞体可能在子物体，沿父链找敌人根物体）
            var dmg = hits[i].GetComponentInParent<Damageable>();
            if (dmg == null) continue;

            Vector3 to = dmg.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > lockRange || dist < 0.0001f) continue;

            // 正前方扇形过滤：角度 = 角色朝向量与 到敌 的水平夹角
            if (Vector3.Angle(transform.forward, to / dist) > lockHalfAngle) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = dmg.transform;
            }
        }
        return best;
    }

    /// <summary>每帧把标记圈画到锁定敌人脚下；无锁定则隐藏</summary>
    void UpdateMarker()
    {
        if (LockedTarget == null)
        {
            if (markerRing != null) markerRing.enabled = false;
            return;
        }

        if (markerRing == null) CreateMarker();
        markerRing.enabled = true;

        Vector3 center = LockedTarget.position + Vector3.up * markerHeight;
        for (int i = 0; i < MarkerSegments; i++)
        {
            float a = i / (float)(MarkerSegments - 1) * Mathf.PI * 2f;
            markerRing.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * markerRadius);
        }
    }

    /// <summary>运行时创建标记圈（纯代码，无预制体/材质依赖；挂玩家根物体下自动清理）</summary>
    void CreateMarker()
    {
        var go = new GameObject("LockMarker");
        go.transform.SetParent(transform, false);

        markerRing = go.AddComponent<LineRenderer>();
        markerRing.positionCount = MarkerSegments;
        markerRing.widthMultiplier = 0.06f;
        markerRing.startColor = markerColor;
        markerRing.endColor = markerColor;
        markerRing.useWorldSpace = true;
        markerRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRing.receiveShadows = false;
    }
}
