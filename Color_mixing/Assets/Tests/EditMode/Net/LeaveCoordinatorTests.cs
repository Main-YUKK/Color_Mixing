using System;
using System.Collections.Generic;
using NUnit.Framework;

// 3 pemain di jaringan palsu: siapa melihat peringatan apa, dan ke mana mereka pergi.
public class LeaveCoordinatorTests
{
    private class Actions : ILeaveActions
    {
        public int halts, mainMenus, lobbies;
        public readonly List<(LeaveAlert alert, int player)> alerts = new List<(LeaveAlert, int)>();
        public Action pendingClose;

        public void Halt() => halts++;
        public void ShowAlert(LeaveAlert alert, int player, Action onClosed) { alerts.Add((alert, player)); pendingClose = onClosed; }
        public void GoMainMenu() => mainMenus++;
        public void GoLobby() => lobbies++;

        public void CloseAlert() { var close = pendingClose; pendingClose = null; close?.Invoke(); }
    }

    private FakeNetwork network;
    private GameNet[] nets;
    private Actions[] actions;
    private LeaveCoordinator[] coordinators;

    [SetUp]
    public void SetUp()
    {
        network = new FakeNetwork();
        nets = network.CreateGameNets();
        actions = new Actions[3];
        coordinators = new LeaveCoordinator[3];
        for (int i = 0; i < 3; i++)
        {
            actions[i] = new Actions();
            coordinators[i] = new LeaveCoordinator(nets[i], actions[i]);
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var c in coordinators) c.Dispose();
    }

    // ---------- Client keluar dengan sengaja ----------

    [Test]
    public void ClientLeaves_GoesToMainMenuAtOnce()
    {
        coordinators[1].LeaveLocally();

        Assert.AreEqual(1, actions[1].halts);
        Assert.AreEqual(1, actions[1].mainMenus);
        Assert.IsEmpty(actions[1].alerts, "yang keluar tidak melihat peringatan");
    }

    [Test]
    public void ClientLeaves_HostAndOtherClientGetAlertThenLobby()
    {
        coordinators[1].LeaveLocally();

        Assert.AreEqual((LeaveAlert.PlayerDisconnectedHost, 1), actions[0].alerts[0]);
        Assert.AreEqual((LeaveAlert.PlayerDisconnectedClient, 1), actions[2].alerts[0]);
        Assert.AreEqual(0, actions[0].lobbies, "belum pindah sebelum peringatan ditutup");
        Assert.AreEqual(0, actions[2].lobbies);

        actions[0].CloseAlert();
        actions[2].CloseAlert();
        Assert.AreEqual(1, actions[0].lobbies);
        Assert.AreEqual(1, actions[2].lobbies);
        Assert.AreEqual(0, actions[0].mainMenus);
        Assert.AreEqual(1, actions[0].halts);
        Assert.AreEqual(1, actions[2].halts);
    }

    [Test]
    public void SecondClientLeaving_IsAlsoRelayed()
    {
        coordinators[2].LeaveLocally();

        Assert.AreEqual((LeaveAlert.PlayerDisconnectedHost, 2), actions[0].alerts[0]);
        Assert.AreEqual((LeaveAlert.PlayerDisconnectedClient, 2), actions[1].alerts[0]);
    }

    // ---------- Host keluar ----------

    [Test]
    public void HostLeaves_BothClientsGetAlertThenMainMenu()
    {
        coordinators[0].LeaveLocally();

        Assert.AreEqual(1, actions[0].mainMenus);
        foreach (int c in new[] { 1, 2 })
        {
            Assert.AreEqual(LeaveAlert.HostLeft, actions[c].alerts[0].alert);
            Assert.AreEqual(0, actions[c].mainMenus);
            actions[c].CloseAlert();
            Assert.AreEqual(1, actions[c].mainMenus);
            Assert.AreEqual(0, actions[c].lobbies);
        }
    }

    // ---------- Putus tanpa pamit ----------

