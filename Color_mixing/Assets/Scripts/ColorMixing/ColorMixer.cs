using System.Collections.Generic;
using UnityEngine;

// Syarat palet: index 0-2 = Red, Yellow, Blue.
public static class ColorMixer
{
    private static readonly Color Orange = new Color32(243, 177, 61, 255);
    private static readonly Color Purple = new Color32(171, 41, 220, 255);
    private static readonly Color Green = new Color32(96, 200, 60, 255);
    private static readonly Color Mud = new Color32(74, 74, 74, 255);

    public static Color Blend(PigmentPalette palette, IReadOnlyList<int> pigmentUnits)
    {
        float count = 0, whiteCount = 0, primaryCount = 0;
        Vector3 primaries = Vector3.zero;
        int usedPigments = 0, lastUsed = -1;

        
        for (int i = 0; i < pigmentUnits.Count; i++)
        {
            int n = pigmentUnits[i];          // jumlah unit pigmen ke-i
            if (n == 0) continue;             // pigmen tidak dipakai → lewati
            usedPigments++; lastUsed = i;     // catat ada berapa jenis pigmen yang dipakai
            count += n;                       // total semua unit

            Vector3 ryb = palette.GetPigment(i).ryb;
            if (ryb == Vector3.zero) whiteCount += n;                                 // ryb putih
            else if (ryb != Vector3.one) { primaries += ryb * n; primaryCount += n; } // R/Y/B
                                                                                      // hitam (1,1,1): tidak masuk ke mana pun, tapi tetap ikut dihitung di "count"
        }

        if (count == 0) return Color.black;                                  // ← guard units.first
        if (usedPigments == 1) return palette.GetPigment(lastUsed).color;    // ← allSatisfy same id
        if (primaryCount == 0) return Gray(whiteCount / count);              // ← guard primaryCount > 0

        // ← shares, center, centerWeight, residual
        Vector3 shares = primaries / (primaries.x + primaries.y + primaries.z);
        float center = Mathf.Min(shares.x, Mathf.Min(shares.y, shares.z));
        float centerWeight = 3 * center;
        Vector3 residual = shares - Vector3.one * center;

        Color red = palette.GetPigment(0).color;
        Color yellow = palette.GetPigment(1).color;
        Color blue = palette.GetPigment(2).color;

        // ← if shares.z == center ... else if shares.y == center ... else
        Color first, middle, last;
        float lastWeight;
        if (shares.z == center) { first = red; middle = Orange; last = yellow; lastWeight = residual.y; }
        else if (shares.y == center) { first = red; middle = Purple; last = blue; lastWeight = residual.z; }
        else { first = yellow; middle = Green; last = blue; lastWeight = residual.z; }

        // ← edgeTotal, edgeFraction, edge
        float edgeTotal = residual.x + residual.y + residual.z;
        float edgeFraction = edgeTotal > 0 ? lastWeight / edgeTotal : 0;
        Color edge = edgeFraction <= 0.5f
            ? Color.Lerp(first, middle, 2 * edgeFraction)
            : Color.Lerp(middle, last, 2 * edgeFraction - 1);

        // ← edge * (1 - centerWeight) + abu * centerWeight
        Color c = Color.Lerp(edge, Mud, centerWeight);

        // ← color * (primaryCount / count) + whiteCount / count, lalu clamp
        float p = primaryCount / count, w = whiteCount / count;
        return new Color(Mathf.Clamp01(c.r * p + w), Mathf.Clamp01(c.g * p + w), Mathf.Clamp01(c.b * p + w), 1f);
    }

    private static Color Gray(float v) => new Color(v, v, v, 1f);
}