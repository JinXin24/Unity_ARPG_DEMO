using UnityEditor;
using UnityEngine;

/// <summary>
/// 工具：打印 Scene 视图相机当前位置和旋转到 Console。
/// 菜单：Tools → 打印 Scene 相机参数
/// 快捷键：Ctrl+Shift+P
/// </summary>
public static class SceneCameraPrinter
{
    [MenuItem("Tools/打印 Scene 相机参数 %#p")]
    public static void PrintSceneCamera()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[SceneCameraPrinter] 没有打开的 Scene 视图");
            return;
        }

        var cam = sceneView.camera;
        if (cam == null)
        {
            Debug.LogWarning("[SceneCameraPrinter] Scene 视图相机不可用");
            return;
        }

        Transform t = cam.transform;
        Vector3 pos = t.position;
        Vector3 euler = t.eulerAngles;

        Debug.Log($"<b>── Scene 相机参数 ──</b>\n" +
                  $"位置: ({pos.x:F3}f, {pos.y:F3}f, {pos.z:F3}f)\n" +
                  $"旋转: ({euler.x:F3}f, {euler.y:F3}f, {euler.z:F3}f)");
    }
}
