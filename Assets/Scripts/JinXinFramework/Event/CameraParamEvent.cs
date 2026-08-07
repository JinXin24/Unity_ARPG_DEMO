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

        public CameraParamEvent(float armLength, float yaw, float pitch, string pivotPath, bool lockInput)
        {
            ArmLength = armLength;
            Yaw = yaw;
            Pitch = pitch;
            PivotPath = pivotPath;
            LockInput = lockInput;
        }

        /// <summary>解除锁定，恢复正常输入</summary>
        public static CameraParamEvent Release => new CameraParamEvent(0, -999f, -999f, null, false);
    }
}
