using TMPro;
using UnityEngine;

// Alert "Waktunya ganti warna..." saat pigmen dirotasi. Selama tampil, tombol Campur disembunyikan.
// Tidak muncul kalau pemain sudah menekan Campur: tombol Batal Campur + teks menunggu lebih penting.
// Pasang di objek yang selalu aktif; yang dinyalakan/dimatikan adalah panel.
public class ColorChangeAlert : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;    // cadangan nomor pemain saat tes tanpa jaringan
    [SerializeField] private GameObject panel;           // latar GantiWarna + teks
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup mixButton;      // disembunyikan selama alert tampil
    [SerializeField] private string message = "Waktunya ganti warna";
    [SerializeField] private int dotCount = 3;
    [SerializeField] private float dotInterval = 0.3f;   // jeda antar titik muncul
    [SerializeField] private float duration = 1.5f;      // samakan dengan lama animasi rotasi di PigmentSlot

    private float timeLeft;

    private int Player => LocalPlayer.Index(player);

    private void OnEnable() => colorMixingController.Rotated += OnRotated;

    private void OnDisable()
    {
        colorMixingController.Rotated -= OnRotated;
        SetShown(false);
    }

    private void Start() => SetShown(false);

    private void OnRotated()
    {
        if (colorMixingController.IsReady(Player)) return;
        timeLeft = duration;
        SetShown(true);
        UpdateDots();
    }

    private void Update()
    {
        if (timeLeft <= 0f) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) SetShown(false);
        else UpdateDots();
    }

    // Titik muncul satu per satu lalu ulang. Titik yang belum muncul tetap ada tapi transparan, jadi teks tidak bergeser.
    private void UpdateDots()
    {
        int shown = (int)((duration - timeLeft) / dotInterval) % (dotCount + 1);
        text.text = message + new string('.', shown) + "<alpha=#00>" + new string('.', dotCount - shown);
    }

    private void SetShown(bool shown)
    {
        if (!shown) timeLeft = 0f;
        panel.SetActive(shown);
        mixButton.alpha = shown ? 0f : 1f;
        mixButton.interactable = !shown;
        mixButton.blocksRaycasts = !shown;
    }
}
