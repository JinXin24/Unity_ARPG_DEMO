namespace JinXinFramework.Event
{
    /// <summary>受击事件 — 伤害结算后发布，UI 飘字 / 血条 / 受击特效订阅。</summary>
    public struct DamageEvent : IEvent
    {
        public Damageable Target;
        public DamageInfo Info;

        public DamageEvent(Damageable target, DamageInfo info)
        {
            Target = target;
            Info = info;
        }
    }
}
