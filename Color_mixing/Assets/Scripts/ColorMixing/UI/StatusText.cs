using TMPro;
using UnityEngine;

// Teks status di atas tombol Campur: tampil hanya saat pemain ini sudah Campur dan menunggu pemain lain.
// Alert "Waktunya ganti warna..." ada di ColorChangeAlert.
public class StatusText : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    private int Player => LocalPlayer.Index(player);   // nomor pemain di device ini
    [SerializeField] private TMP_Text text;
    [SerializeField] private string waitingMessage = "Menunggu semua pemain untuk Campur";

    private void OnEnable() => colorMixingController.StateChanged += Refresh;
    private void OnDisable() => colorMixingController.StateChanged -= Refresh;
    private void Start() => Refresh();

    private void Refresh()
    {
        bool waiting = colorMixingController.IsReady(Player) && !colorMixingController.IsMixConfirmed;
        text.text = waiting ? waitingMessage : "";
    }
}
