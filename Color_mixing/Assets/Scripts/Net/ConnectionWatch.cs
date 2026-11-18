using UnityEngine;

// Detak jantung selama game berjalan: tiap device mengirim HEARTBEAT tiap detik dan mencatat siapa yang masih terdengar.
//   client memantau host; host memantau kedua client.
// Diam terlalu lama = putus. Lalu dinilai siapa yang bermasalah (ConnectionVerdict):
//   - pemain lain putus   -> sama dengan pemain itu keluar (GameNet.PeerLost -> LeaveCoordinator)
//   - koneksi kita sendiri -> peringatan "Your internet connection is unstable", lalu ke lobby
// Dibuat otomatis. Aktif hanya antara GO dan kembali ke lobby / menu (MatchStarter.InGame).
public class ConnectionWatch : MonoBehaviour
{
    private const float SendInterval = 1f;
    private const float StallThreshold = 2f;   // satu frame selama ini = device ini yang macet

    public static ConnectionWatch Instance { get; private set; }

    // Hanya untuk tes manual (panel NET): pura-pura sinyal device ini mati.
    public static bool SimulateNoSignal;

    private HeartbeatMonitor monitor;
    private GameNet net;
    private float nextSend;
    private float lastUpdate;
    private bool armed = true;   // false setelah dihentikan (Stop) sampai game berikutnya

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        Instance = null;
        SimulateNoSignal = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("ConnectionWatch");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ConnectionWatch>();
    }

    // Ada pemain keluar / device ini bermasalah: berhenti memantau sampai game berikutnya.
    public void Stop()
    {
        armed = false;
        Untrack();
    }

    private void Update()
    {
        var current = GameNet.Instance;
        bool shouldRun = MatchStarter.InGame && current != null && !current.IsOffline;

        if (!shouldRun)
        {
            Untrack();
            if (!MatchStarter.InGame) armed = true;   // game sudah selesai: siap untuk game berikutnya
            return;
        }
        if (!armed) return;

        float now = Time.unscaledTime;
        if (net != current) Begin(current, now);

        if (now - lastUpdate > StallThreshold) monitor.Resync(now);
        lastUpdate = now;

        if (now >= nextSend)
        {
            nextSend = now + SendInterval;
            SendHeartbeat();
        }

        var lost = monitor.Check(now);
        if (lost.Count > 0) HandleLost(lost);
    }

    private void Begin(GameNet current, float now)
    {
        Untrack();
        net = current;
        monitor = new HeartbeatMonitor();
        monitor.Unstable += i => Debug.LogWarning($"[HB] Pemain {i} tidak terdengar > {HeartbeatMonitor.DefaultUnstableAfter}s");
        monitor.Recovered += i => Debug.Log($"[HB] Pemain {i} terdengar lagi");

        if (net.IsHost)
            for (int i = 1; i < Mathf.Max(1, net.PlayerCount); i++) monitor.Track(i, now);
        else
            monitor.Track(0, now);

        net.Subscribe(NetMessageType.Heartbeat, OnHeartbeat);
        nextSend = now;
        lastUpdate = now;
    }

    private void Untrack()
    {
        if (net != null) net.Unsubscribe(NetMessageType.Heartbeat, OnHeartbeat);
        net = null;
        monitor = null;
    }

    private void SendHeartbeat()
    {
        if (SimulateNoSignal) return;

        var beat = NetMessage.Create(NetMessageType.Heartbeat);
        if (net.IsHost) net.SendToAll(beat);
        else net.SendToHost(beat);
    }

    private void OnHeartbeat(NetMessage message)
    {
        if (SimulateNoSignal || monitor == null) return;
        monitor.Heard(message.from, Time.unscaledTime);
    }

    private void HandleLost(System.Collections.Generic.List<int> lost)
    {
        bool internet = Application.internetReachability != NetworkReachability.NotReachable && !SimulateNoSignal;
        var verdict = ConnectionVerdict.Decide(net.IsHost, lost.Count, monitor.TrackedCount, internet);
        Debug.LogWarning($"[HB] Putus: [{string.Join(",", lost)}] -> {verdict}");

        if (verdict == Verdict.OwnConnection)
        {
            MatchSession.OwnConnectionLost();
            return;
        }

        var gameNet = net;
        foreach (int index in lost) gameNet.ReportPeerLost(index);
    }
}
