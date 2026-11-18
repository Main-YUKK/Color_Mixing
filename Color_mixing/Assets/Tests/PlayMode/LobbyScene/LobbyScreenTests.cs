using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Tampilan scene Lobby (LobbyScreen): judul room, status, slot pemain, tombol Ready / Play, edit nama room,
// dan perpindahan scene saat room ditutup.
// LobbyScreen memakai singleton LobbyService.Instance, jadi pesan dari device lain ditiru lewat LobbyTestHelpers.
// Nama scene dibuat palsu supaya test tidak benar-benar pindah scene (SceneLoader hanya mencatat error "tidak ditemukan").
public class LobbyScreenTests
{
    private const string FakeMenuScene = "SceneMenuPalsu";
    private const string FakeGameScene = "SceneGamePalsu";

    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;
    private LobbyScreen screen;

    private TextMeshPro roomNameText, roomCodeText, statusText, readyLabel;
    private GameObject statusFrame;
    private SlotRig left, host, right;
    private Button playButton, readyButton, editButton;
    private TMP_InputField roomNameInput;
    private Sprite readySprite, waitingSprite, waitingHostSprite;

    private static int Required => LobbyTestHelpers.ReadConst<int>(typeof(LobbyScreen), "RequiredPlayers");

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        service = LobbyService.Instance;
        Assert.IsNotNull(service);
        service.LeaveRoom();
        service.Players.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) Object.Destroy(o);
        yield return null;      // beri waktu OnDestroy melepas subscribe sebelum test berikutnya

        service.LeaveRoom();
        service.Players.Clear();
    }

    // ---------- Membangun layar palsu ----------

    private GameObject New(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        toDestroy.Add(go);
        return go;
    }

    private TextMeshPro NewText(string name)
    {
        var text = New(name).AddComponent<TextMeshPro>();
        text.text = "-";
        return text;
    }

    private Button NewButton(string name)
    {
        var go = New(name);
        go.AddComponent<Image>();       // Image dulu: Button mengambilnya sebagai targetGraphic
        return go.AddComponent<Button>();
    }

    private Sprite NewSprite(int width, int height)
    {
        var tex = new Texture2D(width, height);
        toDestroy.Add(tex);
        var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        toDestroy.Add(sprite);
        return sprite;
    }

    // Bangun LobbyScreen. Keadaan LobbyService (host / client) harus disiapkan SEBELUM ini dipanggil.
    private IEnumerator BuildScreen(bool withRoomNameEditing = false)
    {
        roomNameText = NewText("RoomNameText");
        roomCodeText = NewText("RoomCodeText");
        statusText = NewText("StatusText");
        statusFrame = New("StatusFrame");
        left = SlotRig.Build("LeftSlot", toDestroy);
        host = SlotRig.Build("HostSlot", toDestroy);
        right = SlotRig.Build("RightSlot", toDestroy);

        playButton = NewButton("PlayButton");
        readyButton = NewButton("ReadyButton");
        ((RectTransform)readyButton.transform).sizeDelta = new Vector2(100f, 50f);
        readyLabel = NewText("ReadyLabel");
        readyLabel.transform.SetParent(readyButton.transform, false);

        readySprite = NewSprite(100, 50);
        waitingSprite = NewSprite(40, 20);
        waitingHostSprite = NewSprite(30, 10);

        // Nonaktif dulu supaya Start() jalan setelah semua field terisi.
        var go = New("LobbyScreen");
        go.SetActive(false);
        screen = go.AddComponent<LobbyScreen>();

        PlayTestHelpers.SetField(screen, "roomNameText", roomNameText);
        PlayTestHelpers.SetField(screen, "roomCodeText", roomCodeText);
        PlayTestHelpers.SetField(screen, "statusText", statusText);
        PlayTestHelpers.SetField(screen, "statusFrame", statusFrame);
        PlayTestHelpers.SetField(screen, "leftSlot", left.view);
        PlayTestHelpers.SetField(screen, "hostSlot", host.view);
        PlayTestHelpers.SetField(screen, "rightSlot", right.view);
        PlayTestHelpers.SetField(screen, "playButton", playButton);
        PlayTestHelpers.SetField(screen, "readyButton", readyButton);
        PlayTestHelpers.SetField(screen, "readyButtonLabel", readyLabel);
        PlayTestHelpers.SetField(screen, "readySprite", readySprite);
        PlayTestHelpers.SetField(screen, "waitingSprite", waitingSprite);
        PlayTestHelpers.SetField(screen, "waitingHostSprite", waitingHostSprite);
        PlayTestHelpers.SetField(screen, "mainMenuSceneName", FakeMenuScene);
        PlayTestHelpers.SetField(screen, "gameSceneName", FakeGameScene);

        if (withRoomNameEditing)
        {
            editButton = NewButton("EditNameButton");
            PlayTestHelpers.SetField(screen, "editRoomNameButton", editButton);

            // Nonaktif dulu supaya TMP_InputField baru aktif setelah teks dan viewport-nya terpasang.
            var inputGo = New("RoomNameInput");
            inputGo.SetActive(false);
            inputGo.AddComponent<Image>();
            var textGo = New("InputText");
            textGo.transform.SetParent(inputGo.transform, false);
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            roomNameInput = inputGo.AddComponent<TMP_InputField>();
            roomNameInput.textViewport = (RectTransform)inputGo.transform;
            roomNameInput.textComponent = inputText;
            inputGo.SetActive(true);
            PlayTestHelpers.SetField(screen, "roomNameInput", roomNameInput);
        }

        // Tanpa input nama room, LobbyScreen memang memberi peringatan bahwa fitur edit nama nonaktif.
        if (!withRoomNameEditing)
            LogAssert.Expect(LogType.Warning, new Regex("Fitur edit nama room nonaktif"));

        go.SetActive(true);
        yield return null;      // Start()
    }

    private static void ClientHello(string name, bool ready = false)
    {
        LobbyTestHelpers.Receive(LobbyService.Instance, "HELLO", name, ready: ready);
    }

    private static void HostHello(string name = "HostX", string roomName = "")
    {
        LobbyTestHelpers.Receive(LobbyService.Instance, "HELLO", name, isHost: true, roomName: roomName);
    }

    // ---------- Start ----------

    [UnityTest]
    public IEnumerator Start_WithoutARoom_CreatesOneAsHostAndShowsItsCode()
    {
        Assert.AreEqual(LobbyMode.None, service.Mode, "syarat awal");

        yield return BuildScreen();

        Assert.AreEqual(LobbyMode.Host, service.Mode);
        Assert.AreEqual($"Kode Ruang: {service.RoomCode}", roomCodeText.text);
    }

    [UnityTest]
    public IEnumerator Start_AfterJoining_StaysAClientWithTheSameCode()
    {
        service.JoinRoom("12345");

        yield return BuildScreen();

        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.AreEqual("12345", service.RoomCode);
        Assert.AreEqual("Kode Ruang: 12345", roomCodeText.text);
    }

    [UnityTest]
    public IEnumerator Start_EndsAnyGameThatWasStillMarkedAsRunning()
    {
        service.CreateRoom("12345");
        service.SetInGame(true);

        yield return BuildScreen();

        Assert.IsFalse(service.InGame, "masuk lobby = game sebelumnya selesai");
    }

    // ---------- Judul room ----------

    [UnityTest]
    public IEnumerator RoomTitle_DefaultsToTheHostName()
    {
        yield return BuildScreen();

        Assert.AreEqual($"Ruang {LobbyTestHelpers.LocalName(service)}", roomNameText.text);
    }

    [UnityTest]
    public IEnumerator RoomTitle_UsesTheCustomNameWhenTheHostSetOne()
    {
        yield return BuildScreen();

        service.SetRoomName("Warung Biru");

        Assert.AreEqual("Warung Biru", roomNameText.text);
    }

    [UnityTest]
    public IEnumerator RoomTitle_ForAClientWhoHasNotFoundTheHost_SaysJoining()
    {
        service.JoinRoom("12345");

        yield return BuildScreen();

        Assert.AreEqual("Bergabung...", roomNameText.text);
    }

    [UnityTest]
    public IEnumerator RoomTitle_ForAClient_FollowsTheHostMessages()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();

        HostHello("Sari");
        Assert.AreEqual("Ruang Sari", roomNameText.text);

        HostHello("Sari", roomName: "Warung Biru");
        Assert.AreEqual("Warung Biru", roomNameText.text);
    }

    [UnityTest]
    public IEnumerator RoomTitle_LongHostName_IsShortened()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();

        HostHello("Abcdefghijklmnopqrst");

        Assert.AreEqual("Ruang Abcdefghijklmn…", roomNameText.text);
    }

    // ---------- Host: status, tombol Play, slot ----------

    [UnityTest]
    public IEnumerator Host_AloneInTheLobby_SeesHowManyPlayersAreStillNeeded()
    {
        yield return BuildScreen();

        Assert.AreEqual($"Perlu total (1/{Required}) pemain untuk mulai", statusText.text);
        Assert.IsTrue(statusFrame.activeSelf);
        Assert.IsFalse(playButton.gameObject.activeSelf);
        Assert.IsFalse(readyButton.gameObject.activeSelf, "tombol Ready hanya untuk client");
    }

    [UnityTest]
    public IEnumerator Host_PlayerCountInStatusGrowsAsPlayersJoin()
    {
        yield return BuildScreen();

        ClientHello("A");

        Assert.AreEqual($"Perlu total (2/{Required}) pemain untuk mulai", statusText.text);
    }

    [UnityTest]
    public IEnumerator Host_FullLobbyButNotEveryoneReady_WaitsForThem()
    {
        yield return BuildScreen();
        for (int i = 0; i < Required - 1; i++) ClientHello("Client" + i);

        Assert.AreEqual("Menunggu semua pemain siap", statusText.text);
        Assert.IsFalse(playButton.gameObject.activeSelf);
        Assert.IsTrue(statusFrame.activeSelf);
    }

    [UnityTest]
    public IEnumerator Host_EveryoneReady_ShowsPlayButtonInsteadOfTheStatusFrame()
    {
        yield return BuildScreen();
        for (int i = 0; i < Required - 1; i++) ClientHello("Client" + i, ready: true);

        Assert.AreEqual("Semua sudah siap! Tekan Play untuk mulai.", statusText.text);
        Assert.IsTrue(playButton.gameObject.activeSelf);
        Assert.IsTrue(playButton.interactable);
        Assert.IsFalse(statusFrame.activeSelf, "posisinya dipakai tombol Play");
    }

    [UnityTest]
    public IEnumerator Host_PlayButtonDisappearsAgainWhenAPlayerLeaves()
    {
        yield return BuildScreen();
        for (int i = 0; i < Required - 1; i++) ClientHello("Client" + i, ready: true);
        Assert.IsTrue(playButton.gameObject.activeSelf, "syarat awal");

        LobbyTestHelpers.ConnectionChanged(service, "Client0", "Disconnected");

        Assert.IsFalse(playButton.gameObject.activeSelf);
        Assert.IsTrue(statusFrame.activeSelf);
    }

    [UnityTest]
    public IEnumerator Slots_ShowHostInTheMiddleAndOthersInArrivalOrder()
    {
        yield return BuildScreen();

        Assert.AreEqual($"{LobbyTestHelpers.LocalName(service)}\n(Host)", host.label.text);
        Assert.AreEqual("Pemain 2", left.label.text, "slot kosong kiri");
        Assert.AreEqual("Pemain 3", right.label.text, "slot kosong kanan");
        Assert.IsTrue(left.silhouette.activeSelf);
        Assert.IsTrue(right.silhouette.activeSelf);

        ClientHello("Budi");
        ClientHello("Sari");

        Assert.AreEqual("Budi", left.label.text);
        Assert.AreEqual("Sari", right.label.text);
        Assert.IsTrue(left.character.activeSelf);
        Assert.IsTrue(right.character.activeSelf);
    }

    [UnityTest]
    public IEnumerator Slots_ReadyIconFollowsTheReadyStateOfEachClient()
    {
        yield return BuildScreen();

        ClientHello("Budi", ready: true);
        ClientHello("Sari", ready: false);

        Assert.IsTrue(left.readyIcon.activeSelf);
        Assert.IsFalse(right.readyIcon.activeSelf);
        Assert.IsFalse(host.readyIcon.activeSelf);
    }

    [UnityTest]
    public IEnumerator Slots_HostSeesKickButtonsOnlyOnOtherPlayers()
    {
        yield return BuildScreen();
        ClientHello("Budi");

        Assert.IsTrue(left.kick.activeSelf);
        Assert.IsFalse(host.kick.activeSelf, "host tidak bisa kick diri sendiri");
    }

    [UnityTest]
    public IEnumerator Slots_ClientDoesNotSeeKickButtons()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello("Sari");
        ClientHello("Budi");

        Assert.IsFalse(left.kick.activeSelf);
        Assert.IsFalse(right.kick.activeSelf);
        Assert.IsFalse(host.kick.activeSelf);
    }

    [UnityTest]
    public IEnumerator Kick_ClickRemovesThatPlayerFromTheLobby()
    {
        yield return BuildScreen();
        ClientHello("Budi");
        ClientHello("Sari");

        left.kickButton.onClick.Invoke();

        Assert.IsFalse(service.Players.Exists(p => p.name == "Budi"));
        Assert.IsTrue(service.Players.Exists(p => p.name == "Sari"));
        Assert.AreEqual("Sari", left.label.text, "Sari naik ke slot pertama");
        Assert.AreEqual("Pemain 3", right.label.text);
    }

    [UnityTest]
    public IEnumerator ClientSlots_FollowTheHostOrderOnEveryDevice()
    {
        service.JoinRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);
        yield return BuildScreen();
        ClientHello("B");

        // Host bilang urutannya: kita dulu, lalu B (di device ini B datang lebih dulu).
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, slots: $"{me}|B");

        Assert.AreEqual(me, left.label.text);
        Assert.AreEqual("B", right.label.text);
    }

    // ---------- Client: tombol Ready ----------

    [UnityTest]
    public IEnumerator Client_BeforeHostIsFound_ReadyButtonIsShownButNotPressable()
    {
        service.JoinRoom("12345");

        yield return BuildScreen();

        Assert.IsTrue(readyButton.gameObject.activeSelf);
        Assert.IsFalse(readyButton.interactable);
        Assert.IsFalse(playButton.gameObject.activeSelf, "tombol Play hanya untuk host");
        Assert.AreSame(readySprite, readyButton.image.sprite);
    }

    [UnityTest]
    public IEnumerator Client_AfterHostIsFound_ReadyButtonBecomesPressable()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();

        HostHello();

        Assert.IsTrue(readyButton.interactable);
        Assert.IsTrue(readyButton.enabled);
        Assert.AreEqual("Siap", readyLabel.text);
        Assert.IsTrue(readyLabel.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Client_Ready_ReplacesTheButtonWithAWaitingMessage()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello();
        ClientHello("B", ready: false);

        readyButton.onClick.Invoke();

        Assert.IsTrue(service.IsLocalReady);
        Assert.IsFalse(readyButton.enabled, "tidak bisa ditekan lagi");
        Assert.IsFalse(readyLabel.gameObject.activeSelf, "tulisan 'Siap' disembunyikan");
        Assert.AreSame(waitingSprite, readyButton.image.sprite, "pemain lain belum semua ready");
        Assert.AreEqual(new Vector2(60f, 30f), ((RectTransform)readyButton.transform).sizeDelta,
            "ukuran = ukuran sprite waiting x waitingScale (1.5)");
        Assert.AreEqual("Menunggu semua pemain siap", statusText.text);
    }

    [UnityTest]
    public IEnumerator Client_Ready_WhenEveryoneElseIsReady_SaysWaitingForHost()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello();
        for (int i = 0; i < Required - 2; i++) ClientHello("Other" + i, ready: true);

        readyButton.onClick.Invoke();

        Assert.AreSame(waitingHostSprite, readyButton.image.sprite);
        Assert.AreEqual(new Vector2(60f, 30f), ((RectTransform)readyButton.transform).sizeDelta,
            "kedua pesan tunggu memakai ukuran yang sama");
        Assert.AreEqual("Menunggu host memulai game...", statusText.text);
    }

    [UnityTest]
    public IEnumerator Client_ReadyClickedTwice_StillOnlyReadiesOnce()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello();
        int changes = 0;
        service.OnPlayersChanged += () => changes++;

        readyButton.onClick.Invoke();
        int afterFirst = changes;
        readyButton.onClick.Invoke();

        Assert.AreEqual(afterFirst, changes, "klik kedua tidak mengirim apa-apa lagi");
        Assert.IsTrue(service.IsLocalReady);
    }

    [UnityTest]
    public IEnumerator Client_ReadyClickedBeforeHostIsFound_IsIgnored()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();

        readyButton.onClick.Invoke();

        Assert.IsFalse(service.IsLocalReady);
    }

    [UnityTest]
    public IEnumerator Client_AfterTheGameEnds_ReadyButtonIsBackToNormal()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello();
        readyButton.onClick.Invoke();
        Assert.IsFalse(readyButton.enabled, "syarat awal");

        service.SetInGame(true);
        service.SetInGame(false);

        Assert.IsFalse(service.IsLocalReady);
        Assert.IsTrue(readyButton.enabled);
        Assert.AreSame(readySprite, readyButton.image.sprite);
        Assert.AreEqual(new Vector2(100f, 50f), ((RectTransform)readyButton.transform).sizeDelta, "ukuran asli dikembalikan");
        Assert.IsTrue(readyLabel.gameObject.activeSelf);
    }

    // ---------- Tombol Play ----------

    [UnityTest]
    public IEnumerator PlayClick_WhenTheGameCannotStartYet_DoesNothing()
    {
        yield return BuildScreen();
        Assert.IsFalse(service.CanStart, "syarat awal");

        // Kalau tombol nekat memuat scene, SceneLoader mencatat error "[LOAD] ... tidak ditemukan" dan test gagal otomatis.
        playButton.onClick.Invoke();

        Assert.IsFalse(SceneLoader.IsLoading);
    }

    // ---------- Edit nama room (khusus host) ----------

    [UnityTest]
    public IEnumerator EditName_PencilShowsOnlyForTheHost()
    {
        yield return BuildScreen(withRoomNameEditing: true);
        Assert.IsTrue(editButton.gameObject.activeSelf, "host");
        Assert.AreEqual(LobbyService.MaxRoomNameLength, roomNameInput.characterLimit);
        Assert.IsFalse(roomNameInput.gameObject.activeSelf, "input tersembunyi sampai pensil ditekan");
    }

    [UnityTest]
    public IEnumerator EditName_PencilHiddenForAClient()
    {
        service.JoinRoom("12345");

        yield return BuildScreen(withRoomNameEditing: true);

        Assert.IsFalse(editButton.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator EditName_ClickingThePencilSwapsTheTitleForTheInput()
    {
        yield return BuildScreen(withRoomNameEditing: true);

        editButton.onClick.Invoke();

        Assert.IsTrue(roomNameInput.gameObject.activeSelf);
        Assert.IsFalse(roomNameText.gameObject.activeSelf, "judul disembunyikan selama mengetik");
        Assert.IsFalse(editButton.gameObject.activeSelf, "pensil juga");
    }

    [UnityTest]
    public IEnumerator EditName_FinishingTyping_SavesTheNameAndShowsTheTitleAgain()
    {
        yield return BuildScreen(withRoomNameEditing: true);
        editButton.onClick.Invoke();

        roomNameInput.onEndEdit.Invoke("  Warung Biru ");

        Assert.AreEqual("Warung Biru", service.RoomName);
        Assert.IsFalse(roomNameInput.gameObject.activeSelf);
        Assert.IsTrue(roomNameText.gameObject.activeSelf);
        Assert.AreEqual("Warung Biru", roomNameText.text);
        Assert.IsTrue(editButton.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator EditName_FinishingWithEmptyText_GoesBackToTheDefaultTitle()
    {
        yield return BuildScreen(withRoomNameEditing: true);
        service.SetRoomName("Warung Biru");
        editButton.onClick.Invoke();

        roomNameInput.onEndEdit.Invoke("");

        Assert.AreEqual("", service.RoomName);
        Assert.AreEqual($"Ruang {LobbyTestHelpers.LocalName(service)}", roomNameText.text);
    }

    [UnityTest]
    public IEnumerator EditName_PencilClickedByAClient_IsIgnored()
    {
        service.JoinRoom("12345");
        yield return BuildScreen(withRoomNameEditing: true);

        editButton.onClick.Invoke();

        Assert.IsFalse(roomNameInput.gameObject.activeSelf);
    }

    // ---------- Room ditutup ----------

    [UnityTest]
    public IEnumerator RoomClosed_ForAnyNormalReason_GoesBackToTheMainMenu()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        LogAssert.Expect(LogType.Error, new Regex(FakeMenuScene));

        LobbyTestHelpers.KickLocal(service);     // "Kamu dikeluarkan oleh host."

        Assert.AreEqual(LobbyMode.None, service.Mode);
    }

    [UnityTest]
    public IEnumerator RoomClosed_WhenHostLeft_ShowsAnAlertAndWaitsForOkBeforeLeaving()
    {
        service.JoinRoom("12345");
        yield return BuildScreen();
        HostHello("Sari");

        LobbyTestHelpers.ConnectionChanged(service, "Sari", "Disconnected");
        yield return null;
        yield return null;

        var okay = GameObject.Find("OkayButton");
        Assert.IsNotNull(okay, "alert 'host keluar' harus tampil");

        // Tanpa batas waktu: alert tetap ada dan belum pindah scene sebelum Oke ditekan.
        for (int i = 0; i < 5; i++) yield return null;
        Assert.IsTrue(okay != null, "alert tidak menutup sendiri");

        LogAssert.Expect(LogType.Error, new Regex(FakeMenuScene));
        okay.GetComponent<Button>().onClick.Invoke();
        yield return null;
        yield return null;
    }

    // ---------- Keluar dan bersih-bersih ----------

    [UnityTest]
    public IEnumerator LeaveRoom_ForTheBackButton_LeavesTheRoom()
    {
        yield return BuildScreen();
        Assert.AreEqual(LobbyMode.Host, service.Mode, "syarat awal");

        screen.LeaveRoom();

        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
    }

    [UnityTest]
    public IEnumerator Destroying_TheScreen_UnsubscribesFromTheService()
    {
        service.CreateRoom("12345");
        int playersBefore = LobbyTestHelpers.Subscribers(service, "OnPlayersChanged");
        int closedBefore = LobbyTestHelpers.Subscribers(service, "OnRoomClosed");
        yield return BuildScreen();
        Assert.AreEqual(playersBefore + 1, LobbyTestHelpers.Subscribers(service, "OnPlayersChanged"), "syarat awal");

        Object.Destroy(screen.gameObject);
        yield return null;

        Assert.AreEqual(playersBefore, LobbyTestHelpers.Subscribers(service, "OnPlayersChanged"));
        Assert.AreEqual(closedBefore, LobbyTestHelpers.Subscribers(service, "OnRoomClosed"));
    }
}
