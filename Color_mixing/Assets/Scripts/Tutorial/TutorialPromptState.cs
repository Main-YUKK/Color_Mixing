using UnityEngine;

// Mencatat apakah alert "mau lihat tutorial?" sudah pernah tampil, supaya hanya muncul saat game pertama kali dibuka.
// Data tersimpan di PlayerPrefs, jadi hilang kalau game dihapus lalu di-download ulang (dianggap pemain baru).
public static class TutorialPromptState
{
    private const string SeenKey = "tutorial_prompt_seen";

    // Tempat penyimpanan. Default PlayerPrefs; test memakai penyimpanan di memori supaya data asli tidak tersentuh.
    public interface IStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
    }

    private class PrefsStore : IStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
    }

    private static IStore store = new PrefsStore();

    // Reload Domain dimatikan: kembalikan ke keadaan awal tiap Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => store = new PrefsStore();

    public static void UseStore(IStore newStore) => store = newStore ?? new PrefsStore();

    // True kalau alert belum pernah tampil.
    public static bool ShouldShow => store.GetInt(SeenKey, 0) == 0;

    public static void MarkShown() => store.SetInt(SeenKey, 1);
}
