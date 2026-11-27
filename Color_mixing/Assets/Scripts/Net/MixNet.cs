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
// Host: keadaan mixing (pigmen, mangkuk, rotasi) disimpan DI SINI sejak klien datang, bukan di scene mixing host.
// Jadi client yang lanjut duluan sudah bisa menuang walau host masih membaca kartu request.
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
    private MixingState hostState;             // host: keadaan mixing klien sekarang
    private DayCycleManager hostDay;           // host: DayCycleManager yang didengarkan (klien baru = hostState dibuang)

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
        if (GameNet.IsAuthority) UpdateHost();
        else if (subscribed != null) ApplyLatest();
    }

    // Host: siapkan keadaan mixing begitu klien datang, lalu jalankan timer rotasi (walau scene mixing host belum dimuat).
    private void UpdateHost()
    {
        var day = DayCycleManager.Instance;
        WatchDay(day);
        if (day != null && day.State == DayState.Mixing) EnsureHostState();
        if (hostState == null || !hostState.Tick(Time.deltaTime, PigmentAssignment.RotationSeconds)) return;

        MarkDirty();
        LiveMixing?.OnHostRotated();
    }

    // Host: keadaan mixing untuk scene mixing yang baru dimuat (dipakai ColorMixingController.Awake).
    public MixingState HostState()
    {
        WatchDay(DayCycleManager.Instance);
        EnsureHostState();   // tanpa DayCycleManager (scene di-Play sendirian): dibuat di sini
        return hostState;
    }

    // Klien baru diminta (hari mulai, Yes, No, timeout) = keadaan klien lama dibuang. Lewat event, bukan nomor foto,
    // supaya tetap benar walau tidak ada jaringan yang menaikkan SyncStamp.
    private void WatchDay(DayCycleManager day)
    {
        if (ReferenceEquals(day, hostDay)) return;
        if (!ReferenceEquals(hostDay, null)) hostDay.OnClientRequested -= OnNewClient;
        hostDay = day;
        hostState = null;   // run / scene hari baru: mulai bersih
        if (day != null) day.OnClientRequested += OnNewClient;
    }

    private void OnNewClient() => hostState = null;   // dibuat ulang di Update berikutnya (hari sedang Mixing)

    // Mangkuk kosong, pigmen dibagi, timer rotasi mulai.
    private void EnsureHostState()
    {
        if (hostState != null) return;
        hostState = new MixingState(PigmentAssignment.PigmentCount);
        hostState.Deal(PigmentAssignment.RotationSeconds);
        Debug.Log($"[MIX] Klien baru: pigmen dibagi [{string.Join(",", hostState.Slots)}]");
        MarkDirty();
    }

    // Controller scene mixing host, hanya kalau memakai keadaan sekarang (bukan scene klien lama yang sedang ditutup).
    private ColorMixingController LiveMixing => mixing != null && mixing.Uses(hostState) ? mixing : null;

    private void LateUpdate()
    {
        // Banyak perubahan dalam satu frame (mis. vote menang + Redo) jadi SATU foto yang konsisten.
        if (!dirty) return;
        dirty = false;

        var net = GameNet.Instance;
        if (net == null || !net.IsHost || net.IsOffline || hostState == null) return;   // tidak ada yang diajak bicara

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
        s.slots = hostState.Slots;
        s.units = hostState.Units;
        s.ready = hostState.ReadyFlags;
        s.confirmed = hostState.IsConfirmed;
        s.rotationLeft = hostState.RotationLeft;
        s.rotationCount = hostState.RotationCount;
        s.lastPourers = hostState.LastPourers;
        // Vote hanya dari scene mixing yang memakai keadaan sekarang: scene klien lama yang sedang ditutup
        // tidak boleh mengirim hasil vote-nya (mis. Apply) ke klien yang baru.
        if (voting != null && LiveMixing != null) voting.FillSnapshot(s);
        else { s.votes = new int[PigmentAssignment.PlayerCount]; }
        return s;
    }

    private void OnAdd(NetMessage message)
    {
        if (!GameNet.IsAuthority || hostState == null) return;
        var data = message.Read<AddData>();
        if (data == null) return;

        // Pengirimnya = message.from. Jumlah dibatasi supaya satu pesan aneh tidak memenuhi mangkuk.
        int amount = Mathf.Clamp(data.amount, 1, MaxAmount);
        var live = LiveMixing;
        if (live != null) live.AddPigment(message.from, data.slot, amount);   // lewat controller: animasi tuang di host
        else if (hostState.TryAdd(message.from, data.slot, amount)) MarkDirty();   // host masih di kartu request
    }

    private void OnReady(NetMessage message)
    {
        if (!GameNet.IsAuthority || hostState == null) return;
        var data = message.Read<ReadyData>();
        if (data == null) return;

        var live = LiveMixing;
        if (live != null) live.SetReady(message.from, data.ready);
        else if (hostState.TrySetReady(message.from, data.ready)) MarkDirty();   // konfirmasi tetap butuh host (pemain 0) Mix di scene
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
