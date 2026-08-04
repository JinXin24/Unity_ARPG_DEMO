namespace JinXinFramework.Event
{
    public interface IEventReceiver<TEvent> where TEvent : IEvent
    {
        void OnEvent(TEvent evt);
    }
}
