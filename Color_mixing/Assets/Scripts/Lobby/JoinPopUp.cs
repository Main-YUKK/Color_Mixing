using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class JoinPopup : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Tombol")]
    [Tooltip("Tombol Join di dalam popup. Kosong = dicari otomatis berdasarkan nama 'JoinButton'.")]
    [SerializeField] private Button joinButton;
    [Tooltip("Tombol Exit di dalam popup. Kosong = dicari otomatis berdasarkan nama 'ExitButton'.")]
    [SerializeField] private Button exitButton;
    [Tooltip("Area gelap di belakang kartu. Kosong = dicari otomatis berdasarkan nama 'Dimmer'. Tap di sini TIDAK menutup popup.")]
    [SerializeField] private Button dimmerButton;

    [Header("Keyboard")]
    [Tooltip("Kartu/panel popup yang digeser naik (BUKAN Dimmer / root fullscreen).")]
    [SerializeField] private RectTransform panel;
    [Tooltip("Jarak aman antara bagian bawah panel dan keyboard (piksel layar).")]
    [SerializeField] private float keyboardMargin = 24f;
    [SerializeField] private float moveSpeed = 12f;

    private bool joining;

    private Canvas rootCanvas;
    private Vector2 basePosition;
    private float restBottomPx;       // posisi bawah panel di layar saat tidak bergeser (piksel)
    private bool measured;
    private float currentOffset;      // offset naik saat ini (satuan canvas)

    private void Awake()
    {
        codeInput.onValueChanged.AddListener(OnCodeChanged);

        AutoWireButtons();

        // Join hanya jalan lewat tombol Join (bukan otomatis saat 5 digit / saat centang keyboard ditekan).
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        else Debug.LogWarning("[JoinPopup] Tombol 'JoinButton' tidak ditemukan di dalam popup, player tidak bisa join.", this);

        // Popup hanya boleh ditutup lewat ExitButton.
        if (exitButton != null) exitButton.onClick.AddListener(Close);
        else Debug.LogWarning("[JoinPopup] Tombol 'ExitButton' tidak ditemukan di dalam popup, popup tidak bisa ditutup.", this);

        // Dimmer tetap menghalangi tap ke menu di belakang, tapi tidak lagi menutup popup.
        // (Komponen Button dimatikan, bukan objeknya, supaya Image-nya tetap menangkap tap.)
        if (dimmerButton != null) dimmerButton.enabled = false;

        // Teks tetap tampil di input field Unity, bukan di bar input native iOS.
        codeInput.shouldHideMobileInput = true;
        codeInput.keyboardType = TouchScreenKeyboardType.NumberPad;
        codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        codeInput.characterLimit = 5;

        if (panel != null)
        {
            basePosition = panel.anchoredPosition;
            rootCanvas = panel.GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
        }
        else
        {
            Debug.LogWarning("[JoinPopup] Field 'Panel' belum diisi di Inspector, popup tidak akan bergeser saat keyboard muncul.", this);
        }
    }

    private void OnEnable()
    {
        joining = false;
        codeInput.interactable = true;
        if (joinButton != null) joinButton.interactable = true;
        codeInput.SetTextWithoutNotify("");

        string last = LobbyService.Instance != null ? LobbyService.Instance.LastMessage : null;
        errorText.text = string.IsNullOrEmpty(last) ? "" : last;

        if (LobbyService.Instance != null)
        {
            LobbyService.Instance.OnJoinConfirmed += HandleJoinConfirmed;
            LobbyService.Instance.OnRoomClosed += HandleJoinFailed;
        }

        ResetPanelPosition();
        codeInput.ActivateInputField();
    }

    private void OnDisable()
    {
        if (LobbyService.Instance != null)
        {
            LobbyService.Instance.OnJoinConfirmed -= HandleJoinConfirmed;
            LobbyService.Instance.OnRoomClosed -= HandleJoinFailed;
        }

        ResetPanelPosition();
    }

    private bool lastKeyboardState;

    private void Update()
    {
        if (panel == null) return;

        // Ukur posisi panel saat diam (sekali per kali popup dibuka).
        if (!measured)
        {
            MeasureRestPosition();
            return;
        }

        bool keyboardUp = TouchScreenKeyboard.visible;
    #if !UNITY_EDITOR
        // Di iOS dengan hideMobileInput, visible kadang tidak terbaca: fokus input dianggap keyboard aktif.
        keyboardUp = keyboardUp || codeInput.isFocused;
    #endif

        if (keyboardUp != lastKeyboardState)
        {
            lastKeyboardState = keyboardUp;
            Debug.Log($"[JoinPopup] keyboard={(keyboardUp ? "naik" : "turun")}, area.height={TouchScreenKeyboard.area.height}, restBottomPx={restBottomPx}");
        }

        float targetOffset = 0f;

        if (keyboardUp)
        {
            float keyboardHeight = TouchScreenKeyboard.area.height;
            if (keyboardHeight <= 0f) keyboardHeight = Screen.height * 0.4f;   // cadangan bila area belum terbaca

            float neededPx = keyboardHeight + keyboardMargin - restBottomPx;
            if (neededPx > 0f)
            {
                float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
                targetOffset = neededPx / Mathf.Max(scale, 0.0001f);
            }
        }

        currentOffset = Mathf.Lerp(currentOffset, targetOffset, 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime));
        panel.anchoredPosition = basePosition + new Vector2(0f, currentOffset);
    }

    private void MeasureRestPosition()
    {
        panel.anchoredPosition = basePosition;
        currentOffset = 0f;

        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);

        Camera cam = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = rootCanvas.worldCamera;

        restBottomPx = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y;
        measured = true;
    }

    private void ResetPanelPosition()
    {
        measured = false;
        currentOffset = 0f;
        if (panel != null) panel.anchoredPosition = basePosition;
    }

    private void OnCodeChanged(string value)
    {
        if (joining) return;

        string digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits != value)
            codeInput.SetTextWithoutNotify(digits);

        errorText.text = "";
    }

    // Dipanggil saat tombol Join ditekan.
    private void OnJoinClicked()
    {
        if (joining) return;

        string code = codeInput.text;
        if (code == null || code.Length != 5)
        {
            errorText.text = "Masukkan 5 digit kode room.";
            return;
        }

        Join(code);
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

        // Jangan pindah scene dulu: tunggu host benar-benar ditemukan.
        codeInput.interactable = false;
        if (joinButton != null) joinButton.interactable = false;
        errorText.text = "Mencari room...";
    }

    private void HandleJoinConfirmed()
    {
        // Nama scene salah / belum masuk Build Settings: LoadScene diam-diam gagal dan popup
        // tertahan di "Mencari room..." padahal di host pemain ini sudah masuk. Batalkan join-nya.
        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"[JoinPopup] Scene '{lobbySceneName}' tidak ada di Build Settings. Cek field 'Lobby Scene Name'.", this);
            LobbyService.Instance.LeaveRoom();
            joining = false;
            codeInput.interactable = true;
            if (joinButton != null) joinButton.interactable = true;
            codeInput.SetTextWithoutNotify("");
            errorText.text = "Lobby tidak bisa dibuka.";
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private void HandleJoinFailed(string reason)
    {
        joining = false;
        codeInput.interactable = true;
        if (joinButton != null) joinButton.interactable = true;
        codeInput.SetTextWithoutNotify("");
        errorText.text = reason;
        codeInput.ActivateInputField();
    }

    // Dipanggil dari ExitButton (satu-satunya cara menutup popup).
    public void Close()
    {
        // Kalau sedang mencari room, batalkan pencariannya.
        if (joining && LobbyService.Instance != null)
            LobbyService.Instance.LeaveRoom();

        gameObject.SetActive(false);
    }

    // Cari tombol berdasarkan nama di dalam popup ini kalau field Inspector masih kosong.
    private void AutoWireButtons()
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            if (joinButton == null && b.gameObject.name == "JoinButton") joinButton = b;
            else if (exitButton == null && b.gameObject.name == "ExitButton") exitButton = b;
            else if (dimmerButton == null && b.gameObject.name == "Dimmer") dimmerButton = b;
        }
    }
}
