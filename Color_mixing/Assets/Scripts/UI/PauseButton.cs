using UnityEngine;
using UnityEngine.UI;

// Pasang di tombol Pause di layar game: membuka menu pause. Waktu game TIDAK berhenti
// (pemain lain tetap main), menu hanya menutupi layar.
[RequireComponent(typeof(Button))]
public class PauseButton : MonoBehaviour
{
    private void Awake() => GetComponent<Button>().onClick.AddListener(PauseMenu.Open);
}
