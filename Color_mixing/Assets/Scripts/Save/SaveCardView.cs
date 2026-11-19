using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Satu kartu di layar Saved Files: nama room (bisa diganti), host, hari terakhir yang selesai, Buka Ruang, hapus.
public class SaveCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_InputField nameInput;     // dimatikan otomatis, muncul saat pensil ditekan
    [SerializeField] private TMP_Text hostText;
    [SerializeField] private TMP_Text dayText;             // opsional
    [SerializeField] private Button openButton;
    [SerializeField] private Button renameButton;          // ikon pensil
    [SerializeField] private Button deleteButton;          // ikon tempat sampah

    [Header("Input ganti nama (kartu putih pill)")]
    [Tooltip("Font teks yang diketik & placeholder. Kosong = pakai font yang sudah terpasang di input.")]
    [SerializeField] private TMP_FontAsset nameFont;
    [Tooltip("Ukuran kartu putih (pill) saat mengetik nama, dalam unit canvas.")]
    [SerializeField] private Vector2 nameInputSize = new Vector2(800f, 100f);
    [SerializeField] private float nameFontSize = 64f;

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
            // Rata kiri seperti judul kartu; jarak kiri lebih kecil supaya teks hampir sejajar dengan nama room.
            PillInputStyle.Apply(nameInput, nameFont, nameInputSize, nameFontSize,
                TextAlignmentOptions.Left, paddingXRatio: 0.3f);
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
        hostText.text = $"Host: {file.hostName}";
        if (dayText != null) dayText.text = $"Day: {file.completedDay}";
    }

    private void StartRename()
    {
        if (nameInput == null) return;

        nameText.gameObject.SetActive(false);
        if (renameButton != null) renameButton.gameObject.SetActive(false);   // pill memakai tempat pensil
        nameInput.gameObject.SetActive(true);
        PillInputStyle.BeginEdit(this, nameInput, file.roomName, LobbyService.MaxRoomNameLength);
    }

    // Keyboard ditutup / fokus hilang: simpan nama (kosong = nama lama tetap).
    private void EndRename(string text)
    {
        SaveService.Rename(file, text);
        nameInput.gameObject.SetActive(false);
        nameText.gameObject.SetActive(true);
        if (renameButton != null) renameButton.gameObject.SetActive(true);
        Refresh();
    }
}
