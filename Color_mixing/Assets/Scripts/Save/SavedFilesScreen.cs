using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Layar Saved Files: satu kartu per file save. Open Room membuka lagi room itu di lobby dan lanjut dari hari tersimpan.
public class SavedFilesScreen : MonoBehaviour
{
    [SerializeField] private SaveCardView cardPrefab;
    [SerializeField] private Transform container;            // Content dari ScrollView (pakai Grid Layout Group)
    [SerializeField] private GameObject emptyText;           // opsional: "Belum ada file tersimpan"

    [Header("Konfirmasi hapus (opsional, kosong = langsung hapus)")]
    [SerializeField] private GameObject deletePanel;
    [SerializeField] private TMP_Text deleteLabel;

    [SerializeField] private string lobbySceneName = "Lobby";

    private SaveFile pendingDelete;

    private void Start()
    {
        if (deletePanel != null) deletePanel.SetActive(false);
        Rebuild();
    }

    private void Rebuild()
    {
        if (cardPrefab == null || container == null)
        {
            Debug.LogError("[SavedFiles] Card Prefab atau Container belum diisi di Inspector.", this);
            return;
        }

        foreach (Transform child in container) Destroy(child.gameObject);

        var saves = SaveService.LoadAll();
        Debug.Log($"[SavedFiles] {saves.Count} save(s) di {SaveService.Dir}");
        if (emptyText != null) emptyText.SetActive(saves.Count == 0);

        foreach (var save in saves)
        {
            var card = Instantiate(cardPrefab, container, false);
            // Scale di asset prefab bisa tersimpan 0 (root UI yang diatur layout group), jadi kartu tidak kelihatan.
            card.transform.localScale = Vector3.one;
            card.Bind(save, OpenRoom, AskDelete);
        }
    }

    private void OpenRoom(SaveFile save)
    {
        if (LobbyService.Instance == null)
        {
            Debug.LogError("[SavedFiles] LobbyService.Instance kosong. Cek error compile di LobbyService.cs.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"[SavedFiles] scene '{lobbySceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            return;
        }

        SaveService.SetActive(save);
        LobbyService.Instance.CreateRoom(save.roomCode, save.roomName);
        SceneManager.LoadScene(lobbySceneName);
    }

    private void AskDelete(SaveFile save)
    {
        if (deletePanel == null)
        {
            DeleteNow(save);
            return;
        }

        pendingDelete = save;
        if (deleteLabel != null) deleteLabel.text = $"Hapus \"{save.roomName}\"?";
        deletePanel.SetActive(true);
    }

    // Hubungkan ke tombol Ya di panel konfirmasi.
    public void ConfirmDelete()
    {
        if (pendingDelete != null) DeleteNow(pendingDelete);
        CancelDelete();
    }

    // Hubungkan ke tombol Batal.
    public void CancelDelete()
    {
        pendingDelete = null;
        if (deletePanel != null) deletePanel.SetActive(false);
    }

    private void DeleteNow(SaveFile save)
    {
        SaveService.Delete(save);
        Rebuild();
    }
}
