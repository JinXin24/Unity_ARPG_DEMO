using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 角色面板 UI — 管理 Attr/Weapon 等子页签切换
/// </summary>
public class CharacterPanel : MonoBehaviour
{
    [SerializeField] private Button btn_Attr;
    [SerializeField] private Button btn_Weapon;
    [SerializeField] private AttrPanel attrPanel;
    [SerializeField] private WeaponPanel weaponPanel;
    [SerializeField] private Button btn_CharacterPrefab;
    [SerializeField] private Transform characterButtonRoot;
    [SerializeField] private List<Button> btn_Characters;

    

    public CharacterManager.Slot CurrentSlot { get; private set; }


    void Start()
    {
        btn_Attr?.onClick.AddListener(ShowAttr);
        btn_Weapon?.onClick.AddListener(ShowWeapon);
        BuildCharacterButtons();
    }


    void BuildCharacterButtons()
    {
        if (btn_CharacterPrefab == null || characterButtonRoot == null) return;

        foreach (var b in btn_Characters)
            if (b != null) Destroy(b.gameObject);
        btn_Characters.Clear();

        var manager = CharacterManager.Instance;
        if (manager == null) return;

        foreach (var slot in manager.OwnedCharacters)
        {
            var btn = Instantiate(btn_CharacterPrefab, characterButtonRoot);
            var name = ConfigManager.Instance.GetCharacterAttr(slot.CharacterId)?.Name ?? $"ID:{slot.CharacterId}";
            var txt = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt != null) txt.text = name;
            btn.onClick.AddListener(() => Show(slot));
            btn_Characters.Add(btn);
        }
    }

    public void Show(CharacterManager.Slot slot)
    {
        CurrentSlot = slot;
        ShowAttr();
    }

    void ShowAttr()
    {
        if (weaponPanel != null) weaponPanel.gameObject.SetActive(false);
        if (attrPanel != null) attrPanel.gameObject.SetActive(true);
        if (attrPanel != null && CurrentSlot != null)
            attrPanel.Refresh(CurrentSlot);
    }

    void ShowWeapon()
    {
        if (attrPanel != null) attrPanel.gameObject.SetActive(false);
        if (weaponPanel != null) weaponPanel.gameObject.SetActive(true);
        if (weaponPanel != null && CurrentSlot != null)
            weaponPanel.Refresh(CurrentSlot);
    }
}
