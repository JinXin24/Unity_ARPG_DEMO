using System.Collections.Generic;
using UnityEngine;

/// <summary>物品类型</summary>
public enum ItemType
{
    Echo,       // 声骸
    Weapon,     // 武器
    Material,   // 材料
    Consumable, // 消耗品
}

/// <summary>物品实例基类 — 背包里每条记录都是一个 ItemInstance</summary>
[System.Serializable]
public class ItemInstance
{
    public int itemId;
    public ItemType itemType;
    public int quantity = 1;

    /// <summary>如果是声骸，转成 EchoInstance 拿词条数据</summary>
    public EchoInstance AsEcho() => this as EchoInstance;
}

/// <summary>声骸副词条</summary>
[System.Serializable]
public class EchoSubStat
{
    public StatType type;
    public int rollQuality;   // 1~4 档
    public float value;
}

/// <summary>声骸主词条</summary>
[System.Serializable]
public class EchoMainStat
{
    public StatType type;
    public float value;
}

/// <summary>
/// 声骸实例 — 继承 ItemInstance，加词条和等级。
/// 获取时由 InventoryManager 随机生成主词条 + 副词条。
/// </summary>
[System.Serializable]
public class EchoInstance : ItemInstance
{
    [Header("声骸")]
    public int cost;
    public int level = 1;

    public EchoMainStat mainStat = new();
    public List<EchoSubStat> subStats = new();

    public EchoInstance()
    {
        itemType = ItemType.Echo;
        quantity = 1;
    }

    /// <summary>显示名（Editor 列表里好看）</summary>
    public string DisplayName
    {
        get
        {
            string sub = subStats.Count > 0
                ? $" | {string.Join(", ", subStats.ConvertAll(s => $"{StatLabel(s.type)}{s.value:F1}"))}"
                : "";
            return $"Lv.{level} {StatLabel(mainStat.type)}{mainStat.value:F1}{sub}";
        }
    }

    /// <summary>Enum → 中文短标签</summary>
    public static string StatLabel(StatType t) => t switch
    {
        StatType.ATK => "攻击",
        StatType.DEF => "防御",
        StatType.HP => "生命",
        StatType.ATKPercent => "攻击%",
        StatType.DEFPercent => "防御%",
        StatType.HPPercent => "生命%",
        StatType.CritRate => "暴击率",
        StatType.CritDMG => "暴伤",
        StatType.FireDMG => "热熔",
        StatType.IceDMG => "冷凝",
        StatType.ThunderDMG => "导电",
        StatType.WindDMG => "气动",
        StatType.LightDMG => "衍射",
        StatType.DarkDMG => "湮灭",
        StatType.EnergyRegen => "充能",
        StatType.BasicATKBonus => "普攻",
        StatType.SkillBonus => "技能",
        StatType.HealingBonus => "治疗",
        _ => t.ToString(),
    };
}
