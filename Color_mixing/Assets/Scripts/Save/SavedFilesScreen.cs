using System.Collections.Generic;
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
#if UNITY_EDITOR
        saves.InsertRange(0, dummySaves);   // save palsu (tombol tes) tampil paling atas
#endif
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
        // Kode ruang tidak disimpan: setiap kali dibuka, room mendapat kode baru.
        LobbyService.Instance.CreateRoom(null, save.roomName);
        SceneLoader.Load(lobbySceneName);
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
#if UNITY_EDITOR
        dummySaves.Remove(save);
#endif
        Rebuild();
    }

    // ---------- Debug (tombol tes di Editor) ----------

#if UNITY_EDITOR
    // Save palsu hanya ada di memori selama Play: tidak ditulis ke disk (lihat SaveService.Write) dan hilang saat Play dihentikan.
    private static readonly List<SaveFile> dummySaves = new List<SaveFile>();
    private static int dummyCounter;

    // Nama ketiga = 20 karakter (batas maksimal nama room) untuk cek nama panjang di kartu.
    private static readonly string[] DummyNames = { "Ruang Player {0}", "Kelas Seni {0}", "Ruangan Seru Banget!" };

    // Reload Domain dimatikan: static tidak reset sendiri saat Play berikutnya.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearDummiesOnPlay()
    {
        dummySaves.Clear();
        dummyCounter = 0;
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);

        // Pojok kanan atas supaya tidak menutupi tombol back.
        float x = Screen.width / scale - 170;
        if (GUI.Button(new Rect(x, 10, 160, 28), "Tambah save palsu"))
        {
            AddDummy();
            Rebuild();
        }
        if (GUI.Button(new Rect(x, 42, 160, 28), "Hapus save palsu") && dummySaves.Count > 0)
        {
            dummySaves.RemoveAt(0);
            Rebuild();
        }
    }

    private static void AddDummy()
    {
        int n = ++dummyCounter;
        int day = (n * 7) % 31;   // campuran angka 1 dan 2 digit
        dummySaves.Insert(0, new SaveFile
        {
            id = SaveService.EditorDummyPrefix + n,
            roomName = string.Format(DummyNames[(n - 1) % DummyNames.Length], n),
            hostName = "Player " + n,
            completedDay = day,
            nextDay = day + 1,
        });
    }
#endif
}
