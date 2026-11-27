using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Bug: client yang lanjut ke mixing duluan tidak bisa menuang selama host masih membaca kartu request,
// karena keadaan mixing host dulu hanya ada di scene mixing host. Sekarang MixNet host menyimpannya sejak klien datang.
public class MixNetHostTests
{
    [Serializable] private class AddData { public int slot; public int amount; }   // bentuk sama dengan MixNet.AddData

    private class HostTransport : INetTransport
    {
        public readonly List<NetMessage> Sent = new List<NetMessage>();
        public bool IsHost => true;
        public int LocalPlayerIndex => 0;
        public int PlayerCount => PigmentAssignment.PlayerCount;
        public event Action<int, byte[]> DataReceived;
#pragma warning disable CS0067
        public event Action<int> PeerLost;
#pragma warning restore CS0067
        public void SendToHost(byte[] data) => DataReceived?.Invoke(0, data);
        public void SendToAll(byte[] data) { if (NetMessage.TryDecode(data, out var m)) Sent.Add(m); }
        public void SendTo(int playerIndex, byte[] data) => SendToAll(data);
        public void Receive(int from, NetMessage m) => DataReceived?.Invoke(from, m.Encode());

        public MixSnapshot LastMix()
        {
            for (int i = Sent.Count - 1; i >= 0; i--)
                if (Sent[i].type == NetMessageType.MixState) return Sent[i].Read<MixSnapshot>();
            return null;
        }
    }

    private GameObject dayObject;
    private DayCycleManager day;
    private HostTransport host;

    [SetUp]
    public void SetUp()
    {
        Assert.IsNotNull(MixNet.Instance);
        host = new HostTransport();
        GameNet.Begin(host);
        dayObject = new GameObject("day");
        day = dayObject.AddComponent<DayCycleManager>();
        day.StartSession(false);   // klien pertama datang, host masih di kartu
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.Destroy(dayObject);
        GameNet.BeginOffline();   // seperti Play di Editor tanpa lobby
    }

    [UnityTest]
    public IEnumerator ClientAdd_WhileHostStillReadingCard_ReachesTheBowl()
    {
        yield return null;
        Assert.IsNull(ColorMixingController.Current, "scene mixing host belum dimuat");
        var first = host.LastMix();
        Assert.IsNotNull(first, "host langsung mengirim foto: client yang lanjut duluan jadi synced");
        Assert.AreEqual(0, Sum(first.units));

        host.Receive(1, NetMessage.Create(NetMessageType.MixAdd, new AddData { slot = 0, amount = 2 }));
        yield return null;

        var after = host.LastMix();
        Assert.AreEqual(2, Sum(after.units));
        Assert.AreEqual(2, after.units[after.slots[2]], "pigmen slot 0 milik pemain 1");
        Assert.AreEqual(2, after.lastPourers[after.slots[2]], "penuang = pemain 1 slot 0");
        Assert.AreEqual(PigmentAssignment.RotationSeconds, after.rotationLeft, 1f, "rotasi sudah jalan sejak klien datang");
    }

    [UnityTest]
    public IEnumerator ClientReady_WhileHostStillReadingCard_IsKept_ButNotConfirmed()
    {
        yield return null;
        host.Receive(1, NetMessage.Create(NetMessageType.MixAdd, new AddData { slot = 0, amount = 1 }));
        host.Receive(1, NetMessage.Create(NetMessageType.MixReady, new ReadyPayload { ready = true }));
        yield return null;

        var s = host.LastMix();
        Assert.IsTrue(s.ready[1]);
        Assert.IsFalse(s.confirmed, "host (pemain 0) belum Mix");
    }

    [UnityTest]
    public IEnumerator NewClient_StartsWithEmptyBowlAndNoMix()
    {
        yield return null;
        host.Receive(1, NetMessage.Create(NetMessageType.MixAdd, new AddData { slot = 0, amount = 3 }));
        host.Receive(1, NetMessage.Create(NetMessageType.MixReady, new ReadyPayload { ready = true }));
        yield return null;
        Assert.AreEqual(3, Sum(host.LastMix().units));

        day.OnClientTimeout();   // waktu habis -> klien baru (tanpa DayCycleNet: SyncStamp tidak naik)
        yield return null;   // Update: keadaan baru dibuat
        yield return null;   // LateUpdate frame itu sudah mengirim fotonya

        var s = host.LastMix();
        Assert.AreEqual(0, Sum(s.units), "klien baru: mangkuk kosong");
        Assert.IsFalse(s.ready[1], "Mix klien lama tidak terbawa");
        Assert.AreEqual(0, Sum(s.votes));
    }

    [Serializable] private class ReadyPayload { public bool ready; }

    private static int Sum(int[] units) { int n = 0; foreach (int u in units) n += u; return n; }
}
