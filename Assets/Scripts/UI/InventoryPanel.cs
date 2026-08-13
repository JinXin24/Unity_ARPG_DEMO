using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包面板 — 在 6列×N行 网格里展示背包物品。
/// 挂 InventoryPanel 根物体上，OnEnable 自动刷新。
/// 顶部页签按钮切换物品类型筛选。
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    [Header("页签")]
    [SerializeField] private Button btn_Echo;      // 声骸
    [SerializeField] private Button btn_Weapon;    // 武器
    [SerializeField] private Button btn_Material;  // 材料
    [SerializeField] private Button btn_Consumable; // 消耗品

    [Header("格子")]
    [Tooltip("Content（挂 GridLayoutGroup 的容器）")]
    [SerializeField] private Transform slotRoot;
    [Tooltip("格子预制体，挂 InventorySlot 组件")]
    [SerializeField] private InventorySlot slotPrefab;

    [Header("数据")]
    [Tooltip("当前显示的物品类型")]
    [SerializeField] private ItemType filter = ItemType.Echo;

    /// <summary>筛选切换回调（页签高亮等 UI 逻辑可挂这里）</summary>
    public System.Action<ItemType> OnFilterChanged;

    private readonly List<InventorySlot> slots = new();

    void Start()
    {
        btn_Echo?.onClick.AddListener(() => SetFilter(ItemType.Echo));
        btn_Weapon?.onClick.AddListener(() => SetFilter(ItemType.Weapon));
        btn_Material?.onClick.AddListener(() => SetFilter(ItemType.Material));
        btn_Consumable?.onClick.AddListener(() => SetFilter(ItemType.Consumable));
    }

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>切换到指定物品类型并刷新</summary>
    public void SetFilter(ItemType type)
    {
        if (filter == type) return;
        filter = type;
        Refresh();
        OnFilterChanged?.Invoke(type);
    }

    /// <summary>从 InventoryManager 拉数据，重建所有格子</summary>
    public void Refresh()
    {
        var inv = InventoryManager.Instance;
        if (inv == null || slotRoot == null || slotPrefab == null) return;

        // 清空旧格子
        foreach (var s in slots)
            if (s != null) Destroy(s.gameObject);
        slots.Clear();

        var items = inv.GetByType(filter);

        // 有几件显示几件，不补空格子
        foreach (var item in items)
        {
            var slot = Instantiate(slotPrefab, slotRoot);
            slots.Add(slot);
            slot.Setup(item);
        }
    }
}
