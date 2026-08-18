using UnityEngine;

/// <summary>
/// 伤害计算器 — 纯函数，命中检测方调它算出最终伤害。
/// 公式：基础(Atk × 倍率) → 防御减免 → 暴击 → 取整(下限1)。
/// 攻击方/防御方无数值时按 0 处理（木桩、未挂 CharacterManager 等）。
/// </summary>
public static class DamageCalculator
{
    private const float DefSoftCap = 1000f;   // 防御软化常数，防越堆收益越低

    /// <param name="ratio">技能倍率：100 = 100% 攻击力</param>
    public static DamageInfo Calculate(CharacterStats attacker, CharacterStats defender, int ratio,
        Vector3 hitPoint, Vector3 hitDir, GameObject source)
    {
        // 基础伤害 = 攻击力 × 倍率；无攻击方则退化为倍率数值本身
        float baseDmg = attacker != null ? attacker.FinalAtk * ratio / 100f : ratio;

        // 防御减免
        int def = defender != null ? defender.FinalDef : 0;
        float mitigated = baseDmg * (1f - def / (def + DefSoftCap));

        // 暴击
        bool isCrit = attacker != null && Random.value < attacker.CritRate;
        float final = isCrit ? mitigated * attacker.CritDmg : mitigated;

        return new DamageInfo
        {
            Damage = Mathf.Max(1, Mathf.RoundToInt(final)),
            IsCrit = isCrit,
            HitPoint = hitPoint,
            HitDir = hitDir,
            Source = source,
        };
    }
}
