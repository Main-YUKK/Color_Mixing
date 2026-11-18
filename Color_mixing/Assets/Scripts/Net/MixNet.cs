using System;
using UnityEngine;

// Jaringan untuk layar mixing. Dibuat otomatis (tidak perlu dipasang di scene).
//
//   client: tap pigmen / Mix / vote  -> kirim PERMINTAAN ke host (MIX_ADD, MIX_READY, MIX_VOTE)
//   host:   periksa lewat aturan yang sama (MixingState / VoteState), lalu kirim FOTO keadaan ke semua (MIX_STATE)
//   client: pasang foto itu -> UI yang sama (pie chart, ikon Mix, vote) berubah tanpa tahu soal jaringan
//
// Siapa pengirimnya selalu diambil dari transport (message.from), bukan dari isi pesan.
// Scene mixing dimuat sendiri-sendiri di tiap device: foto terakhir disimpan di sini dan dipasang begitu scene client siap.
public class MixNet : MonoBehaviour
{
    private const int MaxAmount = 5;   // batas unit per permintaan tambah pigmen

    [Serializable] private class AddData { public int slot; public int amount; }
    [Serializable] private class ReadyData { public bool ready; }
    [Serializable] private class VoteData { public int vote; }

    public static MixNet Instance { get; private set; }

