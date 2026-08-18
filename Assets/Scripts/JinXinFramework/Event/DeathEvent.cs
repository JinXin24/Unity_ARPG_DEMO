namespace JinXinFramework.Event
{
    /// <summary>死亡事件 — 血量归零时发布，死亡动画 / 掉落 / 移除订阅。</summary>
    public struct DeathEvent : IEvent
    {
        public Damageable Target;

        public DeathEvent(Damageable target)
        {
            Target = target;
        }
    }
}
