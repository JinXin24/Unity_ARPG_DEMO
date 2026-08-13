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
        float t = Owner.GetNormalizedTime();
        float clipLen = Owner.GetClipLength();

        foreach (var w in data.weapons)
        {
            if (!w.enabled) continue;
            var tr = string.IsNullOrEmpty(w.weaponPath) ? null : Owner.transform.Find(w.weaponPath);
            if (tr == null) continue;

            float showNorm = w.showSec / clipLen;
            float hideNorm = w.hideSec / clipLen;

            if (t >= showNorm && t < hideNorm)
                tr.gameObject.SetActive(true);
            else if (t >= hideNorm)
                tr.gameObject.SetActive(false);
        }
    }
}
