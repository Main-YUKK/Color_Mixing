using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class GameNetTests
{
    [Serializable]
    private struct Number { public int value; }

    [TearDown]
    public void TearDown() => GameNet.End();

    // ---------- Sesi ----------

    [Test]
    public void Instance_IsNull_UntilBegin_AndAfterEnd()
    {
        GameNet.End();
        Assert.IsNull(GameNet.Instance);

        GameNet.BeginOffline();
        Assert.IsNotNull(GameNet.Instance);

        GameNet.End();
        Assert.IsNull(GameNet.Instance);
    }

    [Test]
    public void Begin_ReplacesPreviousSession()
    {
        GameNet.BeginOffline();
        var first = GameNet.Instance;

        GameNet.BeginOffline();

        Assert.AreNotSame(first, GameNet.Instance);
    }

    // ---------- Tanpa jaringan (Editor / test) ----------

    [Test]
    public void Offline_IsHostWithIndexZero()
    {
        GameNet.BeginOffline();

        Assert.IsTrue(GameNet.Instance.IsOffline);
        Assert.IsTrue(GameNet.Instance.IsHost);
        Assert.AreEqual(0, GameNet.Instance.LocalPlayerIndex);
    }

    [Test]
    public void Offline_SendToHost_ArrivesAtOwnHandler()
    {
        GameNet.BeginOffline();
        NetMessage received = null;
        GameNet.Instance.Subscribe("TEST", m => received = m);

        GameNet.Instance.SendToHost(NetMessage.Create("TEST", new Number { value = 7 }));

        Assert.IsNotNull(received);
        Assert.AreEqual(0, received.from);
        Assert.AreEqual(7, received.Read<Number>().value);
    }

    [Test]
    public void Offline_SendToAll_DoesNotCrashAndDoesNotLoopBack()
    {
        GameNet.BeginOffline();
        int count = 0;
        GameNet.Instance.Subscribe("TEST", _ => count++);

        GameNet.Instance.SendToAll(NetMessage.Create("TEST"));

        Assert.AreEqual(0, count);
    }

    // ---------- 3 pemain (jaringan palsu) ----------

    [Test]
    public void ClientSendToHost_ReachesOnlyHost_WithSenderIndex()
    {
        var nets = new FakeNetwork().CreateGameNets();
        var got = new List<string>();
        nets[0].Subscribe("TEST", m => got.Add($"host dari {m.from}"));
        nets[1].Subscribe("TEST", m => got.Add("client1"));
        nets[2].Subscribe("TEST", m => got.Add("client2"));

        nets[2].SendToHost(NetMessage.Create("TEST"));

        CollectionAssert.AreEqual(new[] { "host dari 2" }, got);
    }

    [Test]
    public void HostSendToAll_ReachesBothClients_NotHost()
    {
        var nets = new FakeNetwork().CreateGameNets();
        var got = new List<string>();
        nets[0].Subscribe("TEST", m => got.Add("host"));
        nets[1].Subscribe("TEST", m => got.Add($"client1 dari {m.from}"));
        nets[2].Subscribe("TEST", m => got.Add($"client2 dari {m.from}"));

        nets[0].SendToAll(NetMessage.Create("TEST"));

        CollectionAssert.AreEquivalent(new[] { "client1 dari 0", "client2 dari 0" }, got);
    }

    [Test]
    public void HostSendTo_ReachesOnlyThatPlayer()
    {
        var nets = new FakeNetwork().CreateGameNets();
        var got = new List<string>();
        nets[1].Subscribe("TEST", m => got.Add("client1"));
        nets[2].Subscribe("TEST", m => got.Add("client2"));

        nets[0].SendTo(2, NetMessage.Create("TEST"));

        CollectionAssert.AreEqual(new[] { "client2" }, got);
    }

    [Test]
    public void Sender_CannotForgeFromIndex()
    {
        var nets = new FakeNetwork().CreateGameNets();
        int from = -99;
        nets[0].Subscribe("TEST", m => from = m.from);

        var forged = NetMessage.Create("TEST");
        forged.from = 0;   // client 1 mengaku sebagai host
        nets[1].SendToHost(forged);

        Assert.AreEqual(1, from);
    }

    [Test]
    public void Roles_AreSetByTransport()
    {
        var nets = new FakeNetwork().CreateGameNets();

        Assert.IsTrue(nets[0].IsHost);
        Assert.IsFalse(nets[1].IsHost);
        Assert.AreEqual(2, nets[2].LocalPlayerIndex);
        Assert.IsFalse(nets[0].IsOffline);
    }

    // ---------- Berlangganan ----------

    [Test]
    public void Handlers_OnlyGetTheirOwnMessageType()
    {
        GameNet.BeginOffline();
        int a = 0, b = 0;
        GameNet.Instance.Subscribe("A", _ => a++);
        GameNet.Instance.Subscribe("B", _ => b++);

        GameNet.Instance.SendToHost(NetMessage.Create("A"));

        Assert.AreEqual(1, a);
        Assert.AreEqual(0, b);
    }

    [Test]
    public void Unsubscribe_StopsDelivery()
    {
        GameNet.BeginOffline();
        int count = 0;
        Action<NetMessage> handler = _ => count++;
        GameNet.Instance.Subscribe("TEST", handler);

        GameNet.Instance.SendToHost(NetMessage.Create("TEST"));
        GameNet.Instance.Unsubscribe("TEST", handler);
        GameNet.Instance.SendToHost(NetMessage.Create("TEST"));

        Assert.AreEqual(1, count);
    }

    [Test]
    public void MessageWithNoSubscriber_IsIgnored()
    {
        GameNet.BeginOffline();

        Assert.DoesNotThrow(() => GameNet.Instance.SendToHost(NetMessage.Create("TIDAK_ADA_YANG_DENGAR")));
    }

    [Test]
    public void ThrowingHandler_DoesNotStopOtherHandlers()
    {
        GameNet.BeginOffline();
        bool secondRan = false;
        GameNet.Instance.Subscribe("TEST", _ => throw new InvalidOperationException("boom"));
        GameNet.Instance.Subscribe("TEST", _ => secondRan = true);

        LogAssert.Expect(LogType.Exception, new Regex("boom"));
        GameNet.Instance.SendToHost(NetMessage.Create("TEST"));

        Assert.IsTrue(secondRan);
    }

    // ---------- Data rusak ----------

    [Test]
    public void GarbageFromNetwork_IsIgnored_AndOthersStillWork()
    {
        var network = new FakeNetwork();
        var nets = network.CreateGameNets();
        int count = 0;
        nets[0].Subscribe("TEST", _ => count++);

        LogAssert.Expect(LogType.Warning, new Regex("tidak terbaca"));
        network.Nodes[0].InjectRaw(1, Encoding.UTF8.GetBytes("sampah"));
        nets[1].SendToHost(NetMessage.Create("TEST"));

        Assert.AreEqual(1, count);
    }
}
