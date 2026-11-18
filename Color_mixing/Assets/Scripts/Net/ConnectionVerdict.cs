public enum Verdict
{
    OwnConnection,   // masalahnya di koneksi device ini sendiri
    PeersDropped,    // pemain yang diam memang putus
}

// Kalau ada pemain yang diam, siapa yang bermasalah: mereka atau kita?
public static class ConnectionVerdict
{
    // lostCount: berapa pemain yang baru dianggap putus pada pemeriksaan yang sama.
    // trackedCount: berapa pemain yang dipantau device ini (host 2, client 1).
    // internetReachable: false = device ini sendiri tidak punya internet.
    public static Verdict Decide(bool isHost, int lostCount, int trackedCount, bool internetReachable)
    {
        if (!internetReachable) return Verdict.OwnConnection;

        // Host mendengar semua client diam bersamaan: lebih mungkin host sendiri yang terputus.
        if (isHost && trackedCount >= 2 && lostCount >= trackedCount) return Verdict.OwnConnection;

        return Verdict.PeersDropped;
    }
}
