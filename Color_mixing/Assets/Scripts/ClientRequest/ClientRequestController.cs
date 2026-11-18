using UnityEngine;


// Pilih request untuk klien aktif (jadwal hari -> bag), simpan ke GameSession, tampilkan di card.
public class ClientRequestController : MonoBehaviour
{
    [SerializeField] private ClientRequestDatabase database;
    [SerializeField] private DaySchedule schedule;
    [SerializeField] private ClientRequestCardView card;

    [SerializeField] private DayCycleManager cycle;

    private void OnEnable() => cycle.OnClientRequested += ShowCurrentClient;
    private void OnDisable() => cycle.OnClientRequested -= ShowCurrentClient;

    private void ShowCurrentClient()
    {
        // Client tidak memilih sendiri: request sudah dipasang ke GameSession dari data host (DayCycleManager.ApplyRemote).
        if (cycle == null || cycle.IsAuthority)
        {
            GameSession.EnsureBag(database);
            var level = PlaytestSettings.Level(schedule.GetLevel(GameSession.Day, GameSession.ClientIndex));
            GameSession.CurrentRequest = GameSession.Bag.Next(level);
            Debug.Log($"[REQ] Hari {GameSession.Day} klien {GameSession.ClientIndex + 1}: {level} -> {GameSession.CurrentRequest.requestId}");
        }

        if (GameSession.CurrentRequest == null)
        {
            Debug.LogError("[REQ] Request tidak ada (id dari host tidak ditemukan di database?)");
            return;
        }
        card.Show(GameSession.CurrentRequest);
    }

    // SEMENTARA: tombol test pengganti feedback asli (Klik kanan komponen di Inspector saat Play).
    [ContextMenu("Klien Berhasil (Yes)")]
    private void ClientSucceeded() => cycle.OnFeedbackRevealed(true);

    [ContextMenu("Klien Gagal (No)")]
    private void ClientFailed() => cycle.OnFeedbackRevealed(false);

    // DEBUG: cek jadwal + bag untuk hari 1-10 tanpa mengganggu GameSession (pakai bag sendiri).    
    [ContextMenu("Simulasi Hari 1-10")]
    private void SimulateDays()
    {
        var bag = new ClientRequestBag(database);
        for (int day = 1; day <= 10; day++)
            for (int client = 0; client < 3; client++)
            {
                var level = schedule.GetLevel(day, client);
                Debug.Log($"[SIM] Hari {day} klien {client + 1}: {level} -> {bag.Next(level).requestId}");
            }
    }
}
