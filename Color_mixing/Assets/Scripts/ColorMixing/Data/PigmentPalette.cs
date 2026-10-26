using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Daftar pigmen. Index = ID pigmen. Index 0-2 WAJIB Red, Yellow, Blue (sudut segitiga ColorMixer).
[CreateAssetMenu(fileName = "PigmentPalette", menuName = "Color Mixing/Pigment Palette")]
public class PigmentPalette : ScriptableObject
{
    [SerializeField] private List<Pigment> pigments = new List<Pigment>();

    public int Count => pigments.Count;
    public Pigment GetPigment(int index) => pigments[index];


//Debug: test bowl dan blend.
#if UNITY_EDITOR
    [ContextMenu("Get Pigment")]
    private void GetPigmentTest()
    {
        for (int i = 0; i < Count; i++)
            Debug.Log($"[MIX] Pigment {i}: {GetPigment(i).pigmentName} {GetPigment(i).color} {GetPigment(i).ryb}");
    }

    [ContextMenu("Test Bowl")]
    private void TestBowl()
    {
        var bowl = new MixingBowl(Count);
        bowl.AddPigment(0, 2);   // 2 Red
        bowl.AddPigment(1);      // 1 Yellow
        Debug.Log($"[MIX] {string.Join(" ", Enumerable.Range(0, Count).Select(i => $"{GetPigment(i).pigmentName}:{bowl.PigmentUnits[i]}"))} total={bowl.TotalUnits}");
        Debug.Log($"[MIX] warna mangkuk -> {ColorMixer.Blend(this, bowl.PigmentUnits)}  harus (0.969, 0.463, 0.159)");

        bowl.Clear();
        Debug.Log($"[MIX] setelah Clear: kosong={bowl.IsEmpty}");
    }

    // Test blend beberapa kombinasi pigmen.
    [ContextMenu("Test Blend")]
    private void TestBlend()
    {
        // urutan: R, Y, B, K(hitam), W(putih)
        LogBlend("R+Y   harus (0.953, 0.694, 0.239)", 1, 1, 0, 0, 0);
        LogBlend("R+B   harus (0.671, 0.161, 0.863)", 1, 0, 1, 0, 0);
        LogBlend("Y+B   harus (0.376, 0.784, 0.235)", 0, 1, 1, 0, 0);
        LogBlend("R+Y+B harus (0.290, 0.290, 0.290)", 1, 1, 1, 0, 0);
        LogBlend("K+2W  harus (0.667, 0.667, 0.667)", 0, 0, 0, 1, 2);
        LogBlend("R+K   harus (0.500, 0.000, 0.000)", 1, 0, 0, 1, 0);
        LogBlend("R+W   harus (1.000, 0.500, 0.500)", 1, 0, 0, 0, 1);
        LogBlend("3R    harus (1.000, 0.000, 0.000)", 3, 0, 0, 0, 0);
    }

    private void LogBlend(string label, params int[] units) =>
        Debug.Log($"[MIX] {label} -> {ColorMixer.Blend(this, units)}");
#endif
}

