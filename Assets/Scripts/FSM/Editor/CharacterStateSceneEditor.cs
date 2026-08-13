using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 视图手柄编辑器：选中挂 CharacterState 的 GameObject 后，
/// 在 Scene 里拖手柄直接摆命中检测线段/扇形/球体，数值自动写回 StateHitSO。
/// </summary>
[InitializeOnLoad]
public static class HitSegmentSceneTool
{
    static HitSegmentSceneTool()
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
        var hitSOProp = so.FindProperty("hitSO");
        var gizmoProp = so.FindProperty("gizmoPreviewStateId");
        var hitSO = hitSOProp?.objectReferenceValue as StateHitSO;
        if (hitSO == null) return;

        int stateId = Application.isPlaying && cs.CurrentState != null
            ? cs.CurrentState.Id
            : gizmoProp?.intValue ?? 0;

        StateHitData data = null;
        for (int i = 0; i < hitSO.states.Count; i++)
            if (hitSO.states[i].StateId == stateId) { data = hitSO.states[i]; break; }
        if (data == null || data.segments.Count == 0) return;

        Transform t = cs.transform;
        Undo.RecordObject(hitSO, "调整命中段");

        for (int i = 0; i < data.segments.Count; i++)
        {
            var seg = data.segments[i];
            if (!seg.enabled) continue;

            Handles.color = seg.shape == HitShape.Sphere ? Color.green :
                            seg.shape == HitShape.Line   ? Color.cyan : Color.yellow;

            Vector3 worldOffset = t.rotation * seg.offset;
            Vector3 center = t.position + worldOffset;
            Vector3 dir = t.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f) * Vector3.forward;

            if (seg.shape == HitShape.Line)
                DrawLineHandles(seg, center, dir, t, i);
            else if (seg.shape == HitShape.Sector)
                DrawSectorHandles(seg, center, dir, t, i);
            else
                DrawSphereHandles(seg, center, t, i);
        }

        if (GUI.changed)
            EditorUtility.SetDirty(hitSO);
    }

    static void DrawLineHandles(HitSegment seg, Vector3 center, Vector3 dir, Transform t, int idx)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, t.rotation);
        if (EditorGUI.EndChangeCheck())
            seg.offset = Quaternion.Inverse(t.rotation) * (newCenter - t.position);

        Vector3 end = center + dir * seg.lineLength;
        EditorGUI.BeginChangeCheck();
        Vector3 newEnd = Handles.PositionHandle(end, t.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 localDir = Quaternion.Inverse(t.rotation) * (newEnd - newCenter);
            if (localDir.sqrMagnitude > 0.0001f)
            {
                seg.lineLength = localDir.magnitude;
                localDir.Normalize();
                seg.pitchOffset = -Mathf.Asin(localDir.y) * Mathf.Rad2Deg;
                seg.yawOffset = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            }
        }

        Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
        if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(dir, Vector3.right).normalized;
        Vector3 widthPt = center + perp * seg.lineWidth;
        float hSize = HandleUtility.GetHandleSize(widthPt) * 0.2f;
        EditorGUI.BeginChangeCheck();
        Vector3 newWidthPt = Handles.Slider(widthPt, perp, hSize, Handles.SphereHandleCap, 0.1f);
        if (EditorGUI.EndChangeCheck())
            seg.lineWidth = Mathf.Max(0, Vector3.Dot(newWidthPt - center, perp));

        Handles.Label(center + Vector3.up * 0.15f, $"段{idx} 起点", EditorStyles.whiteBoldLabel);
        Handles.Label(end + Vector3.up * 0.15f, $"段{idx} 终点", EditorStyles.whiteBoldLabel);
    }

    static void DrawSectorHandles(HitSegment seg, Vector3 center, Vector3 fwd, Transform t, int idx)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, t.rotation);
        if (EditorGUI.EndChangeCheck())
            seg.offset = Quaternion.Inverse(t.rotation) * (newCenter - t.position);

        Vector3 radiusPt = center + fwd * seg.radius;
        float hSize = HandleUtility.GetHandleSize(radiusPt) * 0.2f;
        EditorGUI.BeginChangeCheck();
        Vector3 newRadiusPt = Handles.Slider(radiusPt, fwd, hSize, Handles.SphereHandleCap, 0.1f);
        if (EditorGUI.EndChangeCheck())
            seg.radius = Mathf.Max(0.1f, Vector3.Dot(newRadiusPt - center, fwd));

        Quaternion rot = t.rotation * Quaternion.Euler(seg.pitchOffset, seg.yawOffset, 0f);
        EditorGUI.BeginChangeCheck();
        Quaternion newRot = Handles.RotationHandle(rot, center);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 localFwd = Quaternion.Inverse(t.rotation) * (newRot * Vector3.forward);
            seg.pitchOffset = -Mathf.Asin(localFwd.y) * Mathf.Rad2Deg;
            seg.yawOffset = Mathf.Atan2(localFwd.x, localFwd.z) * Mathf.Rad2Deg;
        }

        Handles.Label(center + Vector3.up * 0.15f, $"段{idx}", EditorStyles.whiteBoldLabel);
    }

    static void DrawSphereHandles(HitSegment seg, Vector3 center, Transform t, int idx)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, t.rotation);
        if (EditorGUI.EndChangeCheck())
            seg.offset = Quaternion.Inverse(t.rotation) * (newCenter - t.position);

        Vector3 radiusPt = center + Vector3.forward * seg.radius;
        float hSize = HandleUtility.GetHandleSize(radiusPt) * 0.2f;
        EditorGUI.BeginChangeCheck();
        Vector3 newRadiusPt = Handles.Slider(radiusPt, Vector3.forward, hSize, Handles.SphereHandleCap, 0.1f);
        if (EditorGUI.EndChangeCheck())
            seg.radius = Mathf.Max(0.1f, Vector3.Distance(center, newRadiusPt));

        Handles.Label(center + Vector3.up * 0.15f, $"段{idx}", EditorStyles.whiteBoldLabel);
    }
}
