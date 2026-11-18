using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Popup "masukkan kode room" di main menu (JoinPopup): validasi kode, mulai mencari room, dan menangani hasilnya.
// Memakai singleton LobbyService.Instance. Nama scene Lobby dibuat palsu supaya test tidak benar-benar pindah scene.
public class JoinPopupTests
{
    private const string FakeLobbyScene = "SceneLobiPalsu";

    private readonly List<GameObject> created = new List<GameObject>();
    private LobbyService service;
    private JoinPopup popup;
    private GameObject popupGo;
    private TMP_InputField codeInput;
    private TextMeshPro errorText;
    private Button joinButton, exitButton, dimmerButton;

    [SetUp]
    public void SetUp()
    {
        created.Clear();
        service = LobbyService.Instance;
        Assert.IsNotNull(service);
        ResetService();
    }

    // LastMessage (pesan gagal terakhir) bertahan di singleton sampai Create/JoinRoom berikutnya,
    // jadi dibersihkan dulu supaya tidak bocor dari test lain.
    private void ResetService()
    {
        service.CreateRoom();
        service.LeaveRoom();
        service.Players.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        // Langsung (bukan Destroy biasa): popup harus berhenti mendengarkan LobbyService sebelum test berikutnya.
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);

        ResetService();
    }

    // ---------- Membangun popup palsu ----------

    private GameObject New(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        created.Add(go);
        return go;
    }

    private Button NewButton(string name, Transform parent)
    {
        var go = New(name, parent);
        go.AddComponent<Image>();
        return go.AddComponent<Button>();
    }

    // Kondisi LobbyService (mis. pesan gagal sebelumnya) harus disiapkan SEBELUM ini dipanggil.
    // assignButtonFields=false: tombol dibiarkan kosong di Inspector dan dicari popup berdasarkan nama.
    private void BuildPopup(bool assignButtonFields = true)
    {
        // Nonaktif dulu supaya Awake/OnEnable jalan setelah semua field terisi.
        popupGo = New("JoinPopup");
        popupGo.SetActive(false);
        popup = popupGo.AddComponent<JoinPopup>();

        var inputGo = New("CodeInput", popupGo.transform);
        inputGo.AddComponent<Image>();
        var textGo = New("InputText", inputGo.transform);
        var inputText = textGo.AddComponent<TextMeshProUGUI>();
        codeInput = inputGo.AddComponent<TMP_InputField>();
        codeInput.textViewport = (RectTransform)inputGo.transform;
        codeInput.textComponent = inputText;

        errorText = New("ErrorText", popupGo.transform).AddComponent<TextMeshPro>();
        errorText.text = "sisa";

        joinButton = NewButton("JoinButton", popupGo.transform);
        exitButton = NewButton("ExitButton", popupGo.transform);
        dimmerButton = NewButton("Dimmer", popupGo.transform);

        PlayTestHelpers.SetField(popup, "codeInput", codeInput);
        PlayTestHelpers.SetField(popup, "errorText", errorText);
        PlayTestHelpers.SetField(popup, "lobbySceneName", FakeLobbyScene);
        if (assignButtonFields)
        {
            PlayTestHelpers.SetField(popup, "joinButton", joinButton);
            PlayTestHelpers.SetField(popup, "exitButton", exitButton);
            PlayTestHelpers.SetField(popup, "dimmerButton", dimmerButton);
        }

        popupGo.SetActive(true);
    }

    private void TypeCode(string code) => codeInput.SetTextWithoutNotify(code);

    private void JoinWith(string code)
    {
        TypeCode(code);
        joinButton.onClick.Invoke();
    }

    // ---------- Saat dibuka ----------

    [Test]
    public void Open_StartsCleanAndReadyToType()
    {
        BuildPopup();

        Assert.AreEqual("", errorText.text, "sisa teks lama dibersihkan");
        Assert.AreEqual("", codeInput.text);
        Assert.IsTrue(codeInput.interactable);
        Assert.IsTrue(joinButton.interactable);
    }

    [Test]
    public void Open_ShowsWhyThePreviousJoinFailed()
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.KickLocal(service);
        Assert.IsNotNull(service.LastMessage, "syarat awal");

        BuildPopup();

        Assert.AreEqual("Kamu dikeluarkan oleh host.", errorText.text);
    }

    [Test]
    public void Open_ConfiguresTheCodeFieldForFiveDigits()
    {
        BuildPopup();

        Assert.AreEqual(5, codeInput.characterLimit);
        Assert.AreEqual(TMP_InputField.ContentType.IntegerNumber, codeInput.contentType);
        Assert.IsTrue(codeInput.shouldHideMobileInput);
    }

    [Test]
    public void Open_DimmerStaysButCannotClosePopup()
    {
        BuildPopup();

        Assert.IsFalse(dimmerButton.enabled);
        dimmerButton.onClick.Invoke();
        Assert.IsTrue(popupGo.activeSelf);
    }

    [Test]
    public void Open_FindsButtonsByNameWhenInspectorFieldsAreEmpty()
    {
        BuildPopup(assignButtonFields: false);

        JoinWith("12345");
        Assert.AreEqual(LobbyMode.Client, service.Mode, "JoinButton ditemukan lewat nama");

        exitButton.onClick.Invoke();
        Assert.IsFalse(popupGo.activeSelf, "ExitButton ditemukan lewat nama");
    }

    // ---------- Mengetik kode ----------

    [Test]
    public void Typing_NonDigits_AreRemoved()
    {
        BuildPopup();

        codeInput.onValueChanged.Invoke("12a3b");

        Assert.AreEqual("123", codeInput.text);
    }

    [Test]
    public void Typing_ClearsTheErrorMessage()
    {
        BuildPopup();
        errorText.text = "Kode tidak valid.";

        codeInput.onValueChanged.Invoke("1");

        Assert.AreEqual("", errorText.text);
    }

    // ---------- Tombol Join ----------

    [TestCase("")]
    [TestCase("1")]
    [TestCase("1234")]
    public void Join_WithFewerThanFiveDigits_ShowsAnErrorAndDoesNotJoin(string code)
    {
        BuildPopup();

        JoinWith(code);

        Assert.AreEqual("Masukkan 5 digit kode room.", errorText.text);
        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsTrue(codeInput.interactable, "form tidak terkunci");
    }

    [Test]
    public void Join_WithFiveDigits_StartsSearchingAndLocksTheForm()
    {
        BuildPopup();

        JoinWith("12345");

        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.AreEqual("12345", service.RoomCode);
        Assert.AreEqual("Mencari room...", errorText.text);
        Assert.IsFalse(codeInput.interactable);
        Assert.IsFalse(joinButton.interactable);
        Assert.IsTrue(popupGo.activeSelf, "popup tetap terbuka sampai host ditemukan");
    }

    [Test]
    public void Join_ClickedAgainWhileSearching_IsIgnored()
    {
        BuildPopup();
        JoinWith("12345");

        JoinWith("54321");

        Assert.AreEqual("12345", service.RoomCode, "pencarian pertama tidak diganti");
    }

    // ---------- Hasil pencarian ----------

    [Test]
    public void SearchFails_ShowsTheReasonAndUnlocksTheFormForARetry()
    {
        BuildPopup();
        JoinWith("12345");

        LobbyTestHelpers.KickLocal(service);     // LobbyService menutup room dengan alasan

        Assert.AreEqual("Kamu dikeluarkan oleh host.", errorText.text);
        Assert.IsTrue(codeInput.interactable);
        Assert.IsTrue(joinButton.interactable);
        Assert.AreEqual("", codeInput.text, "kode lama dihapus");

        JoinWith("54321");
        Assert.AreEqual("54321", service.RoomCode, "bisa mencoba lagi");
    }

    [Test]
    public void SearchSucceeds_ButLobbySceneIsMissing_CancelsTheJoinWithAMessage()
    {
        BuildPopup();
        JoinWith("12345");
        LogAssert.Expect(LogType.Error, new Regex(FakeLobbyScene));

        LobbyTestHelpers.Receive(service, "HELLO", "Sari", isHost: true);     // host ditemukan

        Assert.AreEqual(LobbyMode.None, service.Mode, "join dibatalkan supaya tidak masuk diam-diam di sisi host");
        Assert.AreEqual("Lobby tidak bisa dibuka.", errorText.text);
        Assert.IsTrue(codeInput.interactable);
        Assert.IsTrue(joinButton.interactable);
        Assert.AreEqual("", codeInput.text);
    }

    // ---------- Menutup popup ----------

    [Test]
    public void ExitButton_ClosesThePopup()
    {
        BuildPopup();

        exitButton.onClick.Invoke();

        Assert.IsFalse(popupGo.activeSelf);
    }

    [Test]
    public void Close_WhileSearching_CancelsTheSearch()
    {
        BuildPopup();
        JoinWith("12345");

        popup.Close();

        Assert.IsFalse(popupGo.activeSelf);
        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
    }

    [Test]
    public void Close_WhenNotSearching_LeavesAnExistingRoomAlone()
    {
        service.JoinRoom("11111");      // bukan lewat popup
        BuildPopup();

        popup.Close();

        Assert.IsFalse(popupGo.activeSelf);
        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.AreEqual("11111", service.RoomCode);
    }

    // ---------- Ditutup lalu dibuka lagi ----------

    [Test]
    public void ReopeningThePopup_UnlocksTheForm()
    {
        BuildPopup();
        JoinWith("12345");
        Assert.IsFalse(codeInput.interactable, "syarat awal");

        popupGo.SetActive(false);
        popupGo.SetActive(true);

        Assert.IsTrue(codeInput.interactable);
        Assert.IsTrue(joinButton.interactable);
        Assert.AreEqual("", codeInput.text);
    }

    [Test]
    public void ClosedPopup_StopsListeningToTheLobbyService()
    {
        BuildPopup();
        errorText.text = "tidak boleh berubah";
        int before = LobbyTestHelpers.Subscribers(service, "OnRoomClosed");
        popupGo.SetActive(false);
        Assert.AreEqual(before - 1, LobbyTestHelpers.Subscribers(service, "OnRoomClosed"));

        service.JoinRoom("12345");
        LobbyTestHelpers.KickLocal(service);

        Assert.AreEqual("tidak boleh berubah", errorText.text, "popup tertutup tidak ikut berubah");
    }
}
