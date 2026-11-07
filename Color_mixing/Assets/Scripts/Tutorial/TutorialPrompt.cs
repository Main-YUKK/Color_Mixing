using UnityEngine;
using UnityEngine.UI;

// Alert "Mw lihat tutorial gk?" di scene MainMenu. Tampil otomatis hanya saat game pertama kali dibuka
// (lihat TutorialPromptState). "Iya dong" membuka scene tutorial, "Gk usah" menutup alert.
//
// Pasang di akar alert dan biarkan objeknya AKTIF di scene: kalau nonaktif, Awake tidak pernah jalan
// dan alert tidak akan muncul. Saat game berjalan, script ini yang mematikan objek kalau alert tidak perlu tampil.
public class TutorialPrompt : MonoBehaviour
{
    [Tooltip("Tombol \"Gk usah\": tutup alert.")]
    [SerializeField] private Button skipButton;
    [Tooltip("Tombol \"Iya dong\": buka scene tutorial.")]
    [SerializeField] private Button watchButton;

    private void Awake()
    {
        if (skipButton != null) skipButton.onClick.AddListener(Hide);
        if (watchButton != null) watchButton.onClick.AddListener(OpenTutorial);

        if (!TutorialPromptState.ShouldShow)
        {
            Hide();
            return;
        }

        // Langsung dicatat saat tampil: mau dijawab atau game ditutup, alert tidak muncul lagi.
        TutorialPromptState.MarkShown();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OpenTutorial()
    {
        if (TutorialEntry.Open())
            Hide();
    }
}
