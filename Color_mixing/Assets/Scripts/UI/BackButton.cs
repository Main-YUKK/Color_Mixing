using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BackButton : MonoBehaviour
{
    // Isi di Inspector: nama scene tujuan saat tombol ini ditekan.
    // Nama harus sama persis dengan nama scene dan sudah ada di Build Profiles.
    [SerializeField] private string targetSceneName = "MainMenu";

    // Tempat menambah aksi sebelum pindah scene (opsional).
    // Contoh nanti: keluar dari room Game Center sebelum kembali ke menu.
    [SerializeField] private UnityEvent onBeforeBack;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(GoBack);
    }

    private void GoBack()
    {
        onBeforeBack?.Invoke();

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("BackButton: Target Scene Name masih kosong.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"BackButton: scene '{targetSceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}