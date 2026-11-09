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
    private MixingState state; // aturan mixing (pigmen tiap pemain, isi mangkuk, siapa yang sudah Mix)

    private bool synced;          // client: sudah menerima foto pertama dari host
    private int lastRotationCount;

    // MARK: - Public Properties
    // Controller yang sedang aktif di scene (dipakai MixNet untuk menghubungkan jaringan). Null kalau scene mixing belum dimuat.
    public static ColorMixingController Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Current = null;

    // Host / satu device = pemegang aturan. Client hanya mengirim permintaan ke host dan menampilkan foto dari host.
    public bool IsAuthority => GameNet.IsAuthority;
    // Client: false sampai foto pertama host tiba (sebelum itu pigmen belum dibagi, tombol dimatikan).
    public bool IsSynced => IsAuthority || synced;
    public int RotationCount { get; private set; }   // naik tiap pigmen dirotasi
    public PigmentPalette Palette => palette;
    public MixingBowl MixingBowl => state.Bowl; // referensi ke MixingBowl
    public float RotationTimerLeft { get; private set; } // waktu tersisa untuk rotasi pigmen pemain diacak ulang
    public bool IsMixConfirmed => state.IsConfirmed;   // true setelah semua pemain Mix
    public int ReadyCount => state.ReadyCount;   // jumlah pemain yang sudah Mix
    public bool IsReady(int player) => state.IsReady(player); // cek apakah pemain siap mencampur

    // MARK: - Events
    public event Action StateChanged; // event untuk update UI ketika ada perubahan state (rotasi pigmen, penambahan pigmen ke bowl, dll)
    public event Action MixConfirmed; // event untuk menandai bahwa semua pemain siap mencampur
    public event Action Rotated; // event untuk menandai bahwa pigmen pemain telah diacak ulang

    // MARK: - Unity Lifecycle
    private void Awake()
    {
        Current = this;
        state = new MixingState(palette.Count);

        if (IsAuthority)
        {
            AssignPigments();
        }
        else
        {
            // Sementara, sampai host mengirim pembagian yang asli.
            state.SetSlots(Enumerable.Range(0, PigmentAssignment.PlayerCount * 2).Select(i => i % palette.Count).ToArray());
            RotationTimerLeft = rotationInterval;
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
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
        if (!IsAuthority)
        {
            // Client hanya menghitung mundur untuk tampilan; rotasi sebenarnya datang dari host.
            if (RotationTimerLeft < 0f) RotationTimerLeft = 0f;
            return;
        }
        if (RotationTimerLeft > 0f) return;

        RotationCount++;
        AssignPigments();
        Rotated?.Invoke();
    }

    // MARK: - Pigment Rotation
    // Bagi ulang pigmen ke pemain + reset timer.
    private void AssignPigments()
    {
        state.SetSlots(PigmentAssignment.Shuffle(palette.Count));
        RotationTimerLeft = rotationInterval;
        Debug.Log($"[MIX] Pembagian pigmen: P1 {Name(0, 0)}+{Name(0, 1)}, P2 {Name(1, 0)}+{Name(1, 1)}, P3 {Name(2, 0)}+{Name(2, 1)}");
        StateChanged?.Invoke();
    }

    // slot 0 atau 1 milik pemain -> index pigmen di palet
    public int GetPigment(int player, int slot) => state.GetPigment(player, slot);

    // Penuang terakhir pigmen ini (pemain * 2 + slot), -1 kalau belum ada. Dipakai blob untuk arah tuangan.
    public int LastPourer(int pigment) => state.LastPourer(pigment);

    private string Name(int player, int slot) => palette.GetPigment(GetPigment(player, slot)).pigmentName;

    // MARK: - Player Actions
    // menambahkan pigmen ke bowl
    public void AddPigment(int player, int slot, int amount = 1)
    {
        if (!IsAuthority) { MixNet.RequestAdd(slot, amount); return; }   // client: minta host, hasilnya datang sebagai foto

        // Desain Scene 7: pemain yang sudah Mix tidak bisa menambah pigmen sampai Cancel Mix.
        if (state.IsReady(player))
        {
            Debug.Log($"[MIX] Ditolak: Pemain {player + 1} sudah Mix");
            return;
        }

        if (!state.TryAdd(player, slot, amount)) return;
        int pigment = GetPigment(player, slot);
        Debug.Log($"[MIX] Pemain {player + 1} menambahkan {palette.GetPigment(pigment).pigmentName} ke mangkuk. Total unit: {MixingBowl.TotalUnits}");
        StateChanged?.Invoke();
    }

    // Tombol Mix (ready = true) / Unmix (ready = false).
    public void SetReady(int player, bool ready)
    {
        if (!IsAuthority) { MixNet.RequestReady(ready); return; }

        if (ready && !state.IsConfirmed && !state.IsReady(player) && MixingBowl.IsEmpty)
        {
            Debug.Log("[MIX] Mix ditolak: mangkuk masih kosong");
            return;
        }
        if (!state.TrySetReady(player, ready)) return;

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
        if (!IsAuthority) return;
        state.ResetForRedo();
        enabled = true;      // Update jalan lagi -> rotasi lanjut
        AssignPigments();    // bagi ulang pigmen + timer rotasi 10 s + StateChanged
        Debug.Log("[MIX] Redo: mangkuk dikosongkan, kembali mixing");
    }

    // MARK: - Sinkronisasi
    // Host: isi bagian mixing dari foto.
    public void FillSnapshot(MixSnapshot s)
    {
        s.slots = state.Slots;
        s.units = state.Units;
        s.ready = state.ReadyFlags;
        s.confirmed = state.IsConfirmed;
        s.rotationLeft = RotationTimerLeft;
        s.rotationCount = RotationCount;
        s.lastPourers = state.LastPourers;
    }

    // Client: pasang bagian mixing dari foto host. false = ditolak (ini host, atau datanya rusak).
    public bool ApplyRemote(MixSnapshot s)
    {
        if (IsAuthority || s == null) return false;
        if (!state.Import(s.slots, s.units, s.ready, s.confirmed, s.lastPourers)) return false;

        RotationTimerLeft = s.rotationLeft;
        enabled = !s.confirmed;   // sama seperti host: selesai Mix = timer rotasi berhenti

        bool rotated = synced && s.rotationCount > lastRotationCount;
        lastRotationCount = s.rotationCount;
        synced = true;

        StateChanged?.Invoke();
        if (rotated) Rotated?.Invoke();
        return true;
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
            .Select(p => $"  P{p + 1}: {Name(p, 0)} + {Name(p, 1)}  {(IsReady(p) ? "[MIX]" : "")}"));

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
