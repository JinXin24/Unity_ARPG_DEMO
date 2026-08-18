using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 视图手柄：选中挂 CharacterState 的角色后，读取其 weaponThrowSO（StateWeaponThrowSO），
/// 按调试状态 ID 找到对应投掷段，在 Scene 里直接拖手柄摆"武器丢出去的悬停点"，
/// 数值写回 SO 的 hoverOffset。和 HitSegmentSceneTool 同一套做法：
/// Selection.activeGameObject → CharacterState → SO 字段 → 读写。
/// </summary>
[InitializeOnLoad]
public static class WeaponThrowSceneTool
{
    static WeaponThrowSceneTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        var go = Selection.activeGameObject;
        if (go == null) return;
        var cs = go.GetComponent<CharacterState>();
        if (cs == null) return;

        SerializedObject so = new SerializedObject(cs);
        var throwSOProp = so.FindProperty("weaponThrowSO");
        var previewProp = so.FindProperty("weaponThrowPreviewStateId");
        var throwSO = throwSOProp?.objectReferenceValue as StateWeaponThrowSO;
        if (throwSO == null) return;

        int stateId = Application.isPlaying && cs.CurrentState != null
            ? cs.CurrentState.Id
            : previewProp?.intValue ?? 0;

        StateWeaponThrowData data = null;
        for (int i = 0; i < throwSO.states.Count; i++)
            if (throwSO.states[i].StateId == stateId) { data = throwSO.states[i]; break; }
        if (data == null || data.throws.Count == 0) return;

        Transform t = cs.transform;

        for (int i = 0; i < data.throws.Count; i++)
        {
            var cfg = data.throws[i];
            if (!cfg.enabled) continue;

            // 悬停点世界坐标 = 角色根本地偏移转世界（自动带角色缩放）
            Vector3 hoverWorld = t.TransformPoint(cfg.hoverOffset);

            // 起抛点：优先用武器当前所在（手），找不到就用角色根
            Transform weaponTr = string.IsNullOrEmpty(cfg.weaponPath)
                ? null : t.Find(cfg.weaponPath);
            Vector3 from = weaponTr != null ? weaponTr.position : t.position;

            // 手 → 悬停点 的虚线（可视化丢出去的方向和距离）
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(from, hoverWorld, 4f);

            // 悬停点标记 + 地面投影圈
            Handles.DrawWireDisc(hoverWorld, Vector3.up, 0.2f);
            Handles.SphereHandleCap(0, hoverWorld, t.rotation,
                HandleUtility.GetHandleSize(hoverWorld) * 0.08f, EventType.Repaint);

            // 命中半径圈（可选，detectHit 开启时画，直观看出自转期扫一圈的范围）
            if (cfg.detectHit)
            {
                Handles.color = Color.red;
                Handles.DrawWireDisc(hoverWorld, Vector3.up, cfg.hitRadius);
                Handles.color = Color.yellow;
            }

            // 位置手柄：拖它改 hoverOffset（写回本地偏移）
            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(hoverWorld, t.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(throwSO, "调整投掷悬停点");
                cfg.hoverOffset = t.InverseTransformPoint(newWorld);
                EditorUtility.SetDirty(throwSO);
            }

            Handles.Label(hoverWorld + Vector3.up * 0.15f,
                $"投掷{i} 悬停点 ({cfg.hoverOffset.x:F2}, {cfg.hoverOffset.y:F2}, {cfg.hoverOffset.z:F2})",
                EditorStyles.whiteBoldLabel);
        }
    }
}