    [Test]
    public void ClientDropsWithoutLeave_SameAsLeaving()
    {
        network.Disconnect(2);

        Assert.AreEqual((LeaveAlert.PlayerDisconnectedHost, 2), actions[0].alerts[0]);
        Assert.AreEqual((LeaveAlert.PlayerDisconnectedClient, 2), actions[1].alerts[0], "host memberi tahu client yang tersisa");
        Assert.IsEmpty(actions[2].alerts, "yang putus tidak bisa dihubungi");
    }

    [Test]
    public void HostDropsWithoutLeave_ClientsGoToMainMenu()
    {
        network.Disconnect(0);

        Assert.AreEqual(LeaveAlert.HostLeft, actions[1].alerts[0].alert);
        Assert.AreEqual(LeaveAlert.HostLeft, actions[2].alerts[0].alert);
    }

    [Test]
    public void LeaveThenDrop_IsOneEvent()
    {
        coordinators[1].LeaveLocally();
        network.Disconnect(1);   // koneksi menutup setelah pamit

        Assert.AreEqual(1, actions[0].alerts.Count);
        Assert.AreEqual(1, actions[0].halts);
    }

    [Test]
    public void TwoClientsLeavingTogether_HostHandlesTheFirstOnly()
    {
        coordinators[1].LeaveLocally();
        coordinators[2].LeaveLocally();

        Assert.AreEqual(1, actions[0].alerts.Count);
        Assert.AreEqual(1, actions[0].alerts[0].player);
    }

    // ---------- Kiriman yang tidak masuk akal ----------

    [Test]
    public void PlayerLeftFromAClient_IsIgnored()
    {
        // Hanya host yang boleh bilang "pemain X keluar".
        byte[] fake = NetMessage.Create(NetMessageType.PlayerLeft, new PlayerLeftData { index = 1 }).Encode();
        network.Nodes[2].InjectRaw(1, fake);

        Assert.IsEmpty(actions[2].alerts);
    }

    [Test]
    public void PlayerLeftAboutMyself_IsIgnored()
    {
        byte[] about = NetMessage.Create(NetMessageType.PlayerLeft, new PlayerLeftData { index = 2 }).Encode();
        network.Nodes[2].InjectRaw(0, about);

        Assert.IsEmpty(actions[2].alerts);
    }

    [Test]
    public void LeaveFromTheHostIndexOnHost_IsIgnored()
    {
        byte[] leave = NetMessage.Create(NetMessageType.Leave).Encode();
        network.Nodes[0].InjectRaw(0, leave);

        Assert.IsEmpty(actions[0].alerts);
    }

    [Test]
    public void LeaveFromAnotherClientOnClient_IsIgnored()
    {
        // Client tidak pernah menerima langsung dari client lain; kalau datang, itu palsu.
        byte[] leave = NetMessage.Create(NetMessageType.Leave).Encode();
        network.Nodes[1].InjectRaw(2, leave);

        Assert.IsEmpty(actions[1].alerts);
    }

    // ---------- Setelah pindah scene ----------

    [Test]
    public void AfterResetEnding_NextLeaveIsHandled()
    {
        network.Disconnect(1);
        Assert.IsTrue(coordinators[0].Ending);

        coordinators[0].ResetEnding();
        network.Disconnect(2);

        Assert.AreEqual(2, actions[0].alerts.Count);
        Assert.AreEqual(2, actions[0].alerts[1].player);
    }

    [Test]
    public void Offline_LeaveGoesToMainMenuWithoutSending()
    {
        var net = new GameNet(new OfflineTransport());
        var a = new Actions();
        using (var c = new LeaveCoordinator(net, a))
        {
            c.LeaveLocally();
            c.LeaveLocally();   // tekan dua kali

            Assert.AreEqual(1, a.mainMenus);
            Assert.AreEqual(1, a.halts);
        }
    }

    [Serializable] private class PlayerLeftData { public int index; }
}
