using UnityEngine;
public class CharacterAttrSO : ScriptableObject
{
    public int Id;
    public string Name;
    public int Quality;
    public int WeaponType;
    public int Element;
    public int BaseHp;
    public int BaseAtk;
    public int BaseDef;
    public int MaxHp;
    public int MaxAtk;
    public int MaxDef;
    public float CritRate;
    public float CritDmg;
    public float EnergyRecharge;
    public float HealBonus;
    public float HpGrowth;
    public float AtkGrowth;
    public float DefGrowth;
    public bool HasMechaForm;
}
