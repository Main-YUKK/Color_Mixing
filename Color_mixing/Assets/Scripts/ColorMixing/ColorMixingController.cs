using UnityEngine;
using System;
using System.Linq;

public class ColorMixingController : MonoBehaviour
{
    // referensi ke PigmentPalette
    [SerializeField] private PigmentPalette palette;
    public PigmentPalette Palette => palette;
    // interval acak ulang pigmen pemain (detik)
    [SerializeField] private float rotationInterval = 10f;

    public MixingBowl MixingBowl { get; private set; } // referensi ke MixingBowl
    public float RotationTimerLeft { get; private set; } // waktu tersisa untuk rotasi pigmen pemain diacak ulang

    private int[] slots; // array untuk menyimpan indeks pigmen yang dipegang oleh pemain

    public event Action StateChanged; // event untuk update UI ketika ada perubahan state (rotasi pigmen, penambahan pigmen ke bowl, dll)

    private readonly bool[] readyToMix = new bool[PigmentAssignment.PlayerCount]; // status siap mencampur untuk setiap pemain
    public event Action MixConfirmed; // event untuk menandai bahwa semua pemain siap mencampur

    public bool IsReady(int player) => readyToMix[player]; // cek apakah pemain siap mencampur

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
        // Desain Scene 7: pemain yang sudah Mix tidak bisa menambah pigmen sampai Cancel Mix.
        if (readyToMix[player])
        {
            Debug.Log($"[MIX] Ditolak: Pemain {player + 1} sudah Mix");
            return;
        }

        int pigment = GetPigment(player, slot);
        MixingBowl.AddPigment(pigment, amount);
        Debug.Log($"[MIX] Pemain {player + 1} menambahkan {palette.GetPigment(pigment).pigmentName} ke mangkuk. Total unit: {MixingBowl.TotalUnits}");
        StateChanged?.Invoke();
    }

    // Tombol Mix (ready = true) / Unmix (ready = false).
    public void SetReady(int player, bool ready)
    {
        if (readyToMix[player] == ready) return;
        if (ready && MixingBowl.IsEmpty)
        {
            Debug.Log("[MIX] Mix ditolak: mangkuk masih kosong");
            return;
        }

        readyToMix[player] = ready;
        Debug.Log($"[MIX] Pemain {player + 1} {(ready ? "Mix" : "Unmix")} -> siap {readyToMix.Count(r => r)}/{PigmentAssignment.PlayerCount}");
        StateChanged?.Invoke();

        if (readyToMix.All(r => r))
        {
            enabled = false;   // Update berhenti -> rotasi berhenti
            Debug.Log($"[MIX] Semua pemain Mix! Isi mangkuk dikirim ke reveal: [{string.Join(",", MixingBowl.PigmentUnits)}]");
            MixConfirmed?.Invoke();
        }
    }


#if UNITY_EDITOR
    // Simulasi input 3 pemain di 1 device. Jalankan saat Play: ⋮ di header component.
    [ContextMenu("P1 Add slot 0")] private void P1Add0() => AddPigment(0, 0);
    [ContextMenu("P1 Add slot 1")] private void P1Add1() => AddPigment(0, 1);
    [ContextMenu("P1 Mix")]        private void P1Mix()  => SetReady(0, true);
    [ContextMenu("P1 Unmix")]      private void P1Unmix() => SetReady(0, false);

    [ContextMenu("P2 Add slot 0")] private void P2Add0() => AddPigment(1, 0);
    [ContextMenu("P2 Add slot 1")] private void P2Add1() => AddPigment(1, 1);
    [ContextMenu("P2 Mix")]        private void P2Mix()  => SetReady(1, true);
    [ContextMenu("P2 Unmix")]      private void P2Unmix() => SetReady(1, false);

    [ContextMenu("P3 Add slot 0")] private void P3Add0() => AddPigment(2, 0);
    [ContextMenu("P3 Add slot 1")] private void P3Add1() => AddPigment(2, 1);
    [ContextMenu("P3 Mix")]        private void P3Mix()  => SetReady(2, true);
    [ContextMenu("P3 Unmix")]      private void P3Unmix() => SetReady(2, false);

    [ContextMenu("Status")]
    private void DebugStatus()
    {
        var units = MixingBowl.PigmentUnits;
        string isi = MixingBowl.IsEmpty
            ? "kosong"
            : string.Join(" ", Enumerable.Range(0, palette.Count)
                .Where(i => units[i] > 0)
                .Select(i => $"{palette.GetPigment(i).pigmentName}:{units[i]}"));
        string hex = ColorUtility.ToHtmlStringRGB(ColorMixer.Blend(palette, units));

        string players = string.Join("\n", Enumerable.Range(0, PigmentAssignment.PlayerCount)
            .Select(p => $"  P{p + 1}: {Name(p, 0)} + {Name(p, 1)}  {(readyToMix[p] ? "[MIX]" : "")}"));

        Debug.Log($"[MIX] Status (rotasi {RotationTimerLeft:0.0}s lagi)\n{players}\n  Mangkuk: {isi} (total {MixingBowl.TotalUnits}) -> #{hex} <color=#{hex}>■■■■■</color>");
    }

    [ContextMenu("Kosongkan mangkuk")]
    private void DebugClear()
    {
        MixingBowl.Clear();
        Debug.Log("[MIX] Mangkuk dikosongkan");
        StateChanged?.Invoke();
    }
#endif
}
