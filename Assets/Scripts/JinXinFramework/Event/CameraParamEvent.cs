namespace JinXinFramework.Event
{
    /// <summary>状态机通知相机更新参数。0/-999=不改变当前值。</summary>
    public struct CameraParamEvent : IEvent
    {
        public float ArmLength;    // 0=不改变
        public float Yaw;           // -999=不改变
        public float Pitch;         // -999=不改变
        public string PivotPath;    // null=不改变
        public bool LockInput;      // 是否禁用鼠标/滚轮
        public bool HasLookAt;      // 是否看向指定目标
        public string LookAtPath;   // 目标路径（CameraController 从 target 往下找，找不到再场景根找）

        public CameraParamEvent(float armLength, float yaw, float pitch, string pivotPath, bool lockInput,
            bool hasLookAt = false, string lookAtPath = null)
        {
            ArmLength = armLength;
            Yaw = yaw;
            Pitch = pitch;
            PivotPath = pivotPath;
            LockInput = lockInput;
            HasLookAt = hasLookAt;
            LookAtPath = lookAtPath;
        }

        /// <summary>解除锁定，恢复正常输入</summary>
        public static CameraParamEvent Release => new CameraParamEvent(0, -999f, -999f, null, false);
    }
}
