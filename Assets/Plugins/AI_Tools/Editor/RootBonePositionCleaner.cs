using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generic 动画根骨骼位移清零工具。
/// 解包动画自带位移，此工具扫描动画中所有带 Position 曲线的骨骼，支持单段清零和批量处理。
/// 清零后动画变原地动作，位移由代码接管（见 CombatMovement）。
/// 菜单：Tools → 根骨骼位移清零器
/// </summary>
public class RootBonePositionCleaner : EditorWindow
{
    private AnimationClip targetClip;
    private List<BoneDisplacement> singleScanResult = new List<BoneDisplacement>();
    private Vector2 singleScroll;

    // 缩放动画位移
    private string scaleBonePath;
    private bool scaleX = true, scaleY = false, scaleZ = false;
    private float scaleFactor = 0.5f;

    // 批量
    private List<AnimationClip> batchClipList = new List<AnimationClip>();
    private bool showBatchList = true;
    private Dictionary<AnimationClip, List<BoneDisplacement>> batchScanResult;
    private List<BatchBoneSummary> batchSummary;
    private HashSet<string> selectedBones = new HashSet<string>();
    private Vector2 batchScroll;
    private bool batchScanned;

    [System.Serializable]
    public class BoneDisplacement
    {
        public string path;
        public Vector3 displacement;
        public bool hasDisplacement => displacement.magnitude > 0.0001f;
    }

    public class BatchBoneSummary
    {
        public string bonePath;
        public int clipCount;                                  // 出现在几个 clip 里
        public List<(AnimationClip clip, Vector3 disp)> details = new();
        public float maxDisplacement;
    }

    [MenuItem("Tools/根骨骼位移清零器")]
    public static void ShowWindow()
    {
        var window = GetWindow<RootBonePositionCleaner>("位移清零");
        window.minSize = new Vector2(560, 450);
    }

