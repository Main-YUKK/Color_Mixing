using UnityEngine;
using UnityEngine.UI;

// Mengatur screen tutorial: hanya satu panel yang aktif. Maju ke screen berikutnya dengan tap di mana saja,
// mundur lewat tombol Back (screen 2 dst). Screen pertama memakai tombol Exit, bukan Back.
// Screen terakhir tidak memakai tap di mana saja: pemain keluar lewat tombol Done.
// Exit di screen pertama dan Done di screen terakhir keluar dari tutorial ke scene asal (lihat TutorialEntry).
[ExecuteAlways]
public class TutorialController : MonoBehaviour
{
    [Tooltip("Panel tutorial, urut dari screen pertama sampai terakhir.")]
    [SerializeField] private GameObject[] pages;
    [Tooltip("Background salon bersama untuk screen akhir. Dibuat di luar panel supaya tidak menutupi aset lain saat diedit.")]
    [SerializeField] private GameObject salonBackground;
    [Tooltip("Index screen pertama (mulai 0) yang memakai background salon. Screen 5 = index 4.")]
    [SerializeField] private int salonBackgroundFromPage = 4;
    [SerializeField] private Button backButton;
    [Tooltip("Tombol Exit, hanya tampil di screen pertama (menggantikan Back).")]
    [SerializeField] private Button exitButton;
    [Tooltip("Tombol transparan seukuran layar untuk 'tap di mana saja' = Next. Harus berada di bawah Back/Exit.")]
    [SerializeField] private Button nextButton;
    [Tooltip("Tombol Done di screen terakhir: keluar dari tutorial ke scene asal (MainMenu/Lobby).")]
    [SerializeField] private Button doneButton;

    private const string DoneButtonName = "DoneButton";

    private int currentIndex;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        backButton.onClick.AddListener(ShowPrevious);
        exitButton.onClick.AddListener(TutorialEntry.Exit);
        nextButton.onClick.AddListener(ShowNext);
        if (doneButton == null)
            doneButton = FindDoneButton();

        if (doneButton != null)
            doneButton.onClick.AddListener(TutorialEntry.Exit);
        else
            Debug.LogWarning($"TutorialController: Done Button tidak ditemukan. Isi di Inspector atau beri nama tombolnya '{DoneButtonName}' di page terakhir.", this);
        ShowPage(0);
    }

    // Di Edit Mode background salon mengikuti panel yang sedang dinyalakan, supaya tampilan page 5-6 bisa diatur langsung.
    private void Update()
    {
        if (Application.isPlaying || salonBackground == null || pages == null)
            return;

        bool needed = false;
        for (int i = salonBackgroundFromPage; i < pages.Length; i++)
            needed |= pages[i] != null && pages[i].activeSelf;

        if (salonBackground.activeSelf != needed)
            salonBackground.SetActive(needed);
    }

    // Cadangan kalau field Done Button kosong: cari tombol bernama DoneButton di page terakhir (termasuk yang nonaktif).
    private Button FindDoneButton()
    {
        if (pages == null || pages.Length == 0 || pages[pages.Length - 1] == null)
            return null;

        foreach (Button button in pages[pages.Length - 1].GetComponentsInChildren<Button>(true))
        {
            if (button.name == DoneButtonName)
                return button;
        }

        return null;
    }

    private void ShowPrevious()
    {
        if (currentIndex > 0)
            ShowPage(currentIndex - 1);
    }

    private void ShowNext()
    {
        // Screen terakhir hanya bisa ditutup lewat tombol Done.
        if (currentIndex >= pages.Length - 1)
            return;

        ShowPage(currentIndex + 1);
    }

    private void ShowPage(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == currentIndex);

        if (salonBackground != null)
            salonBackground.SetActive(currentIndex >= salonBackgroundFromPage);

        bool firstPage = currentIndex == 0;
        exitButton.gameObject.SetActive(firstPage);
        backButton.gameObject.SetActive(!firstPage);

        // Tap di mana saja dimatikan di screen terakhir supaya tidak menghalangi tombol Done.
        nextButton.gameObject.SetActive(currentIndex < pages.Length - 1);
    }
}
