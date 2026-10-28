using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Apple.GameKit;
using Apple.GameKit.Multiplayer;
using Apple.GameKit.Players;

public enum LobbyMode { None, Host, Client }

[Serializable]
public class LobbyPlayer
{
    public string name;
    public bool isHost;
    public bool isLocal;
    public bool isReady;      // centang hijau (host tidak pernah perlu ready)
}

[Serializable]
public class LobbyMessage
{
    // "HELLO" (perkenalan, minta balasan), "ACK" (balasan), "ROOM" (ganti nama room),
    // "READY" (client ganti status ready), "KICK" (host mengeluarkan pemain = target),
    // "REFORM" (host: slot kosong, semua yang tersisa mencari match baru dengan kode yang sama)
    public string type;
    public string name;
    public bool isHost;
    public string roomName;   // diisi host: nama room kustom (kosong = default)
    public bool ready;        // status ready pengirim
    public string target;     // dipakai KICK: nama pemain yang dikeluarkan
    public string slots;      // diisi host: nama client dipisah "|", urut sesuai urutan datang
}

public class LobbyService : MonoBehaviour
{
    public static LobbyService Instance { get; private set; }

    public const int MaxRoomNameLength = 20;

    public LobbyMode Mode { get; private set; } = LobbyMode.None;
    public string RoomCode { get; private set; }
    public string RoomName { get; private set; } = "";   // kosong = pakai default "<host>'s Room"
    public string LastMessage { get; private set; }
    public readonly List<LobbyPlayer> Players = new List<LobbyPlayer>();

    public event Action OnPlayersChanged;   // juga dipanggil saat nama room / status ready berubah
    public event Action<string> OnRoomClosed;
    public event Action OnJoinConfirmed;    // client sudah masuk match yang berisi host

    // Jumlah pemain agar game boleh dimulai (tombol Play).
    // Ubah RequiredPlayers dan MaxPlayers ke 2 kalau tes dengan 2 device saja.
    private const int RequiredPlayers = 3;
    private const int MaxPlayers = 3;

    // ---- Cara match dibentuk ----
    // true  (DEFAULT): pemain boleh masuk satu per satu. Match jadi saat host + 1 client ketemu,
    //        lalu host terus memanggil GKMatchmaker.AddPlayers sampai lobby penuh (lihat FillRemainingSlots).
    //        Kalau ada pemain keluar / di-kick, host membuka slotnya lagi dengan cara yang sama.
    // false: FindMatch dengan min = max = MaxPlayers. Match baru jadi setelah host dan SEMUA client
    //        ketemu sekaligus, jadi client pertama menunggu di "Mencari room..." sampai client terakhir
    //        memasukkan kode. Pakai ini sebagai cadangan kalau jalur AddPlayers bermasalah di device.
    private static readonly bool ProgressiveFill = true;
    private static readonly int MinMatchPlayers = ProgressiveFill ? 2 : MaxPlayers;

    private const float AddPlayersRetrySeconds = 3f;           // jeda sebelum AddPlayers dicoba lagi
    private const float AddPlayersBusyRetrySeconds = 5f;       // jeda kalau GameKit menjawab "Already matching"
    private const int AddPlayersBusyBeforeCancel = 3;          // setelah sekian kali "Already matching", request lama dibatalkan
    private const float KickRefillDelaySeconds = 1.5f;         // beri waktu pesan KICK sampai sebelum slot dibuka lagi
    // Lama client menunggu match terbentuk. Mode satu per satu: host selalu mencari selama ada slot kosong,
    // jadi tidak perlu lama. Mode sekaligus: client juga menunggu client lain.
    private static readonly float JoinTimeoutSeconds = ProgressiveFill ? 45f : 120f;
    private const float HostReachTimeoutSeconds = 30f;  // lama client menunggu pesan pertama dari host setelah match jadi
    private const int HandshakeSeconds = 15;            // lama saling kirim HELLO
    private const float EditorJoinTimeoutSeconds = 15f;
    private const float ReformDelaySeconds = 1f;       // jeda setelah REFORM terkirim sebelum match lama diputus

    // GameKit hanya mencocokkan pemain bila OR semua atribut == 0xFFFFFFFF.
    // Host + client = cocok. Client + client atau host + host = TIDAK cocok.
    private const uint HostAttributes = 0xFFFF0000u;
    private const uint ClientAttributes = 0x0000FFFFu;

