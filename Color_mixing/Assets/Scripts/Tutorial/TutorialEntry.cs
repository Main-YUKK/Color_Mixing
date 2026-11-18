using UnityEngine;
using UnityEngine.SceneManagement;

// Pintu masuk ke scene tutorial: mengingat scene asal pemain supaya tutorial bisa mengembalikannya ke sana
// (MainMenu atau Lobby). Semua tombol yang membuka tutorial harus lewat Open().
public static class TutorialEntry
{
    public const string TutorialSceneName = "Tutorial";
    private const string DefaultReturnScene = "MainMenu";

    // Scene tujuan saat pemain keluar dari tutorial. Default MainMenu (mis. tutorial dibuka langsung dari Editor).
    public static string ReturnScene { get; private set; } = DefaultReturnScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => ReturnScene = DefaultReturnScene;

    // Buka tutorial dari scene yang sedang aktif. false = scene tutorial tidak bisa dimuat (scene asal tidak diubah).
    public static bool Open() => Open(SceneManager.GetActiveScene().name);

    public static bool Open(string fromScene)
    {
        if (!Application.CanStreamedLevelBeLoaded(TutorialSceneName))
        {
            Debug.LogError($"TutorialEntry: scene '{TutorialSceneName}' tidak ditemukan. Cek nama dan Build Profiles.");
            return false;
        }

        if (!SceneLoader.Load(TutorialSceneName))
            return false;

        ReturnScene = string.IsNullOrEmpty(fromScene) ? DefaultReturnScene : fromScene;
        return true;
    }

    // Keluar dari tutorial ke scene asal.
    public static void Exit()
    {
        SceneLoader.Load(ReturnScene);
    }
}
