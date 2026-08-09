using UnityEngine;

/// <summary>
/// 生成环形贴图（带抗锯齿软边）。用于技能充能环、CD 圆环等 UGUI 显示。
/// 编辑器里用菜单 Tools → 生成环形贴图 保存成 PNG 资产；运行时也可调用 Generate()。
/// </summary>
public static class RingTextureGenerator
{
    /// <summary>
    /// 生成一张环形贴图。
    /// </summary>
    /// <param name="size">贴图边长（像素），如 100</param>
    /// <param name="thickness">环宽（像素），如 2</param>
    /// <param name="color">环的颜色（建议带 Alpha，用于和底层灰环叠加）</param>
    public static Texture2D Generate(int size, int thickness, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;       // 贴图中心（像素坐标系）
        float outerR = center;                   // 外半径
        float innerR = center - thickness;       // 内半径

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                // 到环带的距离（负数在环内，0 在环中心线，正数在环外）
                float midR = (outerR + innerR) * 0.5f;
                float half = (outerR - innerR) * 0.5f;
                float signedDist = Mathf.Abs(dist - midR) - half;

                // 1px 抗锯齿软边
                float alpha = 1f - Mathf.Clamp01(signedDist + 0.5f);

                Color c = color;
                c.a *= alpha;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>生成环贴图并直接转成 Sprite（运行时用），方便赋给 Image.sprite</summary>
    public static Sprite GenerateSprite(int size, int thickness, Color color)
    {
        Texture2D tex = Generate(size, thickness, color);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