    private GameNet subscribed;
    private ColorMixingController mixing;      // yang terakhir terlihat
    private ApplyRedoVoteController voting;
    private MixSnapshot latest;                // client: foto terakhir dari host
    private bool dirty;                        // host: ada perubahan, kirim foto di akhir frame
    private int sendSeq;
    private bool pulled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("MixNet");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MixNet>();
    }

    // ---------- Permintaan dari client (dipanggil controller kalau device ini bukan host) ----------

    public static void RequestAdd(int slot, int amount) =>
        Send(NetMessage.Create(NetMessageType.MixAdd, new AddData { slot = slot, amount = amount }));

    public static void RequestReady(bool ready) =>
        Send(NetMessage.Create(NetMessageType.MixReady, new ReadyData { ready = ready }));

    public static void RequestVote(Vote vote) =>
        Send(NetMessage.Create(NetMessageType.MixVote, new VoteData { vote = (int)vote }));

    private static void Send(NetMessage message) => GameNet.Instance?.SendToHost(message);

    // ---------- Pemasangan ----------

    private void OnEnable() => ClientTimer.HostTimedOut += OnHostTimedOut;

    private void OnDisable()
    {
        ClientTimer.HostTimedOut -= OnHostTimedOut;
        Detach(subscribed);
        subscribed = null;
    }

    private void Update()
    {
        // GameNet dibuat belakangan (dan bisa diganti): pasang ulang handler kalau instance-nya berubah.
        if (GameNet.Instance != subscribed)
        {
            Detach(subscribed);
            subscribed = GameNet.Instance;
            latest = null;
            if (subscribed != null)
            {
                subscribed.Subscribe(NetMessageType.MixAdd, OnAdd);
                subscribed.Subscribe(NetMessageType.MixReady, OnReady);
                subscribed.Subscribe(NetMessageType.MixVote, OnVote);
                subscribed.Subscribe(NetMessageType.MixPull, OnPull);
                subscribed.Subscribe(NetMessageType.MixState, OnState);
                subscribed.Subscribe(NetMessageType.ClientTimeout, OnClientTimeout);
            }
        }

        TrackControllers();
        if (subscribed != null && !GameNet.IsAuthority) ApplyLatest();
    }

    private void LateUpdate()
    {
        // Banyak perubahan dalam satu frame (mis. vote menang + Redo) jadi SATU foto yang konsisten.
        if (!dirty) return;
        dirty = false;

        var net = GameNet.Instance;
        if (net == null || !net.IsHost || net.IsOffline || mixing == null) return;   // tidak ada yang diajak bicara

        net.SendToAll(NetMessage.Create(NetMessageType.MixState, BuildSnapshot()));
    }

    // Scene mixing dimuat / dibuang: sambungkan atau lepaskan event controller-nya.
    private void TrackControllers()
    {
        var m = ColorMixingController.Current;
        var v = ApplyRedoVoteController.Current;

        if (m != mixing)
        {
            if (mixing != null) mixing.StateChanged -= MarkDirty;
            mixing = m;
            pulled = false;
            appliedSeq = -1;   // scene baru: pasang ulang foto terakhir
            if (mixing != null) mixing.StateChanged += MarkDirty;
            MarkDirty();   // host: kabari semua kalau scene mixing-nya baru siap
        }

        if (v != voting)
        {
            if (voting != null) voting.StateChanged -= MarkDirty;
            voting = v;
            appliedSeq = -1;
            if (voting != null) voting.StateChanged += MarkDirty;
            MarkDirty();
        }

        // Client yang scene-nya baru siap minta keadaan terbaru dari host (foto mungkin sudah dikirim sebelum scene ada).
        if (mixing != null && !GameNet.IsAuthority && !pulled && subscribed != null)
        {
            pulled = true;
            subscribed.SendToHost(NetMessage.Create(NetMessageType.MixPull));
        }
    }

    private void MarkDirty() => dirty = true;

    private void Detach(GameNet net)
    {
        if (net == null) return;
        net.Unsubscribe(NetMessageType.MixAdd, OnAdd);
        net.Unsubscribe(NetMessageType.MixReady, OnReady);
        net.Unsubscribe(NetMessageType.MixVote, OnVote);
        net.Unsubscribe(NetMessageType.MixPull, OnPull);
        net.Unsubscribe(NetMessageType.MixState, OnState);
        net.Unsubscribe(NetMessageType.ClientTimeout, OnClientTimeout);
    }

    // ---------- Host ----------

    private MixSnapshot BuildSnapshot()
    {
        var s = new MixSnapshot
        {
            seq = ++sendSeq,
            dayStamp = DayCycleManager.Instance != null ? DayCycleManager.Instance.SyncStamp : 0,
        };
        mixing.FillSnapshot(s);
        if (voting != null) voting.FillSnapshot(s);
        else { s.votes = new int[PigmentAssignment.PlayerCount]; }
        return s;
    }

    private void OnAdd(NetMessage message)
    {
        if (!GameNet.IsAuthority || mixing == null) return;
        var data = message.Read<AddData>();
        if (data == null) return;

        // Pengirimnya = message.from. Jumlah dibatasi supaya satu pesan aneh tidak memenuhi mangkuk.
        mixing.AddPigment(message.from, data.slot, Mathf.Clamp(data.amount, 1, MaxAmount));
    }

    private void OnReady(NetMessage message)
    {
        if (!GameNet.IsAuthority || mixing == null) return;
        var data = message.Read<ReadyData>();
        if (data == null) return;

        mixing.SetReady(message.from, data.ready);
    }

    private void OnVote(NetMessage message)
    {
        if (!GameNet.IsAuthority || voting == null) return;
        var data = message.Read<VoteData>();
        if (data == null || data.vote < (int)Vote.Apply || data.vote > (int)Vote.Redo) return;

        voting.CastVote(message.from, (Vote)data.vote);
    }

    private void OnPull(NetMessage message)
    {
        if (GameNet.IsAuthority) MarkDirty();
    }

    private void OnHostTimedOut()
    {
        var net = GameNet.Instance;
        if (net != null && net.IsHost) net.SendToAll(NetMessage.Create(NetMessageType.ClientTimeout));
    }

    // ---------- Client ----------

    private void OnState(NetMessage message)
    {
        if (message.from != 0 || GameNet.IsAuthority) return;

        var s = message.Read<MixSnapshot>();
        if (s == null) return;
        if (latest != null && s.dayStamp == latest.dayStamp && s.seq <= latest.seq) return;   // foto lama / dobel

        latest = s;
        ApplyLatest();
    }

    // Pasang foto terakhir ke scene mixing yang sedang aktif, kalau foto itu memang milik klien ini.
    private void ApplyLatest()
    {
        if (latest == null || mixing == null) return;

        var day = DayCycleManager.Instance;
        if (day != null && latest.dayStamp != day.SyncStamp) return;   // milik klien sebelumnya (atau hari belum sampai)
        if (latest.seq == appliedSeq && latest.dayStamp == appliedStamp) return;

        appliedSeq = latest.seq;
        appliedStamp = latest.dayStamp;
        mixing.ApplyRemote(latest);
        if (voting != null) voting.ApplyRemote(latest);
    }

    private int appliedSeq = -1;
    private int appliedStamp = -1;

    private void OnClientTimeout(NetMessage message)
    {
        if (message.from != 0 || GameNet.IsAuthority) return;
        if (ClientTimer.Current != null) ClientTimer.Current.ForceTimeout();
    }
}
