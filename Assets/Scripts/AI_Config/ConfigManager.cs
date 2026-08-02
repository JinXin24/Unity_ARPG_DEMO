using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 配置表加载器 — 从 Resources 加载 Excel 生成的 SO。
/// </summary>
public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

    [Header("配置路径（相对于 Resources）")]
    public string configPath = "Config";

    private Dictionary<string, List<ScriptableObject>> cache = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    void LoadAll()
    {
        cache.Clear();
        var allSO = Resources.LoadAll<ScriptableObject>(configPath);
        foreach (var so in allSO)
        {
            string key = so.GetType().Name;
            if (!cache.ContainsKey(key)) cache[key] = new List<ScriptableObject>();
            cache[key].Add(so);

            
        }
    }

    

    public List<T> GetAll<T>() where T : ScriptableObject
    {
        if (!cache.TryGetValue(typeof(T).Name, out var list)) return new List<T>();
        var result = new List<T>();
        foreach (var so in list) if (so is T t) result.Add(t);
        return result;
    }

    T FindById<T>(int id) where T : ScriptableObject
    {
        var fi = typeof(T).GetField("Id");
        if (fi == null) return null;
        foreach (var so in GetAll<T>())
            if (fi.GetValue(so) is int v && v == id) return (T)so;
        return null;
    }

    public WeaponSO GetWeapon(int id) => FindById<WeaponSO>(id);
    public List<WeaponGrowthSO> GetWeaponGrowth(int weaponId) =>
        GetAll<WeaponGrowthSO>().FindAll(g => g.WeaponId == weaponId);
    public CharacterAttrSO GetCharacterAttr(int id) => FindById<CharacterAttrSO>(id);
    public List<LevelGrowthSO> GetCharacterGrowth(int charId) =>
        GetAll<LevelGrowthSO>().FindAll(g => g.CharacterId == charId);
}
