#if UNITY_EDITOR || (UNITY_STANDALONE && DEVELOPMENT_BUILD)
using System.Collections.Generic;
using UnityEngine;

// Panel tes (hanya Editor / build Mac development): tombol kecil "NET" di kiri bawah.
// Satu salinan klik Host, dua salinan lain klik Join. Dipakai sampai alur Play di lobby memakai jalan lokal.
// Tidak ikut build iOS / build rilis.
public class LocalNetPanel : MonoBehaviour
{
    private const int MaxLog = 8;

    private LocalTransport transport;
    private bool open;
    private string status = "Offline (tanpa jaringan)";
    private readonly List<string> log = new List<string>();
    private GameNet subscribedTo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("LocalNetPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<LocalNetPanel>();
    }

    private void Awake()
    {
        // Salinan yang tidak sedang difokus tetap harus menerima pesan.
        Application.runInBackground = true;
    }

    private void Update()
    {
        transport?.Pump();
    }

    private void OnDestroy()
    {
        StopSession();
    }

    // ---------- Aksi ----------

    private void HostSession()
    {
        StopSession();
        try
        {
            transport = LocalTransport.StartHost();
            transport.PlayerJoined += i => AddLog($"Pemain {i} masuk");
            transport.PlayerLeft += i => AddLog($"Pemain {i} keluar");
            BeginNet();
            status = $"Host, port {LocalTransport.DefaultPort}";
        }
        catch (System.Exception e)
        {
            status = "Gagal jadi host: " + e.Message;
            StopSession();
        }
    }

    private void JoinSession()
    {
        StopSession();
        try
        {
            transport = LocalTransport.StartClient();
            transport.Disconnected += () =>
            {
                status = "Terputus dari host";
                AddLog("Terputus dari host");
            };
            BeginNet();
            status = "Join, menunggu host...";
        }
        catch (System.Exception e)
        {
            status = "Gagal join (host sudah jalan?): " + e.Message;
            StopSession();
        }
    }

    private void BeginNet()
    {
        GameNet.Begin(transport);
        subscribedTo = GameNet.Instance;
        subscribedTo.Subscribe(NetMessageType.Ping, OnPing);
    }

    private void StopSession()
    {
        subscribedTo?.Unsubscribe(NetMessageType.Ping, OnPing);
        subscribedTo = null;

        transport?.Dispose();
        transport = null;
    }

    // Kembali ke keadaan Editor biasa: host sendirian tanpa jaringan.
    private void GoOffline()
    {
        StopSession();
        GameNet.BeginOffline();
        status = "Offline (tanpa jaringan)";
    }

    private void SendPing()
    {
        var net = GameNet.Instance;
        if (net == null) return;

        var ping = NetMessage.Create(NetMessageType.Ping);
        if (net.IsHost) net.SendToAll(ping);
        else net.SendToHost(ping);
        AddLog("Kirim PING");
    }

    private void OnPing(NetMessage message) => AddLog($"Terima PING dari pemain {message.from}");

    private void AddLog(string line)
    {
        log.Add(line);
        if (log.Count > MaxLog) log.RemoveAt(0);
    }

    // ---------- Tampilan ----------

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float viewHeight = Screen.height / scale;

        if (!open)
        {
            if (GUI.Button(new Rect(10, viewHeight - 34, 50, 24), "NET")) open = true;
            return;
        }

        var net = GameNet.Instance;
        string role = net == null ? "-" : net.IsOffline ? "offline" : net.IsHost ? "host (0)" : $"client ({net.LocalPlayerIndex})";

        float height = 150 + MaxLog * 16;
        GUILayout.BeginArea(new Rect(10, viewHeight - height - 10, 250, height), GUI.skin.box);
        GUILayout.Label($"Peran: {role}");
        GUILayout.Label(status);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Host")) HostSession();
        if (GUILayout.Button("Join")) JoinSession();
        if (GUILayout.Button("Offline")) GoOffline();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Kirim PING")) SendPing();

        foreach (string line in log) GUILayout.Label(line);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Tutup")) open = false;
        GUILayout.EndArea();
    }
}
#endif
