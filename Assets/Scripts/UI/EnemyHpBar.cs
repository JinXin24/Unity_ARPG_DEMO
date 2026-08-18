using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JinXinFramework.Event;

/// <summary>
/// 敌人头顶血条 — 挂血条根物体，拖填充 Image。
/// 通过改 anchorMax.x 控制宽度，不依赖 Image Type=Filled，血量低可自动变色。
/// 订阅所属敌人的 DamageEvent 自动刷新（扣血 → 填充变窄 + 数字文本更新），无需外部调用。
///
/// 用法：
///   1. 血条 Canvas（World Space）下建 BG（底图）+ Fill（填充）两个 Image。
///   2. Fill 的 RectTransform 锚点设 Min(0,0)、Max(1,1)，铺满 BG。
///   3. 本脚本挂血条根物体，把 Fill 拖进 fill 槽位。
///   4. 血条 Canvas 必须是敌人根物体的子物体（或拖 owner 指认所属敌人）。
///   5. 可选：建一个 TMP 文本拖进 tmpText，显示 "当前/最大"。
/// </summary>
public class EnemyHpBar : MonoBehaviour, IEventReceiver<DamageEvent>
{
    [Header("引用")]
    [SerializeField] private Image fill;          // 填充条 Image（锚点 Min(0,0)、Max(1,1) 铺满底图）
    [SerializeField] private TMP_Text tmpText;   // 血量数字文本（当前/最大），可选

    [Header("归属（可选）")]
    [SerializeField] private Damageable owner;   // 所属敌人；不拖则从父级自动找

    [Header("低血变色（可选）")]
    [SerializeField] private bool colorByRatio = true;
    [SerializeField] private Color fullColor = new Color(0.35f, 1f, 0.35f);   // 满血绿
    [SerializeField] private Color lowColor = new Color(1f, 0.3f, 0.3f);       // 空血红

    private Damageable cachedTarget;             // 所属敌人缓存，避免每帧 GetComponent

    /// <summary>所属敌人：优先用拖的 owner，否则沿父链找（血条挂敌人子物体时自动匹配）</summary>
    Damageable Target
    {
        get
        {
            if (cachedTarget != null) return cachedTarget;
            cachedTarget = owner != null ? owner : GetComponentInParent<Damageable>();
            return cachedTarget;
        }
    }

    void OnEnable()
    {
        EventBus.Subscribe<DamageEvent>(this);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<DamageEvent>(this);
    }

    void Start()
    {
        var t = Target;
        if (t != null) SetHp(t.Hp, t.MaxHp);   // 初始化：出生满血
    }

    /// <summary>自己所属敌人被打 → 刷新血条（填充宽度 + 数字文本）</summary>
    public void OnEvent(DamageEvent evt)
    {
        if (evt.Target != Target) return;
        SetHp(evt.Target.Hp, evt.Target.MaxHp);
    }

    /// <summary>按比例设血条（0~1，0=空 1=满）</summary>
    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        var a = fill.rectTransform.anchorMax;
        a.x = ratio;                               // 从左往右拉宽
        fill.rectTransform.anchorMax = a;

        if (colorByRatio)
            fill.color = Color.Lerp(lowColor, fullColor, ratio);
    }

    /// <summary>按当前血量 / 上限设血条</summary>
    public void SetHp(int hp, int maxHp)
    {
        SetRatio(maxHp > 0 ? (float)hp / maxHp : 0f);

        if (tmpText != null)
            tmpText.text = $"{hp}/{maxHp}";
    }
}
