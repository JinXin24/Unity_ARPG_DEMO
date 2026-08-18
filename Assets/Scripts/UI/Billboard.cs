using UnityEngine;

/// <summary>
/// 广告牌：让物体始终正对相机（头顶血条、血条底、飘字等世界 UI 用）。
/// 复制相机旋转，平面永远正对屏幕，镜头怎么转都不会歪。
/// 挂血条 Canvas 或其父空物体上。
/// </summary>
public class Billboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;   // 可选：拖相机；不拖则 Start 里自动找主相机
    [SerializeField] private bool faceCamera = true; // true=正对屏幕(UI平面)；false=正面朝向相机(3D物体)

    private Transform cam;

    void Start()
    {
        Camera c = targetCamera != null ? targetCamera : Camera.main;   // 只找一次，别每帧 Camera.main
        cam = c != null ? c.transform : null;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        if (faceCamera)
            transform.rotation = cam.rotation;    // UI 平面：跟相机同向，永远正对屏幕
        else
            transform.forward = cam.forward;      // 3D 物体：正面朝相机
    }
}