    void OnGUI()
    {
        // ═══ 单个处理 ═══
        GUILayout.Label("═══ 单个动画处理 ═══", EditorStyles.boldLabel);
        targetClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", targetClip, typeof(AnimationClip), false);

        bool isSubAsset = targetClip != null && AssetDatabase.IsSubAsset(targetClip);
        if (isSubAsset)
        {
            EditorGUILayout.HelpBox("⚠ 在 FBX 内，无法修改。请先 Ctrl+D 提取为 .anim。", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(targetClip == null || isSubAsset);
        if (GUILayout.Button("🔍 扫描所有有位移的骨骼", GUILayout.Height(28)))
            singleScanResult = ScanAllBonesDisplacement(targetClip);
        EditorGUI.EndDisabledGroup();

        if (singleScanResult.Count > 0)
        {
            var withDisp = singleScanResult.Where(b => b.hasDisplacement).ToList();
            EditorGUILayout.HelpBox($"🟡 有位移: {withDisp.Count} 根  |  ⚪ 无位移: {singleScanResult.Count - withDisp.Count} 根", MessageType.Info);

            if (withDisp.Count > 0)
            {
                singleScroll = EditorGUILayout.BeginScrollView(singleScroll, GUILayout.Height(140));
                foreach (var b in withDisp)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(b.path, GUILayout.MinWidth(150));
                    EditorGUILayout.LabelField($"X:{b.displacement.x:F3} Y:{b.displacement.y:F3} Z:{b.displacement.z:F3}  总:{b.displacement.magnitude:F3}m");
                    if (GUILayout.Button("清零", GUILayout.Width(48)))
                    {
                        ZeroOutRootPosition(targetClip, b.path);
                        AssetDatabase.SaveAssets();
                        singleScanResult = ScanAllBonesDisplacement(targetClip);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ═══ 缩放动画位移 ═══
        EditorGUILayout.Space(15);
        GUILayout.Label("═══ 缩放动画位移 ═══", EditorStyles.boldLabel);

        // 骨骼路径下拉选择（从扫描结果里选）
        var boneOptions = singleScanResult.Select(b => b.path).ToArray();
        if (boneOptions.Length == 0) boneOptions = new[] { "先点上面的'扫描'按钮" };
        int selectedIdx = System.Array.IndexOf(boneOptions, scaleBonePath);
        if (selectedIdx < 0) selectedIdx = 0;
        selectedIdx = EditorGUILayout.Popup("骨骼", selectedIdx, boneOptions);
        if (boneOptions.Length > 0) scaleBonePath = boneOptions[selectedIdx];

        EditorGUILayout.BeginHorizontal();
        scaleX = GUILayout.Toggle(scaleX, "X");
        scaleY = GUILayout.Toggle(scaleY, "Y");
        scaleZ = GUILayout.Toggle(scaleZ, "Z");
        EditorGUILayout.EndHorizontal();

        scaleFactor = EditorGUILayout.FloatField("缩放系数 (0.5=减半, 2=加倍)", scaleFactor);

        EditorGUI.BeginDisabledGroup(targetClip == null || isSubAsset || string.IsNullOrEmpty(scaleBonePath) || boneOptions[0].StartsWith("先点"));
        if (GUILayout.Button("🔨 缩放选中骨骼的位移", GUILayout.Height(28)))
        {
            int count = ScaleBonePosition(targetClip, scaleBonePath, scaleX, scaleY, scaleZ, scaleFactor);
            AssetDatabase.SaveAssets();
            singleScanResult = ScanAllBonesDisplacement(targetClip);
            EditorUtility.DisplayDialog("完成", $"已缩放 {count} 条曲线\n路径: {scaleBonePath}\n系数: {scaleFactor}", "好的");
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space(5);

        // ═══ 批量 ═══
        EditorGUILayout.Space(15);
        GUILayout.Label("═══ 批量处理 ═══", EditorStyles.boldLabel);

        showBatchList = EditorGUILayout.Foldout(showBatchList, $"Clip 列表（{batchClipList.Count} 个）", true);
        if (showBatchList)
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖入 .anim 文件到这里", EditorStyles.helpBox);
            Event evt = Event.current;
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                            if (obj is AnimationClip c && !batchClipList.Contains(c))
                                batchClipList.Add(c);
                        batchScanned = false;
                    }
                    evt.Use();
                }
            }
            for (int i = batchClipList.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                batchClipList[i] = (AnimationClip)EditorGUILayout.ObjectField(batchClipList[i], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(25))) batchClipList.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
        }

        if (GUILayout.Button("📥 加入 Project 窗口选中的 Clip"))
        {
            foreach (var c in Selection.GetFiltered<AnimationClip>(SelectionMode.Assets))
                if (!batchClipList.Contains(c)) batchClipList.Add(c);
            batchScanned = false;
        }

        // ── 批量扫描 ──
        EditorGUILayout.Space(5);
        EditorGUI.BeginDisabledGroup(batchClipList.Count == 0);
        if (GUILayout.Button("🔍 批量扫描所有 Clip 的位移骨骼", GUILayout.Height(30)))
        {
            batchScanned = true;
            selectedBones.Clear();
            batchScanResult = new Dictionary<AnimationClip, List<BoneDisplacement>>();
            foreach (var clip in batchClipList)
            {
                if (clip == null) continue;
                batchScanResult[clip] = ScanAllBonesDisplacement(clip);
            }
            BuildBatchSummary();
        }
        EditorGUI.EndDisabledGroup();

        // ── 扫描结果 ──
        if (batchScanned && batchSummary != null && batchSummary.Count > 0)
        {
            var withDisp = batchSummary.Where(b => b.maxDisplacement > 0.0001f).ToList();

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                $"共发现 {withDisp.Count} 根有位移的骨骼",
                MessageType.Info);

            // 全选 / 全不选
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选")) { foreach (var b in withDisp) selectedBones.Add(b.bonePath); }
            if (GUILayout.Button("全不选")) selectedBones.Clear();
            if (GUILayout.Button("只选出现在多个 Clip 的骨骼"))
            {
                selectedBones.Clear();
                foreach (var b in withDisp.Where(b => b.clipCount > 1))
                    selectedBones.Add(b.bonePath);
            }
            EditorGUILayout.EndHorizontal();

            batchScroll = EditorGUILayout.BeginScrollView(batchScroll, GUILayout.Height(180));
            foreach (var b in withDisp)
            {
                EditorGUILayout.BeginHorizontal();

                bool sel = selectedBones.Contains(b.bonePath);
                bool newSel = EditorGUILayout.Toggle(sel, GUILayout.Width(18));
                if (newSel != sel)
                {
                    if (newSel) selectedBones.Add(b.bonePath);
                    else selectedBones.Remove(b.bonePath);
                }

                // 骨骼名
                string tag = b.clipCount > 1 ? $"⭐ {b.bonePath}" : $"    {b.bonePath}";
                EditorGUILayout.LabelField(tag, GUILayout.MinWidth(160));

                // 出现次数和最大位移
                EditorGUILayout.LabelField(
                    $"出现在 {b.clipCount}/{batchClipList.Count} 个 clip  |  最大位移: {b.maxDisplacement:F3}m",
                    GUILayout.MinWidth(260));

                // 查看详情
                if (GUILayout.Button("详情", GUILayout.Width(48)))
                {
                    string info = $"骨骼: {b.bonePath}\n\n";
                    foreach (var d in b.details)
                        info += $"{d.clip.name}: X={d.disp.x:F3} Y={d.disp.y:F3} Z={d.disp.z:F3}\n";
                    EditorUtility.DisplayDialog("位移详情", info, "好的");
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // ── 清零选中的 ──
            EditorGUILayout.Space(5);
            int selCount = selectedBones.Count;
            EditorGUI.BeginDisabledGroup(selCount == 0);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button($"🔨 清零选中的 {selCount} 根骨骼（覆盖全部 {batchClipList.Count} 个 clip）", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("批量清零确认",
                    $"将对全部 {batchClipList.Count} 个 Clip 中的 {selCount} 根骨骼归零。\n\n" +
                    $"骨骼列表:\n{string.Join("\n", selectedBones)}",
                    "确认清零", "取消"))
                {
                    int total = 0;
                    foreach (var clip in batchClipList)
                    {
                        if (clip == null || AssetDatabase.IsSubAsset(clip)) continue;
                        foreach (var bone in selectedBones)
                            total += ZeroOutRootPosition(clip, bone);
                    }
                    AssetDatabase.SaveAssets();
                    selectedBones.Clear();
                    batchScanned = false;
                    EditorUtility.DisplayDialog("完成", $"已清零 {total} 条曲线", "好的");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
        }

        if (batchClipList.Count > 0 && GUILayout.Button("🗑 清空列表"))
        {
            batchClipList.Clear();
            batchScanned = false;
            batchSummary = null;
        }
    }

    // ════════════════════════════════════════

    void BuildBatchSummary()
    {
        // 收集所有出现过的骨骼路径
        var allBones = new Dictionary<string, List<(AnimationClip clip, Vector3 disp)>>();

        foreach (var kv in batchScanResult)
        {
            foreach (var bone in kv.Value.Where(b => b.hasDisplacement))
            {
                if (!allBones.ContainsKey(bone.path))
                    allBones[bone.path] = new List<(AnimationClip, Vector3)>();
                allBones[bone.path].Add((kv.Key, bone.displacement));
            }
        }

        batchSummary = allBones.Select(kv => new BatchBoneSummary
        {
            bonePath = kv.Key,
            clipCount = kv.Value.Count,
            details = kv.Value,
            maxDisplacement = kv.Value.Max(d => d.disp.magnitude)
        })
        .OrderByDescending(b => b.clipCount)
        .ThenByDescending(b => b.maxDisplacement)
        .ToList();
    }

    public static List<BoneDisplacement> ScanAllBonesDisplacement(AnimationClip clip)
    {
        var result = new List<BoneDisplacement>();
        var allBindings = AnimationUtility.GetCurveBindings(clip);

        var groups = allBindings
            .Where(b => b.propertyName.ToLower().Contains("position") || b.propertyName.ToLower().Contains("m_localposition"))
            .GroupBy(b => b.path);

        foreach (var group in groups)
        {
            var bone = new BoneDisplacement { path = group.Key, displacement = Vector3.zero };
            foreach (var binding in group)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length == 0) continue;
                float range = curve.keys.Max(k => k.value) - curve.keys.Min(k => k.value);
                string prop = binding.propertyName.ToLower();
                if (prop.EndsWith(".x") || prop == "m_localposition.x") bone.displacement.x = range;
                else if (prop.EndsWith(".y") || prop == "m_localposition.y") bone.displacement.y = range;
                else if (prop.EndsWith(".z") || prop == "m_localposition.z") bone.displacement.z = range;
            }
            result.Add(bone);
        }
        result.Sort((a, b) => b.displacement.magnitude.CompareTo(a.displacement.magnitude));
        return result;
    }

    public static int ZeroOutRootPosition(AnimationClip clip, string bonePath)
    {
        int cleared = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != bonePath) continue;
            string prop = binding.propertyName.ToLower();
            if (!prop.Contains("position") && !prop.Contains("m_localposition")) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.keys.Length == 0) continue;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            { keys[i].value = 0f; keys[i].inTangent = 0f; keys[i].outTangent = 0f; }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            cleared++;
        }
        EditorUtility.SetDirty(clip);
        return cleared;
    }

    /// <summary>缩放指定骨骼的 Position 曲线，可选择缩放哪些轴</summary>
    public static int ScaleBonePosition(AnimationClip clip, string bonePath, bool scaleX = true, bool scaleY = true, bool scaleZ = true, float factor = 0.5f)
    {
        int scaled = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != bonePath) continue;
            string prop = binding.propertyName.ToLower();
            if (!prop.Contains("position") && !prop.Contains("m_localposition")) continue;

            // 判断当前曲线属于哪个轴
            bool isX = prop.EndsWith(".x") || prop == "m_localposition.x";
            bool isY = prop.EndsWith(".y") || prop == "m_localposition.y";
            bool isZ = prop.EndsWith(".z") || prop == "m_localposition.z";
            if (!isX && !isY && !isZ) continue;

            // 该轴没勾选 → 跳过
            if (isX && !scaleX) continue;
            if (isY && !scaleY) continue;
            if (isZ && !scaleZ) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.keys.Length == 0) continue;

            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            { keys[i].value *= factor; keys[i].inTangent *= factor; keys[i].outTangent *= factor; }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            scaled++;
        }
        EditorUtility.SetDirty(clip);
        return scaled;
    }
}
