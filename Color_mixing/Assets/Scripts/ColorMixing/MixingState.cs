using System.Linq;

// Aturan mixing tanpa Unity: siapa memegang pigmen apa, isi mangkuk, siapa yang sudah Mix.
// ColorMixingController hanya menambah timer rotasi, event, dan log di atas ini.
// Dipisah supaya aturannya bisa dites tanpa scene, dan nanti host memakai kelas yang sama untuk memeriksa kiriman client.
public class MixingState
{
    private readonly int pigmentCount;
    private readonly bool[] ready = new bool[PigmentAssignment.PlayerCount];
    private int[] slots;
    private readonly int[] lastPourers;   // per pigmen: slot penuang terakhir (pemain * 2 + slot), -1 = belum ada

    public MixingState(int pigmentCount)
    {
        this.pigmentCount = pigmentCount;
        Bowl = new MixingBowl(pigmentCount);
        lastPourers = Enumerable.Repeat(-1, pigmentCount).ToArray();
    }

    public MixingBowl Bowl { get; }
    public bool IsConfirmed { get; private set; }            // true setelah semua pemain Mix
    public int ReadyCount => ready.Count(r => r);
    public bool IsReady(int player) => IsPlayer(player) && ready[player];

    // Salinan pembagian pigmen (6 slot: pemain p memegang slot 2p dan 2p + 1). Null kalau belum dibagi.
    public int[] Slots => slots == null ? null : (int[])slots.Clone();

    // Salinan keadaan untuk dikirim ke client.
    public int[] Units => Bowl.PigmentUnits.ToArray();
    public bool[] ReadyFlags => (bool[])ready.Clone();
    public int[] LastPourers => (int[])lastPourers.Clone();

    // Siapa yang terakhir menuang pigmen ini: pemain * 2 + slot, atau -1 kalau belum ada / tidak diketahui.
    // Disimpan saat menuang, jadi tetap benar walau pigmen sudah dirotasi ke pemain lain.
    public int LastPourer(int pigment) => pigment >= 0 && pigment < pigmentCount ? lastPourers[pigment] : -1;

    // Client: ganti seluruh keadaan dengan kiriman host. false (dan tidak ada yang berubah) kalau datanya tidak masuk akal.
    // newLastPourers boleh null / tidak cocok (kiriman versi lama): dianggap tidak diketahui, foto tetap diterima.
    public bool Import(int[] newSlots, int[] units, bool[] newReady, bool confirmed, int[] newLastPourers = null)
    {
        if (newReady == null || newReady.Length != ready.Length) return false;
        if (units == null || units.Length != pigmentCount || units.Any(n => n < 0)) return false;
        if (!SetSlots(newSlots)) return false;

        Bowl.Set(units);
        System.Array.Copy(newReady, ready, ready.Length);
        IsConfirmed = confirmed;

        bool pourersValid = newLastPourers != null && newLastPourers.Length == pigmentCount
                            && newLastPourers.All(p => p >= -1 && p < PigmentAssignment.PlayerCount * 2);
        for (int i = 0; i < pigmentCount; i++) lastPourers[i] = pourersValid ? newLastPourers[i] : -1;
        return true;
    }

    // Pasang pembagian pigmen (hasil PigmentAssignment.Shuffle di host, atau kiriman host di client).
    // false kalau panjangnya salah atau ada index pigmen di luar palet.
    public bool SetSlots(int[] newSlots)
    {
        if (newSlots == null || newSlots.Length != PigmentAssignment.PlayerCount * 2) return false;
        if (newSlots.Any(p => p < 0 || p >= pigmentCount)) return false;

        slots = (int[])newSlots.Clone();
        return true;
    }

    // slot 0 atau 1 milik pemain -> index pigmen di palet. -1 kalau tidak valid.
    public int GetPigment(int player, int slot) =>
        slots != null && IsPlayer(player) && (slot == 0 || slot == 1) ? slots[player * 2 + slot] : -1;

    // Pemain yang sudah Mix tidak bisa menambah pigmen sampai Un-Mix.
    public bool TryAdd(int player, int slot, int amount = 1)
    {
        int pigment = GetPigment(player, slot);
        if (pigment < 0 || amount < 1 || IsConfirmed || ready[player]) return false;

        Bowl.AddPigment(pigment, amount);
        lastPourers[pigment] = player * 2 + slot;
        return true;
    }

    // Mix (ready = true) atau Un-Mix (false). false kalau tidak ada yang berubah: sudah final, status sama, atau mangkuk kosong.
    public bool TrySetReady(int player, bool value)
    {
        if (!IsPlayer(player) || IsConfirmed || ready[player] == value) return false;
        if (value && Bowl.IsEmpty) return false;

        ready[player] = value;
        IsConfirmed = ready.All(r => r);
        return true;
    }

    // Redo menang: mangkuk kosong, semua Mix dibatalkan. Pembagian pigmen dibagi ulang oleh pemanggil (SetSlots).
    public void ResetForRedo()
    {
        Bowl.Clear();
        System.Array.Clear(ready, 0, ready.Length);
        for (int i = 0; i < pigmentCount; i++) lastPourers[i] = -1;
        IsConfirmed = false;
    }

    private static bool IsPlayer(int player) => player >= 0 && player < PigmentAssignment.PlayerCount;
}
