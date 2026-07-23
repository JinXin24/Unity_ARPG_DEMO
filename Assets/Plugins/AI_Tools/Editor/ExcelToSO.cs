using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Excel → ScriptableObject 自动生成工具。
/// 两步操作：先读 Excel 表头生成 C# 类文件，Unity 编译后再生成 .asset 实例。
/// 免 Luban 依赖，策划改完 Excel → 运行此工具 → 直接可用。
/// </summary>
public class ExcelToSO : EditorWindow
{
    private Object excelFile;
    private string outputPath = "Assets/Resources/Config/";
    private string codePath = "Assets/Scripts/Gen/";

    [MenuItem("Tools/Excel 转 ScriptableObject")]
    public static void ShowWindow() => GetWindow<ExcelToSO>("Excel → SO");

    [MenuItem("Assets/Excel/导入为 SO", false, 50)]
    static void ImportFromMenu()
    {
        
        var obj = Selection.activeObject;
        if (obj == null || !AssetDatabase.GetAssetPath(obj).EndsWith(".xlsx")) return;
        var window = GetWindow<ExcelToSO>("Excel → SO");
        window.excelFile = obj;
        window.GenCode();
    }

    [MenuItem("Assets/Excel/导入为 SO", true)]
    static bool ImportFromMenuValidate() => Selection.activeObject != null
        && AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".xlsx");

    void OnGUI()
    {
        GUILayout.Label("Excel → ScriptableObject", EditorStyles.boldLabel);
        excelFile = EditorGUILayout.ObjectField("Excel 文件", excelFile, typeof(Object), false);
        outputPath = EditorGUILayout.TextField("SO 输出路径", outputPath);
        codePath = EditorGUILayout.TextField("代码输出路径", codePath);
        EditorGUILayout.Space(10);
        GUI.enabled = excelFile != null;
        if (GUILayout.Button("第 1 步：生成 C# 代码", GUILayout.Height(36))) GenCode();
        if (GUILayout.Button("第 2 步：生成 SO 文件", GUILayout.Height(36))) GenSO();
        GUI.enabled = true;
    }

    // ════════════════════════════════════════

    void GenCode()
    {
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        Directory.CreateDirectory(codePath);
        foreach (var kv in sheets)
            GenClass(kv.Key, kv.Value, codePath);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"C# 代码已生成到 {codePath}\n等 Unity 编译完再点第 2 步。", "好的");
    }

    void GenSO()
    {
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        CreateAssets(sheets);
    }

    Dictionary<string, string[][]> LoadSheets(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("错误", $"文件不存在:\n{fullPath}", "好的");
            return null;
        }
        var sheets = ReadExcel(fullPath);
        if (sheets == null || sheets.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "读取失败", "好的");
            return null;
        }
        return sheets;
    }

    void CreateAssets(Dictionary<string, string[][]> sheets)
    {
        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

        int created = 0;
        foreach (var kv in sheets)
        {
            var rows = kv.Value;
            if (rows.Length < 4) continue;

            string typeName = SnakeToPascal(kv.Key) + "SO";
            var type = FindType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[Excel→SO] 类型 {typeName} 未编译，请重新导入");
                continue;
            }

            var headers = rows[0];
            for (int r = 3; r < rows.Length; r++)
            {
                if (IsEmpty(rows[r])) continue;
                var so = CreateInstance(type, headers, rows[r]);
                if (so == null) continue;

                string name = GetName(so, kv.Key, r);
                string dir = Path.Combine(outputPath, typeName);
                Directory.CreateDirectory(dir);
                string soPath = Path.Combine(dir, name + ".asset");
                AssetDatabase.CreateAsset(so, soPath);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Excel→SO] 创建了 {created} 个 SO");
        EditorUtility.DisplayDialog("完成", $"创建了 {created} 个 ScriptableObject\n输出: {outputPath}", "好的");
    }

    // ═══════ C# 代码生成 ═══════

    void GenClass(string sheetName, string[][] rows, string dir)
    {
        if (rows.Length < 2) return;
        var fields = rows[0];
        var types = rows[1];
        string cn = SnakeToPascal(sheetName) + "SO";
        string path = Path.Combine(dir, cn + ".cs");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine($"public class {cn} : ScriptableObject");
        sb.AppendLine("{");
        for (int i = 0; i < fields.Length && i < types.Length; i++)
        {
            string f = CleanField(fields[i]);
            string t = MapCsType(types[i].Trim());
            if (!string.IsNullOrEmpty(f) && !string.IsNullOrEmpty(t))
                sb.AppendLine($"    public {t} {f};");
        }
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }

    // ═══════ 动态创建 ═══════

    ScriptableObject CreateInstance(System.Type type, string[] headers, string[] row)
    {
        var so = (ScriptableObject)ScriptableObject.CreateInstance(type);
        var flags = BindingFlags.Public | BindingFlags.Instance;
        for (int i = 0; i < headers.Length && i < row.Length; i++)
        {
            string f = CleanField(headers[i]);
            string v = row[i].Trim();
            if (string.IsNullOrEmpty(f) || string.IsNullOrEmpty(v)) continue;

            var fi = type.GetField(f, flags);
            if (fi == null) continue;
            try { fi.SetValue(so, ConvertVal(v, fi.FieldType)); } catch { }
        }
        return so;
    }

    string GetName(ScriptableObject so, string sheet, int row)
    {
        var nf = so.GetType().GetField("Name") ?? so.GetType().GetField("name");
        if (nf != null)
        {
            var v = nf.GetValue(so);
            if (v != null && !string.IsNullOrEmpty(v.ToString()))
                return Sanitize(v.ToString());
        }
        var idf = so.GetType().GetField("Id") ?? so.GetType().GetField("id");
        if (idf != null)
        {
            var v = idf.GetValue(so);
            if (v != null) return $"{sheet}_{v}";
        }
        return $"{sheet}_{row}";
    }

    // ═══════ 工具方法 ═══════

    object ConvertVal(string v, System.Type t)
    {
        if (t == typeof(int)) return int.Parse(v);
        if (t == typeof(float)) return float.Parse(v);
        if (t == typeof(bool)) return bool.Parse(v);
        if (t == typeof(string[])) return v.Split(';');
        return v;
    }

    string MapCsType(string t)
    {
        switch (t.ToLower()) { case "int": return "int"; case "string": return "string"; case "float": return "float"; case "bool": return "bool"; default: return "string"; }
    }

    string CleanField(string s) { if (string.IsNullOrEmpty(s)) return ""; int h = s.IndexOf('#'); if (h >= 0) s = s.Substring(0, h); return s.Trim().Replace(" ", ""); }

    string SnakeToPascal(string s) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace("_", " ")).Replace(" ", "");

    string Sanitize(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(" ", "_"); }

    bool IsEmpty(string[] row) { foreach (var c in row) if (!string.IsNullOrEmpty(c)) return false; return true; }

    System.Type FindType(string name) { foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) { var t = a.GetType(name); if (t != null) return t; } return null; }

    string AssetPathToFull(Object obj) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(obj)));

    // ═══════ Excel 读取 ═══════

    Dictionary<string, string[][]> ReadExcel(string path)
    {
        string py = path.Replace("\\", "/");
        string script = @"
import openpyxl, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
wb = openpyxl.load_workbook(r'" + py + @"', data_only=True)
for s in wb.sheetnames:
    print('===Sheet:' + s + '===')
    for row in wb[s].iter_rows(values_only=True):
        print('|'.join([str(c) if c is not None else '' for c in row]))
";
        try
        {
            var p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "python3"; p.StartInfo.Arguments = $"-c \"{script.Replace("\"", "\\\"")}\"";
            p.StartInfo.UseShellExecute = false; p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true; p.StartInfo.CreateNoWindow = true;
            p.Start(); string outStr = p.StandardOutput.ReadToEnd(); p.WaitForExit(30000);

            if (outStr.TrimStart().StartsWith("Traceback")) { Debug.LogError(outStr); return null; }

            var result = new Dictionary<string, string[][]>();
            string curSheet = null; var curRows = new List<string[]>();
            foreach (string line in outStr.Split('\n'))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.Contains("UserWarning")) continue;
                if (t.StartsWith("===Sheet:")) { if (curSheet != null) result[curSheet] = curRows.ToArray(); curSheet = t.Replace("===Sheet:", "").Replace("===", "").Trim(); curRows = new List<string[]>(); }
                else curRows.Add(t.Split('|'));
            }
            if (curSheet != null && curRows.Count > 0) result[curSheet] = curRows.ToArray();
            return result.Count > 0 ? result : null;
        }
        catch (System.Exception e) { Debug.LogError(e.Message); return null; }
    }
}
