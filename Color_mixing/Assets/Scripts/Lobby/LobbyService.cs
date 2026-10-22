using System;
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
}

[Serializable]
public class LobbyMessage
{
    public string type;   // "HELLO"
    public string name;
    public bool isHost;
}

public class LobbyService : MonoBehaviour
{
    public static LobbyService Instance { get; private set; }

    public LobbyMode Mode { get; private set; } = LobbyMode.None;
    public string RoomCode { get; private set; }
    public string LastMessage { get; private set; }
    public readonly List<LobbyPlayer> Players = new List<LobbyPlayer>();

    public event Action OnPlayersChanged;
    public event Action<string> OnRoomClosed;

    private const int MaxPlayers = 3;
    private const float JoinTimeoutSeconds = 10f;
    private const float HostCheckSeconds = 3f;

    private GKMatch match;
    private string localName;
    private int session;   // dinaikkan tiap Create/Join/Leave supaya proses lama diabaikan

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("LobbyService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<LobbyService>();
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

    // ---------- Matchmaking ----------

#if UNITY_EDITOR
    private Task RunMatchmaking(bool isHost, int mySession)
    {
        Debug.Log("[Lobby] Editor: Game Center dilewati (hanya tampilan).");
        return Task.CompletedTask;
    }
#else
    private async Task RunMatchmaking(bool isHost, int mySession)
    {
        try
        {
            var request = GKMatchRequest.Init();
            request.MinPlayers = 2;
            request.MaxPlayers = MaxPlayers;
            // Jika compile error di baris ini, tipe PlayerGroup bukan uint: ganti cast (uint) sesuai pesan error.
            request.PlayerGroup = (uint)int.Parse(RoomCode);

            var findTask = GKMatchmaker.Shared.FindMatch(request);
            findTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            if (!isHost)
            {
                var finished = await Task.WhenAny(findTask, Task.Delay(TimeSpan.FromSeconds(JoinTimeoutSeconds)));
                if (mySession != session) return;
                if (finished != findTask)
                {
                    GKMatchmaker.Shared.Cancel();
                    Fail("Room tidak ditemukan atau sudah penuh.");
                    return;
                }
            }

            var found = await findTask;
            if (mySession != session)
            {
                found.Disconnect();
                return;
            }

            match = found;
            match.Delegate.DataReceived += OnDataReceived;
            match.Delegate.PlayerConnectionChanged += (player, state) => OnPlayerConnectionChanged(player, state.ToString());
            match.Delegate.DidFailWithError += e => Debug.LogError("[Lobby] Match error: " + e.Message);

            Debug.Log("[Lobby] Match terbentuk.");
            SendHello();
            if (!isHost) CheckHostPresent(mySession);
        }
        catch (Exception e)
        {
            if (mySession != session) return;
            Debug.LogError("[Lobby] Matchmaking gagal: " + e.Message);
            Fail("Gagal terhubung ke room.");
        }
    }
#endif

    private async void CheckHostPresent(int mySession)
    {
        await Task.Delay(TimeSpan.FromSeconds(HostCheckSeconds));
        if (mySession != session || Mode != LobbyMode.Client) return;
        if (!Players.Exists(p => p.isHost)) Fail("Room tidak ditemukan.");
    }

    // ---------- Pesan antar-device ----------

    private void SendHello()
    {
#if !UNITY_EDITOR
        if (match == null) return;
        var msg = new LobbyMessage { type = "HELLO", name = localName, isHost = Mode == LobbyMode.Host };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        try { match.Send(data, GKMatch.GKSendDataMode.Reliable); }
        catch (Exception e) { Debug.LogWarning("[Lobby] Kirim HELLO gagal: " + e.Message); }
#endif
    }

    private void OnDataReceived(byte[] data, GKPlayer from)
    {
        var msg = JsonUtility.FromJson<LobbyMessage>(Encoding.UTF8.GetString(data));
        if (msg == null || msg.type != "HELLO") return;

        bool isNew = AddOrUpdatePlayer(msg.name, msg.isHost, false);
        if (isNew) SendHello();   // balas supaya pendatang baru tahu kita ada
        OnPlayersChanged?.Invoke();
    }

    private void OnPlayerConnectionChanged(GKPlayer player, string state)
    {
    Debug.Log($"[Lobby] {player.DisplayName} -> {state}");

    if (string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase))
    {
        SendHello();
        return;
    }

    if (!string.Equals(state, "Disconnected", StringComparison.OrdinalIgnoreCase)) return;

    var p = Players.Find(x => x.name == player.DisplayName);
    if (p == null) return;

    Players.Remove(p);
    if (p.isHost && Mode == LobbyMode.Client)
    {
        Fail("Host keluar dari room.");
        return;
    }
    OnPlayersChanged?.Invoke();
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
        return "Editor Player";
#else
        return GKLocalPlayer.Local.DisplayName;
#endif
    }

    private void ResetSession()
    {
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
        ResetSession();
        Mode = LobbyMode.None;
        RoomCode = null;
        LastMessage = reason;
        Debug.LogWarning("[Lobby] " + reason);
        OnRoomClosed?.Invoke(reason);
    }
}