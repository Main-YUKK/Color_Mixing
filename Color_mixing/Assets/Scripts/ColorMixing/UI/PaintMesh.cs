using System;
using UnityEngine;
using UnityEngine.UI;

// Gaya gambar cat: outline hitam, isi warna, pita highlight (atas) dan shadow (bawah) ala inner shadow Sketch.
[Serializable]
public struct PaintStyle
{
    public float outlineWidth;
    public float highlightOffset;
    [Range(0f, 1f)] public float highlightOpacity;
    public float shadowOffset;
    [Range(0f, 1f)] public float shadowOpacity;
    public float shadeBlur;

    public PaintStyle(float outlineWidth, float highlightOffset, float highlightOpacity, float shadowOffset, float shadowOpacity, float shadeBlur)
    {
        this.outlineWidth = outlineWidth;
        this.highlightOffset = highlightOffset;
        this.highlightOpacity = highlightOpacity;
        this.shadowOffset = shadowOffset;
        this.shadowOpacity = shadowOpacity;
        this.shadeBlur = shadeBlur;
    }
}

// Menggambar satu bentuk cat tertutup (blob mangkuk, tetesan) dengan PaintStyle. Satu sumber supaya gayanya selalu sama.
public static class PaintMesh
{
    // Normal yang menghadap keluar. sign = 1 untuk titik berlawanan arah jarum jam, -1 untuk searah.
    public static void ComputeNormals(Vector2[] ring, Vector2[] normals, int count, float sign)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 tangent = ring[(i + 1) % count] - ring[(i + count - 1) % count];
            normals[i] = new Vector2(tangent.y, -tangent.x).normalized * sign;
        }
    }

    // Outline (fan hitam penuh) + isi (fan yang masuk sejauh outline) + pita bayangan.
    public static void Draw(VertexHelper vh, Vector2[] ring, Vector2[] normals, int count, Color color, PaintStyle style)
    {
        AddFan(vh, ring, normals, count, 0f, Color.black);
        AddFan(vh, ring, normals, count, style.outlineWidth, color);
        AddShadeBand(vh, ring, normals, count, color, style);
    }

    private static void AddFan(VertexHelper vh, Vector2[] ring, Vector2[] normals, int count, float inset, Color32 fanColor)
    {
        int start = vh.currentVertCount;
        Vector2 center = Vector2.zero;
        for (int i = 0; i < count; i++) center += ring[i];
        center /= count;

        vh.AddVert(center, fanColor, Vector2.zero);
        for (int i = 0; i < count; i++)
            vh.AddVert(ring[i] - normals[i] * inset, fanColor, Vector2.zero);
        for (int i = 0; i < count; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }

    // Meniru inner shadow Sketch: tebal pita mengikuti arah tepi, diukur dari tepi luar.
    private static void AddShadeBand(VertexHelper vh, Vector2[] ring, Vector2[] normals, int count, Color baseColor, PaintStyle style)
    {
        int start = vh.currentVertCount;
        Color32 highlight = Color.Lerp(baseColor, Color.white, style.highlightOpacity);
        Color32 shadow = Color.Lerp(baseColor, Color.black, style.shadowOpacity);
        Color32 plain = baseColor;

        for (int i = 0; i < count; i++)
        {
            float up = normals[i].y;
            float depth = up > 0f ? style.highlightOffset * up : style.shadowOffset * -up;
            Color32 tint = up > 0f ? highlight : shadow;
            float solidEnd = Mathf.Max(style.outlineWidth, depth - style.shadeBlur * 0.5f);
            float fadeEnd = Mathf.Max(style.outlineWidth, depth + style.shadeBlur * 0.5f);

            vh.AddVert(ring[i] - normals[i] * style.outlineWidth, tint, Vector2.zero);
            vh.AddVert(ring[i] - normals[i] * solidEnd, tint, Vector2.zero);
            vh.AddVert(ring[i] - normals[i] * fadeEnd, plain, Vector2.zero);
        }

        for (int i = 0; i < count; i++)
        {
            int a = start + i * 3;
            int b = start + (i + 1) % count * 3;
            for (int row = 0; row < 2; row++)
            {
                vh.AddTriangle(a + row, a + row + 1, b + row);
                vh.AddTriangle(b + row, a + row + 1, b + row + 1);
            }
        }
    }
}
