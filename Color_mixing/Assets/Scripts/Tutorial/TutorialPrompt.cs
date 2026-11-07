using UnityEngine;
using UnityEngine.UI;

// Alert "Mw lihat tutorial gk?" di scene MainMenu. Tampil otomatis hanya saat game pertama kali dibuka
// (lihat TutorialPromptState). "Iya dong" membuka scene tutorial, "Gk usah" menutup alert.
// Objek ini harus selalu aktif; isi alert ada di "content" dan disembunyikan kalau tidak perlu tampil.
public class TutorialPrompt : MonoBehaviour
{
    [Tooltip("Isi alert (Dimmer + Card). Dinyalakan hanya saat alert tampil.")]
    [SerializeField] private GameObject content;
    [Tooltip("Tombol \"Gk usah\": tutup alert.")]
    [SerializeField] private Button skipButton;
    [Tooltip("Tombol \"Iya dong\": buka scene tutorial.")]
    [SerializeField] private Button watchButton;
    [Tooltip("Nama scene tutorial. Harus ada di Build Profiles.")]
    [SerializeField] private string tutorialSceneName = "Tutorial";

    private void Awake()
    {
        if (skipButton != null) skipButton.onClick.AddListener(Hide);
        if (watchButton != null) watchButton.onClick.AddListener(OpenTutorial);

        content.SetActive(false);
    }

    private void Start()
    {
        if (!TutorialPromptState.ShouldShow)
            return;

        // Langsung dicatat saat tampil: mau dijawab atau game ditutup, alert tidak muncul lagi.
        TutorialPromptState.MarkShown();
        content.SetActive(true);
    }

    private void Hide()
    {
        content.SetActive(false);
    }

    private void OpenTutorial()
    {
        if (!Application.CanStreamedLevelBeLoaded(tutorialSceneName))
        {
            Debug.LogError($"TutorialPrompt: scene '{tutorialSceneName}' tidak ditemukan. Cek nama dan Build Profiles.", this);
            return;
        }

        Hide();
        SceneLoader.Load(tutorialSceneName);
    }
}
