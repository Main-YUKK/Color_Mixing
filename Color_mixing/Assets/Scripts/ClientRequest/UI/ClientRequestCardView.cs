using UnityEngine;
using TMPro;
public class ClientRequestCardView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text descriptionText;   // teks deskripsi request
  

    public void Show(ClientRequest request)
    {
        descriptionText.text = request.description;
    }
}
