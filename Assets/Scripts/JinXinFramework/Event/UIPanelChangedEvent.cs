namespace JinXinFramework.Event
{
    /// <summary>
    /// UI 层开合状态变化 — 通知输入系统在 探索/UI 模式间切换。
    /// AnyPanelOpen = 是否有主界面以外的界面处于打开状态。
    /// </summary>
    public struct UIPanelChangedEvent : IEvent
    {
        public bool AnyPanelOpen;

        public UIPanelChangedEvent(bool anyPanelOpen)
        {
            AnyPanelOpen = anyPanelOpen;
        }
    }
}
