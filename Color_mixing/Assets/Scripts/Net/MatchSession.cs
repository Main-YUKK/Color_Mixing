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
        if (DayCycleManager.Instance != null) DayCycleManager.Instance.Halt();
        if (ClientTimer.Current != null) ClientTimer.Current.Stop();
    }

    public void ShowAlert(LeaveAlert alert, int player, Action onClosed)
    {
        switch (alert)
        {
            case LeaveAlert.HostLeft:
                AlertService.Show("HostLeftRoom", 1, onClosed, fallbackText: "Host have left the Room");
                break;
            case LeaveAlert.PlayerDisconnectedHost:
                AlertService.Show("PlayerDisconnectedHost", player + 1, onClosed, fallbackText: $"[Player {player + 1}] is disconnected");
                break;
            default:
                AlertService.Show("PlayerDisconnectedClient", player + 1, onClosed, fallbackText: $"[Player {player + 1}] is disconnected");
                break;
        }
    }

    // Keluar dari game: sesi jaringan ditutup, room ditinggalkan, kembali ke menu utama.
    public void GoMainMenu()
    {
        GameNet.End();
        if (LobbyService.Instance != null) LobbyService.Instance.LeaveRoom();
        StartCoroutine(LoadWhenFree(MainMenuScene));
    }

    // Pemain yang tersisa kembali ke lobby; sesi jaringan dibiarkan (lobby / transport yang memutuskan langkah selanjutnya).
    public void GoLobby() => StartCoroutine(LoadWhenFree(LobbyScene));

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
