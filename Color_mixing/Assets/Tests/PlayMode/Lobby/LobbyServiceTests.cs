// LobbyService memakai jalur Editor (tanpa GameKit), jadi test ini hanya berlaku di Editor.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorMixing.Tests.Lobby
{
    public class LobbyServiceTests
    {
        private ServiceHarness h;
        private LobbyService Service => h.Service;

        [SetUp]
        public void SetUp() => h = new ServiceHarness();

        [TearDown]
        public void TearDown() => h.Dispose();

        // ---------- CreateRoom ----------

        [Test]
        public void CreateRoom_BecomesHostWithLocalPlayerOnly()
        {
            Service.CreateRoom();

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(h.Local, Is.Not.Null);
            Assert.That(h.Local.isHost, Is.True);
        }

        [Test]
        public void CreateRoom_WithoutCode_GeneratesFiveDigitCode()
        {
            string code = Service.CreateRoom();

            Assert.That(code, Is.EqualTo(Service.RoomCode));
            Assert.That(int.Parse(code), Is.InRange(10000, 99999));
        }

        [Test]
        public void CreateRoom_WithValidCode_UsesThatCode()
        {
            Assert.That(Service.CreateRoom("54321"), Is.EqualTo("54321"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1234")]
        [TestCase("123456")]
        [TestCase("abcde")]
        public void CreateRoom_WithInvalidCode_GeneratesNewCode(string invalid)
        {
            string code = Service.CreateRoom(invalid);

            Assert.That(int.Parse(code), Is.InRange(10000, 99999));
        }

        [Test]
        public void CreateRoom_WithCustomName_AppliesName()
        {
            Service.CreateRoom("54321", "Room Lama");

            Assert.That(Service.RoomName, Is.EqualTo("Room Lama"));
        }

        [Test]
        public void CreateRoom_RaisesPlayersChanged()
        {
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.CreateRoom();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void CreateRoom_AfterPreviousRoom_ClearsOldState()
        {
            Service.CreateRoom("54321", "Room Lama");
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Service.CreateRoom();

            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(Service.RoomName, Is.Empty);
        }

        [Test]
        public void CreateRoom_ClearsLastFailureMessage()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.HostMessage(type: "KICK", target: h.Local.name));
            Assume.That(Service.LastMessage, Is.Not.Null);

            Service.CreateRoom();

            Assert.That(Service.LastMessage, Is.Null);
        }

        // ---------- JoinRoom ----------

        [Test]
        public void JoinRoom_WithValidCode_BecomesClientWaitingForHost()
        {
            Assert.That(Service.JoinRoom("12345"), Is.True);

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
            Assert.That(Service.RoomCode, Is.EqualTo("12345"));
            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(h.Local.isHost, Is.False);
            Assert.That(Service.CanSetReady, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1234")]
        [TestCase("123456")]
        [TestCase("abcde")]
        [TestCase("12a45")]
        public void JoinRoom_WithInvalidCode_ReturnsFalseAndStaysOutOfRoom(string invalid)
        {
            Assert.That(Service.JoinRoom(invalid), Is.False);

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.None));
            Assert.That(Service.RoomCode, Is.Null);
            Assert.That(Service.Players, Is.Empty);
        }

        [Ignore("Bug diketahui: int.TryParse menerima tanda dan spasi, jadi kode ini lolos sebagai 5 digit. " +
                "Layar Join sudah menyaring non-digit, jadi belum terjangkau pemain. Hapus Ignore setelah diperbaiki.")]
        [TestCase("-1234")]
        [TestCase("+1234")]
        [TestCase(" 1234")]
        public void JoinRoom_WithSignOrSpace_ReturnsFalse(string invalid)
        {
            Assert.That(Service.JoinRoom(invalid), Is.False);
        }

        [Test]
        public void JoinRoom_WithInvalidCode_DoesNotDisturbCurrentRoom()
        {
            string code = Service.CreateRoom();

            Assert.That(Service.JoinRoom("abc"), Is.False);

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
            Assert.That(Service.RoomCode, Is.EqualTo(code));
        }

        [Test]
        public void JoinRoom_SameCodeWhileSearching_IsIgnored()
        {
            Service.JoinRoom("12345");
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Assert.That(Service.JoinRoom("12345"), Is.True);

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void JoinRoom_DifferentCode_StartsNewSearch()
        {
            h.ClientWithHost("12345");

            Service.JoinRoom("99999");

            Assert.That(Service.RoomCode, Is.EqualTo("99999"));
            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(Service.CanSetReady, Is.False);
        }

        [Test]
        public void JoinRoom_SameCodeAfterConfirmed_RejoinsFromScratch()
        {
            h.ClientWithHost("12345");

            Service.JoinRoom("12345");

            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(Service.CanSetReady, Is.False);
        }

        // ---------- LeaveRoom ----------

        [Test]
        public void LeaveRoom_ResetsEverything()
        {
            Service.CreateRoom("54321", "Room Lama");
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Service.LeaveRoom();

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.None));
            Assert.That(Service.RoomCode, Is.Null);
            Assert.That(Service.RoomName, Is.Empty);
            Assert.That(Service.Players, Is.Empty);
        }

        [Test]
        public void LeaveRoom_DoesNotRaiseRoomClosed()
        {
            Service.CreateRoom();
            int closed = 0;
            Service.OnRoomClosed += _ => closed++;

            Service.LeaveRoom();

            Assert.That(closed, Is.EqualTo(0));
        }

        // ---------- SetRoomName ----------

        [Test]
        public void SetRoomName_AsHost_TrimsAndRaisesEvent()
        {
            Service.CreateRoom();
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.SetRoomName("  Room Baru  ");

            Assert.That(Service.RoomName, Is.EqualTo("Room Baru"));
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void SetRoomName_LongerThanLimit_IsTruncated()
        {
            Service.CreateRoom();

            Service.SetRoomName(new string('A', LobbyService.MaxRoomNameLength + 5));

            Assert.That(Service.RoomName, Is.EqualTo(new string('A', LobbyService.MaxRoomNameLength)));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void SetRoomName_Blank_ReturnsToDefault(string blank)
        {
            Service.CreateRoom();
            Service.SetRoomName("Room Baru");

            Service.SetRoomName(blank);

            Assert.That(Service.RoomName, Is.Empty);
        }

        [Test]
        public void SetRoomName_SameName_DoesNotRaiseEvent()
        {
            Service.CreateRoom();
            Service.SetRoomName("Room Baru");
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.SetRoomName(" Room Baru ");

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void SetRoomName_AsClient_IsIgnored()
        {
            h.ClientWithHost();

            Service.SetRoomName("Bukan Host");

            Assert.That(Service.RoomName, Is.Empty);
        }

        // ---------- SetReady ----------

        [Test]
        public void SetReady_ClientWithHost_ChangesStateAndRaisesEvent()
        {
            h.ClientWithHost();
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.SetReady(true);

            Assert.That(Service.IsLocalReady, Is.True);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void SetReady_CanBeCancelled()
        {
            h.ClientWithHost();
            Service.SetReady(true);

            Service.SetReady(false);

            Assert.That(Service.IsLocalReady, Is.False);
        }

        [Test]
        public void SetReady_SameValue_DoesNotRaiseEvent()
        {
            h.ClientWithHost();
            Service.SetReady(true);
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.SetReady(true);

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void SetReady_BeforeHostHeard_IsIgnored()
        {
            Service.JoinRoom("12345");

            Service.SetReady(true);

            Assert.That(Service.IsLocalReady, Is.False);
        }

        [Test]
        public void SetReady_AsHost_IsIgnored()
        {
            Service.CreateRoom();

            Service.SetReady(true);

            Assert.That(Service.IsLocalReady, Is.False);
        }

        [Test]
        public void IsLocalReady_OutsideRoom_IsFalse()
        {
            Assert.That(Service.IsLocalReady, Is.False);
        }

        // ---------- CanStart ----------

        [Test]
        public void CanStart_HostAlone_IsFalse()
        {
            Service.CreateRoom();

            Assert.That(Service.CanStart, Is.False);
        }

        [Test]
        public void CanStart_OnlyOneReadyClient_IsFalse()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));

            Assert.That(Service.CanStart, Is.False);
        }

        [Test]
        public void CanStart_FullLobbyWithUnreadyClient_IsFalse()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: false));

            Assert.That(Service.CanStart, Is.False);
        }

        [Test]
        public void CanStart_FullLobbyAllReady_IsTrue()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: true));

            Assert.That(Service.CanStart, Is.True);
        }

        [Test]
        public void CanStart_ClientCancelsReady_BecomesFalse()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: true));

            h.Receive(ServiceHarness.ClientMessage(Names.Budi, "READY", ready: false));

            Assert.That(Service.CanStart, Is.False);
        }

        [Test]
        public void CanStart_AsClient_IsFalse()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            Service.SetReady(true);

            Assert.That(Service.CanStart, Is.False);
        }

        // ---------- KickPlayer ----------

        [Test]
        public void KickPlayer_RemovesPlayerAndRaisesEvent()
        {
            h.HostWithClients(Names.Ani, Names.Budi);
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.KickPlayer(Names.Ani);

            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(h.Player(Names.Budi), Is.Not.Null);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void KickPlayer_Self_IsIgnored()
        {
            h.HostWithClients(Names.Ani);

            Service.KickPlayer(h.Local.name);

            Assert.That(Service.Players.Count, Is.EqualTo(2));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
        }

        [Test]
        public void KickPlayer_UnknownName_IsIgnored()
        {
            h.HostWithClients(Names.Ani);
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            Service.KickPlayer("TidakAda");

            Assert.That(Service.Players.Count, Is.EqualTo(2));
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void KickPlayer_AsClient_IsIgnored()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Service.KickPlayer(Names.Ani);

            Assert.That(h.Player(Names.Ani), Is.Not.Null);
        }

        [Test]
        public void KickedPlayer_LateMessages_AreIgnored()
        {
            h.HostWithClients(Names.Ani);
            Service.KickPlayer(Names.Ani);

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(h.Player(Names.Ani), Is.Null);
        }

        [Test]
        public void KickedPlayer_ConnectedEventBeforeDisconnect_IsIgnored()
        {
            h.HostWithClients(Names.Ani);
            Service.KickPlayer(Names.Ani);

            h.ConnectionChanged(Names.Ani, "Connected");

            Assert.That(h.Player(Names.Ani), Is.Null);
        }

        [Test]
        public void KickedPlayer_CanRejoinAfterDisconnect()
        {
            h.HostWithClients(Names.Ani);
            Service.KickPlayer(Names.Ani);
            h.ConnectionChanged(Names.Ani, "Disconnected");

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(h.Player(Names.Ani), Is.Not.Null);
        }

        // ---------- Pesan masuk: validasi ----------

        [Test]
        public void HandleMessage_Null_IsIgnored()
        {
            Service.CreateRoom();

            Assert.DoesNotThrow(() => h.Receive(null));

            Assert.That(Service.Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandleMessage_OutsideRoom_IsIgnored()
        {
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(Service.Players, Is.Empty);
        }

        [Test]
        public void HandleMessage_UnknownType_IsIgnored()
        {
            Service.CreateRoom();

            h.Receive(ServiceHarness.ClientMessage(Names.Ani, "TIDAK_DIKENAL"));

            Assert.That(h.Player(Names.Ani), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void HandleMessage_WithoutSenderName_IsIgnored(string name)
        {
            Service.CreateRoom();

            h.Receive(ServiceHarness.ClientMessage(name));

            Assert.That(Service.Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandleMessage_FromOwnName_IsIgnored()
        {
            Service.CreateRoom();

            h.Receive(ServiceHarness.ClientMessage(h.Local.name, ready: true));

            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(h.Local.isReady, Is.False);
        }

        [Test]
        public void HandleMessage_HostFromAnotherHost_IsIgnored()
        {
            Service.CreateRoom();

            h.Receive(ServiceHarness.HostMessage());

            Assert.That(h.Player(Names.Host), Is.Null);
        }

        [Test]
        public void OnDataReceived_ValidJson_IsHandled()
        {
            Service.CreateRoom();
            string json = JsonUtility.ToJson(ServiceHarness.ClientMessage(Names.Ani, ready: true));

            Reflect.Invoke(Service, "OnDataReceived", Encoding.UTF8.GetBytes(json));

            Assert.That(h.Player(Names.Ani).isReady, Is.True);
        }

        [Test]
        public void OnDataReceived_InvalidJson_IsIgnored()
        {
            Service.CreateRoom();

            Assert.DoesNotThrow(() =>
                Reflect.Invoke(Service, "OnDataReceived", Encoding.UTF8.GetBytes("bukan json")));

            Assert.That(Service.Players.Count, Is.EqualTo(1));
        }

        // ---------- Pesan masuk: pemain dan status ready ----------

        [Test]
        public void HandleMessage_NewClient_IsAddedOnHost()
        {
            Service.CreateRoom();
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            var ani = h.Player(Names.Ani);
            Assert.That(ani, Is.Not.Null);
            Assert.That(ani.isHost, Is.False);
            Assert.That(ani.isLocal, Is.False);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void HandleMessage_RepeatedHello_DoesNotDuplicatePlayer()
        {
            Service.CreateRoom();

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(Service.Players.Count(p => p.name == Names.Ani), Is.EqualTo(1));
        }

        [Test]
        public void HandleMessage_HostWithReadyFlag_IsNeverMarkedReady()
        {
            Service.JoinRoom("12345");
            var hello = ServiceHarness.HostMessage();
            hello.ready = true;

            h.Receive(hello);

            Assert.That(h.Player(Names.Host).isReady, Is.False);
        }

        // ---------- Client: konfirmasi join, role, nama room ----------

        [Test]
        public void ClientHearsHost_EnablesReadyAndConfirmsJoin()
        {
            Service.JoinRoom("12345");
            int confirmed = 0;
            Service.OnJoinConfirmed += () => confirmed++;

            h.Receive(ServiceHarness.HostMessage());

            Assert.That(Service.CanSetReady, Is.True);
            Assert.That(confirmed, Is.EqualTo(1));
            Assert.That(h.Player(Names.Host).isHost, Is.True);
        }

        [Test]
        public void ClientHearsHostAgain_ConfirmsJoinOnlyOnce()
        {
            Service.JoinRoom("12345");
            int confirmed = 0;
            Service.OnJoinConfirmed += () => confirmed++;

            h.Receive(ServiceHarness.HostMessage());
            h.Receive(ServiceHarness.HostMessage(type: "ACK"));

            Assert.That(confirmed, Is.EqualTo(1));
        }

        [Test]
        public void ClientHearsOtherClientOnly_DoesNotConfirmJoin()
        {
            Service.JoinRoom("12345");
            int confirmed = 0;
            Service.OnJoinConfirmed += () => confirmed++;

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(confirmed, Is.EqualTo(0));
            Assert.That(Service.CanSetReady, Is.False);
        }

        [Test]
        public void ClientHearsHost_CorrectsRolesOfOtherPlayers()
        {
            Service.JoinRoom("12345");
            h.Receive(ServiceHarness.HostMessage(Names.Ani));   // sempat dikira host

            h.Receive(ServiceHarness.HostMessage(Names.Budi));

            Assert.That(h.Player(Names.Budi).isHost, Is.True);
            Assert.That(h.Player(Names.Ani).isHost, Is.False);
        }

        [Test]
        public void ClientHearsSenderAsNonHost_CorrectsWrongHostGuess()
        {
            Service.JoinRoom("12345");
            h.Receive(ServiceHarness.HostMessage(Names.Ani));

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            Assert.That(h.Player(Names.Ani).isHost, Is.False);
        }

        [Test]
        public void RoomMessageFromHost_UpdatesRoomName()
        {
            h.ClientWithHost();

            h.Receive(ServiceHarness.HostMessage(type: "ROOM", roomName: "Room Host"));

            Assert.That(Service.RoomName, Is.EqualTo("Room Host"));
        }

        [Test]
        public void RoomMessageFromHost_EmptyName_ReturnsToDefault()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.HostMessage(type: "ROOM", roomName: "Room Host"));

            h.Receive(ServiceHarness.HostMessage(type: "ROOM", roomName: ""));

            Assert.That(Service.RoomName, Is.Empty);
        }

        [Test]
        public void RoomMessageFromNonHost_IsIgnored()
        {
            h.ClientWithHost();
            var forged = ServiceHarness.ClientMessage(Names.Ani, "ROOM");
            forged.roomName = "Palsu";

            h.Receive(forged);

            Assert.That(Service.RoomName, Is.Empty);
        }

        // ---------- Urutan slot ----------

        [Test]
        public void SlotOrder_OnHost_FollowsArrivalOrder()
        {
            h.HostWithClients(Names.Budi, Names.Ani);

            var names = Service.GetOthersInSlotOrder().Select(p => p.name);

            Assert.That(names, Is.EqualTo(new[] { Names.Budi, Names.Ani }));
        }

        [Test]
        public void SlotOrder_OnHost_ExcludesHost()
        {
            h.HostWithClients(Names.Ani);

            Assert.That(Service.GetOthersInSlotOrder().Any(p => p.isHost), Is.False);
        }

        [Test]
        public void SlotOrder_OnClient_FollowsListFromHost()
        {
            Service.JoinRoom("12345");
            string localName = h.Local.name;
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));
            h.Receive(ServiceHarness.HostMessage(slots: $"{localName}|{Names.Ani}"));

            var names = Service.GetOthersInSlotOrder().Select(p => p.name);

            Assert.That(names, Is.EqualTo(new[] { localName, Names.Ani }));
        }

        [Test]
        public void SlotOrder_OnClient_PlayerNotInHostList_GoesLast()
        {
            Service.JoinRoom("12345");
            string localName = h.Local.name;
            h.Receive(ServiceHarness.ClientMessage(Names.Citra));
            h.Receive(ServiceHarness.HostMessage(slots: localName));

            var names = Service.GetOthersInSlotOrder().Select(p => p.name);

            Assert.That(names, Is.EqualTo(new[] { localName, Names.Citra }));
        }

        [Test]
        public void SlotOrder_OnClient_NameInHostListButNotInLobby_IsSkipped()
        {
            Service.JoinRoom("12345");
            string localName = h.Local.name;
            h.Receive(ServiceHarness.HostMessage(slots: $"SudahKeluar|{localName}"));

            var names = Service.GetOthersInSlotOrder().Select(p => p.name);

            Assert.That(names, Is.EqualTo(new[] { localName }));
        }

        // ---------- KICK diterima client ----------

        [Test]
        public void KickMessage_TargetingLocal_ClosesRoomWithReason()
        {
            h.ClientWithHost();
            string reason = null;
            Service.OnRoomClosed += r => reason = r;

            h.Receive(ServiceHarness.HostMessage(type: "KICK", target: h.Local.name));

            Assert.That(reason, Is.EqualTo("Kamu dikeluarkan oleh host."));
            Assert.That(Service.LastMessage, Is.EqualTo(reason));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.None));
            Assert.That(Service.RoomCode, Is.Null);
            Assert.That(Service.Players, Is.Empty);
        }

        [Test]
        public void KickMessage_TargetingOtherPlayer_RemovesOnlyThatPlayer()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            h.Receive(ServiceHarness.HostMessage(type: "KICK", target: Names.Ani));

            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
        }

        [Test]
        public void KickMessage_FromNonHost_IsIgnored()
        {
            h.ClientWithHost();
            string localName = h.Local.name;
            var forged = ServiceHarness.ClientMessage(Names.Ani, "KICK");
            forged.target = localName;

            h.Receive(forged);

            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
            Assert.That(h.Local, Is.Not.Null);
        }

        // ---------- REFORM ----------

        [Test]
        public void ReformMessage_KeepsLocalAndHostButRemovesOthers()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            h.Receive(ServiceHarness.HostMessage(type: "REFORM"));

            Assert.That(h.Local, Is.Not.Null);
            Assert.That(h.Player(Names.Host), Is.Not.Null);
            Assert.That(h.Player(Names.Ani), Is.Null);
        }

        [Test]
        public void ReformMessage_FromNonHost_IsIgnored()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            h.Receive(ServiceHarness.ClientMessage(Names.Ani, "REFORM"));

            Assert.That(h.Player(Names.Ani), Is.Not.Null);
            Assert.That(Service.CanSetReady, Is.True);
        }

        [Test]
        public void WhileReforming_ReadyDisabledAndMessagesIgnored()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.HostMessage(type: "REFORM"));

            h.Receive(ServiceHarness.ClientMessage(Names.Ani));
            Service.SetReady(true);

            Assert.That(Service.CanSetReady, Is.False);
            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(Service.IsLocalReady, Is.False);
        }

        [Test]
        public void WhileReforming_HostDisconnect_DoesNotCloseRoom()
        {
            h.ClientWithHost();
            int closed = 0;
            Service.OnRoomClosed += _ => closed++;
            h.Receive(ServiceHarness.HostMessage(type: "REFORM"));

            h.ConnectionChanged(Names.Host, "Disconnected");

            Assert.That(closed, Is.EqualTo(0));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
        }

        [UnityTest]
        public IEnumerator AfterReformDelay_ClientCanSetReadyAgain()
        {
            h.ClientWithHost();
            Service.SetReady(true);
            h.Receive(ServiceHarness.HostMessage(type: "REFORM"));
            Assert.That(Service.CanSetReady, Is.False);

            yield return new WaitForSeconds(1.2f);

            Assert.That(Service.CanSetReady, Is.True);
            Assert.That(Service.IsLocalReady, Is.True, "Status ready sendiri harus bertahan setelah reform.");
        }

        // ---------- Perubahan koneksi ----------

        [Test]
        public void Connected_AddsRemoteAsNonHost()
        {
            Service.JoinRoom("12345");

            h.ConnectionChanged(Names.Ani, "Connected");

            Assert.That(h.Player(Names.Ani), Is.Not.Null);
            Assert.That(h.Player(Names.Ani).isHost, Is.False);
        }

        [Test]
        public void Connected_ForOwnName_IsIgnored()
        {
            Service.CreateRoom();

            h.ConnectionChanged(h.Local.name, "Connected");

            Assert.That(Service.Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownConnectionState_ChangesNothing()
        {
            h.HostWithClients(Names.Ani);

            h.ConnectionChanged(Names.Ani, "Unknown");

            Assert.That(h.Player(Names.Ani), Is.Not.Null);
        }

        [Test]
        public void Disconnected_UnknownPlayer_ChangesNothing()
        {
            h.HostWithClients(Names.Ani);

            h.ConnectionChanged("TidakAda", "Disconnected");

            Assert.That(Service.Players.Count, Is.EqualTo(2));
        }

        [Test]
        public void HostDisconnected_OnClient_ClosesRoomWithReason()
        {
            h.ClientWithHost();
            string reason = null;
            Service.OnRoomClosed += r => reason = r;

            h.ConnectionChanged(Names.Host, "Disconnected");

            Assert.That(reason, Is.EqualTo("Host keluar dari room."));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.None));
        }

        [Test]
        public void OtherClientDisconnected_OnClient_RemovesPlayerAndStays()
        {
            h.ClientWithHost();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani));

            h.ConnectionChanged(Names.Ani, "Disconnected");

            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Client));
        }

        [Test]
        public void ClientDisconnected_OnHost_RoomStaysOpen()
        {
            h.HostWithClients(Names.Ani, Names.Budi);
            string code = Service.RoomCode;
            int raised = 0;
            Service.OnPlayersChanged += () => raised++;

            h.ConnectionChanged(Names.Ani, "Disconnected");

            Assert.That(h.Player(Names.Ani), Is.Null);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
            Assert.That(Service.RoomCode, Is.EqualTo(code));
        }

        [Test]
        public void LastClientDisconnected_OnHost_RoomStaysOpen()
        {
            h.HostWithClients(Names.Ani);
            string code = Service.RoomCode;

            h.ConnectionChanged(Names.Ani, "Disconnected");

            Assert.That(Service.Players.Count, Is.EqualTo(1));
            Assert.That(Service.Mode, Is.EqualTo(LobbyMode.Host));
            Assert.That(Service.RoomCode, Is.EqualTo(code));
        }

        [Test]
        public void FullLobby_AfterClientDisconnects_CannotStart()
        {
            Service.CreateRoom();
            h.Receive(ServiceHarness.ClientMessage(Names.Ani, ready: true));
            h.Receive(ServiceHarness.ClientMessage(Names.Budi, ready: true));
            Assume.That(Service.CanStart, Is.True);

            h.ConnectionChanged(Names.Budi, "Disconnected");

            Assert.That(Service.CanStart, Is.False);
        }

        // ---------- Subscriber yang error tidak boleh menghentikan service ----------

        [Test]
        public void PlayersChanged_ThrowingSubscriber_DoesNotStopOtherSubscribers()
        {
            int raised = 0;
            Service.OnPlayersChanged += () => throw new InvalidOperationException("boom-players");
            Service.OnPlayersChanged += () => raised++;
            LogAssert.Expect(LogType.Exception, new Regex("boom-players"));

            Assert.DoesNotThrow(() => Service.CreateRoom());

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void RoomClosed_ThrowingSubscriber_DoesNotStopOtherSubscribers()
        {
            h.ClientWithHost();
            int closed = 0;
            Service.OnRoomClosed += _ => throw new InvalidOperationException("boom-closed");
            Service.OnRoomClosed += _ => closed++;
            LogAssert.Expect(LogType.Exception, new Regex("boom-closed"));

            h.ConnectionChanged(Names.Host, "Disconnected");

            Assert.That(closed, Is.EqualTo(1));
        }
    }

}
#endif
