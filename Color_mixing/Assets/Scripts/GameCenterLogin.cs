using System;
using System.Threading.Tasks;
using Apple.GameKit;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameCenterLogin : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelaySeconds = 3f;

    private async void Start()
    {
        Debug.Log("[GC] Start dipanggil");

#if UNITY_EDITOR || !UNITY_IOS
        // Game Center hanya jalan di device iOS, jadi di Editor langsung lanjut
        Debug.Log("[GC] Bukan device iOS, skip login");
        SceneManager.LoadScene(mainMenuScene);
        return;
#else
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            Debug.Log($"[GC] Mencoba login (percobaan {attempt}/{maxRetries})");

            try
            {
                var player = await GKLocalPlayer.Authenticate();
                Debug.Log($"[GC] Login berhasil: {player.DisplayName}");

                SceneManager.LoadScene(mainMenuScene);
                return;
            }
            catch (GameKitException e)
            {
                Debug.LogError($"[GC] GameKitException: {e.Message}");
            }
            catch (Exception e)
            {
                // Termasuk DllNotFoundException kalau plugin native GameKit tidak ter-build
                Debug.LogError($"[GC] Error tak terduga: {e.GetType().Name} - {e.Message}");
            }

            if (attempt < maxRetries)
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        }

        Debug.LogError("[GC] Login gagal setelah semua percobaan. Pemain tetap di scene login.");
#endif
    }
}