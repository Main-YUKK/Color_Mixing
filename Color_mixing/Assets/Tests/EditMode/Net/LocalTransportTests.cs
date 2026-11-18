using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Memakai socket sungguhan di 127.0.0.1 dengan port bebas. Pump dipanggil manual (tanpa Update).
public class LocalTransportTests
{
    private readonly List<LocalTransport> created = new List<LocalTransport>();

    [TearDown]
    public void TearDown()
    {
        foreach (var t in created) t.Dispose();
        created.Clear();
        GameNet.End();
    }

    private LocalTransport Host()
    {
        var t = LocalTransport.StartHost(0);
        created.Add(t);
        return t;
    }

    private LocalTransport Client(LocalTransport host)
    {
        var t = LocalTransport.StartClient("127.0.0.1", host.Port);
        created.Add(t);
        return t;
    }

    // Pump semua sampai syarat terpenuhi (maks 3 detik).
    private bool PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var t in created) t.Pump();
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return false;
    }

    // Pump sebentar tanpa syarat, untuk memberi waktu kalau ada kiriman nyasar.
    private void PumpFor(int milliseconds)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < end)
        {
            foreach (var t in created) t.Pump();
            Thread.Sleep(5);
        }
    }

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    private static string Text(byte[] b) => Encoding.UTF8.GetString(b);

    // ---------- Masuk ----------

    [Test]
    public void Clients_GetIndexOneThenTwo()
    {
        var host = Host();
        var c1 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.LocalPlayerIndex == 1));
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c2.LocalPlayerIndex == 2));

        Assert.AreEqual(0, host.LocalPlayerIndex);
        Assert.AreEqual(2, host.ConnectedCount);
        Assert.IsTrue(c1.IsConnected && c2.IsConnected);
    }

    [Test]
    public void Host_RaisesPlayerJoined()
    {
        var host = Host();
        var joined = new List<int>();
        host.PlayerJoined += joined.Add;

        Client(host);
        Assert.IsTrue(PumpUntil(() => joined.Count == 1));

        CollectionAssert.AreEqual(new[] { 1 }, joined);
    }

    [Test]
    public void Client_IsNotConnectedUntilWelcome()
    {
        var host = Host();
        var c1 = Client(host);

        Assert.AreEqual(-1, c1.LocalPlayerIndex);
        Assert.IsFalse(c1.IsConnected);
    }

    [Test]
    public void FourthClient_IsRejected()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        var c3 = Client(host);
        bool disconnected = false;
        c3.Disconnected += () => disconnected = true;

        Assert.IsTrue(PumpUntil(() => disconnected));
        Assert.AreEqual(-1, c3.LocalPlayerIndex);
        Assert.AreEqual(2, host.ConnectedCount);
    }

    [Test]
    public void StartClient_WithoutHost_Throws()
    {
        var host = Host();
        int port = host.Port;
        host.Dispose();

        Assert.Catch(() => LocalTransport.StartClient("127.0.0.1", port));
    }

    // ---------- Kirim ----------

    [Test]
    public void ClientSendToHost_HostSeesSenderIndex()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        var got = new List<string>();
        host.DataReceived += (from, data) => got.Add($"{from}:{Text(data)}");

        c2.SendToHost(Bytes("halo"));
        Assert.IsTrue(PumpUntil(() => got.Count == 1));

        CollectionAssert.AreEqual(new[] { "2:halo" }, got);
    }

    [Test]
    public void HostSendToAll_ReachesBothClientsFromIndexZero()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        var got = new List<string>();
        c1.DataReceived += (from, data) => got.Add($"c1 dari {from}:{Text(data)}");
        c2.DataReceived += (from, data) => got.Add($"c2 dari {from}:{Text(data)}");

        host.SendToAll(Bytes("semua"));
        Assert.IsTrue(PumpUntil(() => got.Count == 2));

        CollectionAssert.AreEquivalent(new[] { "c1 dari 0:semua", "c2 dari 0:semua" }, got);
    }

    [Test]
    public void HostSendTo_ReachesOnlyThatClient()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        var got = new List<string>();
        c1.DataReceived += (from, data) => got.Add("c1");
        c2.DataReceived += (from, data) => got.Add("c2");

        host.SendTo(2, Bytes("khusus"));
        Assert.IsTrue(PumpUntil(() => got.Count >= 1));
        PumpFor(200);   // beri waktu kalau ada kiriman nyasar ke client 1

        CollectionAssert.AreEqual(new[] { "c2" }, got);
    }

    [Test]
    public void ClientSendToAll_IsRefused()
    {
        var host = Host();
        var c1 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected));

        LogAssert.Expect(LogType.Warning, new Regex("host"));
        c1.SendToAll(Bytes("tidak boleh"));
    }

    [Test]
    public void ManyQuickMessages_ArriveInOrder()
    {
        var host = Host();
        var c1 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected));

        var got = new List<int>();
        host.DataReceived += (from, data) => got.Add(int.Parse(Text(data)));

        for (int i = 0; i < 200; i++) c1.SendToHost(Bytes(i.ToString()));
        Assert.IsTrue(PumpUntil(() => got.Count == 200));

        for (int i = 0; i < 200; i++) Assert.AreEqual(i, got[i]);
    }

    // ---------- Putus ----------

    [Test]
    public void ClientLeaves_HostRaisesPlayerLeft_AndIndexIsReused()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        var left = new List<int>();
        host.PlayerLeft += left.Add;

        c1.Dispose();
        created.Remove(c1);
        Assert.IsTrue(PumpUntil(() => left.Count == 1));
        CollectionAssert.AreEqual(new[] { 1 }, left);
        Assert.AreEqual(1, host.ConnectedCount);

        var c3 = Client(host);
        Assert.IsTrue(PumpUntil(() => c3.IsConnected));
        Assert.AreEqual(1, c3.LocalPlayerIndex);   // slot yang kosong terisi lagi
    }

    [Test]
    public void HostLeaves_ClientsRaiseDisconnected()
    {
        var host = Host();
        var c1 = Client(host);
        var c2 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected && c2.IsConnected));

        int disconnected = 0;
        c1.Disconnected += () => disconnected++;
        c2.Disconnected += () => disconnected++;

        host.Dispose();
        created.Remove(host);
        Assert.IsTrue(PumpUntil(() => disconnected == 2));

        Assert.IsFalse(c1.IsConnected);
        Assert.AreEqual(-1, c1.LocalPlayerIndex);
    }

    // ---------- Bersama GameNet ----------

    [Test]
    public void GameNet_OverLocalTransport_ClientToHostAndBack()
    {
        var host = Host();
        var c1 = Client(host);
        Assert.IsTrue(PumpUntil(() => c1.IsConnected));

        var hostNet = new GameNet(host);
        var clientNet = new GameNet(c1);

        NetMessage atHost = null, atClient = null;
        hostNet.Subscribe(NetMessageType.Leave, m => atHost = m);
        clientNet.Subscribe(NetMessageType.Go, m => atClient = m);

        clientNet.SendToHost(NetMessage.Create(NetMessageType.Leave));
        Assert.IsTrue(PumpUntil(() => atHost != null));
        Assert.AreEqual(1, atHost.from);

        hostNet.SendToAll(NetMessage.Create(NetMessageType.Go));
        Assert.IsTrue(PumpUntil(() => atClient != null));
        Assert.AreEqual(0, atClient.from);
    }
}
