using UnityEngine;

// State satu run gameplay. Static: tetap hidup saat pindah scene.
// Wajib panggil Reset() saat run baru dimulai (Reload Domain dimatikan, jadi nilai static tidak reset sendiri).
public static class GameSession
{
    public static ClientRequestBag Bag { get; private set; }
    public static int Day { get; set; }
    public static int ClientIndex { get; set; }
    public static ClientRequest CurrentRequest { get; set; }

    public static void Reset(ClientRequestDatabase database)
    {
        Bag = new ClientRequestBag(database);
        Day = 1;
        ClientIndex = 0;
        CurrentRequest = null;
    }

    // Mulai run dari hari tertentu (Continue memakai hari tersimpan). Bag dibuat ulang saat request pertama.
    public static void StartRun(int startDay)
    {
        Bag = null;
        Day = startDay;
        ClientIndex = 0;
        CurrentRequest = null;
    }

    public static void EnsureBag(ClientRequestDatabase database)
    {
        Bag ??= new ClientRequestBag(database);
    }

    // Clear session data saat Play dimulai.
    // Reload Domain mati -> static tidak reset sendiri. Dipanggil Unity setiap Play dimulai,
    // supaya Play berikutnya di Editor tidak melanjutkan run sebelumnya.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Bag = null;
}