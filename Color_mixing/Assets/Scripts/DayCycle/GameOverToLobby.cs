using System.Collections;
using UnityEngine;

// Setelah game over, tunggu sebentar lalu semua pemain kembali ke lobby. Delay 0 = tidak otomatis (pemain pakai tombol di panel).
// Game over sampai ke semua device lewat sinkronisasi hari, jadi tiap device menjalankan ini sendiri.
public class GameOverToLobby : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
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
        MatchSession.ReturnToLobby();
    }
}
