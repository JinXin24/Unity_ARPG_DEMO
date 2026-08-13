/// <summary>属性类型枚举</summary>
public enum StatType
{
    // 固定值
    ATK,           // 攻击力
    DEF,           // 防御力
    HP,            // 生命值

    // 百分比
    ATKPercent,    // 攻击力%
    DEFPercent,    // 防御力%
    HPPercent,     // 生命值%

    // 双暴
    CritRate,      // 暴击率%
    CritDMG,       // 暴击伤害%

    // 元素伤害
    FireDMG,       // 热熔伤害%
    IceDMG,        // 冷凝伤害%
    ThunderDMG,    // 导电伤害%
    WindDMG,       // 气动伤害%
    LightDMG,      // 衍射伤害%
    DarkDMG,       // 湮灭伤害%

    // 其他
    EnergyRegen,   // 充能效率%
    BasicATKBonus, // 普攻伤害加成%
    SkillBonus,    // 技能伤害加成%
    HealingBonus,  // 治疗效果加成%
}
