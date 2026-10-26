using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
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
}

[Serializable]
public class LobbyMessage
{
    public string type;       // "HELLO" (perkenalan, minta balasan), "ACK" (balasan), "ROOM" (ganti nama room)
    public string name;
    public bool isHost;
    public string roomName;   // diisi host: nama room kustom (kosong = default)
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

    public event Action OnPlayersChanged;   // juga dipanggil saat nama room berubah
    public event Action<string> OnRoomClosed;
    public event Action OnJoinConfirmed;    // client sudah masuk match yang berisi host

    // Match HANYA terbentuk kalau jumlah pemain ini sudah terkumpul (semua mencari bersamaan).
    // Ubah ke 2 kalau tes dengan 2 device saja.
    private const int RequiredPlayers = 3;
    private const int MaxPlayers = 3;

    private const float JoinTimeoutSeconds = 60f;     // lama client menunggu match terbentuk
    private const int HandshakeSeconds = 15;          // lama saling kirim HELLO
    private const float EditorJoinTimeoutSeconds = 15f;
    private const bool ShowOnScreenLog = false;        // set false sebelum rilis

    // GameKit hanya mencocokkan pemain bila OR semua atribut == 0xFFFFFFFF.
    // Host + client = cocok. Client + client atau host + host = TIDAK cocok.
    private const uint HostAttributes = 0xFFFF0000u;
    private const uint ClientAttributes = 0x0000FFFFu;

    private const string EditorTestName = "Ivone";    // nama pengganti saat tes di Unity Editor

    private GKMatch match;
    private string localName;
    private int session;              // dinaikkan tiap Create/Join/Leave supaya proses lama diabaikan
    private int matchSession;         // session milik match yang sedang aktif
    private int roomNameVersion;      // dinaikkan tiap host mengganti nama room
    private bool joinConfirmed;       // client sudah masuk match
    private readonly HashSet<string> heardFrom = new HashSet<string>();   // nama pemain yang pesannya sudah diterima

    private readonly List<string> logLines = new List<string>();
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

    // ---------- API untuk UI ----------

