using UnityEngine;

// Taruh di scene gameplay. Save aktif (dipilih dari Saved Files) = lanjut dari hari tersimpan, kalau tidak mulai dari Day 1.
// Hanya host yang menjalankan hari; client menunggu data dari host (langkah berikutnya).
// Kalau scene dimuat lewat MatchStarter, hari baru mulai setelah GO (semua device siap).
public class DayCycleStarter : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;

    private void Start()
    {
        var net = GameNet.Instance;
        if (net != null && !net.IsHost) return;

        if (MatchStarter.WaitingForGo) MatchStarter.Go += BeginSession;
        else BeginSession();
    }

    private void OnDestroy() => MatchStarter.Go -= BeginSession;

    private void BeginSession()
    {
        MatchStarter.Go -= BeginSession;
        if (cycle != null) cycle.StartSession(SaveService.Active != null);
    }
}
