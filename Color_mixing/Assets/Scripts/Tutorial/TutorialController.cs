using UnityEngine;
using UnityEngine.UI;

// Mengatur screen tutorial: hanya satu panel yang aktif, berpindah lewat tombol Back dan Next.
// Back di screen pertama dan Next di screen terakhir keluar dari tutorial ke scene asal (lihat TutorialEntry).
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
    [SerializeField] private Button nextButton;

    private int currentIndex;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        backButton.onClick.AddListener(ShowPrevious);
        nextButton.onClick.AddListener(ShowNext);
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

    private void ShowPrevious()
    {
        if (currentIndex == 0)
        {
            TutorialEntry.Exit();
            return;
        }

        ShowPage(currentIndex - 1);
    }

    private void ShowNext()
    {
        if (currentIndex >= pages.Length - 1)
        {
            TutorialEntry.Exit();
            return;
        }

        ShowPage(currentIndex + 1);
    }

    private void ShowPage(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == currentIndex);

        if (salonBackground != null)
            salonBackground.SetActive(currentIndex >= salonBackgroundFromPage);
    }
}