    // Host: buat room, kembalikan kode 5 digit.
    public string CreateRoom()
    {
        LastMessage = null;
        ResetSession();
        Mode = LobbyMode.Host;
        RoomCode = UnityEngine.Random.Range(10000, 100000).ToString();
        localName = GetLocalName();
        AddOrUpdatePlayer(localName, true, true);
        OnPlayersChanged?.Invoke();
        _ = RunMatchmaking(true, session);
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
        OnPlayersChanged?.Invoke();
        _ = RunMatchmaking(false, session);
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
        OnPlayersChanged?.Invoke();

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

    // ---------- Matchmaking ----------

#if UNITY_EDITOR
    private Task RunMatchmaking(bool isHost, int mySession)
    {
        Log("Editor: Game Center dilewati (hanya tampilan).");
        if (!isHost) StartCoroutine(EditorJoinWatchdog(mySession));
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
    private async Task RunMatchmaking(bool isHost, int mySession)
    {
        try
        {
            var request = GKMatchRequest.Init();
            request.MinPlayers = RequiredPlayers;
            request.MaxPlayers = MaxPlayers;
            request.PlayerGroup = (uint)int.Parse(RoomCode);
            request.PlayerAttributes = isHost ? HostAttributes : ClientAttributes;
            Log($"Mencari match, kode={RoomCode}, host={isHost}, butuh={RequiredPlayers}");

            var findTask = GKMatchmaker.Shared.FindMatch(request);
            findTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            if (!isHost)
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
                        Fail("Room tidak ditemukan atau pemain lain belum bergabung.");
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
        matchSession = mySession;

        // Semua callback dipindah ke main thread dan dicek session-nya.
        match.Delegate.DataReceived += (data, from) =>
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

        // Dugaan: iOS bisa memakai event data lain selain DataReceived. Pasang juga di semuanya.
        HookExtraDataEvents(match.Delegate);

        Log("Match terbentuk.");

        // Pemain yang sudah terhubung sebelum handler terpasang (eventnya terlewat).
        try
        {
            foreach (var gkPlayer in match.Players)
            {
                Log("match.Players: " + gkPlayer.DisplayName);
                RegisterRemote(gkPlayer.DisplayName);
            }
        }
        catch (Exception e)
        {
            Log("Baca match.Players gagal: " + e.Message);
        }

        // Berkat PlayerAttributes, match yang didapat client pasti berisi host.
        if (!isHost) ConfirmJoin();

        StartCoroutine(HelloLoop(mySession, isHost));
    }

    // Mendaftar ke semua event bernama "Data..." selain DataReceived (yang sudah dipasang di atas).
    // Pesan yang sama bisa datang lewat dua event, itu aman (diproses idempoten).
    private void HookExtraDataEvents(object del)
    {
        try
        {
            var names = new List<string>();
            foreach (var ev in del.GetType().GetEvents())
            {
                names.Add(ev.Name);
                if (ev.Name == "DataReceived" || !ev.Name.StartsWith("Data")) continue;

                var ps = ev.EventHandlerType.GetMethod("Invoke").GetParameters();
                if (ps.Length < 2 || ps[0].ParameterType != typeof(byte[])) continue;

                string handlerName = ps.Length == 2 ? nameof(ExtraData2) : ps.Length == 3 ? nameof(ExtraData3) : null;
                if (handlerName == null) continue;

                var target = GetType().GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
                var handler = Delegate.CreateDelegate(ev.EventHandlerType, this, target, false);
                if (handler == null) { Log("Hook gagal (tipe tidak cocok): " + ev.Name); continue; }

                ev.AddEventHandler(del, handler);
                Log("Hook event: " + ev.Name);
            }
            Log("Events: " + string.Join(", ", names));
        }
        catch (Exception e)
        {
            Log("Hook event data gagal: " + e.Message);
        }
    }

    private void ExtraData2(byte[] data, object a) { OnExtraData(data); }
    private void ExtraData3(byte[] data, object a, object b) { OnExtraData(data); }

    private void OnExtraData(byte[] data)
    {
        int s = matchSession;
        RunOnMain(() =>
        {
            if (s != session) return;
            OnDataReceived(data);
        });
    }
#endif

    // Host selesai kalau semua pemain lain sudah terdengar pesannya.
    // Client selesai kalau host sudah terdengar pesannya.
    private bool IsHandshakeDone(bool isHost)
    {
        if (isHost) return heardFrom.Count >= RequiredPlayers - 1;
        return Players.Exists(p => p.isHost && !p.isLocal && heardFrom.Contains(p.name));
    }

    // Kirim HELLO tiap 1 detik sampai handshake selesai (untuk memastikan role & nama).
    private IEnumerator HelloLoop(int mySession, bool isHost)
    {
        for (int i = 0; i < HandshakeSeconds; i++)
        {
            if (mySession != session || match == null) yield break;
            if (IsHandshakeDone(isHost)) yield break;

            SendMsg("HELLO");
            yield return new WaitForSeconds(1f);
        }

        if (mySession != session) yield break;
        if (!IsHandshakeDone(isHost))
            Log("Handshake: belum semua pesan diterima (koneksi tetap dipakai).");
    }

    // ---------- Pesan antar-device ----------

    private void SendMsg(string type)
    {
#if !UNITY_EDITOR
        if (match == null) return;
        var msg = new LobbyMessage
        {
            type = type,
            name = localName,
            isHost = Mode == LobbyMode.Host,
            roomName = Mode == LobbyMode.Host ? (RoomName ?? "") : ""
        };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        try
        {
            match.Send(data, GKMatch.GKSendDataMode.Reliable);
            Log(type == "ROOM" ? $"Kirim ROOM \"{msg.roomName}\"" : $"Kirim {type}");
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

        Log($"Terima {msg.type} dari {msg.name} (host={msg.isHost})");
        if (msg.type != "HELLO" && msg.type != "ACK" && msg.type != "ROOM") return;
        if (string.IsNullOrEmpty(msg.name) || msg.name == localName) return;

        // Ganti nama room hanya sah kalau datang dari host.
        if (msg.type == "ROOM" && !msg.isHost) return;

        // Dua host dengan kode yang sama (kebetulan tabrakan): abaikan.
        if (Mode == LobbyMode.Host && msg.isHost)
        {
            Log("Pesan dari host lain diabaikan (kode bentrok).");
            return;
        }

        heardFrom.Add(msg.name);
        AddOrUpdatePlayer(msg.name, msg.isHost, false);

        if (Mode == LobbyMode.Client)
        {
            // Pesan adalah sumber role yang pasti: koreksi tebakan dari event koneksi.
            var sender = Players.Find(p => p.name == msg.name);
            if (sender != null) sender.isHost = msg.isHost;
            if (msg.isHost)
            {
                foreach (var p in Players)
                    if (!p.isLocal && p.name != msg.name) p.isHost = false;

                // Host adalah sumber kebenaran untuk nama room (kosong = default).
                RoomName = msg.roomName ?? "";
            }
        }

        OnPlayersChanged?.Invoke();

        // HELLO selalu dibalas ACK. ACK dan ROOM tidak dibalas (mencegah ping-pong).
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

        var p = Players.Find(x => x.name == playerName);
        if (p == null) return;

        Players.Remove(p);
        heardFrom.Remove(playerName);
        if (p.isHost && Mode == LobbyMode.Client)
        {
            Fail("Host keluar dari room.");
            return;
        }
        OnPlayersChanged?.Invoke();
    }

    // Pemain lain terhubung ke match (dari event koneksi atau match.Players).
    // Hanya kalau match berisi 2 pemain, remote-nya pasti host (bagi client).
    // Dengan 3 pemain, role ditentukan oleh pesan HELLO/ACK.
    private void RegisterRemote(string playerName)
    {
        if (Mode == LobbyMode.None) return;
        if (string.IsNullOrEmpty(playerName) || playerName == localName) return;

        bool guessHost = Mode == LobbyMode.Client && RequiredPlayers <= 2 && !Players.Exists(p => p.isHost);
        AddOrUpdatePlayer(playerName, guessHost, false);
        OnPlayersChanged?.Invoke();
    }

    private void ConfirmJoin()
    {
        if (joinConfirmed) return;
        joinConfirmed = true;
        Log("Join dikonfirmasi.");
        OnJoinConfirmed?.Invoke();
    }

    // ---------- Helper ----------

    private bool AddOrUpdatePlayer(string name, bool isHost, bool isLocal)
    {
        var existing = Players.Find(p => p.name == name);
        if (existing != null)
        {
            if (isHost) existing.isHost = true;
            return false;
        }
        Players.Add(new LobbyPlayer { name = name, isHost = isHost, isLocal = isLocal });
        return true;
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
        heardFrom.Clear();
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
        OnRoomClosed?.Invoke(reason);
    }

    private void Log(string message)
    {
        Debug.Log("[Lobby] " + message);
        lock (logLines)
        {
            logLines.Add(message);
            if (logLines.Count > 9) logLines.RemoveAt(0);
        }
    }

    // ---------- Debug (log di layar + tombol tes di Editor) ----------

    private void OnGUI()
    {
        if (!ShowOnScreenLog && !Application.isEditor) return;

        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float h = Screen.height / scale;

        string text;
        lock (logLines) { text = string.Join("\n", logLines); }

        var style = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(8, h - 150, 520, 145), text, style);

#if UNITY_EDITOR
        if (Mode == LobbyMode.None) return;

        if (GUI.Button(new Rect(10, 10, 160, 28), "Tambah pemain palsu"))
        {
            AddOrUpdatePlayer("Pemain " + (Players.Count + 1), false, false);
            OnPlayersChanged?.Invoke();
        }
        if (GUI.Button(new Rect(10, 42, 160, 28), "Hapus pemain palsu"))
        {
            var p = Players.FindLast(x => !x.isLocal);
            if (p != null)
            {
                Players.Remove(p);
                OnPlayersChanged?.Invoke();
            }
        }

        // Untuk tes alur join di Editor: pura-pura host merespons.
        if (Mode == LobbyMode.Client && !Players.Exists(x => x.isHost))
        {
            if (GUI.Button(new Rect(10, 74, 160, 28), "Simulasi host merespons"))
            {
                HandleMessage(new LobbyMessage { type = "HELLO", name = "HostPalsu", isHost = true, roomName = "Room Palsu" });
            }
        }
#endif
    }
}