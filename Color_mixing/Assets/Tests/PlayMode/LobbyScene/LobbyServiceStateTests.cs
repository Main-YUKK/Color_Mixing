using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Status lobby untuk UI: siapa yang sudah ready, kapan Play aktif, tombol Ready, urutan slot, dan Kick.
public class LobbyServiceStateTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;
    private int changes;

    // Jumlah client agar lobby penuh (host tidak dihitung). Dibaca dari LobbyService: 2 kalau RequiredPlayers = 3.
    private static int FullClients => LobbyTestHelpers.RequiredPlayers - 1;

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        service = LobbyTestHelpers.NewService(toDestroy);
        changes = 0;
        service.OnPlayersChanged += () => changes++;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) UnityEngine.Object.Destroy(o);
    }

    // Host dengan sejumlah client; client pertama sebanyak readyCount sudah ready.
    private void HostLobby(int clientCount, int readyCount)
    {
        service.CreateRoom("12345");
        for (int i = 0; i < clientCount; i++)
            LobbyTestHelpers.AddPlayer(service, "Client" + (i + 1), ready: i < readyCount);
    }

    // Client yang sudah masuk match dan mendengar host.
    private void JoinedClient()
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true);
    }

    // ---------- AllClientsReady ----------

    [Test]
    public void AllClientsReady_FullLobbyAndEveryoneReady_IsTrue()
    {
        HostLobby(FullClients, FullClients);

        Assert.IsTrue(service.AllClientsReady);
    }

    [Test]
    public void AllClientsReady_OneClientNotReady_IsFalse()
    {
        HostLobby(FullClients, FullClients - 1);

        Assert.IsFalse(service.AllClientsReady);
    }

    [Test]
    public void AllClientsReady_LobbyNotFullYet_IsFalseEvenIfEveryoneThereIsReady()
    {
        HostLobby(FullClients - 1, FullClients - 1);

        Assert.IsFalse(service.AllClientsReady);
    }

    [Test]
    public void AllClientsReady_HostIsNeverRequiredToBeReady()
    {
        HostLobby(FullClients, FullClients);
        Assert.IsFalse(service.Players.Find(p => p.isHost).isReady, "syarat awal: host tidak ready");

        Assert.IsTrue(service.AllClientsReady);
    }

    // ---------- CanStart (tombol Play) ----------

    [Test]
    public void CanStart_HostWithEveryoneReady_IsTrue()
    {
        HostLobby(FullClients, FullClients);

        Assert.IsTrue(service.CanStart);
    }

    [Test]
    public void CanStart_HostButSomeoneNotReady_IsFalse()
    {
        HostLobby(FullClients, FullClients - 1);

        Assert.IsFalse(service.CanStart);
    }

    [Test]
    public void CanStart_ClientNeverCanStart()
    {
        JoinedClient();
        service.Players.Find(p => p.isLocal).isReady = true;
        for (int i = 0; i < FullClients - 1; i++)
            LobbyTestHelpers.AddPlayer(service, "Other" + i, ready: true);
        Assert.IsTrue(service.AllClientsReady, "syarat awal: semua client sudah ready");

        Assert.IsFalse(service.CanStart);
    }

    [Test]
    public void CanStart_WithoutARoom_IsFalse()
    {
        Assert.IsFalse(service.CanStart);
    }

    // ---------- CanSetReady / IsLocalReady / MarkReady ----------

    [Test]
    public void CanSetReady_ClientAfterHostIsHeard_IsTrue()
    {
        JoinedClient();

        Assert.IsTrue(service.CanSetReady);
    }

    [Test]
    public void CanSetReady_ClientBeforeHostIsHeard_IsFalse()
    {
        service.JoinRoom("12345");

        Assert.IsFalse(service.CanSetReady);
    }

    [Test]
    public void CanSetReady_Host_IsFalse()
    {
        HostLobby(FullClients, 0);

        Assert.IsFalse(service.CanSetReady);
    }

    [Test]
    public void CanSetReady_WhileRoomIsBeingRebuilt_IsFalse()
    {
        JoinedClient();
        LobbyTestHelpers.Receive(service, "REFORM", "HostX", isHost: true);

        Assert.IsFalse(service.CanSetReady);
    }

    [Test]
    public void IsLocalReady_StartsFalse_AndFalseWithoutARoom()
    {
        Assert.IsFalse(service.IsLocalReady, "tanpa room");

        JoinedClient();
        Assert.IsFalse(service.IsLocalReady, "client baru masuk");
    }

    [Test]
    public void MarkReady_BeforeHostIsHeard_DoesNothing()
    {
        service.JoinRoom("12345");
        changes = 0;

        service.MarkReady();

        Assert.IsFalse(service.IsLocalReady);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void MarkReady_AsClient_MarksLocalPlayerAndNotifiesOnce()
    {
        JoinedClient();
        changes = 0;

        service.MarkReady();

        Assert.IsTrue(service.IsLocalReady);
        Assert.IsTrue(service.Players.Find(p => p.isLocal).isReady);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void MarkReady_Twice_DoesNotNotifyAgain()
    {
        JoinedClient();
        service.MarkReady();
        changes = 0;

        service.MarkReady();

        Assert.IsTrue(service.IsLocalReady, "ready tidak bisa dibatalkan");
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void MarkReady_AsHost_DoesNothing()
    {
        HostLobby(FullClients, 0);
        changes = 0;

        service.MarkReady();

        Assert.IsFalse(service.IsLocalReady);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void MarkReady_WhileRoomIsBeingRebuilt_DoesNothing()
    {
        JoinedClient();
        LobbyTestHelpers.Receive(service, "REFORM", "HostX", isHost: true);

        service.MarkReady();

        Assert.IsFalse(service.IsLocalReady);
    }

    // ---------- Urutan slot ----------

    [Test]
    public void SlotOrder_AsHost_FollowsArrivalOrderAndSkipsHost()
    {
        service.CreateRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "B");
        LobbyTestHelpers.AddPlayer(service, "C");
        LobbyTestHelpers.AddPlayer(service, "A");

        var names = service.GetOthersInSlotOrder().Select(p => p.name).ToList();

        CollectionAssert.AreEqual(new[] { "B", "C", "A" }, names);
    }

    [Test]
    public void SlotOrder_AsClient_FollowsTheOrderTheHostSent()
    {
        service.JoinRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        LobbyTestHelpers.Receive(service, "HELLO", "C");
        // Di device ini B datang lebih dulu, tapi host bilang urutannya C, kita, lalu B.
        // (Pemain lokal ikut dihitung: slot "selain host" mencakup diri sendiri.)
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, slots: $"C|{me}|B");

        var names = service.GetOthersInSlotOrder().Select(p => p.name).ToList();

        CollectionAssert.AreEqual(new[] { "C", me, "B" }, names);
    }

    [Test]
    public void SlotOrder_AsClient_PlayersMissingFromHostListGoLast()
    {
        service.JoinRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        LobbyTestHelpers.Receive(service, "HELLO", "C");
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, slots: "C");

        var names = service.GetOthersInSlotOrder().Select(p => p.name).ToList();

        // Yang tidak ada di daftar host menyusul di belakang, sesuai urutan datang di device ini.
        CollectionAssert.AreEqual(new[] { "C", me, "B" }, names);
    }

    [Test]
    public void SlotOrder_AsClient_NamesInHostListThatAreNotHereAreSkipped()
    {
        service.JoinRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, slots: "Z|B");

        var names = service.GetOthersInSlotOrder().Select(p => p.name).ToList();

        CollectionAssert.AreEqual(new[] { "B", me }, names);
    }

    // ---------- KickPlayer ----------

    [Test]
    public void KickPlayer_RemovesThePlayerAndNotifies()
    {
        service.CreateRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "A");
        LobbyTestHelpers.AddPlayer(service, "B");
        changes = 0;

        service.KickPlayer("A");

        Assert.IsFalse(service.Players.Exists(p => p.name == "A"));
        Assert.IsTrue(service.Players.Exists(p => p.name == "B"), "pemain lain tidak ikut keluar");
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void KickPlayer_UnknownName_DoesNothing()
    {
        service.CreateRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "A");
        changes = 0;

        service.KickPlayer("Siapa");

        Assert.AreEqual(2, service.Players.Count);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void KickPlayer_HostCannotKickThemselves()
    {
        service.CreateRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);

        service.KickPlayer(me);

        Assert.IsTrue(service.Players.Exists(p => p.isLocal));
    }

    [Test]
    public void KickPlayer_AsClient_IsIgnored()
    {
        JoinedClient();
        LobbyTestHelpers.AddPlayer(service, "A");
        changes = 0;

        service.KickPlayer("A");

        Assert.IsTrue(service.Players.Exists(p => p.name == "A"));
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void KickPlayer_WhileRoomIsBeingRebuilt_IsIgnored()
    {
        // Hanya client yang bisa masuk keadaan "menyusun ulang" lewat pesan REFORM,
        // jadi keadaan itu di sisi host dibuat lewat field private.
        service.CreateRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "A");
        PlayTestHelpers.SetField(service, "reforming", true);

        service.KickPlayer("A");

        Assert.IsTrue(service.Players.Exists(p => p.name == "A"));
    }

    // ---------- SetInGame ----------

    [Test]
    public void SetInGame_SameValueAgain_DoesNotNotify()
    {
        HostLobby(FullClients, FullClients);
        changes = 0;

        service.SetInGame(false);

        Assert.AreEqual(0, changes);
        Assert.IsTrue(service.AllClientsReady, "centang ready tidak disentuh");
    }

    [Test]
    public void SetInGame_Finished_AllClientsMustReadyAgain()
    {
        HostLobby(FullClients, FullClients);
        service.SetInGame(true);

        service.SetInGame(false);

        Assert.IsFalse(service.AllClientsReady);
        Assert.IsFalse(service.CanStart);
    }
}
