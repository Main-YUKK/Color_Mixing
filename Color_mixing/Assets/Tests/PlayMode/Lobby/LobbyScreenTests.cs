// LobbyScreen memakai LobbyService jalur Editor (tanpa GameKit), jadi test ini hanya berlaku di Editor.
#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ColorMixing.Tests.Lobby
{
    public class LobbyScreenTests
    {
        private const string MissingScene = "SceneTidakAda_UntukTest";

        private ServiceHarness h;
        private GameObject root;
        private LobbyScreen screen;

        private TMP_Text roomNameText;
        private TMP_Text roomCodeText;
        private TMP_Text statusText;
        private SlotHarness leftSlot;
        private SlotHarness hostSlot;
        private SlotHarness rightSlot;
        private Button playButton;
        private Button readyButton;
        private TMP_Text readyLabel;
        private Button editButton;
        private TMP_InputField roomNameInput;
        private Sprite readySprite;
        private Sprite unreadySprite;

        private LobbyService Service => h.Service;

        [SetUp]
        public void SetUp()
        {
            h = new ServiceHarness(asInstance: true);

            // Root nonaktif sampai semua referensi terisi, sama seperti scene yang sudah di-wire.
            root = new GameObject("LobbyScreen");
            root.SetActive(false);
            screen = root.AddComponent<LobbyScreen>();

            roomNameText = UiFactory.Text("RoomName", root.transform);
            roomCodeText = UiFactory.Text("RoomCode", root.transform);
            statusText = UiFactory.Text("Status", root.transform);

            leftSlot = NewSlot();
            hostSlot = NewSlot();
            rightSlot = NewSlot();

            playButton = UiFactory.Button("PlayButton", root.transform);
            readyButton = UiFactory.Button("ReadyButton", root.transform);
            readyLabel = UiFactory.Text("ReadyLabel", readyButton.transform);
            editButton = UiFactory.Button("EditNameButton", root.transform);
            roomNameInput = UiFactory.Child("RoomNameInput", root.transform).AddComponent<TMP_InputField>();

            readySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            unreadySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);

            Reflect.SetField(screen, "roomNameText", roomNameText);
            Reflect.SetField(screen, "roomCodeText", roomCodeText);
            Reflect.SetField(screen, "statusText", statusText);
            Reflect.SetField(screen, "leftSlot", leftSlot.View);
            Reflect.SetField(screen, "hostSlot", hostSlot.View);
            Reflect.SetField(screen, "rightSlot", rightSlot.View);
            Reflect.SetField(screen, "playButton", playButton);
            Reflect.SetField(screen, "readyButton", readyButton);
            Reflect.SetField(screen, "readyButtonLabel", readyLabel);
            Reflect.SetField(screen, "readySprite", readySprite);
            Reflect.SetField(screen, "unreadySprite", unreadySprite);
            Reflect.SetField(screen, "editRoomNameButton", editButton);
            Reflect.SetField(screen, "roomNameInput", roomNameInput);
            Reflect.SetField(screen, "mainMenuSceneName", MissingScene);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(readySprite);
            Object.DestroyImmediate(unreadySprite);
            h.Dispose();
        }

        private SlotHarness NewSlot()
        {
            var slot = new SlotHarness(root.transform);
            slot.Root.SetActive(true);   // Awake tetap menunggu root aktif
            return slot;
        }

        // Mengaktifkan layar dan menunggu Start() berjalan.
        private IEnumerator OpenScreen()
        {
            root.SetActive(true);
            yield return null;
        }

        private IEnumerator OpenAsHost(params string[] clients)
        {
            h.HostWithClients(clients);
            yield return OpenScreen();
        }

        private IEnumerator OpenAsClientWithHost()
        {
            h.ClientWithHost();
            yield return OpenScreen();
        }

        // ---------- Masuk ke lobby ----------

        [UnityTest]
        public IEnumerator Start_WithoutRoom_CreatesRoomAsHost()
        {
            yield return OpenScreen();

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
            Assert.That(roomCodeText.text, Is.EqualTo($"Room Code: {Service.RoomCode}"));
        }

        [UnityTest]
        public IEnumerator Start_AsClient_KeepsClientRoom()
        {
            Service.JoinRoom("12345");

            yield return OpenScreen();

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
            Assert.That(roomCodeText.text, Is.EqualTo("Room Code: 12345"));
        }

        [UnityTest]
        public IEnumerator Start_SetsRoomNameInputLimitAndHidesIt()
        {
            yield return OpenAsHost();

            Assert.That(roomNameInput.characterLimit, Is.EqualTo(LobbyService.MaxRoomNameLength));
            Assert.That(roomNameInput.gameObject.activeSelf, Is.False);
        }

        // ---------- Tombol per role ----------

        [UnityTest]
        public IEnumerator Host_SeesPlayAndEdit_NotReady()
        {
            yield return OpenAsHost();

            Assert.That(playButton.gameObject.activeSelf, Is.True);
            Assert.That(editButton.gameObject.activeSelf, Is.True);
            Assert.That(readyButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Client_SeesReady_NotPlayOrEdit()
        {
            yield return OpenAsClientWithHost();

            Assert.That(readyButton.gameObject.activeSelf, Is.True);
            Assert.That(playButton.gameObject.activeSelf, Is.False);
            Assert.That(editButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayButton_EnabledOnlyWhenEveryoneReady()
        {
            yield return OpenAsHost();
            Assert.That(playButton.interactable, Is.False);

            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: false));
            Assert.That(playButton.interactable, Is.False);

            h.Receive(ServiceHarness.ClientMessage(Names.Budi, "READY", ready: true));
            Assert.That(playButton.interactable, Is.True);
        }

        // ---------- Nama room ----------

        [UnityTest]
        public IEnumerator RoomName_ClientBeforeHost_ShowsJoining()
        {
            Service.JoinRoom("12345");

            yield return OpenScreen();

            Assert.That(roomNameText.text, Is.EqualTo("Joining..."));
        }

        [UnityTest]
        public IEnumerator RoomName_Default_UsesHostName()
        {
            yield return OpenAsClientWithHost();

            Assert.That(roomNameText.text, Is.EqualTo($"{Names.Host}'s Room"));
        }

        [UnityTest]
        public IEnumerator RoomName_LongHostName_IsShortened()
        {
            Service.JoinRoom("12345");
            h.Receive(ServiceHarness.HostMessage("ABCDEFGHIJKLMNOPQRST"));

            yield return OpenScreen();

            Assert.That(roomNameText.text, Is.EqualTo("ABCDEFGHIJKLMN…'s Room"));
        }

        [UnityTest]
        public IEnumerator RoomName_FromHost_UpdatesClientLive()
        {
            yield return OpenAsClientWithHost();

            h.Receive(ServiceHarness.HostMessage(type: "ROOM", roomName: "Room Host"));

            Assert.That(roomNameText.text, Is.EqualTo("Room Host"));
        }

        // ---------- Edit nama room ----------

        [UnityTest]
        public IEnumerator EditClick_AsHost_OpensInputWithCurrentName()
        {
            Service.CreateRoom("54321", "Room Lama");
            yield return OpenScreen();

            editButton.onClick.Invoke();

            Assert.That(roomNameInput.gameObject.activeSelf, Is.True);
            Assert.That(roomNameInput.text, Is.EqualTo("Room Lama"));
            Assert.That(roomNameText.gameObject.activeSelf, Is.False);
            Assert.That(editButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EditEnd_SavesNameAndClosesInput()
        {
            yield return OpenAsHost();
            editButton.onClick.Invoke();

            roomNameInput.onEndEdit.Invoke("Room Baru");

            Assert.That(Service.RoomName, Is.EqualTo("Room Baru"));
            Assert.That(roomNameText.text, Is.EqualTo("Room Baru"));
            Assert.That(roomNameText.gameObject.activeSelf, Is.True);
            Assert.That(roomNameInput.gameObject.activeSelf, Is.False);
            Assert.That(editButton.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator EditEnd_Empty_ReturnsToDefaultName()
        {
            Service.CreateRoom("54321", "Room Lama");
            yield return OpenScreen();
            editButton.onClick.Invoke();

            roomNameInput.onEndEdit.Invoke("");

            Assert.That(roomNameText.text, Is.EqualTo($"{h.Local.name}'s Room"));
        }

        [UnityTest]
        public IEnumerator EditClick_AsClient_DoesNothing()
        {
            yield return OpenAsClientWithHost();

            editButton.onClick.Invoke();

            Assert.That(roomNameInput.gameObject.activeSelf, Is.False);
        }

        // ---------- Teks status ----------

        [UnityTest]
        public IEnumerator Status_NotEnoughPlayers_ShowsCount()
        {
            yield return OpenAsHost(Names.Ani);

            Assert.That(statusText.text, Is.EqualTo("Must have a total of 3 players to start the game (2/3)"));
        }

        [UnityTest]
        public IEnumerator Status_FullButNotReady_ShowsWaiting()
        {
            yield return OpenAsHost(Names.Ani, Names.Budi);

            Assert.That(statusText.text, Is.EqualTo("Waiting for all players to be ready"));
        }

        [UnityTest]
        public IEnumerator Status_AllReady_OnHost_PromptsPlay()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: true));

            yield return OpenScreen();

            Assert.That(statusText.text, Is.EqualTo("Everyone is ready! Press Play to start."));
        }

        [UnityTest]
        public IEnumerator Status_AllReady_OnClient_WaitsForHost()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            Service.SetReady(true);

            yield return OpenScreen();

            Assert.That(statusText.text, Is.EqualTo("Waiting for the host to start the game..."));
        }

        // ---------- Tombol ready ----------

        [UnityTest]
        public IEnumerator ReadyButton_BeforeHostHeard_IsDisabledAndClickDoesNothing()
        {
            Service.JoinRoom("12345");
            yield return OpenScreen();

            Assert.That(readyButton.interactable, Is.False);
            readyButton.onClick.Invoke();

            Assert.That(Service.IsLocalReady, Is.False);
        }

        [UnityTest]
        public IEnumerator ReadyButton_BecomesEnabledWhenHostHeard()
        {
            Service.JoinRoom("12345");
            yield return OpenScreen();

            h.Receive(ServiceHarness.HostMessage());

            Assert.That(readyButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator ReadyButton_NotReady_ShowsReadyLookAndLabel()
        {
            yield return OpenAsClientWithHost();

            Assert.That(readyLabel.text, Is.EqualTo("Ready"));
            Assert.That(readyButton.image.sprite, Is.SameAs(readySprite));
        }

        [UnityTest]
        public IEnumerator ReadyButton_Click_SetsReadyAndChangesLook()
        {
            yield return OpenAsClientWithHost();

            readyButton.onClick.Invoke();

            Assert.That(Service.IsLocalReady, Is.True);
            Assert.That(readyLabel.text, Is.EqualTo("Cancel Ready"));
            Assert.That(readyButton.image.sprite, Is.SameAs(unreadySprite));
        }

        [UnityTest]
        public IEnumerator ReadyButton_SecondClick_CancelsReady()
        {
            yield return OpenAsClientWithHost();
            readyButton.onClick.Invoke();

            readyButton.onClick.Invoke();

            Assert.That(Service.IsLocalReady, Is.False);
            Assert.That(readyLabel.text, Is.EqualTo("Ready"));
            Assert.That(readyButton.image.sprite, Is.SameAs(readySprite));
        }

        // ---------- Slot pemain ----------

        [UnityTest]
        public IEnumerator Slots_OnHost_PlaceHostInMiddleAndClientsInArrivalOrder()
        {
            yield return OpenAsHost(Names.Ani, Names.Budi);

            Assert.That(hostSlot.NameLabel.text, Is.EqualTo($"{h.Local.name} (host)"));
            Assert.That(leftSlot.NameLabel.text, Is.EqualTo(Names.Ani));
            Assert.That(rightSlot.NameLabel.text, Is.EqualTo(Names.Budi));
        }

        [UnityTest]
        public IEnumerator Slots_WithoutClients_AreEmpty()
        {
            yield return OpenAsHost();

            Assert.That(leftSlot.NameLabel.text, Is.EqualTo("[player]"));
            Assert.That(rightSlot.NameLabel.text, Is.EqualTo("[player]"));
        }

        [UnityTest]
        public IEnumerator Slots_OnClient_FollowOrderFromHost()
        {
            Service.JoinRoom("12345");
            string localName = Service.Players[0].name;
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));
            h.Receive(ServiceHarness.HostMessage(slots: $"{Names.Ani}|{localName}"));

            yield return OpenScreen();

            Assert.That(hostSlot.NameLabel.text, Is.EqualTo($"{Names.Host} (host)"));
            Assert.That(leftSlot.NameLabel.text, Is.EqualTo(Names.Ani));
            Assert.That(rightSlot.NameLabel.text, Is.EqualTo(localName));
        }

        [UnityTest]
        public IEnumerator Slots_UpdateWhenPlayerJoinsLive()
        {
            yield return OpenAsHost();

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(leftSlot.NameLabel.text, Is.EqualTo(Names.Ani));
        }

        [UnityTest]
        public IEnumerator Slots_ShowReadyIconWhenClientReady()
        {
            yield return OpenAsHost(Names.Ani);
            Assert.That(leftSlot.ReadyIcon.activeSelf, Is.False);

            h.Receive(ServiceHarness.ClientMessage(Names.Ani, "READY", ready: true));

            Assert.That(leftSlot.ReadyIcon.activeSelf, Is.True);
        }

        // ---------- Kick ----------

        [UnityTest]
        public IEnumerator Kick_OnlyVisibleToHostForOtherPlayers()
        {
            yield return OpenAsHost(Names.Ani);

            Assert.That(leftSlot.KickObject.activeSelf, Is.True);
            Assert.That(hostSlot.KickObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Kick_NotVisibleToClient()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            yield return OpenScreen();

            Assert.That(hostSlot.KickObject.activeSelf, Is.False);
            Assert.That(leftSlot.KickObject.activeSelf, Is.False);
            Assert.That(rightSlot.KickObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator KickClick_RemovesPlayerAndShiftsSlots()
        {
            yield return OpenAsHost(Names.Ani, Names.Budi);

            leftSlot.KickButton.onClick.Invoke();

            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(leftSlot.NameLabel.text, Is.EqualTo(Names.Budi));
            Assert.That(rightSlot.NameLabel.text, Is.EqualTo("[player]"));
        }

        // ---------- Keluar dari room ----------

        [UnityTest]
        public IEnumerator LeaveRoom_LeavesServiceRoom()
        {
            yield return OpenAsHost(Names.Ani);

            screen.LeaveRoom();

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.None));
            Assert.That(Service.Players, Is.Empty);
        }

        // Scene tujuan sengaja tidak ada, jadi LoadScene hanya mencatat error dan test tetap di scene ini.
        // Error kedua (LoadScene dua kali) akan membuat test gagal karena hanya satu yang diharapkan.
        [UnityTest]
        public IEnumerator RoomClosed_LoadsMainMenuOnlyOnce()
        {
            yield return OpenAsClientWithHost();
            LogAssert.Expect(LogType.Error, new Regex(MissingScene));

            h.Receive(ServiceHarness.HostMessage(type: "KICK", target: h.Local.name));
            Reflect.Invoke(Service, "Fail", "Penutupan kedua");

            yield return null;
        }

        // ---------- Berlangganan event ----------

        [UnityTest]
        public IEnumerator Destroy_UnsubscribesFromService()
        {
            yield return OpenAsHost();
            Assume.That(Reflect.GetField(Service, nameof(LobbyService.OnPlayersChanged)), Is.Not.Null);

            Object.DestroyImmediate(root);

            Assert.That(Reflect.GetField(Service, nameof(LobbyService.OnPlayersChanged)), Is.Null);
            Assert.That(Reflect.GetField(Service, nameof(LobbyService.OnRoomClosed)), Is.Null);
        }
    }
}
#endif
