using UnityEngine;


// Pilih request untuk klien aktif (jadwal hari -> bag), simpan ke GameSession, tampilkan di card.
public class ClientRequestController : MonoBehaviour
{
    [SerializeField] private ClientRequestDatabase database;
    [SerializeField] private DaySchedule schedule;
    [SerializeField] private ClientRequestCardView card;

    private void Start()
    {

        // SEMENTARA: run dimulai di sini supaya scene bisa di-Play langsung dari Editor.
        // Nanti GameSession.Reset() dipanggil saat host menekan Play di lobby.
        if (GameSession.Bag == null)
            GameSession.Reset(database);

        ShowCurrentClient();
    }

    private void ShowCurrentClient()
    {
        var level = schedule.GetLevel(GameSession.Day, GameSession.ClientIndex);
        GameSession.CurrentRequest = GameSession.Bag.Next(level);
        card.Show(GameSession.CurrentRequest);
        Debug.Log($"[REQ] Hari {GameSession.Day} klien {GameSession.ClientIndex + 1}: {level} -> {GameSession.CurrentRequest.requestId}");
    }

    // SEMENTARA: tombol test pengganti flow asli (hasil feedback & akhir hari dari).
    // Klien berhasil: slot naik. 3 Yes per hari -> hari selesai, mulai hari berikutnya dari slot 0.
    // Nanti: Yes -> ClientIndex++ setelah feedback, Day++ di akhir hari, lalu scene ClientArrival dimuat ulang.
    [ContextMenu("Klien Berhasil (Yes)")]
    private void ClientSucceeded()
    {
        GameSession.ClientIndex++;
        if (GameSession.ClientIndex >= 3)
        {
            GameSession.Day++;
            GameSession.ClientIndex = 0;
        }
        ShowCurrentClient();
    }

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

    //SEMENTARA: Client Fail 
    [ContextMenu("Klien Gagal (No)")]
    private void ClientFailed() => ShowCurrentClient();
}
