using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Sambungan lobby -> game (IGameLink): daftar nama pemain yang dipakai GameKitTransport untuk memberi nomor pemain.
// Urutannya harus sama di semua device: [host, client slot 1, client slot 2].
public class LobbyGameLinkTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        service = LobbyTestHelpers.NewService(toDestroy);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) UnityEngine.Object.Destroy(o);
    }

    [Test]
    public void GameLink_IsTheSameObjectEveryTime()
    {
        Assert.AreSame(service.GameLink, service.GameLink);
    }

    [Test]
    public void Host_ReportsItselfAsHostWithItsOwnName()
    {
        service.CreateRoom("12345");

        Assert.IsTrue(service.GameLink.IsHost);
        Assert.AreEqual(LobbyTestHelpers.LocalName(service), service.GameLink.LocalName);
    }

    [Test]
    public void Client_IsNotHost()
    {
        service.JoinRoom("12345");

        Assert.IsFalse(service.GameLink.IsHost);
        Assert.AreEqual(LobbyTestHelpers.LocalName(service), service.GameLink.LocalName);
    }

    [Test]
    public void Roster_AsHost_IsHostThenClientsInArrivalOrder()
    {
        service.CreateRoom("12345");
        LobbyTestHelpers.AddPlayer(service, "B");
        LobbyTestHelpers.AddPlayer(service, "C");

        var roster = service.GameLink.CurrentRoster();

        CollectionAssert.AreEqual(new[] { LobbyTestHelpers.LocalName(service), "B", "C" }, roster);
    }

    [Test]
    public void Roster_AsClient_FollowsTheSlotOrderFromTheHost()
    {
        service.JoinRoom("12345");
        string me = LobbyTestHelpers.LocalName(service);
        LobbyTestHelpers.Receive(service, "HELLO", "B");
        // Host mengirim urutan slot semua client, termasuk kita.
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true, slots: $"B|{me}");

        var roster = service.GameLink.CurrentRoster();

        CollectionAssert.AreEqual(new[] { "HostX", "B", me }, roster, "sama dengan yang dilihat host");
    }

    [Test]
    public void Roster_WhenHostIsNotKnownYet_StartsWithAnEmptyName()
    {
        service.JoinRoom("12345");

        var roster = service.GameLink.CurrentRoster();

        Assert.AreEqual("", roster[0]);
    }

    [Test]
    public void SendRaw_InTheEditor_DoesNothingAndDoesNotThrow()
    {
        service.CreateRoom("12345");

        Assert.DoesNotThrow(() => service.GameLink.SendRaw(new byte[] { 1, 2, 3 }));
    }
}
