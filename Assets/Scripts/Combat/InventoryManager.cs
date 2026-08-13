using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 统一背包管理器 — 挂场景单例。
/// 管理所有物品（声骸/武器/材料/消耗品），Inspector 可直接观察 items 列表。
/// 声骸配置从 ExcelToSO 生成的 List SO 自动加载（Resources/Config/）。
/// </summary>
public class InventoryManager : Singleton<InventoryManager>
{
    [Header("物品列表")]
    [SerializeField] private List<ItemInstance> items = new();

    // ==================== 配置缓存（从 List SO 加载） ====================

    [Header("已加载配置（只读，自动从 List SO 填充）")]
    [SerializeField] private List<EchobaseSO> loadedEchoBases = new();
    [SerializeField] private List<MainstatpoolSO> loadedMainStatPools = new();
    [SerializeField] private List<SubstatpoolSO> loadedSubStatPools = new();
    [SerializeField] private List<StatcurveSO> loadedStatCurves = new();

    private Dictionary<int, EchobaseSO> echoBaseDict = new();
    private Dictionary<int, List<MainstatpoolSO>> mainStatPoolDict = new();
    private Dictionary<int, List<SubstatpoolSO>> subStatPoolDict = new();
    private Dictionary<(int curveId, int level), StatcurveSO> statCurveDict = new();
    private bool configsLoaded;

    protected override void Awake()
    {
        base.Awake();
        LoadConfigs();
    }

    void LoadConfigs()
    {
        echoBaseDict.Clear();
        mainStatPoolDict.Clear();
        subStatPoolDict.Clear();
        statCurveDict.Clear();

        // EchoBase
        var echoList = Resources.Load<EchobaseSOList>("Config/EchobaseSOList/EchobaseSOList");
        if (echoList != null)
        {
            loadedEchoBases = new List<EchobaseSO>(echoList.list);
            foreach (var e in echoList.list)
                echoBaseDict[e.EchoId] = e;
        }

        // MainStatPool
        var mainList = Resources.Load<MainstatpoolSOList>("Config/MainstatpoolSOList/MainstatpoolSOList");
        if (mainList != null)
        {
            loadedMainStatPools = new List<MainstatpoolSO>(mainList.list);
            foreach (var m in mainList.list)
            {
                if (!mainStatPoolDict.ContainsKey(m.PoolId))
                    mainStatPoolDict[m.PoolId] = new List<MainstatpoolSO>();
                mainStatPoolDict[m.PoolId].Add(m);
            }
        }

        // SubStatPool
        var subList = Resources.Load<SubstatpoolSOList>("Config/SubstatpoolSOList/SubstatpoolSOList");
        if (subList != null)
        {
            loadedSubStatPools = new List<SubstatpoolSO>(subList.list);
            foreach (var s in subList.list)
            {
                if (!subStatPoolDict.ContainsKey(s.PoolId))
                    subStatPoolDict[s.PoolId] = new List<SubstatpoolSO>();
                subStatPoolDict[s.PoolId].Add(s);
            }
        }

        // StatCurve
        var curveList = Resources.Load<StatcurveSOList>("Config/StatcurveSOList/StatcurveSOList");
        if (curveList != null)
        {
            loadedStatCurves = new List<StatcurveSO>(curveList.list);
            foreach (var c in curveList.list)
                statCurveDict[(c.CurveId, c.Level)] = c;
        }

        configsLoaded = echoBaseDict.Count > 0;
        Debug.Log($"[Inventory] 配置加载: {echoBaseDict.Count}声骸 {mainStatPoolDict.Count}池(主) {subStatPoolDict.Count}池(副) {statCurveDict.Count}曲线");
    }

    // ==================== 通用物品操作 ====================

    public void AddItem(ItemInstance item)
    {
        if (item == null) return;
        items.Add(item);
    }

    public void RemoveItem(ItemInstance item) => items.Remove(item);

    public List<ItemInstance> GetByType(ItemType type)
        => items.Where(i => i.itemType == type).ToList();

    public ItemInstance Find(int itemId)
        => items.Find(i => i.itemId == itemId);

    /// <summary>获取所有声骸</summary>
    public List<EchoInstance> GetAllEchoes()
        => GetByType(ItemType.Echo).Select(i => i as EchoInstance).Where(e => e != null).ToList();

    /// <summary>按 EchoId 查声骸配置（UI 显示用）</summary>
    public EchobaseSO GetEchoBase(int echoId)
        => echoBaseDict.TryGetValue(echoId, out var cfg) ? cfg : null;

    // ==================== 声骸业务 ====================

