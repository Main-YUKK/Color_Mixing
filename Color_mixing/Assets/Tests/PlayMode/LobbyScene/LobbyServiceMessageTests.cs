using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

// Pesan dari device lain (HELLO, READY, ROOM, KICK, REFORM), event koneksi pemain, dan data mentah dari GameKit.
// Pesan ditiru lewat LobbyTestHelpers (memanggil method private LobbyService yang sama dengan jalur GameKit).
public class LobbyServiceMessageTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;
    private int changes;
    private int joinConfirmed;
    private readonly List<string> closedReasons = new List<string>();

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        closedReasons.Clear();
        service = LobbyTestHelpers.NewService(toDestroy);
        changes = 0;
        joinConfirmed = 0;
        service.OnPlayersChanged += () => changes++;
        service.OnJoinConfirmed += () => joinConfirmed++;
        service.OnRoomClosed += reason => closedReasons.Add(reason);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) UnityEngine.Object.Destroy(o);
    }

    private void AsHost()
    {
        service.CreateRoom("12345");
        changes = 0;
    }

    private void AsClientWithHost(string roomName = "")
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, roomName: roomName);
        changes = 0;
        joinConfirmed = 0;
    }

    // ---------- Pesan yang harus diabaikan ----------

    [Test]
    public void Message_WithoutARoom_IsIgnored()
    {
        LobbyTestHelpers.Receive(service, "HELLO", "A");

        Assert.AreEqual(0, service.Players.Count);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Message_OfUnknownType_IsIgnored()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "FOO", "A");

        Assert.AreEqual(1, service.Players.Count);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Message_WithEmptyName_IsIgnored()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "HELLO", "");

        Assert.AreEqual(1, service.Players.Count);
    }

    [Test]
    public void Message_FromOurOwnName_IsIgnored()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "HELLO", LobbyTestHelpers.LocalName(service));

        Assert.AreEqual(1, service.Players.Count, "pesan yang menggema tidak menambah pemain");
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Message_NullMessage_IsIgnored()
    {
        AsHost();

        Assert.DoesNotThrow(() => LobbyTestHelpers.DataReceived(service, Encoding.UTF8.GetBytes("null"), "A"));
        Assert.AreEqual(1, service.Players.Count);
    }

    // ---------- HELLO / READY ----------

    [Test]
    public void Hello_FromNewClient_AddsThemAsNotReadyClientAndNotifies()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "HELLO", "A");

        var a = service.Players.Find(p => p.name == "A");
        Assert.NotNull(a);
        Assert.IsFalse(a.isHost);
        Assert.IsFalse(a.isLocal);
        Assert.IsFalse(a.isReady);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void Hello_Twice_DoesNotDuplicateThePlayer()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "HELLO", "A");
        LobbyTestHelpers.Receive(service, "HELLO", "A");

        Assert.AreEqual(2, service.Players.Count);
    }

    [Test]
    public void Ready_FromClient_ChecksThemOff()
    {
        AsHost();
        LobbyTestHelpers.Receive(service, "HELLO", "A");

        LobbyTestHelpers.Receive(service, "READY", "A", ready: true);

        Assert.IsTrue(service.Players.Find(p => p.name == "A").isReady);
    }

    [Test]
    public void Ready_ReportedInTheFirstMessage_IsKept()
    {
        AsHost();

        // Client yang ready sebelum host mendengarnya (mis. lobby disusun ulang): status ready ikut di HELLO.
        LobbyTestHelpers.Receive(service, "HELLO", "A", ready: true);

        Assert.IsTrue(service.Players.Find(p => p.name == "A").isReady);
    }

    [Test]
    public void Ready_ClaimedByHost_IsNeverShown()
    {
        AsClientWithHost();

        LobbyTestHelpers.Receive(service, "READY", "HostX", isHost: true, ready: true);

        Assert.IsFalse(service.Players.Find(p => p.name == "HostX").isReady, "host tidak pernah perlu ready");
    }

    // ---------- Client menerima pesan dari host ----------

    [Test]
    public void Client_FirstMessageFromHost_AddsHostAndConfirmsJoinOnce()
    {
        service.JoinRoom("12345");

        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true);
        LobbyTestHelpers.Receive(service, "ACK", "HostX", isHost: true);

        var host = service.Players.Find(p => p.name == "HostX");
        Assert.NotNull(host);
        Assert.IsTrue(host.isHost);
        Assert.AreEqual(1, joinConfirmed, "OnJoinConfirmed hanya sekali walau host mengirim berkali-kali");
    }

    [Test]
    public void Client_MessageFromAnotherClient_DoesNotConfirmJoin()
    {
        service.JoinRoom("12345");

        LobbyTestHelpers.Receive(service, "HELLO", "B");

        Assert.AreEqual(0, joinConfirmed);
    }

    [Test]
    public void Client_HostMessage_SetsRoomNameAndEmptyMeansDefault()
    {
        AsClientWithHost("Warung Biru");
        Assert.AreEqual("Warung Biru", service.RoomName);

        LobbyTestHelpers.Receive(service, "ACK", "HostX", isHost: true, roomName: "");

        Assert.AreEqual("", service.RoomName);
    }

    [Test]
    public void Client_RoomMessageFromHost_RenamesTheRoom()
    {
        AsClientWithHost("Lama");

        LobbyTestHelpers.Receive(service, "ROOM", "HostX", isHost: true, roomName: "Baru");

        Assert.AreEqual("Baru", service.RoomName);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void RoomMessage_FromNonHost_IsRejected()
    {
        AsClientWithHost("Asli");

        LobbyTestHelpers.Receive(service, "ROOM", "B", isHost: false, roomName: "Curang");

        Assert.AreEqual("Asli", service.RoomName);
        Assert.IsFalse(service.Players.Exists(p => p.name == "B"), "pengirim palsu tidak ditambahkan");
    }

    [Test]
    public void Client_HostMessage_CorrectsAWronglyGuessedHost()
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "Tebakan", isHost: true);

        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true);

        Assert.IsFalse(service.Players.Find(p => p.name == "Tebakan").isHost);
        Assert.IsTrue(service.Players.Find(p => p.name == "HostX").isHost);
    }

    [Test]
    public void Host_MessageFromAnotherHost_IsIgnored()
    {
        AsHost();

        LobbyTestHelpers.Receive(service, "HELLO", "Kembaran", isHost: true);

        Assert.AreEqual(1, service.Players.Count, "dua host dengan kode sama: yang kedua diabaikan");
        Assert.AreEqual(0, changes);
    }

    // ---------- KICK ----------

    [Test]
    public void Kick_TargetingUs_LeavesTheRoomWithAReason()
    {
        service.JoinRoom("12345");

        LobbyTestHelpers.KickLocal(service);

        CollectionAssert.AreEqual(new[] { "Kamu dikeluarkan oleh host." }, closedReasons);
        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
        Assert.AreEqual(0, service.Players.Count);
        Assert.AreEqual("Kamu dikeluarkan oleh host.", service.LastMessage);
    }

    [Test]
    public void Kick_TargetingAnotherPlayer_RemovesOnlyThem()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        LobbyTestHelpers.Receive(service, "HELLO", "C");
        changes = 0;

        LobbyTestHelpers.Receive(service, "KICK", "HostX", isHost: true, target: "B");

        Assert.IsFalse(service.Players.Exists(p => p.name == "B"));
        Assert.IsTrue(service.Players.Exists(p => p.name == "C"));
        Assert.AreEqual(LobbyMode.Client, service.Mode, "kita tetap di room");
        Assert.AreEqual(0, closedReasons.Count);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void Kick_FromNonHost_IsRejected()
    {
        service.JoinRoom("12345");

        LobbyTestHelpers.Receive(service, "KICK", "B", isHost: false, target: LobbyTestHelpers.LocalName(service));

        Assert.AreEqual(LobbyMode.Client, service.Mode, "pemain biasa tidak boleh mengeluarkan orang");
        Assert.AreEqual(0, closedReasons.Count);
    }

    [Test]
    public void KickedPlayer_LateMessages_AreIgnoredUntilTheyDisconnect()
    {
        AsHost();
        LobbyTestHelpers.Receive(service, "HELLO", "A");
        service.KickPlayer("A");

        LobbyTestHelpers.Receive(service, "HELLO", "A");
        Assert.IsFalse(service.Players.Exists(p => p.name == "A"), "sisa pesan dari pemain yang baru di-kick");

        LobbyTestHelpers.ConnectionChanged(service, "A", "Disconnected");
        LobbyTestHelpers.Receive(service, "HELLO", "A");
        Assert.IsTrue(service.Players.Exists(p => p.name == "A"), "setelah terputus dia boleh masuk lagi");
    }

    // ---------- REFORM ----------

    [Test]
    public void Reform_FromHost_KeepsOnlyUsAndTheHost()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        LobbyTestHelpers.Receive(service, "HELLO", "C");

        LobbyTestHelpers.Receive(service, "REFORM", "HostX", isHost: true);

        Assert.AreEqual(2, service.Players.Count);
        Assert.IsTrue(service.Players.Exists(p => p.isLocal));
        Assert.IsTrue(service.Players.Exists(p => p.name == "HostX"));
    }

    [Test]
    public void Reform_FromNonHost_IsRejected()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "HELLO", "B");

        LobbyTestHelpers.Receive(service, "REFORM", "B", isHost: false);

        Assert.AreEqual(3, service.Players.Count);
    }

    [Test]
    public void WhileReforming_MessagesFromTheOldMatchAreIgnored()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "REFORM", "HostX", isHost: true);

        LobbyTestHelpers.Receive(service, "HELLO", "B");

        Assert.IsFalse(service.Players.Exists(p => p.name == "B"));
    }

    // ---------- Koneksi pemain ----------

    [Test]
    public void Connected_AddsTheRemotePlayer_EvenWithLowercaseState()
    {
        AsHost();

        LobbyTestHelpers.ConnectionChanged(service, "A", "connected");

        Assert.IsTrue(service.Players.Exists(p => p.name == "A" && !p.isHost));
        Assert.AreEqual(1, changes);
    }

    [TestCase("")]
    [TestCase(null)]
    public void Connected_WithoutAName_IsIgnored(string name)
    {
        AsHost();

        LobbyTestHelpers.ConnectionChanged(service, name, "Connected");

        Assert.AreEqual(1, service.Players.Count);
    }

    [Test]
    public void Connected_OurOwnName_IsIgnored()
    {
        AsHost();

        LobbyTestHelpers.ConnectionChanged(service, LobbyTestHelpers.LocalName(service), "Connected");

        Assert.AreEqual(1, service.Players.Count);
    }

    [Test]
    public void Connected_WithoutARoom_IsIgnored()
    {
        LobbyTestHelpers.ConnectionChanged(service, "A", "Connected");

        Assert.AreEqual(0, service.Players.Count);
    }

    [Test]
    public void Disconnected_Client_IsRemovedFromTheHostLobby()
    {
        AsHost();
        LobbyTestHelpers.Receive(service, "HELLO", "A");
        changes = 0;

        LobbyTestHelpers.ConnectionChanged(service, "A", "Disconnected");

        Assert.IsFalse(service.Players.Exists(p => p.name == "A"));
        Assert.IsTrue(changes >= 1, "UI harus diberi tahu");
        Assert.AreEqual(0, closedReasons.Count, "host tetap di room");
    }

    [Test]
    public void Disconnected_Host_ClosesTheRoomForTheClient()
    {
        AsClientWithHost();

        LobbyTestHelpers.ConnectionChanged(service, "HostX", "Disconnected");

        CollectionAssert.AreEqual(new[] { LobbyService.HostLeftReason }, closedReasons);
        Assert.AreEqual(LobbyMode.None, service.Mode);
    }

    [Test]
    public void Disconnected_OtherClient_JustRemovesThemForAClient()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        changes = 0;

        LobbyTestHelpers.ConnectionChanged(service, "B", "Disconnected");

        Assert.IsFalse(service.Players.Exists(p => p.name == "B"));
        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.AreEqual(1, changes);
        Assert.AreEqual(0, closedReasons.Count);
    }

    [Test]
    public void Disconnected_UnknownPlayer_ChangesNothing()
    {
        AsHost();

        LobbyTestHelpers.ConnectionChanged(service, "Siapa", "Disconnected");

        Assert.AreEqual(1, service.Players.Count);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Disconnected_DuringReform_IsNotTreatedAsHostLeaving()
    {
        AsClientWithHost();
        LobbyTestHelpers.Receive(service, "REFORM", "HostX", isHost: true);

        LobbyTestHelpers.ConnectionChanged(service, "HostX", "Disconnected");

        Assert.AreEqual(0, closedReasons.Count, "match lama memang sedang diputus");
        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.IsTrue(service.Players.Exists(p => p.name == "HostX"));
    }

    [Test]
    public void Disconnected_AfterBeingKicked_DoesNotRemoveAnythingElse()
    {
        AsHost();
        LobbyTestHelpers.Receive(service, "HELLO", "A");
        service.KickPlayer("A");
        changes = 0;

        LobbyTestHelpers.ConnectionChanged(service, "A", "Disconnected");

        Assert.AreEqual(0, changes, "dia sudah dihapus saat di-kick");
    }

    [Test]
    public void OtherConnectionStates_AreIgnored()
    {
        AsHost();
        LobbyTestHelpers.Receive(service, "HELLO", "A");
        changes = 0;

        LobbyTestHelpers.ConnectionChanged(service, "A", "Unknown");

        Assert.IsTrue(service.Players.Exists(p => p.name == "A"));
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Disconnected_IsAlsoReportedToTheRunningGame()
    {
        AsHost();
        string gone = null;
        service.GameLink.PlayerGone += name => gone = name;

        LobbyTestHelpers.ConnectionChanged(service, "A", "Disconnected");

        Assert.AreEqual("A", gone, "game yang sedang berjalan harus tahu, walau pemainnya tidak ada di lobby");
    }

    // ---------- Data mentah dari GameKit ----------

    [Test]
    public void Data_LobbyJson_IsHandledAsAMessage()
    {
        AsHost();
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LobbyMessage { type = "HELLO", name = "A" }));

        LobbyTestHelpers.DataReceived(service, data, "A");

        Assert.IsTrue(service.Players.Exists(p => p.name == "A"));
    }

    [Test]
    public void Data_GameFrame_GoesToTheGameAndNotToTheLobby()
    {
        AsHost();
        string from = null;
        byte[] received = null;
        service.GameLink.RawReceived += (name, data) => { from = name; received = data; };
        byte[] frame = { GameKitTransport.FrameMarker, 1, 2, 3 };

        LobbyTestHelpers.DataReceived(service, frame, "A");

        Assert.AreEqual("A", from);
        CollectionAssert.AreEqual(frame, received);
        Assert.AreEqual(1, service.Players.Count, "pesan game tidak mengubah lobby");
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Data_GameFrame_WithoutAGameListener_DoesNotThrow()
    {
        AsHost();

        Assert.DoesNotThrow(() =>
            LobbyTestHelpers.DataReceived(service, new byte[] { GameKitTransport.FrameMarker, 9 }, "A"));
    }

    [TestCase("bukan json sama sekali")]
    [TestCase("")]
    [TestCase("{\"type\":")]
    public void Data_Garbage_IsIgnoredWithoutThrowing(string text)
    {
        AsHost();

        Assert.DoesNotThrow(() => LobbyTestHelpers.DataReceived(service, Encoding.UTF8.GetBytes(text), "A"));
        Assert.AreEqual(1, service.Players.Count);
    }

    [Test]
    public void Data_Null_IsIgnoredWithoutThrowing()
    {
        AsHost();

        Assert.DoesNotThrow(() => LobbyTestHelpers.DataReceived(service, null, "A"));
        Assert.AreEqual(1, service.Players.Count);
    }
}
