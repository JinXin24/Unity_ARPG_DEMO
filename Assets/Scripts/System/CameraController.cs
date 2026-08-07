using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 手写相机系统 — 参照 UCameraController.cs + Camera_Design_Notes.md
/// 状态机通过事件改 desired 目标值，LateUpdate 统一做 SmoothDamp 平滑追赶。
/// </summary>
public class CameraController : MonoBehaviour, IEventReceiver<FormSwitchedEvent>, IEventReceiver<CameraZoomEvent>, IEventReceiver<CameraParamEvent>, IEventReceiver<CameraLockEvent>, IEventReceiver<CameraReleaseEvent>
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("臂参数")]
    [SerializeField] private float armLength = 5f;
    [SerializeField] private float minArmLength = 2f;
    [SerializeField] private float maxArmLength = 15f;

    [Header("旋转")]
    [SerializeField] private float yawSpeed = 2f;
    [SerializeField] private float pitchSpeed = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 80f;

    [Header("旋转平滑")]
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("臂长平滑")]
    [SerializeField] private float armSmoothTime = 0.2f;

    [Header("移动惯性")]
    [SerializeField] private float movementSmoothTime = 0.15f;

    [Header("碰撞")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float sphereRadius = 0.25f;
    [SerializeField] private float collisionOffset = 0.1f;

    [Header("看向目标 (测试)")]
    [SerializeField] private bool lookAtOther;
    [SerializeField] private Transform lookAtTarget;

    // ---- 当前值（每帧 SmoothDamp 追赶目标值） ----
    [SerializeField] private float yaw;
    [SerializeField] private float pitch;
    [SerializeField] private bool lockInput;

    // ---- 目标值（事件写入，输入叠加，LateUpdate 统一平滑） ----
    private float desiredYaw;
    private float desiredPitch;
    private float desiredArmLength;

    private string currentPivotPath;
    private float yawVelocity, pitchVelocity;
    private float armVelocity;
    private Vector3 smoothPivot;
    private Vector3 pivotVelocity;

    void Start()
    {
        yaw = 0f;
        pitch = 0f;
        desiredYaw = yaw;
        desiredPitch = pitch;
        desiredArmLength = armLength;
        smoothPivot = GetRawPivot();
    }

    Vector3 GetRawPivot()
    {
        if (target == null) return transform.position;
        if (string.IsNullOrEmpty(currentPivotPath)) return target.position;
        var child = target.Find(currentPivotPath);
        return child != null ? child.position : target.position;
    }

    void OnEnable()
    {
        EventBus.Subscribe<FormSwitchedEvent>(this);
        EventBus.Subscribe<CameraZoomEvent>(this);
        EventBus.Subscribe<CameraParamEvent>(this);
        EventBus.Subscribe<CameraLockEvent>(this);
        EventBus.Subscribe<CameraReleaseEvent>(this);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<FormSwitchedEvent>(this);
        EventBus.Unsubscribe<CameraZoomEvent>(this);
        EventBus.Unsubscribe<CameraParamEvent>(this);
        EventBus.Unsubscribe<CameraLockEvent>(this);
        EventBus.Unsubscribe<CameraReleaseEvent>(this);
    }

    public void OnEvent(FormSwitchedEvent evt)
    {
        if (evt.Follow != null) target = evt.Follow;
        smoothPivot = GetRawPivot();
        pivotVelocity = Vector3.zero;
    }

    public void OnEvent(CameraZoomEvent evt)
    {
        minArmLength = evt.MinDistance;
        maxArmLength = evt.MaxDistance;
        desiredArmLength = Mathf.Clamp(desiredArmLength, minArmLength, maxArmLength);
        armLength = Mathf.Clamp(armLength, minArmLength, maxArmLength);
    }

    public void OnEvent(CameraParamEvent evt)
    {
        // 哨兵值：0=不改变臂长, -999=不改变角度, null=不改变挂点
        if (evt.ArmLength != 0)
            desiredArmLength = Mathf.Clamp(evt.ArmLength, minArmLength, maxArmLength);
        if (evt.Yaw != -999f)
            desiredYaw = evt.Yaw;
        if (evt.Pitch != -999f)
            desiredPitch = evt.Pitch;
        if (evt.PivotPath != null)
            currentPivotPath = evt.PivotPath;
        lockInput = evt.LockInput;
    }

    public void OnEvent(CameraLockEvent evt)
    {
        if (evt.ArmLength != 0)
            desiredArmLength = Mathf.Clamp(evt.ArmLength, minArmLength, maxArmLength);
        if (evt.Yaw != -999f)
            desiredYaw = evt.Yaw;
        if (evt.Pitch != -999f)
            desiredPitch = evt.Pitch;
        if (evt.PivotPath != null)
            currentPivotPath = evt.PivotPath;
        lockInput = evt.LockInput;
    }

    public void OnEvent(CameraReleaseEvent evt)
    {
        lockInput = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 输入叠加到目标值（未锁时）
        if (!lockInput)
        {
            desiredYaw   += Input.GetAxis("Mouse X") * yawSpeed;
            desiredPitch -= Input.GetAxis("Mouse Y") * pitchSpeed;

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            desiredArmLength = Mathf.Clamp(desiredArmLength - wheel * 2f, minArmLength, maxArmLength);
        }
        desiredPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);

        // 2. 平滑追赶目标值
        yaw       = Mathf.SmoothDamp(yaw,       desiredYaw,       ref yawVelocity,   rotationSmoothTime);
        pitch     = Mathf.SmoothDamp(pitch,     desiredPitch,     ref pitchVelocity, rotationSmoothTime);
        armLength = Mathf.SmoothDamp(armLength, desiredArmLength, ref armVelocity,   armSmoothTime);

        // 2.5 球体投射碰撞：碰墙秒缩，恢复靠 SmoothDamp
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 camDir = rot * Vector3.back; // (0,0,-1) 转到世界方向
        if (Physics.SphereCast(smoothPivot, sphereRadius, camDir, out RaycastHit hit, armLength + sphereRadius, collisionMask))
        {
            float safeDist = Mathf.Max(hit.distance - sphereRadius - collisionOffset, 0f);
            armLength = Mathf.Min(armLength, safeDist);
        }

        // 3. 枢轴 + 相机臂公式（绕目标独立公转，不跟角色自转）
        smoothPivot = Vector3.SmoothDamp(smoothPivot, GetRawPivot(), ref pivotVelocity, movementSmoothTime);
        transform.position = smoothPivot + rot * new Vector3(0, 0, -armLength);
        transform.rotation = rot;
    }
}
