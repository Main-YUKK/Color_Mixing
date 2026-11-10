using System;
using System.Collections;
using UnityEngine;

// Menghubungkan aturan keluar / putus (LeaveCoordinator) dengan game sungguhan: peringatan, pindah scene, menutup sesi.
// Dibuat otomatis. Tombol Exit di menu pause cukup memanggil MatchSession.Leave().
public class MatchSession : MonoBehaviour, ILeaveActions
{
    private const string MainMenuScene = "MainMenu";
    private const string LobbyScene = "Lobby";

    public static MatchSession Instance { get; private set; }

    private GameNet watching;
    private LeaveCoordinator coordinator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("MatchSession");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MatchSession>();
    }

    // Pemain di device ini keluar dari game (Exit di menu pause).
    public static void Leave()
    {
        if (Instance == null) return;

        if (Instance.coordinator != null) Instance.coordinator.LeaveLocally();
        else Instance.GoMainMenu();   // tidak ada sesi jaringan
    }

    // Game over: semua device kembali ke lobby (dipanggil di tiap device, karena game over sampai ke semuanya).
    public static void ReturnToLobby()
    {
        if (Instance == null) return;
        Instance.coordinator?.BeginReturn();
        Instance.GoLobby();
    }

    // Koneksi device ini sendiri bermasalah (dinilai ConnectionWatch).
    public static void OwnConnectionLost() => Instance?.coordinator?.OwnConnectionLost();

    private void OnEnable() => SceneLoader.Loaded += OnSceneLoaded;
    private void OnDisable() => SceneLoader.Loaded -= OnSceneLoaded;

    private void Update()
    {
        // GameNet dibuat dan diganti belakangan: pasang koordinator baru kalau instance-nya berubah.
        if (GameNet.Instance == watching) return;

        coordinator?.Dispose();
        coordinator = null;
        watching = GameNet.Instance;
        if (watching != null) coordinator = new LeaveCoordinator(watching, this);
    }

    private void OnSceneLoaded() => coordinator?.ResetEnding();

    // ---------- ILeaveActions ----------

    public void Halt()
    {
        PauseMenu.Close();   // menu pause tidak boleh menutupi peringatan
        if (ConnectionWatch.Instance != null) ConnectionWatch.Instance.Stop();
        if (DayCycleManager.Instance != null) DayCycleManager.Instance.Halt();
        if (ClientTimer.Current != null) ClientTimer.Current.Stop();
    }

    public void ShowAlert(LeaveAlert alert, int player, Action onClosed)
    {
        switch (alert)
        {
            case LeaveAlert.HostLeft:
                AlertService.Show("HostLeftRoom", 1, onClosed, fallbackText: "Host have left the Room", playerName: NameOf(0));
                break;
            case LeaveAlert.PlayerDisconnectedHost:
                AlertService.Show("PlayerDisconnectedHost", player + 1, onClosed, fallbackText: $"{NameOf(player) ?? $"Player {player + 1}"} is disconnected", playerName: NameOf(player));
                break;
            case LeaveAlert.OwnConnectionHost:
                AlertService.Show("UnstableConnectionHost", 1, onClosed, fallbackText: "Your internet connection is unstable", playerName: NameOf(0));
                break;
            case LeaveAlert.OwnConnectionClient:
                AlertService.Show("UnstableConnectionClient", player + 1, onClosed, fallbackText: "Your internet connection is unstable", hostName: HostName(), playerName: NameOf(player));
                break;
            default:
                AlertService.Show("PlayerDisconnectedClient", player + 1, onClosed, fallbackText: $"{NameOf(player) ?? $"Player {player + 1}"} is disconnected", playerName: NameOf(player));
                break;
        }
    }

    // Keluar dari game: sesi jaringan ditutup, room ditinggalkan, kembali ke menu utama.
    public void GoMainMenu()
    {
        MatchStarter.EndGame();
        GameNet.End();
        if (LobbyService.Instance != null) LobbyService.Instance.LeaveRoom();
        StartCoroutine(LoadWhenFree(MainMenuScene));
    }

    // Pemain yang tersisa kembali ke lobby; sesi jaringan dibiarkan (lobby / transport yang memutuskan langkah selanjutnya).
    public void GoLobby()
    {
        MatchStarter.EndGame();
        StartCoroutine(LoadWhenFree(LobbyScene));
    }

    // Nama Game Center pemain dengan index game ini (0 = host). Daftar pemain dikunci selama game, jadi nama pemain
    // yang baru keluar masih ada. Null kalau tidak ketahuan.
    private static string NameOf(int index)
    {
        var roster = (GameNet.Instance != null ? GameNet.Instance.Transport as IRosterTransport : null)?.Roster;
        if (roster != null && index >= 0 && index < roster.Count && !string.IsNullOrEmpty(roster[index])) return roster[index];
        return null;
    }

    // Nama host untuk teks peringatan ("Please wait for [Host] to start the room again").
    private static string HostName()
    {
        var lobby = LobbyService.Instance;
        var host = lobby != null ? lobby.Players.Find(p => p.isHost) : null;
        return host != null && !string.IsNullOrEmpty(host.name) ? host.name : "Host";
    }

    // Layar loading mungkin sedang dipakai (mis. keluar saat game baru dimulai): tunggu sebentar lalu coba lagi.
    private IEnumerator LoadWhenFree(string scene)
    {
        for (float waited = 0f; waited < 10f; waited += Time.unscaledDeltaTime)
        {
            if (!SceneLoader.IsLoading && SceneLoader.Load(scene)) yield break;
            yield return null;
        }
        Debug.LogError($"[LEAVE] Gagal pindah ke '{scene}'");
        coordinator?.ResetEnding();
    }
}
