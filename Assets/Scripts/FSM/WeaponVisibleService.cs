using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 武器显隐服务：按 StateWeaponSO 配置，状态进入时隐藏该状态的所有武器，
/// 动画播放到 [showSec, hideSec) 窗口内显示对应武器（播完自动隐藏）。
/// 从 CharacterState 剥离出的独立服务，配置方式和原来完全一致。
/// </summary>
public class WeaponVisibleService : FSMServiceBase
{
    private StateWeaponSO weaponSO;
    private Dictionary<int, StateWeaponData> weaponDict;

    // 当前状态已显示（露出）的武器，状态被切走时统一隐藏，防止残留到下一个状态
    private readonly List<Transform> shownThisState = new();

    /// <summary>传入武器显隐配置 SO（Inspector 里 CharacterState 拖的那个）</summary>
    public WeaponVisibleService(StateWeaponSO so)
    {
        weaponSO = so;
        if (weaponSO != null)
            weaponDict = weaponSO.states.Where(s => s.weapons.Count > 0)
                .ToDictionary(s => s.StateId);
    }

    public override void Init()
    {
        if (weaponDict == null)
        {
            Debug.Log("[WeaponVisibleService] 未拖入 StateWeaponSO，武器显隐不生效");
            return;
        }
        Debug.Log($"[WeaponVisibleService] 已加载 {weaponDict.Count} 个武器配置: {string.Join(", ", weaponDict.Keys)}");
    }

    public override void OnBegin()
    {
        // 切到新状态：先把上一状态所有暴露过的武器无条件隐藏（不管 hideSec 到没到），杜绝残留
        HideAllShown();

        if (weaponDict == null || !weaponDict.TryGetValue(Owner.CurrentState.Id, out var data)) return;
        foreach (var w in data.weapons)
        {
            var t = string.IsNullOrEmpty(w.weaponPath) ? null : Owner.transform.Find(w.weaponPath);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    public override void OnUpdate()
    {
        if (weaponDict == null || !weaponDict.TryGetValue(Owner.CurrentState.Id, out var data)) return;
        float elapsed = Owner.GetStateElapsed();   // 进入状态后的流逝秒（showSec/hideSec 是秒，直接比）

        foreach (var w in data.weapons)
        {
            if (!w.enabled) continue;
            var tr = string.IsNullOrEmpty(w.weaponPath) ? null : Owner.transform.Find(w.weaponPath);
            if (tr == null) continue;

            // 掐秒逻辑保留：窗口内显示，到 hideSec 隐藏
            if (elapsed >= w.showSec && elapsed < w.hideSec)
            {
                if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
                if (!shownThisState.Contains(tr)) shownThisState.Add(tr);
            }
            else if (elapsed >= w.hideSec)
            {
                tr.gameObject.SetActive(false);
                shownThisState.Remove(tr);
            }
        }
    }

    /// <summary>状态被切走兜底：隐藏所有当前状态已显示、还没到 hideSec 的武器</summary>
    void HideAllShown()
    {
        foreach (var t in shownThisState)
            if (t != null) t.gameObject.SetActive(false);
        shownThisState.Clear();
    }
}
