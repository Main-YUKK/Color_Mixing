using UnityEngine;
using TMPro;
public class ClientRequestCardView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text descriptionText;   // teks deskripsi request
    [Tooltip("Aktifkan di scene yang dimuat setelah request dipilih (mis. ColorMixing): kartu langsung menampilkan GameSession.CurrentRequest.")]
    [SerializeField] private bool showCurrentOnStart;

    private void Start()
    {
        if (showCurrentOnStart && GameSession.CurrentRequest != null) Show(GameSession.CurrentRequest);
    }

    public void Show(ClientRequest request)
    {
        descriptionText.text = request.description;
    }
}
