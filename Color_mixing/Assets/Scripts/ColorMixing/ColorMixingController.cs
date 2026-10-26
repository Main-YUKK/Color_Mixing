using UnityEngine;
using System;
using System.Linq;

public class ColorMixingController : MonoBehaviour
{
    // referensi ke PigmentPalette
    [SerializeField] private PigmentPalette palette;
    // interval acak ulang pigmen pemain (detik)
    [SerializeField] private float rotationInterval = 10f;

    public MixingBowl MixingBowl { get; private set; } // referensi ke MixingBowl
    public float RotationTimerLeft { get; private set; } // waktu tersisa untuk rotasi warna target

    private int[] slots; // array untuk menyimpan indeks pigmen yang dipegang oleh pemain

    public event Action StateChanged;

    private void Awake()
    {
        MixingBowl = new MixingBowl(palette.Count);
        Rotate();
    }

    private void Update()
    {
        RotationTimerLeft -= Time.deltaTime;
        if (RotationTimerLeft <= 0f) Rotate();
    }

    // slot 0 atau 1 milik pemain -> index pigmen di palet
    public int GetPigment(int player, int slot) => slots[player * 2 + slot];

    private void Rotate()
    {
        slots = PigmentAssignment.Shuffle(palette.Count);
        RotationTimerLeft = rotationInterval;
        Debug.Log($"[MIX] Rotasi: P1 {Name(0, 0)}+{Name(0, 1)}, P2 {Name(1, 0)}+{Name(1, 1)}, P3 {Name(2, 0)}+{Name(2, 1)}");
        StateChanged?.Invoke();
    }

    private string Name(int player, int slot) => palette.GetPigment(GetPigment(player, slot)).pigmentName;

    // menambahkan pigmen ke bowl
    public void AddPigment(int player, int slot, int amount = 1)
    {
        int pigment = GetPigment(player, slot);
        MixingBowl.AddPigment(pigment, amount);
        Debug.Log($"[MIX] Pemain {player + 1} menambahkan {palette.GetPigment(pigment).pigmentName} ke mangkuk. Total unit: {MixingBowl.TotalUnits}");
        StateChanged?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: P1 tambah slot 0")] private void DebugP1Slot0() => AddPigment(0, 0);
    [ContextMenu("Debug: P2 tambah slot 1 (3 unit)")] private void DebugP2Slot1() => AddPigment(1, 1, 3);

    [ContextMenu("Debug: Lihat isi mangkuk")]
    private void DebugBowl()
    {
        var units = MixingBowl.PigmentUnits;
        string isi = MixingBowl.IsEmpty
            ? "kosong"
            : string.Join(" ", Enumerable.Range(0, palette.Count)
                .Where(i => units[i] > 0)
                .Select(i => $"{palette.GetPigment(i).pigmentName}:{units[i]}"));

        Color hasil = ColorMixer.Blend(palette, units);
        string hex = ColorUtility.ToHtmlStringRGB(hasil);
        Debug.Log($"[MIX] Isi mangkuk: {isi} (total {MixingBowl.TotalUnits}) -> hasil #{hex} <color=#{hex}>■■■■■</color>");
    }

    [ContextMenu("Debug: Kosongkan mangkuk")]
    private void DebugClear()
    {
        MixingBowl.Clear();
        Debug.Log("[MIX] Mangkuk dikosongkan");
        StateChanged?.Invoke();
    }
#endif

}
