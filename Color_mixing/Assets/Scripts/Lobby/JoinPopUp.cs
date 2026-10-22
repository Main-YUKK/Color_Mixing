using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JoinPopup : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private string lobbySceneName = "HostLobby";

    private bool joining;

    private void Awake()
    {
        codeInput.onValueChanged.AddListener(OnCodeChanged);
    }

    private void OnEnable()
    {
        joining = false;
        codeInput.SetTextWithoutNotify("");

        // Tampilkan alasan gagal terakhir (mis. "Room tidak ditemukan") bila ada.
        string last = LobbyService.Instance != null ? LobbyService.Instance.LastMessage : null;
        errorText.text = string.IsNullOrEmpty(last) ? "" : last;

        codeInput.ActivateInputField();   // memunculkan keyboard
    }

    private void OnCodeChanged(string value)
    {
        if (joining) return;

        // Buang karakter selain angka (mis. tanda minus).
        string digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits != value)
        {
            codeInput.SetTextWithoutNotify(digits);
        }

        errorText.text = "";

        if (digits.Length == 5)
        {
            Join(digits);
        }
    }

    private void Join(string code)
    {
        joining = true;

        if (!LobbyService.Instance.JoinRoom(code))
        {
            errorText.text = "Kode tidak valid.";
            joining = false;
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    // Dipanggil saat area gelap (Dimmer) ditekan
    public void Close()
    {
        gameObject.SetActive(false);
    }
}