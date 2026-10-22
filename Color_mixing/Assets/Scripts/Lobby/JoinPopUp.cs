using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JoinPopup : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private string lobbySceneName = "HostLobby";

    private void OnEnable()
    {
        codeInput.text = "";
        errorText.text = "";
    }

    // Dipanggil tombol Join di dalam popup
    public void OnJoinClicked()
    {
        if (!LobbyService.Instance.JoinRoom(codeInput.text))
        {
            errorText.text = "Kode harus 5 digit angka.";
            return;
        }
        SceneManager.LoadScene(lobbySceneName);
    }
}