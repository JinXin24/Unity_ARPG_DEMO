using UnityEngine;
using TMPro;

public class AttrPanel : MonoBehaviour
{
    [Header("属性")]
    [SerializeField] private TMP_Text txt_Hp;
    [SerializeField] private TMP_Text txt_Atk;
    [SerializeField] private TMP_Text txt_Def;
    [SerializeField] private TMP_Text txt_CritRate;
    [SerializeField] private TMP_Text txt_CritDmg;
    [SerializeField] private TMP_Text txt_Level;
    [SerializeField] private TMP_Text txt_WeaponSubStat;

    public void Refresh(CharacterManager.Slot slot)
    {
        if (slot == null) return;
        var stats = CharacterManager.Instance.GetStats(slot.CharacterId);
        if (stats == null) return;









        txt_Level.text = $"Lv.{slot.Level}/90";
        txt_Hp.text = stats.FinalHp.ToString();
        txt_Atk.text = stats.FinalAtk.ToString();
        txt_Def.text = stats.FinalDef.ToString();
        txt_CritRate.text = (stats.CritRate * 100).ToString("F1") + "%";
        txt_CritDmg.text = (stats.CritDmg * 100).ToString("F1") + "%";


        if (txt_WeaponSubStat != null && stats.Weapon != null)
        {


            // SubStatType: 7=攻击力 8=暴击率
            int st = stats.Weapon.SubStatType;
            string name = st switch { 7 => "攻击", 8 => "暴击率", _ => "" };
            txt_WeaponSubStat.text = $"{name} +{stats.WeaponSubStat:F1}%";
        }
    }
}
