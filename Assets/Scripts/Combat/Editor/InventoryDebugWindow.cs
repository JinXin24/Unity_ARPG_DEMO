using UnityEditor;
using UnityEngine;

/// <summary>
/// 背包调试窗口 — Tools → 背包调试器
/// 可快速生成指定 Cost 的声骸到背包，观察词条随机结果。
/// </summary>
public class InventoryDebugWindow : EditorWindow
{
    private int cost = 4;
    private int level = 1;
    private int batchCount = 1;
    private Vector2 scroll;

    [MenuItem("Tools/背包调试器")]
    static void Open() => GetWindow<InventoryDebugWindow>("背包调试器");

    void OnGUI()
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            EditorGUILayout.HelpBox("场景中没有 InventoryManager。请在场景里创建一个 GameObject 并挂上 InventoryManager 脚本。", MessageType.Warning);
            if (GUILayout.Button("自动创建"))
            {
                var go = new GameObject("InventoryManager");
                go.AddComponent<InventoryManager>();
                Selection.activeGameObject = go;
            }
            return;
        }

        // === 生成区 ===
        EditorGUILayout.LabelField("生成声骸", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Cost", GUILayout.Width(40));
        if (GUILayout.Toggle(cost == 4, "4费", EditorStyles.miniButtonLeft)) cost = 4;
        if (GUILayout.Toggle(cost == 3, "3费", EditorStyles.miniButtonMid)) cost = 3;
        if (GUILayout.Toggle(cost == 1, "1费", EditorStyles.miniButtonRight)) cost = 1;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("等级", GUILayout.Width(40));
        level = EditorGUILayout.IntSlider(level, 1, 25);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("批量", GUILayout.Width(40));
        batchCount = EditorGUILayout.IntField(batchCount, GUILayout.Width(40));
        if (batchCount < 1) batchCount = 1;
        if (batchCount > 100) batchCount = 100;

        if (GUILayout.Button($"生成 {batchCount} 个声骸"))
        {
            for (int i = 0; i < batchCount; i++)
                inv.EditorAcquireEcho();
        }
        EditorGUILayout.Space();

        // === 操作区 ===
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空背包")) inv.EditorClear();
        if (GUILayout.Button("打印背包")) inv.EditorPrint();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // === 背包内容 ===
        EditorGUILayout.LabelField($"背包 ({inv.GetAllEchoes().Count} 个声骸)", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var echo in inv.GetAllEchoes())
        {
            DrawEchoCard(echo);
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawEchoCard(EchoInstance echo)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 标题行
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"[{echo.cost}费] Lv.{echo.level}", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField($"ID: {echo.itemId}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // 主词条
        EditorGUILayout.LabelField($"  主: {EchoInstance.StatLabel(echo.mainStat.type)} +{echo.mainStat.value:F1}");

        // 副词条
        foreach (var sub in echo.subStats)
        {
            string bar = new string('█', sub.rollQuality);
            EditorGUILayout.LabelField($"  副: {EchoInstance.StatLabel(sub.type)} +{sub.value:F1}  [{bar}]");
        }

        EditorGUILayout.EndVertical();
    }
}
