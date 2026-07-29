using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色仓库 — 管理已获得的角色、等级、装备武器。
/// </summary>
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [System.Serializable]
    public class Slot
    {
        public int CharacterId;
        public int Level = 1;
        public int WeaponId;
        public int WeaponLevel = 1;
        public int WeaponRefine = 1;  // 1~5
    }

    public List<Slot> OwnedCharacters = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 角色 → 专武映射
    private static readonly Dictionary<int, int> ExclusiveWeaponMap = new()
    {
        { 1001, 1001 },  // 爱弥斯 → 永远的启明星
    };

    /// <summary>添加角色，默认带专武</summary>
    public void AddCharacter(int characterId, int level = 1, int weaponId = 0)
    {
        if (OwnedCharacters.Exists(c => c.CharacterId == characterId)) return;
        if (weaponId == 0 && ExclusiveWeaponMap.TryGetValue(characterId, out int defaultWeapon))
            weaponId = defaultWeapon;
        OwnedCharacters.Add(new Slot { CharacterId = characterId, Level = level, WeaponId = weaponId });
    }

 


    /// <summary>获取角色数值快照</summary>
    public CharacterStats GetStats(int characterId)
    {
        var slot = OwnedCharacters.Find(c => c.CharacterId == characterId);
        if (slot == null) return null;
        var attr = ConfigManager.Instance.GetCharacterAttr(slot.CharacterId);
        if (attr == null) return null;
        var growth = ConfigManager.Instance.GetCharacterGrowth(slot.CharacterId);
        var weapon = ConfigManager.Instance.GetWeapon(slot.WeaponId);
        return CharacterStats.Build(attr, slot.Level, growth, weapon, slot.WeaponLevel);
    }

    /// <summary>设置武器精炼阶数</summary>
    public void SetWeaponRefine(int characterId, int rank)
    {
        var slot = OwnedCharacters.Find(c => c.CharacterId == characterId);
        if (slot != null) slot.WeaponRefine = Mathf.Clamp(rank, 1, 5);
    }

    /// <summary>设置武器等级</summary>
    public void SetWeaponLevel(int characterId, int level)
    {
        var slot = OwnedCharacters.Find(c => c.CharacterId == characterId);
        if (slot != null) slot.WeaponLevel = Mathf.Clamp(level, 1, 90);
    }

    /// <summary>给角色换武器</summary>
    public void EquipWeapon(int characterId, int weaponId)
    {
        var slot = OwnedCharacters.Find(c => c.CharacterId == characterId);
        if (slot != null) slot.WeaponId = weaponId;
    }
}
