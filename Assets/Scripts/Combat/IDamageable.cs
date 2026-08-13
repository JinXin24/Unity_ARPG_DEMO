/// <summary>
/// 可受伤接口：被击方（敌人/木桩/角色）实现它，接收命中伤害。
/// HitDetectorService 不认识具体敌人类型，只认这个接口 —— 解耦。
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
