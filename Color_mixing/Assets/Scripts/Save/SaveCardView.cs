using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Satu kartu di layar Saved Files: nama room (bisa diganti), kode, host, hari berikutnya, Open Room, hapus.
public class SaveCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_InputField nameInput;     // dimatikan otomatis, muncul saat pensil ditekan
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text hostText;
    [SerializeField] private TMP_Text dayText;             // opsional
    [SerializeField] private Button openButton;
    [SerializeField] private Button renameButton;          // ikon pensil
    [SerializeField] private Button deleteButton;          // ikon tempat sampah

    private SaveFile file;
    private Action<SaveFile> onOpen;
    private Action<SaveFile> onDelete;

    private void Awake()
    {
        openButton.onClick.AddListener(() => onOpen?.Invoke(file));
        deleteButton.onClick.AddListener(() => onDelete?.Invoke(file));

        if (renameButton != null) renameButton.onClick.AddListener(StartRename);
        if (nameInput != null)
        {
            nameInput.characterLimit = LobbyService.MaxRoomNameLength;
            nameInput.onEndEdit.AddListener(EndRename);
            nameInput.gameObject.SetActive(false);
        }
    }

    public void Bind(SaveFile save, Action<SaveFile> open, Action<SaveFile> delete)
    {
        file = save;
        onOpen = open;
        onDelete = delete;
        Refresh();
    }

    private void Refresh()
    {
        nameText.text = file.roomName;
        codeText.text = $"Kode Ruang: {file.roomCode}";
        hostText.text = $"Host: {file.hostName}";
        if (dayText != null) dayText.text = $"Lanjut: Hari {file.nextDay}";
    }

    private void StartRename()
    {
        if (nameInput == null) return;

        nameText.gameObject.SetActive(false);
        nameInput.gameObject.SetActive(true);
        nameInput.text = file.roomName;
        nameInput.ActivateInputField();
    }

    // Keyboard ditutup / fokus hilang: simpan nama (kosong = nama lama tetap).
    private void EndRename(string text)
    {
        SaveService.Rename(file, text);
        nameInput.gameObject.SetActive(false);
        nameText.gameObject.SetActive(true);
        Refresh();
    }
}
