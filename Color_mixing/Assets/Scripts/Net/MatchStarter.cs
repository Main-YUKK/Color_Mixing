using System;
using UnityEngine;

// Mulai game bersamaan di semua device:
//   host: START (scene + hari) -> semua memuat scene, layar loading TETAP tampil -> tiap device kirim SCENE_READY
//   host: semua siap -> GO -> layar loading dibuka di semua device, game jalan.
// Kalau ada device yang tidak melapor dalam ReadyTimeoutSeconds, host jalan terus (penanganan pemain putus ada di langkah leave/disconnect).
public class MatchStarter : MonoBehaviour
{
    public const float ReadyTimeoutSeconds = 30f;

    [Serializable]
    private class StartData
    {
        public string scene;
        public int day;
    }

    public static MatchStarter Instance { get; private set; }

    // Layar loading sudah memuat scene dan menunggu GO.
    public static bool WaitingForGo { get; private set; }

    // true dari GO sampai kembali ke lobby / menu. Dipakai pemantau koneksi (ConnectionWatch).
    public static bool InGame { get; private set; }

    // Hari awal yang dikirim host (client memakainya di langkah berikutnya).
    public static int StartDay { get; private set; } = 1;

    // Semua siap, game boleh jalan.
    public static event Action Go;

    private GameNet subscribed;
    private StartHandshake handshake;     // hanya host
    private bool awaitingLoad;            // menunggu SceneLoader.Loaded dari pemuatan yang kita mulai
    private float waitedForReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        Instance = null;
        WaitingForGo = false;
        InGame = false;
        StartDay = 1;
        Go = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("MatchStarter");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MatchStarter>();
    }

    // ---------- Host ----------

    // false = tidak bisa mulai (bukan host, belum ada sesi jaringan, atau scene tidak bisa dimuat).
    public static bool HostStart(string sceneName, int day)
    {
        var net = GameNet.Instance;
        if (Instance == null || net == null || !net.IsHost)
        {
            Debug.LogWarning("[START] HostStart ditolak: bukan host / belum ada sesi jaringan");
            return false;
        }
        return Instance.BeginAsHost(net, sceneName, day);
    }

    private bool BeginAsHost(GameNet net, string sceneName, int day)
    {
        if (WaitingForGo || SceneLoader.IsLoading) return false;

        StartDay = day;
        handshake = new StartHandshake(Mathf.Max(1, net.PlayerCount));
        waitedForReady = 0f;

        if (!BeginLoad(sceneName)) { handshake = null; return false; }

        net.SendToAll(NetMessage.Create(NetMessageType.Start, new StartData { scene = sceneName, day = day }));
        Debug.Log($"[START] Host mulai '{sceneName}' hari {day}, menunggu {handshake.PlayerCount} device");
        return true;
    }

    // ---------- Semua pemain ----------

    private bool BeginLoad(string sceneName)
    {
        awaitingLoad = true;
        if (!SceneLoader.Load(sceneName, hold: true))
        {
            awaitingLoad = false;
            return false;
        }
        WaitingForGo = true;
        LobbyService.Instance?.SetInGame(true);   // lobby tidak boleh membuka slot / menyusun ulang match selama game
        return true;
    }

    // Game selesai (game over, ada yang keluar, atau kembali ke menu): lobby boleh bekerja lagi dan ready dikosongkan.
    // Aman dipanggil berkali-kali, dari jalur mana pun yang membuka lobby / menu.
    public static void EndGame()
    {
        WaitingForGo = false;
        InGame = false;
        LobbyService.Instance?.SetInGame(false);
    }

    private void OnStart(NetMessage message)
    {
        if (GameNet.Instance != null && GameNet.Instance.IsHost) return;   // host tidak menerima START dari siapa pun
        if (message.from != 0) return;                                      // hanya host yang boleh menyuruh mulai

        var data = message.Read<StartData>();
        if (data == null || string.IsNullOrEmpty(data.scene)) return;
        if (WaitingForGo) return;                                           // START dobel

        StartDay = data.day;
        if (!BeginLoad(data.scene))
            Debug.LogError($"[START] Client gagal memuat '{data.scene}', host akan menunggu sampai batas waktu");
    }

    private void OnSceneLoaded()
    {
        if (!awaitingLoad) return;
        awaitingLoad = false;

        var net = GameNet.Instance;
        if (net == null) return;

        if (net.IsHost) MarkReady(net.LocalPlayerIndex);
        else net.SendToHost(NetMessage.Create(NetMessageType.SceneReady));
    }

    // ---------- Host: tunggu semua siap ----------

    private void OnSceneReady(NetMessage message)
    {
        if (handshake == null) return;
        MarkReady(message.from);
    }

    private void MarkReady(int index)
    {
        if (handshake == null || !handshake.MarkReady(index)) return;
        Debug.Log($"[START] Device {index} siap");
        if (handshake.AllReady) Finish();
    }

    private void Finish()
    {
        handshake = null;
        GameNet.Instance?.SendToAll(NetMessage.Create(NetMessageType.Go));
        Open();
    }

    private void OnGo(NetMessage message)
    {
        if (message.from != 0 || !WaitingForGo) return;
        Open();
    }

    private void Open()
    {
        WaitingForGo = false;
        InGame = true;
        SceneLoader.Release();
        var evt = Go;
        if (evt == null) return;
        foreach (Action handler in evt.GetInvocationList())
        {
            try { handler(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // ---------- Pemasangan ----------

    private void OnEnable() => SceneLoader.Loaded += OnSceneLoaded;
    private void OnDisable() => SceneLoader.Loaded -= OnSceneLoaded;

    private void Update()
    {
        // GameNet dibuat belakangan (dan bisa diganti): pasang ulang handler kalau instance-nya berubah.
        if (GameNet.Instance != subscribed)
        {
            Detach(subscribed);
            subscribed = GameNet.Instance;
            if (subscribed != null)
            {
                subscribed.Subscribe(NetMessageType.Start, OnStart);
                subscribed.Subscribe(NetMessageType.SceneReady, OnSceneReady);
                subscribed.Subscribe(NetMessageType.Go, OnGo);
            }
        }

        if (handshake != null)
        {
            waitedForReady += Time.unscaledDeltaTime;
            if (waitedForReady > ReadyTimeoutSeconds)
            {
                Debug.LogWarning($"[START] Device {string.Join(",", handshake.Missing())} tidak melapor, game jalan tanpa menunggu");
                Finish();
            }
        }
    }

    private static void Detach(GameNet net)
    {
        if (net == null || Instance == null) return;
        net.Unsubscribe(NetMessageType.Start, Instance.OnStart);
        net.Unsubscribe(NetMessageType.SceneReady, Instance.OnSceneReady);
        net.Unsubscribe(NetMessageType.Go, Instance.OnGo);
    }
}
