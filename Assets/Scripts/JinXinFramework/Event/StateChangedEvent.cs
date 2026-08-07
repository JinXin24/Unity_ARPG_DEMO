namespace JinXinFramework.Event
{
    public struct StateChangedEvent : IEvent
    {
        public int StateId;
        public global::CameraStateSO CameraConfig;
        public StateChangedEvent(int stateId, global::CameraStateSO cameraConfig)
        {
            StateId = stateId;
            CameraConfig = cameraConfig;
        }
    }
}
