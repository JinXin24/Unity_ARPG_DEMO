using UnityEngine;
using Cinemachine;

/// <summary>
/// 镜头系统控制器 — Singleton。管理 Cinemachine 镜头缩放、切换、震屏。
/// </summary>
public class CameraSystemController : Singleton<CameraSystemController>
{
    [SerializeField] private CinemachineVirtualCamera defaultVCam;

    [Header("双形态挂点")]
    [SerializeField] private Transform humanFollow;
    [SerializeField] private Transform humanLookAt;
    [SerializeField] private Transform mechFollow;
    [SerializeField] private Transform mechLookAt;

    /// <summary>切形态时调用：true=机甲, false=人类</summary>
    public void SetForm(bool isMech)
    {
        if (defaultVCam == null) return;
        defaultVCam.Follow = isMech ? mechFollow : humanFollow;
        defaultVCam.LookAt = isMech ? mechLookAt : humanLookAt;
    }

    [Header("缩放")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 1f;

    [Header("俯仰")]
    [SerializeField] private float pitchSpeed = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 30f;

    private CinemachineTransposer transposer;
    private Vector3 baseFollowOffset;

    void Start()
    {
        if (defaultVCam != null)
        {
            transposer = defaultVCam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
                baseFollowOffset = transposer.m_FollowOffset;
        }
    }

    void Update()
    {
        if (transposer == null) return;

        // 缩放
        float scroll = InputSystemController.Instance.GetScrollInput();
        if (!Mathf.Approximately(scroll, 0f))
        {
            Vector3 dir = transposer.m_FollowOffset.normalized;
            float currentDist = transposer.m_FollowOffset.magnitude;
            float newDist = Mathf.Clamp(currentDist - scroll * zoomSpeed, minDistance, maxDistance);
            transposer.m_FollowOffset = dir * newDist;
        }

        // 俯仰：绕角色右轴旋转 Follow Offset 向量
        float pitchDelta = -Input.GetAxis("Mouse Y") * pitchSpeed;
        if (!Mathf.Approximately(pitchDelta, 0f))
        {
            Vector3 pivot = transposer.FollowTarget != null
                ? transposer.FollowTarget.position : transform.position;

            Vector3 camPos = pivot + transposer.m_FollowOffset;
            Vector3 right = Vector3.Cross((camPos - pivot).normalized, Vector3.up).normalized;
            Vector3 newOffset = Quaternion.AngleAxis(pitchDelta, right) * transposer.m_FollowOffset;

            // 限制仰角（与水平面的夹角）
            float pitch = Vector3.Angle(newOffset, Vector3.up) - 90f; // 0=水平, -向下, +向上
            if (pitch >= minPitch && pitch <= maxPitch)
                transposer.m_FollowOffset = newOffset;
        }
    }
}
