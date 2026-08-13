using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// 手写相机系统 — 参照 UCameraController.cs + Camera_Design_Notes.md
/// 状态机通过事件改 desired 目标值，LateUpdate 统一做 SmoothDamp 平滑追赶。
/// </summary>
public class CameraController : MonoBehaviour, IEventReceiver<FormSwitchedEvent>, IEventReceiver<CameraZoomEvent>, IEventReceiver<CameraParamEvent>, IEventReceiver<CameraLockEvent>, IEventReceiver<CameraReleaseEvent>
{
    // ═══════ 运行时值（SmoothDamp 追赶的结果，每帧实时变化） ═══════
    [Header("── 运行时值 ──")]
    [SerializeField] private float yaw;
    [SerializeField] private float pitch;
    [SerializeField] private float armLength = 5f;
    [SerializeField] private bool lockInput;

    // ═══════ 目标值（事件写入 / 输入叠加 / 面板改，SmoothDamp 往这里追） ═══════
    [Header("── 目标值 ──")]
    [SerializeField] private float desiredYaw;
    [SerializeField] private float desiredPitch;
    [SerializeField] private float desiredArmLength = 5f;

    // ═══════ 跟随目标 ═══════
    [Header("── 跟随目标 ──")]
    [SerializeField] private Transform target;

    // ═══════ 参数限制 ═══════
    [Header("── 参数限制 ──")]
    [SerializeField] private float minArmLength = 2f;
    [SerializeField] private float maxArmLength = 15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 80f;

    // ═══════ 平滑参数 ═══════
    [Header("── 平滑参数 ──")]
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float armSmoothTime = 0.2f;
    [SerializeField] private float movementSmoothTime = 0.15f;

    // ═══════ 输入灵敏度 ═══════
    [Header("── 输入灵敏度 ──")]
    [SerializeField] private float yawSpeed = 2f;
    [SerializeField] private float pitchSpeed = 2f;

    // ═══════ 碰撞 ═══════
    [Header("── 碰撞 ──")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float sphereRadius = 0.25f;
    [SerializeField] private float collisionOffset = 0.1f;

    // ═══════ 看向目标 (测试) ═══════
    [Header("── 看向目标 (测试) ──")]
    [SerializeField] private bool lookAtOther;
    [SerializeField] private Transform lookAtTarget;

    // ---- 运行时内部 ----
    private string currentPivotPath;
    private float yawVelocity, pitchVelocity;
    private float armVelocity;
    private Vector3 smoothPivot;
    private Vector3 pivotVelocity;
    private float lookAtWeight;
    private float lookAtWeightVelocity;
    private Quaternion lastLookRot;
    private string eventLookAtPath;    // 事件下发的目标路径

    void Start()
    {
        // 用 Inspector 初始值，不硬编码 0
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

    Transform ResolveLookAtPath(string path)
    {
        if (target == null) return null;
        // 1. 先从跟随目标子物体找
        var child = target.Find(path);
        if (child != null) return child;
        // 2. 再从场景根找
        var go = GameObject.Find(path);
        return go != null ? go.transform : null;
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
        eventLookAtPath = evt.HasLookAt ? evt.LookAtPath : null;
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

        // 1. 输入叠加到目标值（未锁 且 鼠标未呼出时）
        if (!lockInput && !InputSystemController.Instance.IsCursorVisible())
        {
            // 看向目标时禁止鼠标旋转
            bool freeLook = !(lookAtOther && lookAtTarget != null);
            if (freeLook)
            {
                desiredYaw   += Input.GetAxis("Mouse X") * yawSpeed;
                desiredPitch -= Input.GetAxis("Mouse Y") * pitchSpeed;
            }
            desiredYaw = Mathf.Repeat(desiredYaw, 360f);

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            desiredArmLength = Mathf.Clamp(desiredArmLength - wheel * 2f, minArmLength, maxArmLength);
        }
        desiredPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);

        // 2. 平滑追赶目标值（yaw 用 SmoothDampAngle 避免 359→0 转一圈）
        yaw       = Mathf.SmoothDampAngle(yaw,       desiredYaw,       ref yawVelocity,   rotationSmoothTime);
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

        // 看向目标：位置不动，SmoothDamp 过渡镜头朝向（事件路径优先，Inspector 测试字段兜底）
        Vector3 lookPoint = Vector3.zero;
        bool doLookAt = false;

        if (!string.IsNullOrEmpty(eventLookAtPath))
        {
            var lookTr = ResolveLookAtPath(eventLookAtPath);
            if (lookTr != null) { lookPoint = lookTr.position; doLookAt = true; }
        }
        else if (lookAtOther && lookAtTarget != null)
        {
            lookPoint = lookAtTarget.position;
            doLookAt = true;
        }

        float targetWeight = doLookAt ? 1f : 0f;
        lookAtWeight = Mathf.SmoothDamp(lookAtWeight, targetWeight, ref lookAtWeightVelocity, rotationSmoothTime);

        if (lookAtWeight > 0.001f)
        {
            // doLookAt=true: 每帧更新目标点；doLookAt=false: 用 lastLookRot 平滑退出
            if (doLookAt)
                lastLookRot = Quaternion.LookRotation((lookPoint - transform.position).normalized);
            transform.rotation = Quaternion.Slerp(rot, lastLookRot, lookAtWeight);
        }
        else
        {
            transform.rotation = rot;
        }
    }
}
