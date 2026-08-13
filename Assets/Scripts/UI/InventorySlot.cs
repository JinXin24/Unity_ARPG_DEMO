using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包格子 — 显示单个物品。挂 slot 预制体根物体上。
/// Setup(null) 显示空格子。
/// 注意：本组件只改 sprite，不碰 Image 的 enabled，组件开合由预制体决定。
/// </summary>
public class InventorySlot : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] private Image iconImage;     // 图标（可选）
    [SerializeField] private TMP_Text nameText;   // 名称（可选）
    [SerializeField] private TMP_Text levelText;  // 等级/数量（可选）

    /// <summary>填充格子数据，null = 空格子</summary>
    public void Setup(ItemInstance item)
    {
        if (item == null)
        {
            if (iconImage != null) iconImage.sprite = null;
            if (nameText != null) nameText.text = "";
            if (levelText != null) levelText.text = "";
            return;
        }

        // 统一按 itemId 查声骸配置（手工加的 ItemInstance 也能显示名字和图标）
        var cfg = InventoryManager.Instance?.GetEchoBase(item.itemId);

        if (item is EchoInstance echo)
        {
            if (nameText != null) nameText.text = cfg?.Name ?? $"声骸 {echo.itemId}";
            if (levelText != null) levelText.text = $"Lv.{echo.level}";
            SetIcon(cfg?.Icon);
            return;
        }

        // 其他类型：能查到配置就按配置显示，否则兜底 id
        if (nameText != null) nameText.text = cfg?.Name ?? item.itemId.ToString();
        if (levelText != null) levelText.text = $"x{item.quantity}";
        SetIcon(cfg?.Icon);
    }

    /// <summary>设置图标；加载不到就清空 sprite，不关闭 Image 组件</summary>
    void SetIcon(string iconPath)
    {
        if (iconImage == null) return;

        if (string.IsNullOrEmpty(iconPath))
        {
            iconImage.sprite = null;
            return;
        }

        var sprite = Resources.Load<Sprite>(iconPath);

        // 图标还没导入为 Sprite 时回退：Texture2D → 运行时 Sprite.Create
        if (sprite == null)
        {
            var tex = Resources.Load<Texture2D>(iconPath);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                sprite.name = tex.name;
            }
        }

        iconImage.sprite = sprite;  // 为 null 时自然不显示，组件保持开启
    }
}
