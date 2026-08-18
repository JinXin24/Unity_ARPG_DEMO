using System.Collections.Generic;
using UnityEngine;
using JinXinFramework.Event;

/// <summary>
/// UI 总管 — 挂 Canvas 上，管理所有面板的开关。
/// </summary>
public class UIManager : Singleton<UIManager>
{
    [Header("面板")]
    [SerializeField] private MainPanel mainPanel;
    [SerializeField] private GameObject inventoryPanel;
    [Tooltip("主界面以外的所有界面，任一打开时输入系统切到 UI 模式（鼠标全程显示）")]
    [SerializeField] private List<GameObject> uiPanels;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log($"[UIManager] Awake, Instance={(Instance != null)}, inventoryPanel={(inventoryPanel != null ? inventoryPanel.name : "NULL")}");
    }



    void Start()
    {
        CloseAll();
    }

    /// <summary>打开/关闭背包</summary>
    public void ToggleInventory()
    {
        Debug.Log($"[UIManager] ToggleInventory called, panel={(inventoryPanel != null ? inventoryPanel.name : "NULL")}");

        if (inventoryPanel == null)
        {
            Debug.LogWarning("[UIManager] inventoryPanel 没赋值！请在 Inspector 把 InventoryPanel 拖到 UIManager 的 inventoryPanel 槽位");
            return;
        }

        bool open = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(open);
        Debug.Log($"[UIManager] InventoryPanel → {(open ? "打开" : "关闭")}");
        RefreshPanelState();
    }

    public void OpenInventory()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
        RefreshPanelState();
    }

    public void CloseInventory()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        RefreshPanelState();
    }

    public bool IsInventoryOpen() => inventoryPanel != null && inventoryPanel.activeSelf;

    public void CloseAll()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (uiPanels != null)
            foreach (var p in uiPanels)
                if (p != null) p.SetActive(false);
        RefreshPanelState();
    }

    /// <summary>
    /// 根据当前面板开合状态发布 UIPanelChangedEvent，驱动输入模式切换。
    /// 任一非主界面打开 → UI 模式；全部关闭 → 探索模式。
    /// </summary>
    void RefreshPanelState()
    {
        bool anyOpen = false;
        if (inventoryPanel != null && inventoryPanel.activeSelf) anyOpen = true;
        if (!anyOpen && uiPanels != null)
        {
            foreach (var p in uiPanels)
            {
                if (p != null && p.activeSelf)
                {
                    anyOpen = true;
                    break;
                }
            }
        }
        EventBus.Publish(new UIPanelChangedEvent(anyOpen));
    }
}

