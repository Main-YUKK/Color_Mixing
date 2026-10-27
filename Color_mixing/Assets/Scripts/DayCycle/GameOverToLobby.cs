using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Setelah game over, tunggu sebentar lalu kembali ke lobby. Delay 0 = tidak otomatis (pemain pakai tombol di panel).
public class GameOverToLobby : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private string lobbySceneName = "HostLobby";
    [SerializeField] private float delay = 3f;

    private void OnEnable() => cycle.OnGameOver += Go;
    private void OnDisable() => cycle.OnGameOver -= Go;

    private void Go()
    {
        if (delay > 0f) StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSeconds(delay);
        // NET: suruh semua device memuat lobby. Sementara hanya device ini.
        SceneManager.LoadScene(lobbySceneName);
    }
}
