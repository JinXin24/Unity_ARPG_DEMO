using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面 HUD — 功能入口按钮栏。
/// </summary>
public class MainPanel : MonoBehaviour
{
    [Header("功能按钮")]
    [SerializeField] private Button inventoryButton;

    void Start()
    {
        Debug.Log($"[MainPanel] Start, inventoryButton={(inventoryButton != null ? inventoryButton.name : "NULL")}");

        if (inventoryButton != null)
        {
            inventoryButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainPanel] 背包按钮被点击");
                UIManager.Instance.ToggleInventory();
            });
            Debug.Log("[MainPanel] 背包按钮监听已注册");
        }
        else
        {
            Debug.LogWarning("[MainPanel] inventoryButton 没赋值！请在 Inspector 把 InventoryButton 拖到 MainPanel 的 inventoryButton 槽位");
        }
    }
}
