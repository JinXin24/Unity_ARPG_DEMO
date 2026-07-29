using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色数值快照 — 纯数据，从配置表 + 等级 + 武器算出。
/// </summary>
public class CharacterStats
{
    public CharacterAttrSO Attr;
    public int Level;
    public WeaponSO Weapon;

    public int Hp, Atk, Def, WeaponAtk;
    public float WeaponSubStat;

    public int FinalHp => Hp;
    public int FinalAtk => Atk + WeaponAtk;
    public int FinalDef => Def;

    public float CritRate => (Attr != null ? Attr.CritRate : 0.05f) + GetWeaponSubStat(4);  // 4=暴击率
    public float CritDmg => (Attr != null ? Attr.CritDmg : 1.5f) + GetWeaponSubStat(5);     // 5=暴击伤害

    float GetWeaponSubStat(int statKind)
    {
        if (Weapon == null) return 0;
        // 原表 SubStatType: 7=攻击力(数值) 8=暴击率  映射到本系统
        int raw = Weapon.SubStatType;
        int kind = raw;
        if (raw == 7) kind = 1; // 攻击力
        if (raw == 8) kind = 4; // 暴击率
        if (kind != statKind) return 0;
        return WeaponSubStat / 100f;
    }

    public static CharacterStats Build(CharacterAttrSO attr, int level, List<LevelGrowthSO> growth, WeaponSO weapon, int weaponLevel)
    {
        var s = new CharacterStats { Attr = attr, Level = level, Weapon = weapon };

        growth.Sort((a, b) => a.Level.CompareTo(b.Level));
        LevelGrowthSO lo = null, hi = null;
        foreach (var g in growth) { if (g.Level <= level) lo = g; if (g.Level >= level && hi == null) hi = g; }

        if (lo != null && hi != null && lo.Level != hi.Level)
        {
            float t = (float)(level - lo.Level) / (hi.Level - lo.Level);
            s.Hp = Mathf.RoundToInt(Mathf.Lerp(lo.Hp, hi.Hp, t));
            s.Atk = Mathf.RoundToInt(Mathf.Lerp(lo.Atk, hi.Atk, t));
            s.Def = Mathf.RoundToInt(Mathf.Lerp(lo.Def, hi.Def, t));
        }
        else if (lo != null) { s.Hp = lo.Hp; s.Atk = lo.Atk; s.Def = lo.Def; }

        if (weapon != null)
        {
            var wg = ConfigManager.Instance.GetWeaponGrowth(weapon.Id);
            if (wg != null && wg.Count > 0)
            {
                wg.Sort((a, b) => a.Level.CompareTo(b.Level));
                int wa = 0; float ws = 0;
                foreach (var g in wg) if (g.Level <= weaponLevel) { wa = g.Atk; ws = g.SubStatValue; }
                s.WeaponAtk = wa > 0 ? wa : wg[0].Atk;
                s.WeaponSubStat = ws > 0 ? ws : wg[0].SubStatValue;
            }
        }
        return s;
    }
}
