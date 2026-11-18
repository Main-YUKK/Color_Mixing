// Nomor pemain di device ini (0 = host, 1 dan 2 = client).
// Tanpa sesi jaringan dipakai nilai cadangan (field "player" di Inspector), jadi tes satu device di Editor tetap jalan.
public static class LocalPlayer
{
    public static int Index(int fallback) =>
        GameNet.Instance != null ? GameNet.Instance.LocalPlayerIndex : fallback;
}
