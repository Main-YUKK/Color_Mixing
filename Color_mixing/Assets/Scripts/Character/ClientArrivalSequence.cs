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

    [Header("Next scene (opsional)")]
    [Tooltip("Kosong = tetap di scene ini, klien berikutnya muncul lewat Yes / No.")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private float cardSeconds = 3f;       // lama kartu dibaca sebelum pindah scene

    private Coroutine routine;

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
        if (routine != null) StopCoroutine(routine);
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
        if (routine != null) StopCoroutine(routine);
        if (cardRoot != null) cardRoot.SetActive(false);
        UnloadNextScene();
        spawner.SpawnNext();
    }

    // Tutup scene additive klien sebelumnya supaya klien baru mulai bersih (aman kalau belum dimuat).
    private void UnloadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;
        if (SceneManager.GetSceneByName(nextSceneName).isLoaded)
            SceneManager.UnloadSceneAsync(nextSceneName);
    }


    private void OnClientArrived()
    {
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
        yield return new WaitForSeconds(cardSeconds);

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"ClientArrivalSequence: scene '{nextSceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Additive);
    }
}
