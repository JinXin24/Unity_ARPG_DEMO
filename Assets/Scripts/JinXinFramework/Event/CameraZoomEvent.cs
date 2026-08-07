namespace JinXinFramework.Event
{
    public struct CameraZoomEvent : IEvent
    {
        public float MinDistance;
        public float MaxDistance;
        public CameraZoomEvent(float min, float max)
        {
            MinDistance = min;
            MaxDistance = max;
        }
    }
}
