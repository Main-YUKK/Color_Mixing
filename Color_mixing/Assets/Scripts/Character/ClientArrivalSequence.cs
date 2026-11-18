using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Alur klien: klien baru diminta -> klien lama pergi, klien baru jalan masuk -> menghadap (flip)
// -> kartu request muncul -> (opsional) pindah scene. Taruh di scene ClientArrival.
// CharacterSpawner yang menggerakkan karakter, DayCycleManager yang meminta klien baru.
public class ClientArrivalSequence : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private CharacterSpawner spawner;
    [SerializeField] private GameObject cardRoot;          // objek kartu request, disembunyikan sampai klien tiba
    [SerializeField] private bool flipOnArrival = true;
    [Tooltip("Opsional. Kartu awal hari: klien baru menunggu kartunya selesai menghilang dulu. Kosong = dicari otomatis di scene.")]
    [SerializeField] private StartOfDayCard dayCard;

    [Header("Next scene (opsional)")]
    [Tooltip("Kosong = tetap di scene ini, klien berikutnya muncul lewat Yes / No.")]
    [SerializeField] private string nextSceneName;
    [Tooltip("Lama kartu dibaca sebelum pindah scene. Tidak dipakai kalau kartu memakai typewriter (pemain tap untuk lanjut).")]
    [SerializeField] private float cardSeconds = 3f;

    private Coroutine routine;
    private bool flowStopped;   // hari selesai / game over: klien yang masih berjalan masuk tidak boleh memunculkan kartu lagi

    public CharacterSpawner Spawner => spawner;   // dipakai layar hasil untuk bereaksi pada klien yang sedang berdiri

    private void OnEnable()
    {
        cycle.OnClientRequested += OnClientRequested;
        cycle.OnGameOver += StopClientFlow;
        cycle.OnDayEnded += OnDayEnded;
        spawner.OnClientArrived += OnClientArrived;
    }

    private void OnDisable()
    {
        cycle.OnClientRequested -= OnClientRequested;
        cycle.OnGameOver -= StopClientFlow;
        cycle.OnDayEnded -= OnDayEnded;
        spawner.OnClientArrived -= OnClientArrived;
    }
    // Hari selesai / game over: hentikan alur klien dan tutup layar mixing supaya laporan / panel game over terlihat.
    private void StopClientFlow()
    {
        flowStopped = true;
        if (routine != null) StopCoroutine(routine);
        if (cardRoot != null) cardRoot.SetActive(false);   // klien terakhir pergi tanpa kartu request di layar
        UnloadNextScene();
    }

    private void OnDayEnded(DayResult _) => StopClientFlow();
    private void Start()
    {
        if (cardRoot != null) cardRoot.SetActive(false);
    }

    // Klien baru diminta (hari mulai, Yes, No, atau timeout): kartu ditutup, scene mixing ditutup, klien lama pergi, klien baru masuk.
    private void OnClientRequested()
    {
        flowStopped = false;
        if (routine != null) StopCoroutine(routine);
        if (cardRoot != null) cardRoot.SetActive(false);
        UnloadNextScene();

        // Rupa klien dipilih sekarang (host mengirimnya ke client tepat setelah event ini); kalau kartu awal hari
        // masih tampil, hanya gerakan klien yang menunggu kartunya hilang.
        if (dayCard == null) dayCard = FindFirstObjectByType<StartOfDayCard>();
        spawner.SpawnNext(() => dayCard != null && dayCard.IsShowing);
    }

    // Tutup scene additive klien sebelumnya supaya klien baru mulai bersih (aman kalau belum dimuat).
    public void UnloadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;
        if (SceneManager.GetSceneByName(nextSceneName).isLoaded)
            SceneManager.UnloadSceneAsync(nextSceneName);
    }


    private void OnClientArrived()
    {
        if (flowStopped) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        if (flipOnArrival && spawner.Current != null)
        {
            Vector3 s = spawner.Current.transform.localScale;
            s.x = -s.x;
            spawner.Current.transform.localScale = s;
        }

        if (cardRoot != null) cardRoot.SetActive(true);

        if (string.IsNullOrEmpty(nextSceneName)) yield break;

        // Kartu typewriter: tunggu pemain tap (tampilkan semua, lalu tap lagi untuk lanjut). Kalau tidak, tunggu beberapa detik.
        var card = cardRoot != null ? cardRoot.GetComponent<ClientRequestCardView>() : null;
        if (card != null && card.UsesTapToContinue)
        {
            bool go = false;
            void OnContinue() => go = true;
            card.ContinueRequested += OnContinue;
            try { yield return new WaitUntil(() => go); }
            finally { card.ContinueRequested -= OnContinue; }
        }
        else yield return new WaitForSeconds(cardSeconds);

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"ClientArrivalSequence: scene '{nextSceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Additive);
    }
}
