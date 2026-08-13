using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 技能充能环：底层灰环（未充能部分）+ 上层红环（充能进度，Filled+Radial 驱动）。
/// 两种用法：
///   1. 自动充能：填 chargeSpeed，跑起来自动从 0 充到 100%
///   2. 外部驱动：调 SetFill(0~1) / AddFill(amount)，由业务代码控制
/// </summary>
public class SkillChargeRing : MonoBehaviour
{
    [Header("── 引用 ──")]
    [Tooltip("底层灰环（未充能部分）")]
    [SerializeField] private Image bgRing;
    [Tooltip("上层红环（充能进度）")]
    [SerializeField] private Image fillRing;

    [Header("── 充能参数 ──")]
    [Tooltip("自动充能速度（/秒），0 = 不自动充能")]
    [SerializeField] private float chargeSpeed = 0.2f;
    [Tooltip("充能满后是否保持")]
    [SerializeField] private bool holdWhenFull = true;

    // ---- 运行时 ----
    private float fill;      // 0~1 当前充能
    private bool charging;   // 是否在充能
    public bool IsFull => fill >= 1f;

    /// <summary>充能满事件（外部订阅，如解锁技能）</summary>
    public event Action OnCharged;

    void Awake()
    {
        if (fillRing == null)
            fillRing = GetComponent<Image>();
    }

    void OnValidate()
    {
        if (fillRing != null)
            fillRing.type = Image.Type.Filled;
        if (fillRing != null)
            fillRing.fillMethod = Image.FillMethod.Radial360;
    }

    void Update()
    {
        if (!charging) return;
        if (chargeSpeed <= 0f) return;

        fill = Mathf.Clamp01(fill + chargeSpeed * Time.deltaTime);
        ApplyFill();

        if (fill >= 1f)
        {
            charging = false;
            if (!holdWhenFull)
                SetFill(0f);
            OnCharged?.Invoke();
        }
    }

    /// <summary>开始自动充能（从当前值继续充）</summary>
    public void StartCharge()
    {
        charging = true;
    }

    /// <summary>停止充能（保留当前进度）</summary>
    public void StopCharge()
    {
        charging = false;
    }

    /// <summary>直接设置充能进度 0~1</summary>
    public void SetFill(float value)
    {
        fill = Mathf.Clamp01(value);
        ApplyFill();
    }

    /// <summary>增加充能进度（外部驱动，如命中目标加能量）</summary>
    public void AddFill(float amount)
    {
        fill = Mathf.Clamp01(fill + amount);
        ApplyFill();
        if (fill >= 1f)
            OnCharged?.Invoke();
    }

    /// <summary>清空充能</summary>
    public void ResetRing()
    {
        fill = 0f;
        charging = false;
        ApplyFill();
    }

    void ApplyFill()
    {
        if (fillRing != null)
            fillRing.fillAmount = fill;
    }
}
