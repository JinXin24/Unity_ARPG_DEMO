using UnityEngine;

/// <summary>
/// 测试木桩：实现 IDamageable 的最小示例，用来验证命中检测。
/// 命中 → 扣血 + 闪红 + 日志；血尽 → 销毁。
/// 仅测试用，正式敌人以后再写。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TestDummy : MonoBehaviour, IDamageable
{
    [Header("── 测试参数 ──")]
    [SerializeField] private int maxHp = 1000;

    private int hp;
    private Renderer[] renderers;
    private Material[] originalMats;

    void Awake()
    {
        hp = maxHp;
        renderers = GetComponentsInChildren<Renderer>();
        // 存原始材质，闪红后恢复
        originalMats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                originalMats[i] = renderers[i].sharedMaterial;
    }

    public void TakeDamage(DamageInfo info)
    {
        hp -= info.Damage;
        Debug.Log($"[木桩] {name} 受击 {info.Damage}，剩余 {hp}/{maxHp}");
        FlashRed();

        if (hp <= 0)
        {
            Debug.Log($"[木桩] {name} 被击倒");
            Destroy(gameObject, 0.3f);
        }
    }

    /// <summary>受击闪红 0.15 秒后恢复（测试简化实现）</summary>
    void FlashRed()
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.material.color = Color.red; // material 会实例化，测试够用
        }
        CancelInvoke(nameof(RestoreColor));
        Invoke(nameof(RestoreColor), 0.15f);
    }

    void RestoreColor()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || originalMats[i] == null) continue;
            renderers[i].sharedMaterial = originalMats[i];
        }
    }
}
