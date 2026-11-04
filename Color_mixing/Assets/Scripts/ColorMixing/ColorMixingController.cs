using UnityEngine;
using System;
using System.Linq;

public class ColorMixingController : MonoBehaviour
{
    // MARK: - Inspector
    // referensi ke PigmentPalette
    [SerializeField] private PigmentPalette palette;
    // interval acak ulang pigmen pemain (detik)
    [SerializeField] private float rotationInterval = 10f;

    // MARK: - State
    private int[] slots; // array untuk menyimpan indeks pigmen yang dipegang oleh pemain
    private readonly bool[] readyToMix = new bool[PigmentAssignment.PlayerCount]; // status siap mencampur untuk setiap pemain

    // MARK: - Public Properties
    public PigmentPalette Palette => palette;
    public MixingBowl MixingBowl { get; private set; } // referensi ke MixingBowl
    public float RotationTimerLeft { get; private set; } // waktu tersisa untuk rotasi pigmen pemain diacak ulang
    public bool IsMixConfirmed { get; private set; }   // true setelah semua pemain Mix
    public int ReadyCount => readyToMix.Count(r => r);   // jumlah pemain yang sudah Mix
    public bool IsReady(int player) => readyToMix[player]; // cek apakah pemain siap mencampur

    // MARK: - Events
    public event Action StateChanged; // event untuk update UI ketika ada perubahan state (rotasi pigmen, penambahan pigmen ke bowl, dll)
    public event Action MixConfirmed; // event untuk menandai bahwa semua pemain siap mencampur
    public event Action Rotated; // event untuk menandai bahwa pigmen pemain telah diacak ulang

    // MARK: - Unity Lifecycle
    private void Awake()
    {
        MixingBowl = new MixingBowl(palette.Count);
        AssignPigments();
    }

    // Fase mixing dimulai: timer klien mulai jalan (kalau scene ini dimuat dari ClientArrival).
    private void Start()
    {
        var timer = FindAnyObjectByType<ClientTimer>();
        if (timer != null) timer.StartTimer();
    }

    private void Update()
    {
        RotationTimerLeft -= Time.deltaTime;
        if (RotationTimerLeft > 0f) return;

        AssignPigments();
        Rotated?.Invoke();
    }

    // MARK: - Pigment Rotation
    // Bagi ulang pigmen ke pemain + reset timer.
    private void AssignPigments()
    {
        slots = PigmentAssignment.Shuffle(palette.Count);
        RotationTimerLeft = rotationInterval;
        Debug.Log($"[MIX] Pembagian pigmen: P1 {Name(0, 0)}+{Name(0, 1)}, P2 {Name(1, 0)}+{Name(1, 1)}, P3 {Name(2, 0)}+{Name(2, 1)}");
        StateChanged?.Invoke();
    }

    // slot 0 atau 1 milik pemain -> index pigmen di palet
    public int GetPigment(int player, int slot) => slots[player * 2 + slot];

    private string Name(int player, int slot) => palette.GetPigment(GetPigment(player, slot)).pigmentName;

    // MARK: - Player Actions
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
        if (IsMixConfirmed || readyToMix[player] == ready) return;
        if (ready && MixingBowl.IsEmpty)
        {
            Debug.Log("[MIX] Mix ditolak: mangkuk masih kosong");
            return;
        }

        readyToMix[player] = ready;
        IsMixConfirmed = readyToMix.All(r => r);
        if (IsMixConfirmed) enabled = false;   // Update berhenti -> rotasi berhenti

        Debug.Log($"[MIX] Pemain {player + 1} {(ready ? "Mix" : "Unmix")} -> siap {ReadyCount}/{PigmentAssignment.PlayerCount}");
        StateChanged?.Invoke();   // UI refresh SETELAH state final

        if (IsMixConfirmed)
        {
            Debug.Log($"[MIX] Semua pemain Mix! Isi mangkuk dikirim ke reveal: [{string.Join(",", MixingBowl.PigmentUnits)}]");
            MixConfirmed?.Invoke();
        }
    }

    // MARK: - Redo
    // Redo menang: mangkuk kosong, vote Mix di-reset, kembali ke fase mixing.
    public void ResetForRedo()
    {
        MixingBowl.Clear();
        Array.Clear(readyToMix, 0, readyToMix.Length);
        IsMixConfirmed = false;
        enabled = true;      // Update jalan lagi -> rotasi lanjut
        AssignPigments();    // bagi ulang pigmen + timer rotasi 10 s + StateChanged
        Debug.Log("[MIX] Redo: mangkuk dikosongkan, kembali mixing");
    }

#if UNITY_EDITOR
    // MARK: - Debug (Editor Only)
    // Simulasi input 3 pemain di 1 device. Jalankan saat Play: ⋮ di header component.
    [ContextMenu("P1 Add slot 0")] private void P1Add0() => AddPigment(0, 0);
    [ContextMenu("P1 Add slot 1")] private void P1Add1() => AddPigment(0, 1);
    [ContextMenu("P1 Mix")] private void P1Mix() => SetReady(0, true);
    [ContextMenu("P1 Unmix")] private void P1Unmix() => SetReady(0, false);

    [ContextMenu("P2 Add slot 0")] private void P2Add0() => AddPigment(1, 0);
    [ContextMenu("P2 Add slot 1")] private void P2Add1() => AddPigment(1, 1);
    [ContextMenu("P2 Mix")] private void P2Mix() => SetReady(1, true);
    [ContextMenu("P2 Unmix")] private void P2Unmix() => SetReady(1, false);

    [ContextMenu("P3 Add slot 0")] private void P3Add0() => AddPigment(2, 0);
    [ContextMenu("P3 Add slot 1")] private void P3Add1() => AddPigment(2, 1);
    [ContextMenu("P3 Mix")] private void P3Mix() => SetReady(2, true);
    [ContextMenu("P3 Unmix")] private void P3Unmix() => SetReady(2, false);

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
    [ContextMenu("Redo (reset)")] private void DebugRedo() => ResetForRedo();
    // Inspector saat Play: pigmen tiap pemain saat ini + tombol simulasi 3 pemain.
    [UnityEditor.CustomEditor(typeof(ColorMixingController))]
    public class ColorMixingControllerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;   // ikut berganti saat rotasi

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;

            var controller = (ColorMixingController)target;
            for (int player = 0; player < PigmentAssignment.PlayerCount; player++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"P{player + 1}", GUILayout.Width(24));

                for (int slot = 0; slot < 2; slot++)
                {
                    Pigment pigment = controller.Palette.GetPigment(controller.GetPigment(player, slot));
                    GUI.backgroundColor = pigment.color;
                    if (GUILayout.Button(pigment.pigmentName)) controller.AddPigment(player, slot);
                }

                GUI.backgroundColor = Color.white;
                bool ready = controller.IsReady(player);
                if (GUILayout.Button(ready ? "Unmix" : "Mix")) controller.SetReady(player, !ready);
                GUILayout.EndHorizontal();
            }
        }
    }
#endif
}