    /// <summary>获取声骸 — 按 EchoId 查表，随机主词条 + 副词条，加入背包</summary>
    public EchoInstance AcquireEcho(int echoId, int level = 1)
    {
        if (!echoBaseDict.TryGetValue(echoId, out var baseCfg))
        {
            Debug.LogWarning($"[Inventory] 找不到声骸配置 EchoId={echoId}");
            return null;
        }

        var echo = new EchoInstance
        {
            itemId = echoId,
            cost = baseCfg.Cost,
            level = level,
        };

        echo.mainStat = RollMainStat(baseCfg, level);
        echo.subStats = RollSubStats(baseCfg);

        items.Add(echo);
        Debug.Log($"[Inventory] 获得声骸: {echo.DisplayName}");
        return echo;
    }

    /// <summary>强化声骸 — 每 5 级触发副词条操作</summary>
    public void EnhanceEcho(EchoInstance echo, int addLevels = 5)
    {
        if (echo == null) return;
        if (!echoBaseDict.TryGetValue(echo.itemId, out var baseCfg)) return;

        echo.level += addLevels;

        // 刷新主词条值
        var newMain = RollMainStat(baseCfg, echo.level);
        echo.mainStat.value = newMain.value;

        // 副词条操作
        if (echo.subStats.Count < baseCfg.MaxSubCount)
        {
            // 未满 → 新增一条（去重）
            var existingTypes = new HashSet<StatType>(echo.subStats.Select(s => s.type));
            var newSubs = RollSubStats(baseCfg, 1, existingTypes);
            if (newSubs.Count > 0) echo.subStats.AddRange(newSubs);
        }
        else if (echo.subStats.Count > 0)
        {
            // 已满 → 随机选一条升档 +5%
            int idx = Random.Range(0, echo.subStats.Count);
            var sub = echo.subStats[idx];
            sub.rollQuality = Mathf.Min(sub.rollQuality + 1, 4);
            sub.value *= 1.05f;
        }

        Debug.Log($"[Inventory] 强化声骸 → Lv.{echo.level}: {echo.DisplayName}");
    }

    // ==================== 随机逻辑 ====================

    EchoMainStat RollMainStat(EchobaseSO baseCfg, int level)
    {
        if (!mainStatPoolDict.TryGetValue(baseCfg.MainStatPoolId, out var pool) || pool.Count == 0)
            return new EchoMainStat { type = StatType.ATKPercent, value = 6f };

        var picked = WeightedPick(pool, e => e.Weight);
        var statType = ParseStatType(picked.StatType);
        float value = GetCurveValue(picked.CurveId, level);

        return new EchoMainStat { type = statType, value = value };
    }

    /// <summary>随机 N 条副词条（去重）</summary>
    List<EchoSubStat> RollSubStats(EchobaseSO baseCfg, int? countOverride = null, HashSet<StatType> exclude = null)
    {
        var result = new List<EchoSubStat>();
        if (!subStatPoolDict.TryGetValue(baseCfg.SubStatPoolId, out var pool) || pool.Count == 0)
            return result;

        int count = countOverride ?? Random.Range(baseCfg.InitialSubMin, baseCfg.InitialSubMax + 1);
        var used = new HashSet<StatType>(exclude ?? new HashSet<StatType>());

        for (int i = 0; i < count; i++)
        {
            var available = pool.Where(s => !used.Contains(ParseStatType(s.StatType))).ToList();
            if (available.Count == 0) break;

            var picked = WeightedPick(available, e => e.Weight);
            var statType = ParseStatType(picked.StatType);
            used.Add(statType);

            int tier = RollTier(picked);
            float val = tier switch
            {
                1 => Random.Range(picked.RollMin1, picked.RollMax1),
                2 => Random.Range(picked.RollMin2, picked.RollMax2),
                3 => Random.Range(picked.RollMin3, picked.RollMax3),
                4 => Random.Range(picked.RollMin4, picked.RollMax4),
                _ => 0,
            };

            result.Add(new EchoSubStat { type = statType, rollQuality = tier, value = val });
        }
        return result;
    }

