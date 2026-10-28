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
        spawner.OnClientArrived += OnClientArrived;
    }

    private void OnDisable()
    {
        cycle.OnClientRequested -= OnClientRequested;
        spawner.OnClientArrived -= OnClientArrived;
    }

    private void Start()
    {
        if (cardRoot != null) cardRoot.SetActive(false);
    }

    // Klien baru diminta (hari mulai, Yes, No, atau timeout): kartu ditutup, klien lama pergi, klien baru masuk.
    private void OnClientRequested()
    {
        if (routine != null) StopCoroutine(routine);
        if (cardRoot != null) cardRoot.SetActive(false);
        spawner.SpawnNext();
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

        SceneManager.LoadScene(nextSceneName);
    }
}
