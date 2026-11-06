using UnityEngine;
using UnityEngine.UI;

// Mengatur screen tutorial: hanya satu panel yang aktif, berpindah lewat tombol Back dan Next.
public class TutorialController : MonoBehaviour
{
    [Tooltip("Panel tutorial, urut dari screen pertama sampai terakhir.")]
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;

    private int currentIndex;

    private void Awake()
    {
        backButton.onClick.AddListener(ShowPrevious);
        nextButton.onClick.AddListener(ShowNext);
        ShowPage(0);
    }

    private void ShowPrevious()
    {
        ShowPage(currentIndex - 1);
    }

    private void ShowNext()
    {
        ShowPage(currentIndex + 1);
    }

    private void ShowPage(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == currentIndex);

        // Tujuan tombol di screen pertama dan terakhir ditentukan saat navigasi antar-scene dibuat.
        backButton.gameObject.SetActive(currentIndex > 0);
        nextButton.gameObject.SetActive(currentIndex < pages.Length - 1);
    }
}
