using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Excel → ScriptableObject 自动生成工具。
/// 依赖：Python 3 + openpyxl（pip install openpyxl）
/// 两步操作：先读 Excel 表头生成 C# 类文件，Unity 编译后再生成 .asset 实例。
/// </summary>
public class ExcelToSO : EditorWindow
{
    private Object excelFile;
    private string outputPath = "Assets/Resources/Config/";
    private string codePath = "Assets/Scripts/Gen/";

    [MenuItem("Tools/Excel 转 ScriptableObject")]
    public static void ShowWindow() => GetWindow<ExcelToSO>("Excel → SO");

    [MenuItem("Assets/Excel/导出配置", false, 50)]
    static void ImportFromMenu()
    {
        var obj = Selection.activeObject;
        if (obj == null || !AssetDatabase.GetAssetPath(obj).EndsWith(".xlsx")) return;
        var window = GetWindow<ExcelToSO>("Excel → 配置导出");
        window.excelFile = obj;
        window.Show();
    }

    [MenuItem("Assets/Excel/导出配置", true)]
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
        if (GUILayout.Button("生成 C# 代码（修改表结构时才需要）", GUILayout.Height(30))) GenCode();
        if (GUILayout.Button("生成 C# List 容器类", GUILayout.Height(30))) GenListCode();
        EditorGUILayout.Space(5);
        if (GUILayout.Button("导出 SO（本地调试）", GUILayout.Height(28))) GenSO();
        if (GUILayout.Button("导出 List SO（整表一个文件）", GUILayout.Height(28))) GenListSO();
        if (GUILayout.Button("导出 JSON（客户端+服务端）", GUILayout.Height(28))) GenJSON();
        if (GUILayout.Button("导出 Lua（热更用）", GUILayout.Height(28))) GenLua();
        GUI.enabled = true;
    }

    // ════════════════════════════════════════

    void GenCode()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("请等待", "Unity 正在编译，编译完再点。", "好的");
            return;
        }
        string fullPath = AssetPathToFull(excelFile);
        Debug.Log("[ExcelToSO] reading: " + fullPath);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) { Debug.LogError("[ExcelToSO] LoadSheets returned null — Python 报错？"); return; }

        Debug.Log("[ExcelToSO] got " + sheets.Count + " sheets");
        Directory.CreateDirectory(codePath);
        foreach (var kv in sheets)
        {
            Debug.Log("[ExcelToSO] generating class for: " + kv.Key);
            GenClass(kv.Key, kv.Value, codePath);
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"C# 代码已生成到 {codePath}\n编译完再点第 2 步。", "好的");
    }

    void GenListCode()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("请等待", "Unity 正在编译，编译完再点。", "好的");
            return;
        }
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        Directory.CreateDirectory(codePath);
        foreach (var kv in sheets)
        {
            Debug.Log("[ExcelToSO] generating list class for: " + kv.Key);
            GenListClass(kv.Key, codePath);
        }
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"List 容器类已生成到 {codePath}\n编译完再点'导出 List SO'。", "好的");
    }

    void GenJSON()
    {
        if (EditorApplication.isCompiling) { EditorUtility.DisplayDialog("请等待", "Unity 正在编译。", "好的"); return; }
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        string jsonDir = Path.Combine(Application.dataPath, "Resources/Config/");
        if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);

        int total = 0;
        foreach (var kv in sheets)
        {
            var rows = kv.Value;
            if (rows.Length < 4) continue;

            var headers = rows[0];
            var list = new List<string>();
            for (int r = 3; r < rows.Length; r++)
            {
                if (IsEmpty(rows[r])) continue;

                // 每行转为 JSON 对象字符串
                var parts = new List<string>();
                for (int i = 0; i < headers.Length && i < rows[r].Length; i++)
                {
                    string key = CleanField(headers[i]);
                    string val = rows[r][i].Trim();
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;

                    // 判断值类型
                    if (int.TryParse(val, out _) || float.TryParse(val, out _) || val == "True" || val == "False")
                        parts.Add($"\"{key}\":{val}");
                    else
                        parts.Add($"\"{key}\":\"{val.Replace("\"", "\\\"")}\"");
                }
                list.Add("{" + string.Join(",", parts) + "}");
                total++;
            }

            string json = "[\n  " + string.Join(",\n  ", list) + "\n]";
            string fileName = SnakeToPascal(kv.Key) + "SO.json";
            File.WriteAllText(Path.Combine(jsonDir, fileName), json);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"已生成 {total} 条 JSON 数据\n路径: {jsonDir}", "好的");
    }

    void GenLua()
    {
        if (EditorApplication.isCompiling) { EditorUtility.DisplayDialog("请等待", "Unity 正在编译。", "好的"); return; }
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        string luaDir = Path.Combine(Application.dataPath, "Resources/LuaConfig/");
        if (!Directory.Exists(luaDir)) Directory.CreateDirectory(luaDir);

        int total = 0;
        foreach (var kv in sheets)
        {
            var rows = kv.Value;
            if (rows.Length < 4) continue;

            var headers = rows[0];
            var sb = new System.Text.StringBuilder();
            string tableName = SnakeToPascal(kv.Key) + "SO";

            sb.AppendLine($"-- Auto-generated from {Path.GetFileName(fullPath)}");
            sb.AppendLine($"local {tableName} = {{");

            // 找 Id 列索引做 key
            int idCol = -1;
            for (int i = 0; i < headers.Length; i++)
                if (CleanField(headers[i]).ToLower() == "id") { idCol = i; break; }

            for (int r = 3; r < rows.Length; r++)
            {
                if (IsEmpty(rows[r])) continue;
                string key = idCol >= 0 && idCol < rows[r].Length ? rows[r][idCol].Trim() : $"{r}";
                if (string.IsNullOrEmpty(key)) key = $"{r}";

                sb.AppendLine($"    [{key}] = {{");
                for (int i = 0; i < headers.Length && i < rows[r].Length; i++)
                {
                    string f = CleanField(headers[i]);
                    string v = rows[r][i].Trim();
                    if (string.IsNullOrEmpty(f) || string.IsNullOrEmpty(v)) continue;

                    string luaVal;
                    if (v == "True") luaVal = "true";
                    else if (v == "False") luaVal = "false";
                    else if (int.TryParse(v, out _) || float.TryParse(v, out _)) luaVal = v;
                    else luaVal = $"\"{v.Replace("\"", "\\\"")}\"";

                    sb.AppendLine($"        {f} = {luaVal},");
                }
                sb.AppendLine("    },");
                total++;
            }
            sb.AppendLine("}");
            sb.AppendLine($"return {tableName}");

            string fileName = SnakeToPascal(kv.Key) + "SO.lua";
            File.WriteAllText(Path.Combine(luaDir, fileName), sb.ToString());
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"已生成 {total} 条 Lua 配置\n路径: {luaDir}", "好的");
    }

    void GenSO()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("请等待", "Unity 正在编译，编译完再点。", "好的");
            return;
        }
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        CreateAssets(sheets);
    }

    void GenListSO()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("请等待", "Unity 正在编译，编译完再点。", "好的");
            return;
        }
        string fullPath = AssetPathToFull(excelFile);
        var sheets = LoadSheets(fullPath);
        if (sheets == null) return;

        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

        int created = 0;
        foreach (var kv in sheets)
        {
            var rows = kv.Value;
            if (rows.Length < 4) continue;

            string typeName = ClassNameFromSheet(kv.Key);
            string listTypeName = typeName + "List";
            var elementType = FindType(typeName);
            var listType = FindType(listTypeName);
            if (elementType == null || listType == null)
            {
                Debug.LogWarning($"[Excel→SO] {listTypeName} 未编译，请先点'生成 C# 代码'");
                continue;
            }

            var headers = rows[0];

            // 创建/更新容器 SO
            string dir = Path.Combine(outputPath, typeName + "List");
            Directory.CreateDirectory(dir);
            string listPath = Path.Combine(dir, listTypeName + ".asset");

            var listSo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(listPath);
            if (listSo == null || listSo.GetType() != listType)
            {
                listSo = (ScriptableObject)ScriptableObject.CreateInstance(listType);
                AssetDatabase.CreateAsset(listSo, listPath);
            }

            // 清空旧 list
            var listField = listType.GetField("list", BindingFlags.Public | BindingFlags.Instance);
            var listObj = listField.GetValue(listSo);
            var clearMethod = listObj.GetType().GetMethod("Clear");
            clearMethod.Invoke(listObj, null);
            var addMethod = listObj.GetType().GetMethod("Add");

            // 删除旧的子资产，避免残留
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(listPath);
            foreach (var sub in subAssets)
            {
                if (sub == listSo) continue;
                AssetDatabase.RemoveObjectFromAsset(sub);
            }

            // 填入每行（元素作为子资产挂到容器下才能序列化保存）
            for (int r = 3; r < rows.Length; r++)
            {
                if (IsEmpty(rows[r])) continue;
                var element = CreateInstance(elementType, headers, rows[r]);
                if (element == null) continue;
                element.name = $"{kv.Key}_{r}";
                AssetDatabase.AddObjectToAsset(element, listSo);
                addMethod.Invoke(listObj, new object[] { element });
                created++;
            }

            EditorUtility.SetDirty(listSo);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Excel→SO] List SO 填入了 {created} 条数据");
        EditorUtility.DisplayDialog("完成", $"List SO 已生成，共 {created} 条数据\n输出: {outputPath}", "好的");
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

            string typeName = ClassNameFromSheet(kv.Key);
            var type = FindType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[Excel→SO] 类型 {typeName} 未编译，请重新导入");
                continue;
            }

            var headers = rows[0];
            int counter = 0;
            for (int r = 3; r < rows.Length; r++)
            {
                if (IsEmpty(rows[r])) continue;
                var so = CreateInstance(type, headers, rows[r]);
                if (so == null) continue;

                counter++;
                string name = $"{kv.Key}_{counter}";
                string dir = Path.Combine(outputPath, typeName);
                Directory.CreateDirectory(dir);
                string soPath = Path.Combine(dir, name + ".asset");

                // 已有 SO 则原地更新，保留 Inspector 引用
                var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(soPath);
                if (existing != null && existing.GetType() == type)
                {
                    EditorUtility.CopySerialized(so, existing);
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(so, soPath);
                }
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
        string cn = ClassNameFromSheet(sheetName);
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

    void GenListClass(string sheetName, string dir)
    {
        string cn = ClassNameFromSheet(sheetName);
        string path = Path.Combine(dir, cn + "List.cs");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"public class {cn}List : ScriptableObject");
        sb.AppendLine("{");
        sb.AppendLine($"    public List<{cn}> list = new();");
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
        var t = so.GetType();
        // 优先 Name
        foreach (var f in t.GetFields())
            if (f.Name.Equals("Name", System.StringComparison.OrdinalIgnoreCase))
            {
                var v = f.GetValue(so);
                if (v != null && !string.IsNullOrEmpty(v.ToString()))
                    return Sanitize(v.ToString());
            }
        // 组合 CharacterId + StateId
        object cid = null, sid = null;
        foreach (var f in t.GetFields())
        {
            if (f.Name.Equals("CharacterId", System.StringComparison.OrdinalIgnoreCase)) cid = f.GetValue(so);
            if (f.Name.Equals("StateId", System.StringComparison.OrdinalIgnoreCase)) sid = f.GetValue(so);
        }
        if (cid != null && sid != null) return $"{sheet}_{cid}_{sid}";
        // 普通 Id 兜底
        foreach (var f in t.GetFields())
            if (f.Name.EndsWith("Id", System.StringComparison.OrdinalIgnoreCase))
            {
                var v = f.GetValue(so);
                if (v != null) return $"{sheet}_{v}";
            }
        return $"{sheet}_{row}";
    }


    // ═══════ 工具方法 ═══════

    object ConvertVal(string v, System.Type t)
    {
        if (t == typeof(int)) return int.Parse(v);
        if (t == typeof(float)) return float.Parse(v);
        if (t == typeof(bool)) return v.ToUpper() == "TRUE";
        if (t == typeof(string[])) return v.Split(';');
        if (t == typeof(int[])) return System.Array.ConvertAll(v.Split(';'), int.Parse);
        if (t == typeof(float[])) return System.Array.ConvertAll(v.Split(';'), float.Parse);
        return v;
    }

    string MapCsType(string t)
    {
        switch (t.ToLower().Replace(" ", ""))
        {
            case "int": return "int";
            case "string": return "string";
            case "float": return "float";
            case "bool": return "bool";
            case "int[]": return "int[]";
            case "float[]": return "float[]";
            case "string[]": return "string[]";
            default: return "string";
        }
    }

    string CleanField(string s) { if (string.IsNullOrEmpty(s)) return ""; int h = s.IndexOf('#'); if (h >= 0) s = s.Substring(0, h); return s.Trim().Replace(" ", ""); }

    string SnakeToPascal(string s) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Replace("_", " ")).Replace(" ", "");
    string ClassNameFromSheet(string sheet) => SnakeToPascal(sheet) + "SO";

    string Sanitize(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(" ", "_"); }

    bool IsEmpty(string[] row)
    {
        if (row.Length < 2) return true;
        // 前两列（通常是 CharacterId 和 StateName/Id）都空 → 跳过
        if (string.IsNullOrEmpty(row[0]) && string.IsNullOrEmpty(row[1])) return true;
        return false;
    }

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
        vals = [str(c).replace('\n',' ').replace('\r','') if c is not None else '' for c in row]
        if any(v.strip() for v in vals):
            print('\t'.join(vals))
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
                else curRows.Add(t.Split('\t'));
            }
            if (curSheet != null && curRows.Count > 0) result[curSheet] = curRows.ToArray();
            return result.Count > 0 ? result : null;
        }
        catch (System.Exception e) { Debug.LogError(e.Message); return null; }
    }
}