    /// <summary>4 档权重随机：1~4 档，权重来自 SubstatpoolSO.RollWeight1~4</summary>
    int RollTier(SubstatpoolSO sub)
    {
        int[] w = { sub.RollWeight1, sub.RollWeight2, sub.RollWeight3, sub.RollWeight4 };
        int total = w.Sum();
        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < 4; i++)
        {
            acc += w[i];
            if (roll < acc) return i + 1;
        }
        return 1;
    }

    /// <summary>从 StatCurve 表查成长值（CurveId + Level → 数值）</summary>
    float GetCurveValue(int curveId, int level)
    {
        level = Mathf.Clamp(level, 1, 25);
        if (!statCurveDict.TryGetValue((curveId, level), out var curve))
            return 6f;

        // 4 种曲线类型，只有一种非零
        if (curve.PercentA != 0) return curve.PercentA;
        if (curve.CritRate != 0) return curve.CritRate;
        if (curve.CritDMG != 0) return curve.CritDMG;
        if (curve.FlatStat != 0) return curve.FlatStat;
        return 0;
    }

    /// <summary>字符串 → StatType 枚举</summary>
    static StatType ParseStatType(string s)
    {
        if (string.IsNullOrEmpty(s)) return StatType.ATK;
        if (Enum.TryParse<StatType>(s, out var r)) return r;
        return StatType.ATK;
    }

    T WeightedPick<T>(List<T> list, Func<T, int> weightSelector)
    {
        int total = list.Sum(weightSelector);
        int roll = Random.Range(0, total);
        int acc = 0;
        foreach (var item in list)
        {
            acc += weightSelector(item);
            if (roll < acc) return item;
        }
        return list[0];
    }

    // ==================== Editor 调试 ====================

#if UNITY_EDITOR
    [Header("Editor 调试")]
    [SerializeField] private int debugCost = 4;
    [SerializeField] private int debugLevel = 1;

    [ContextMenu("随机获取声骸")]
    public void EditorAcquireEcho()
    {
        if (!configsLoaded || echoBaseDict.Count == 0)
        {
            Debug.LogWarning($"[Inventory] 配置未加载（需先用 ExcelToSO 导出 List SO 到 Resources/Config/），生成随机测试声骸");
            GenerateTestEcho(debugCost, debugLevel);
            return;
        }
        var candidates = echoBaseDict.Values.Where(e => e.Cost == debugCost).ToList();
        if (candidates.Count == 0)
        {
            GenerateTestEcho(debugCost, debugLevel);
            return;
        }
        var cfg = candidates[Random.Range(0, candidates.Count)];
        AcquireEcho(cfg.EchoId, debugLevel);
    }

    [ContextMenu("清空背包")]
    public void EditorClear()
    {
        items.Clear();
        Debug.Log("[Inventory] 背包已清空");
    }

    [ContextMenu("打印背包")]
    public void EditorPrint()
    {
        Debug.Log($"=== 背包 {items.Count} 件 ===");
        foreach (var item in items)
        {
            if (item is EchoInstance e)
                Debug.Log($"  [{e.cost}费] {e.DisplayName}");
            else
                Debug.Log($"  [{item.itemType}] id={item.itemId} x{item.quantity}");
        }
    }

    /// <summary>没有配置时生成纯随机测试声骸</summary>
    void GenerateTestEcho(int cost, int level)
    {
        var mainPool = cost switch
        {
            4 => new[] { StatType.ATKPercent, StatType.DEFPercent, StatType.HPPercent, StatType.CritRate, StatType.CritDMG, StatType.HealingBonus },
            3 => new[] { StatType.ATKPercent, StatType.DEFPercent, StatType.HPPercent, StatType.FireDMG, StatType.IceDMG, StatType.ThunderDMG, StatType.WindDMG, StatType.LightDMG, StatType.DarkDMG },
            _ => new[] { StatType.ATK, StatType.DEF, StatType.HP },
        };

        var subPool = new[] { StatType.ATKPercent, StatType.HPPercent, StatType.DEFPercent, StatType.CritRate, StatType.CritDMG, StatType.ATK, StatType.HP, StatType.EnergyRegen };

        var echo = new EchoInstance
        {
            itemId = Random.Range(10000, 99999),
            cost = cost,
            level = level,
            mainStat = new EchoMainStat
            {
                type = mainPool[Random.Range(0, mainPool.Length)],
                value = Random.Range(5f, 10f),
            },
        };

        int subCount = Random.Range(2, 4);
        var used = new HashSet<StatType>();
        for (int i = 0; i < subCount; i++)
        {
            var candidates = subPool.Where(s => !used.Contains(s)).ToArray();
            if (candidates.Length == 0) break;
            var picked = candidates[Random.Range(0, candidates.Length)];
            used.Add(picked);
            echo.subStats.Add(new EchoSubStat
            {
                type = picked,
                rollQuality = Random.Range(1, 5),
                value = Random.Range(5f, 11f),
            });
        }

        items.Add(echo);
        Debug.Log($"[Inventory] 测试声骸: {echo.DisplayName}");
    }
#endif
}
