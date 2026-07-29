using UnityEngine;
using TMPro;

public class WeaponPanel : MonoBehaviour
{
    [Header("武器信息")]
    [SerializeField] private TMP_Text txt_Name;
    [SerializeField] private TMP_Text txt_Level;
    [SerializeField] private TMP_Text txt_Atk;
    [SerializeField] private TMP_Text txt_SubStatTypeName;
    [SerializeField] private TMP_Text txt_SubStatTypeValue;
    [SerializeField] private TMP_Text txt_SkillName;
    [SerializeField] private TMP_Text txt_SkillDesc;

    public void Refresh(CharacterManager.Slot slot)
    {
        if (slot == null) return;

        var stats = CharacterManager.Instance.GetStats(slot.CharacterId);
        if (stats == null || stats.Weapon == null) return;

        var w = stats.Weapon;
        int rank = slot.WeaponRefine;

        txt_Name.text = w.Name;
        txt_Level.text = $"Lv.{slot.WeaponLevel}/90";
        txt_Atk.text = stats.WeaponAtk.ToString();
        txt_SkillName.text = $"【{rank}阶】{w.SkillName}";

        // 精炼参数填入 {0}{1}{2}
        string desc = w.SkillDesc;
        if (!string.IsNullOrEmpty(w.RefineParams))
        {
            var allRanks = w.RefineParams.Split('|');
            if (rank - 1 < allRanks.Length)
            {
                var vals = allRanks[rank - 1].Split(';');
                string template = w.SkillDesc;
                for (int i = 0; i < vals.Length; i++)
                    template = template.Replace($"{{{i}}}", vals[i]);
                desc = template;
            }
        }
        txt_SkillDesc.text = desc;

        // 副属性: 7=攻击力 8=暴击率
        int st = w.SubStatType;
        string subName = st switch { 7 => "攻击", 8 => "暴击率", _ => "" };
        txt_SubStatTypeName.text = subName;
        txt_SubStatTypeValue.text = stats.WeaponSubStat.ToString("F1") + "%";
    }
}