    private const string EditorTestName = "Ivone";    // nama pengganti saat tes di Unity Editor

    private GKMatch match;
    private string localName;
    private int session;              // dinaikkan tiap Create/Join/Leave/Reform supaya proses lama diabaikan
    private int roomNameVersion;      // dinaikkan tiap host mengganti nama room
    private bool joinConfirmed;       // client sudah masuk match
    private bool reforming;           // lagi menyusun ulang match (setelah kick / ada yang keluar)
    private GKMatch fillingMatch;     // host: match yang sedang diisi lewat AddPlayers (null = tidak ada loop)
    private readonly HashSet<string> heardFrom = new HashSet<string>();   // nama pemain yang pesannya sudah diterima
    private readonly HashSet<string> kickedNames = new HashSet<string>(); // host: sudah di-kick, belum terputus dari match
    private readonly List<string> slotOrder = new List<string>();         // client: urutan slot dari host

    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("LobbyService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<LobbyService>();
    }

    private void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // Jalankan sesuatu di main thread (callback GameKit bisa datang dari thread lain).
    private void RunOnMain(Action action)
    {
        mainThreadQueue.Enqueue(action);
    }

    // ---------- Event yang aman ----------
    // Kalau satu subscriber UI melempar exception, sisa alur service (HelloLoop, dll) tidak ikut terhenti.

    private void NotifyPlayersChanged()
    {
        RaiseSafe(OnPlayersChanged);
    }

