namespace JinXinFramework.Event
{
    /// <summary>通知相机进入锁定状态</summary>
    public struct CameraLockEvent : IEvent
    {
        public float ArmLength;    // 0=不改变
        public float Yaw;           // -999=不改变
        public float Pitch;         // -999=不改变
        public string PivotPath;    // null=不改变
        public bool LockInput;      // 是否禁用鼠标/滚轮

        public CameraLockEvent(float armLength, float yaw, float pitch, string pivotPath, bool lockInput)
        {
            ArmLength = armLength;
            Yaw = yaw;
            Pitch = pitch;
            PivotPath = pivotPath;
            LockInput = lockInput;
        }
    }

    /// <summary>通知相机解除锁定</summary>
    public struct CameraReleaseEvent : IEvent { }
}
