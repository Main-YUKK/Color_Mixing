using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class NavigateButton : MonoBehaviour
{
    // Isi di Inspector: nama scene tujuan saat tombol ini ditekan.
    // Nama harus sama persis dengan nama scene dan sudah ada di Build Profiles.
    [SerializeField] private string targetSceneName = "HostLobby";

    // Tempat menambah aksi sebelum pindah scene (opsional).
    // Contoh nanti: membuat room Game Center sebelum masuk lobby.
    [SerializeField] private UnityEvent onBeforeNavigate;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Navigate);
    }

    private void Navigate()
    {
        onBeforeNavigate?.Invoke();

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("NavigateButton: Target Scene Name masih kosong.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"NavigateButton: scene '{targetSceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}