    private void RaiseSafe(Action evt)
    {
        if (evt == null) return;
        foreach (Action handler in evt.GetInvocationList())
        {
            try { handler(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private void RaiseRoomClosed(string reason)
    {
        var evt = OnRoomClosed;
        if (evt == null) return;
        foreach (Action<string> handler in evt.GetInvocationList())
        {
            try { handler(reason); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // ---------- Status untuk UI ----------

    // Host: Play aktif kalau lobby penuh dan SEMUA pemain selain host sudah ready.
    public bool CanStart
    {
        get
        {
            if (Mode != LobbyMode.Host || reforming) return false;

            int others = 0;
            foreach (var p in Players)
            {
                if (p.isLocal) continue;
                if (!p.isReady) return false;
                others++;
            }
            return others >= RequiredPlayers - 1;
        }
    }

    // Client: tombol Ready baru boleh ditekan setelah masuk match yang berisi host.
    public bool CanSetReady
    {
        get { return Mode == LobbyMode.Client && !reforming && Players.Exists(p => p.isHost && !p.isLocal); }
    }

    public bool IsLocalReady
    {
        get { var me = Players.Find(p => p.isLocal); return me != null && me.isReady; }
    }

    // Pemain selain host, urut sesuai urutan datang. Host yang menentukan urutannya,
    // client mengikuti daftar dari host supaya posisi slot sama di semua device.
    public List<LobbyPlayer> GetOthersInSlotOrder()
    {
        var others = Players.FindAll(p => !p.isHost);
        if (Mode != LobbyMode.Client) return others;   // host: urutan list = urutan datang

        var ordered = new List<LobbyPlayer>();
        foreach (var n in slotOrder)
        {
            var p = others.Find(x => x.name == n);
            if (p != null) ordered.Add(p);
        }
        // Pemain yang belum tercatat di daftar host ditaruh paling belakang.
        foreach (var p in others)
            if (!ordered.Contains(p)) ordered.Add(p);
        return ordered;
    }

    // ---------- API untuk UI ----------

    // Host: buat room, kembalikan kode 5 digit.
    // code / customName diisi saat membuka kembali room dari file save (kode kosong atau tidak valid = acak).
    public string CreateRoom(string code = null, string customName = null)
    {
        LastMessage = null;
        ResetSession();
        Mode = LobbyMode.Host;
        bool validCode = !string.IsNullOrEmpty(code) && code.Length == 5 && int.TryParse(code, out _);
        RoomCode = validCode ? code : UnityEngine.Random.Range(10000, 100000).ToString();
        localName = GetLocalName();
        AddOrUpdatePlayer(localName, true, true);
        if (!string.IsNullOrEmpty(customName)) SetRoomName(customName);
        NotifyPlayersChanged();
        _ = RunMatchmaking(true, session, true);
        return RoomCode;
    }

    // Pemain: masuk room dengan kode. Return false kalau format kode salah.
    public bool JoinRoom(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 5 || !int.TryParse(code, out _))
            return false;

        // Pencarian untuk kode ini sudah berjalan: abaikan panggilan ganda.
        if (Mode == LobbyMode.Client && RoomCode == code && !joinConfirmed)
            return true;

        LastMessage = null;
        ResetSession();
        Mode = LobbyMode.Client;
        RoomCode = code;
        localName = GetLocalName();
        AddOrUpdatePlayer(localName, false, true);
        NotifyPlayersChanged();
        _ = RunMatchmaking(false, session, true);
        return true;
    }

    public void LeaveRoom()
    {
        ResetSession();
        Mode = LobbyMode.None;
        RoomCode = null;
    }

    // Khusus host: ganti nama room. Kosong = kembali ke nama default.
    // Langsung dikirim ke semua pemain, dan client otomatis memperbarui tampilannya.
    public void SetRoomName(string newName)
    {
        if (Mode != LobbyMode.Host) return;

        newName = (newName ?? "").Trim();
        if (newName.Length > MaxRoomNameLength) newName = newName.Substring(0, MaxRoomNameLength);
        if (newName == (RoomName ?? "")) return;

        RoomName = newName;
        roomNameVersion++;
        NotifyPlayersChanged();

        SendMsg("ROOM");
        StartCoroutine(ResendRoomName(session, roomNameVersion));
    }

    // Kirim ulang beberapa kali supaya client yang telat tetap dapat.
    private IEnumerator ResendRoomName(int mySession, int version)
    {
        for (int i = 0; i < 2; i++)
        {
            yield return new WaitForSeconds(i == 0 ? 1f : 2f);
            if (mySession != session || version != roomNameVersion || Mode != LobbyMode.Host) yield break;
            SendMsg("ROOM");
        }
    }

    // Client: ubah status ready (centang hijau). Dikirim ke semua pemain.
    public void SetReady(bool ready)
    {
        if (!CanSetReady) return;

        var me = Players.Find(p => p.isLocal);
        if (me == null || me.isReady == ready) return;

        me.isReady = ready;
        NotifyPlayersChanged();
        SendMsg("READY");
    }

    // Host: keluarkan pemain. Pemain itu kembali ke main menu, sedangkan room (kode & nama yang sama)
    // tetap hidup: host dan pemain yang tersisa mencari match baru, jadi slot kosong bisa diisi lagi.
    public void KickPlayer(string playerName)
    {
        if (Mode != LobbyMode.Host || reforming) return;
        if (!Players.Exists(p => p.name == playerName && !p.isLocal)) return;

        SendMsg("KICK", playerName);

        if (!ProgressiveFill)
        {
            BeginReform();
            return;
        }

        // Pemain yang di-kick memutus dirinya sendiri setelah menerima KICK. Di host dia langsung
        // hilang dari lobby, lalu slotnya dibuka lagi lewat AddPlayers.
        kickedNames.Add(playerName);
        Players.RemoveAll(p => p.name == playerName && !p.isLocal);
        heardFrom.Remove(playerName);
        NotifyPlayersChanged();
        StartCoroutine(RefillAfter(session, KickRefillDelaySeconds));
    }

    // ---------- Mengisi slot kosong (mode satu per satu) ----------

    private IEnumerator RefillAfter(int mySession, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mySession != session || Mode != LobbyMode.Host) yield break;
        RefillEmptySlots();
    }

    // Host: ada slot kosong. Kalau masih ada client di match, match itu diisi lagi lewat AddPlayers.
    // Kalau tinggal host sendiri, cari match baru dari awal (tidak ada yang perlu dikabari).
    private void RefillEmptySlots()
    {
        if (Mode != LobbyMode.Host || reforming) return;

        if (Players.Exists(p => !p.isLocal))
        {
            StartFillingSlots();
            return;
        }

        RestartSearch();
    }

    private void StartFillingSlots()
    {
#if !UNITY_EDITOR
        if (!ProgressiveFill || Mode != LobbyMode.Host || match == null) return;
        if (fillingMatch == match) return;   // loop untuk match ini sudah berjalan
        StartCoroutine(FillRemainingSlots(match, session));
#endif
    }

    // ---------- Menyusun ulang match (slot kosong) ----------

    // Dipanggil host kalau ada pemain yang keluar / di-kick.
    // Match GameKit yang sudah ditinggalkan satu pemain tidak dijamin terisi lagi, jadi: kabari semua
    // (REFORM), lalu host dan pemain yang tersisa sama-sama mencari match baru dengan kode room yang sama.
    private void BeginReform()
    {
        if (reforming || Mode != LobbyMode.Host) return;

        Log("Slot kosong: menyusun ulang room.");
        SendMsg("REFORM");
        EnterReformState();
        StartCoroutine(RestartSearchAfter(session, ReformDelaySeconds));
    }

    // Kembali ke keadaan "menunggu match baru": daftar pemain dikosongkan (kecuali diri sendiri,
    // dan host untuk client). Status ready milik sendiri dipertahankan; pemain lain muncul lagi
    // (lengkap dengan status ready-nya) saat match baru terbentuk.
    private void EnterReformState()
    {
        reforming = true;
        heardFrom.Clear();
        slotOrder.Clear();

        bool keepHost = Mode == LobbyMode.Client;
        Players.RemoveAll(p => !p.isLocal && !(keepHost && p.isHost));

        NotifyPlayersChanged();
    }

    private IEnumerator RestartSearchAfter(int mySession, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (mySession != session || !reforming || Mode == LobbyMode.None) yield break;
        RestartSearch();
    }

    private void RestartSearch()
    {
        session++;   // semua callback match lama diabaikan mulai sekarang
#if !UNITY_EDITOR
        try { GKMatchmaker.Shared.Cancel(); } catch { }
        if (match != null)
        {
            try { match.Disconnect(); } catch { }
            match = null;
        }
#endif
        _ = RunMatchmaking(Mode == LobbyMode.Host, session, false);
    }

    // ---------- Matchmaking ----------

#if UNITY_EDITOR
    private Task RunMatchmaking(bool isHost, int mySession, bool useTimeout)
    {
        reforming = false;   // di Editor tidak ada match baru, anggap langsung selesai
        if (!isHost && useTimeout) StartCoroutine(EditorJoinWatchdog(mySession));
        return Task.CompletedTask;
    }

    // Di Editor tidak ada matchmaking, jadi client tidak boleh menggantung selamanya.
    private IEnumerator EditorJoinWatchdog(int mySession)
    {
        yield return new WaitForSeconds(EditorJoinTimeoutSeconds);
        if (mySession != session || Mode != LobbyMode.Client || joinConfirmed) yield break;
        Fail("Room tidak ditemukan.");
    }
#else
    // useTimeout: false saat menyusun ulang (host masih hidup, jadi client boleh menunggu).
    private async Task RunMatchmaking(bool isHost, int mySession, bool useTimeout)
    {
        try
        {
            var request = GKMatchRequest.Init();
            request.MinPlayers = MinMatchPlayers;   // lihat ProgressiveFill
            request.MaxPlayers = MaxPlayers;
            request.PlayerGroup = (uint)int.Parse(RoomCode);
            request.PlayerAttributes = isHost ? HostAttributes : ClientAttributes;
            Log($"Mencari match, kode={RoomCode}, host={isHost}, min={MinMatchPlayers}, max={MaxPlayers}");

            var findTask = GKMatchmaker.Shared.FindMatch(request);
            findTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            if (!isHost && useTimeout)
            {
                var finished = await Task.WhenAny(findTask, Task.Delay(TimeSpan.FromSeconds(JoinTimeoutSeconds)));
                if (mySession != session) return;
                if (finished != findTask)
                {
                    RunOnMain(() =>
                    {
                        if (mySession != session) return;
                        Log($"Timeout {JoinTimeoutSeconds}s mencari kode {RoomCode}");
                        try { GKMatchmaker.Shared.Cancel(); } catch { }
                        Fail("Room tidak ditemukan atau sudah penuh.");
                    });
                    return;
                }
            }

            var found = await findTask;
            RunOnMain(() => OnMatchFound(found, mySession, isHost));
        }
        catch (Exception e)
        {
            string error = e.Message;
            RunOnMain(() =>
            {
                if (mySession != session) return;
                Log("Matchmaking gagal: " + error);
                Fail("Gagal terhubung ke room.");
            });
        }
    }

    // Selalu dipanggil di main thread.
    private void OnMatchFound(GKMatch found, int mySession, bool isHost)
    {
        if (mySession != session)
        {
            try { found.Disconnect(); } catch { }
            return;
        }

        match = found;
        reforming = false;

        // Semua callback dipindah ke main thread dan dicek session-nya.
        match.Delegate.DataReceived += (data, from) =>
        {
            RunOnMain(() =>
            {
                if (mySession != session) return;
                OnDataReceived(data);
            });
        };
        // iOS bisa mengirim pesan lewat event ini (bukan DataReceived). Pesan yang sama bisa datang
        // lewat dua event, itu aman (diproses idempoten).
        match.Delegate.DataReceivedForPlayer += (data, recipient, from) =>
        {
            RunOnMain(() =>
            {
                if (mySession != session) return;
                OnDataReceived(data);
            });
        };
        match.Delegate.PlayerConnectionChanged += (player, state) =>
        {
            string playerName = player.DisplayName;   // dibaca di thread asal
            string stateText = state.ToString();
            RunOnMain(() =>
            {
                if (mySession != session) return;
                OnPlayerConnectionChanged(playerName, stateText);
            });
        };
        match.Delegate.DidFailWithError += e => Log("Match error: " + e.Message);

        long expectedCount = -1;
        try { expectedCount = match.ExpectedPlayerCount; } catch { }
        Log($"Match terbentuk (host={isHost}, expectedPlayerCount={expectedCount}).");

        // Pemain yang sudah terhubung sebelum handler terpasang (eventnya terlewat).
        try
        {
            foreach (var gkPlayer in match.Players)
                RegisterRemote(gkPlayer.DisplayName);
        }
        catch (Exception e)
        {
            Log("Baca match.Players gagal: " + e.Message);
        }

        StartCoroutine(HelloLoop(mySession, isHost));

        // Mode satu per satu: host membuka slot kosong lewat AddPlayers.
        if (isHost) StartFillingSlots();

        // Client: kalau host tidak kunjung terdengar, jangan menggantung selamanya.
        if (!isHost)
            StartCoroutine(HostReachWatchdog(mySession));

        NotifyPlayersChanged();

        // Berkat PlayerAttributes, match yang didapat client pasti berisi host.
        // Dipanggil paling akhir supaya error di UI tidak menghentikan langkah-langkah di atas.
        if (!isHost) ConfirmJoin();
    }

    // Host, mode satu per satu: selama lobby belum penuh, minta GameKit menambahkan pemain ke match ini.
    // Urutan mengikuti dokumentasi Apple untuk addPlayers(to:matchRequest:):
    //  - dipanggil dari SATU pemain saja (host);
    //  - tunggu dulu sampai pemain yang sudah cocok benar-benar terhubung (expectedPlayerCount == 0);
    //  - request berisi ukuran TOTAL match. GameKit menolak min < 2, jadi request ini bukan
    //    "jumlah pemain tambahan" (kesimpulan dari pesan error di log tes sebelumnya).
    // "Already matching" berarti request awal (FindMatch min=2, max=3) masih mengisi slot sisa sendiri.
    // Itu dibiarkan dulu, dan baru dibatalkan kalau terus-terusan.
    private IEnumerator FillRemainingSlots(GKMatch targetMatch, int mySession)
    {
        fillingMatch = targetMatch;
        yield return FillLoop(targetMatch, mySession);
        if (fillingMatch == targetMatch) fillingMatch = null;
    }

    private bool IsFillingStillValid(GKMatch targetMatch, int mySession)
    {
        return mySession == session && !reforming && Mode == LobbyMode.Host && match == targetMatch;
    }

    private IEnumerator FillLoop(GKMatch targetMatch, int mySession)
    {
        int busyCount = 0;
        while (true)
        {
            if (!IsFillingStillValid(targetMatch, mySession)) yield break;

            if (Players.Count >= MaxPlayers) yield break;   // lobby penuh

            long expected;
            try { expected = targetMatch.ExpectedPlayerCount; } catch { expected = 0; }
            if (expected > 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (busyCount >= AddPlayersBusyBeforeCancel)
            {
                Log("AddPlayers: GameKit terus sibuk, request lama dibatalkan.");
                try { GKMatchmaker.Shared.Cancel(); } catch { }
                busyCount = 0;
            }

            Task addTask = null;
            try
            {
                var req = GKMatchRequest.Init();
                req.MinPlayers = MaxPlayers;   // ukuran TOTAL match
                req.MaxPlayers = MaxPlayers;
                req.PlayerGroup = (uint)int.Parse(RoomCode);
                req.PlayerAttributes = HostAttributes;
                Log($"AddPlayers: total={MaxPlayers}, pemain sekarang={Players.Count}");
                addTask = GKMatchmaker.Shared.AddPlayers(targetMatch, req);
                addTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception e)
            {
                Log("AddPlayers gagal dipanggil: " + e.Message);
            }

            float retryDelay = AddPlayersRetrySeconds;
            if (addTask != null)
            {
                while (!addTask.IsCompleted)
                {
                    if (!IsFillingStillValid(targetMatch, mySession)) yield break;
                    yield return null;
                }

                if (addTask.IsFaulted)
                {
                    string err = addTask.Exception != null ? addTask.Exception.GetBaseException().Message : "unknown";
                    Log("AddPlayers gagal: " + err);
                    if (err.IndexOf("already matching", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        busyCount++;
                        retryDelay = AddPlayersBusyRetrySeconds;
                    }
                    else busyCount = 0;
                }
                else if (addTask.IsCanceled)
                {
                    Log("AddPlayers dibatalkan.");
                }
                else
                {
                    busyCount = 0;
                    retryDelay = 1f;
                    Log("AddPlayers selesai, menunggu pemain baru terhubung.");
                }
            }

            yield return new WaitForSeconds(retryDelay);
        }
    }
#endif

    // Client: setelah match jadi, host harus terdengar dalam HostReachTimeoutSeconds.
    // Kalau tidak, gagal dengan pesan yang jelas (bukan menggantung sampai timeout lain).
    private IEnumerator HostReachWatchdog(int mySession)
    {
        yield return new WaitForSeconds(HostReachTimeoutSeconds);
        if (mySession != session || Mode != LobbyMode.Client || reforming) yield break;

        bool heardHost = Players.Exists(p => p.isHost && !p.isLocal && heardFrom.Contains(p.name));
        if (heardHost) yield break;

        Log($"Host tidak terdengar setelah {HostReachTimeoutSeconds}s sejak match terbentuk.");
        Fail("Tidak bisa terhubung ke host.");
    }

    // Selesai kalau ada minimal 1 pemain lain dan SEMUA pemain lain yang kita tahu sudah terdengar
    // pesannya. Untuk client, host juga harus sudah terdengar. Pemain yang masuk belakangan
    // memperkenalkan diri sendiri lewat HELLO-nya, jadi loop ini tidak perlu menunggu mereka.
    private bool IsHandshakeDone(bool isHost)
    {
        bool anyRemote = false;
        foreach (var p in Players)
        {
            if (p.isLocal) continue;
            anyRemote = true;
            if (!heardFrom.Contains(p.name)) return false;
        }
        if (!anyRemote) return false;

        if (!isHost) return Players.Exists(p => p.isHost && !p.isLocal && heardFrom.Contains(p.name));
        return true;
    }

    // Kirim HELLO tiap 1 detik sampai handshake selesai (untuk memastikan role, nama, dan status ready).
    private IEnumerator HelloLoop(int mySession, bool isHost)
    {
        for (int i = 0; i < HandshakeSeconds; i++)
        {
            if (mySession != session || match == null) yield break;
            if (IsHandshakeDone(isHost)) yield break;

            SendMsg("HELLO");
            yield return new WaitForSeconds(1f);
        }
    }

    // ---------- Pesan antar-device ----------

    private void SendMsg(string type, string target = null)
    {
#if !UNITY_EDITOR
        if (match == null) return;
        var me = Players.Find(p => p.isLocal);

        // Host menyertakan urutan slot (urutan datang) di setiap pesannya.
        string slots = "";
        if (Mode == LobbyMode.Host)
        {
            var names = new List<string>();
            foreach (var p in Players)
                if (!p.isHost) names.Add(p.name);
            slots = string.Join("|", names);
        }

        var msg = new LobbyMessage
        {
            type = type,
            name = localName,
            isHost = Mode == LobbyMode.Host,
            roomName = Mode == LobbyMode.Host ? (RoomName ?? "") : "",
            ready = me != null && me.isReady,
            target = target ?? "",
            slots = slots
        };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        try
        {
            match.Send(data, GKMatch.GKSendDataMode.Reliable);
        }
        catch (Exception e)
        {
            Log($"Kirim {type} gagal: {e.Message}");
        }
#endif
    }

    private void OnDataReceived(byte[] data)
    {
        LobbyMessage msg;
        try { msg = JsonUtility.FromJson<LobbyMessage>(Encoding.UTF8.GetString(data)); }
        catch (Exception e) { Log("Pesan tidak terbaca: " + e.Message); return; }

        HandleMessage(msg);
    }

    // Selalu dipanggil di main thread.
    private void HandleMessage(LobbyMessage msg)
    {
        if (msg == null) return;
        if (Mode == LobbyMode.None) return;   // sisa dari session lama
        if (reforming) return;                // lagi menyusun ulang: pesan dari match lama diabaikan

        if (msg.type != "HELLO" && msg.type != "ACK" && msg.type != "ROOM"
            && msg.type != "READY" && msg.type != "KICK" && msg.type != "REFORM") return;
        if (string.IsNullOrEmpty(msg.name) || msg.name == localName) return;
        if (kickedNames.Contains(msg.name)) return;   // sisa pesan dari pemain yang baru di-kick

        // Ganti nama room, KICK, dan REFORM hanya sah kalau datang dari host.
        if ((msg.type == "ROOM" || msg.type == "KICK" || msg.type == "REFORM") && !msg.isHost) return;

        // Dua host dengan kode yang sama (kebetulan tabrakan): abaikan.
        if (Mode == LobbyMode.Host && msg.isHost)
        {
            Log("Pesan dari host lain diabaikan (kode bentrok).");
            return;
        }

        if (msg.type == "KICK")
        {
            if (Mode != LobbyMode.Client) return;

            if (msg.target == localName)
            {
                Fail("Kamu dikeluarkan oleh host.");
                return;
            }

            Players.RemoveAll(p => p.name == msg.target && !p.isLocal);
            heardFrom.Remove(msg.target);
            NotifyPlayersChanged();
            return;
        }

        if (msg.type == "REFORM")
        {
            if (Mode != LobbyMode.Client) return;

            Log("Host menyusun ulang room, ikut mencari match baru.");
            EnterReformState();
            StartCoroutine(RestartSearchAfter(session, ReformDelaySeconds));
            return;
        }

        heardFrom.Add(msg.name);
        AddOrUpdatePlayer(msg.name, msg.isHost, false);

        var sender = Players.Find(p => p.name == msg.name);
        if (sender != null) sender.isReady = !msg.isHost && msg.ready;

        if (Mode == LobbyMode.Client)
        {
            // Pesan adalah sumber role yang pasti: koreksi tebakan dari event koneksi.
            if (sender != null) sender.isHost = msg.isHost;
            if (msg.isHost)
            {
                foreach (var p in Players)
                    if (!p.isLocal && p.name != msg.name) p.isHost = false;

                // Host adalah sumber kebenaran untuk nama room (kosong = default).
                RoomName = msg.roomName ?? "";

                // ...dan untuk urutan slot (urutan datang).
                slotOrder.Clear();
                if (!string.IsNullOrEmpty(msg.slots)) slotOrder.AddRange(msg.slots.Split('|'));
            }
        }

        NotifyPlayersChanged();

        // HELLO selalu dibalas ACK. ACK, ROOM, dan READY tidak dibalas (mencegah ping-pong).
        if (msg.type == "HELLO") SendMsg("ACK");

        if (Mode == LobbyMode.Client && msg.isHost) ConfirmJoin();
    }

    private void OnPlayerConnectionChanged(string playerName, string state)
    {
        Log($"{playerName} -> {state}");

        if (string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase))
        {
            RegisterRemote(playerName);
            SendMsg("HELLO");
            return;
        }

        if (!string.Equals(state, "Disconnected", StringComparison.OrdinalIgnoreCase)) return;

        // Pemain yang di-kick sudah dihapus dari lobby saat KickPlayer; sekarang dia boleh masuk lagi nanti.
        if (kickedNames.Remove(playerName)) return;

        var p = Players.Find(x => x.name == playerName);
        if (p == null) return;

        // Lagi menyusun ulang: match lama memang sedang diputus, itu bukan "host keluar".
        if (reforming) return;

        Players.Remove(p);
        heardFrom.Remove(playerName);

        if (p.isHost && Mode == LobbyMode.Client)
        {
            Fail("Host keluar dari room.");
            return;
        }

        // Ada client yang keluar: slot kosong dibuka lagi.
        // Mode satu per satu: match tetap dipakai dan diisi lewat AddPlayers.
        // Mode sekaligus: host menyusun ulang (client lain menyusul lewat pesan REFORM).
        if (Mode == LobbyMode.Host)
        {
            if (ProgressiveFill)
            {
                NotifyPlayersChanged();
                RefillEmptySlots();
            }
            else
            {
                BeginReform();
            }
            return;
        }

        NotifyPlayersChanged();
    }

    // Pemain lain terhubung ke match (dari event koneksi atau match.Players).
    // Hanya kalau match berisi 2 pemain, remote-nya pasti host (bagi client).
    // Dengan 3 pemain, role ditentukan oleh pesan HELLO/ACK.
    private void RegisterRemote(string playerName)
    {
        if (Mode == LobbyMode.None) return;
        if (string.IsNullOrEmpty(playerName) || playerName == localName) return;
        if (kickedNames.Contains(playerName)) return;

        bool guessHost = Mode == LobbyMode.Client && RequiredPlayers <= 2 && !Players.Exists(p => p.isHost);
        AddOrUpdatePlayer(playerName, guessHost, false);
        NotifyPlayersChanged();
    }

    private void ConfirmJoin()
    {
        if (joinConfirmed) return;
        joinConfirmed = true;

        RaiseSafe(OnJoinConfirmed);
    }

    // ---------- Helper ----------

    private void AddOrUpdatePlayer(string name, bool isHost, bool isLocal)
    {
        var existing = Players.Find(p => p.name == name);
        if (existing != null)
        {
            if (isHost) existing.isHost = true;
            return;
        }
        Players.Add(new LobbyPlayer { name = name, isHost = isHost, isLocal = isLocal });
    }

    private string GetLocalName()
    {
#if UNITY_EDITOR
        return EditorTestName;
#else
        return GKLocalPlayer.Local.DisplayName;
#endif
    }

    private void ResetSession()
    {
        joinConfirmed = false;
        reforming = false;
        heardFrom.Clear();
        slotOrder.Clear();
        kickedNames.Clear();
        fillingMatch = null;
        RoomName = "";
        roomNameVersion++;
        session++;
#if !UNITY_EDITOR
        try { GKMatchmaker.Shared.Cancel(); } catch { }
        if (match != null)
        {
            try { match.Disconnect(); } catch { }
            match = null;
        }
#endif
        Players.Clear();
    }

    private void Fail(string reason)
    {
        Log("Gagal: " + reason);
        ResetSession();
        Mode = LobbyMode.None;
        RoomCode = null;
        LastMessage = reason;
        RaiseRoomClosed(reason);
    }

    private void Log(string message)
    {
        Debug.Log("[Lobby] " + message);
    }

    // ---------- Debug (tombol tes di Editor) ----------

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (Mode == LobbyMode.None) return;

        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);

        if (GUI.Button(new Rect(10, 10, 160, 28), "Tambah pemain palsu"))
        {
            AddOrUpdatePlayer("Pemain " + (Players.Count + 1), false, false);
            NotifyPlayersChanged();
        }
        if (GUI.Button(new Rect(10, 42, 160, 28), "Hapus pemain palsu"))
        {
            var p = Players.FindLast(x => !x.isLocal);
            if (p != null)
            {
                Players.Remove(p);
                NotifyPlayersChanged();
            }
        }

        // Tes centang ready tanpa device lain: ubah status pemain palsu terakhir.
        if (GUI.Button(new Rect(10, 74, 160, 28), "Toggle ready pemain palsu"))
        {
            var p = Players.FindLast(x => !x.isLocal && !x.isHost);
            if (p != null)
            {
                p.isReady = !p.isReady;
                NotifyPlayersChanged();
            }
        }

        // Untuk tes alur join di Editor: pura-pura host merespons.
        if (Mode == LobbyMode.Client && !Players.Exists(x => x.isHost))
        {
            if (GUI.Button(new Rect(10, 106, 160, 28), "Simulasi host merespons"))
            {
                HandleMessage(new LobbyMessage { type = "HELLO", name = "HostPalsu", isHost = true, roomName = "Room Palsu" });
            }
        }
    }
#endif
}