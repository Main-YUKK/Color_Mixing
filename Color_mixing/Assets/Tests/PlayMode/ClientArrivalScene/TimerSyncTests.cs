using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Timer klien: host satu-satunya jam. Client tidak mulai sendiri, tapi mengikuti TIMER_STATE dari host.
public class TimerSyncTests
{
    [Serializable] private class TimerData { public int stamp; public bool running; public float remaining; }   // bentuk sama dengan ClientTimer.TimerData

    private class Transport : INetTransport
    {
        public readonly List<NetMessage> Sent = new List<NetMessage>();
        public Transport(int index) { LocalPlayerIndex = index; }
        public bool IsHost => LocalPlayerIndex == 0;
        public int LocalPlayerIndex { get; }
        public int PlayerCount => PigmentAssignment.PlayerCount;
        public event Action<int, byte[]> DataReceived;
#pragma warning disable CS0067
        public event Action<int> PeerLost;
#pragma warning restore CS0067
        public void SendToHost(byte[] data) => Record(data);
        public void SendToAll(byte[] data) => Record(data);
        public void SendTo(int playerIndex, byte[] data) => Record(data);
        private void Record(byte[] data) { if (NetMessage.TryDecode(data, out var m)) Sent.Add(m); }
        public void Receive(int from, NetMessage m) => DataReceived?.Invoke(from, m.Encode());
    }

    private const float Seconds = 10f;
    private GameObject cycleGo, timerGo;
    private DayCycleManager cycle;
    private ClientTimer timer;
    private Transport transport;

    private void Setup(int playerIndex)
    {
        transport = new Transport(playerIndex);
        GameNet.Begin(transport);
        cycleGo = new GameObject("Cycle");
        cycle = cycleGo.AddComponent<DayCycleManager>();
        if (playerIndex == 0) cycle.StartSession(false);

        timerGo = new GameObject("Timer");
        timerGo.SetActive(false);   // OnEnable jalan setelah field terisi
        timer = timerGo.AddComponent<ClientTimer>();
        PlayTestHelpers.SetField(timer, "cycle", cycle);
        PlayTestHelpers.SetField(timer, "startManually", true);
        PlayTestHelpers.SetField(timer, "secondsPerClient", Seconds);
        timerGo.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.Destroy(timerGo);
        UnityEngine.Object.Destroy(cycleGo);
        GameNet.BeginOffline();   // seperti Play di Editor tanpa lobby
        GameSession.StartRun(1);
    }

    private void HostSays(int stamp, float remaining) =>
        transport.Receive(0, NetMessage.Create(NetMessageType.TimerState, new TimerData { stamp = stamp, running = true, remaining = remaining }));

    [Test]
    public void Host_StartTimer_TellsClientsTheTimeLeft()
    {
        Setup(0);
        timer.StartTimer();

        var sent = transport.Sent.FindLast(m => m.type == NetMessageType.TimerState);
        Assert.IsNotNull(sent, "host mengirim TIMER_STATE saat timer mulai");
        var data = sent.Read<TimerData>();
        Assert.IsTrue(data.running);
        Assert.AreEqual(Seconds, data.remaining, 0.001f);
        Assert.AreEqual(cycle.SyncStamp, data.stamp);
    }

    [Test]
    public void Client_DoesNotStartOnItsOwn()
    {
        Setup(1);
        timer.StartTimer();   // mis. dari ColorMixingController.Start di client
        Assert.IsFalse(timer.IsRunning);
        Assert.IsFalse(timer.IsShown);
    }

    [Test]
    public void Client_StartsAndFollowsTheHost()
    {
        Setup(1);
        HostSays(cycle.SyncStamp, 6f);
        Assert.IsTrue(timer.IsRunning, "mulai saat host bilang mulai");
        Assert.IsTrue(timer.IsShown);
        Assert.AreEqual(6f, timer.SecondsLeft, 0.001f, "masuk di tengah jalan: ikut sisa waktu host");
        Assert.IsFalse(timer.InIntro);

        HostSays(cycle.SyncStamp, 5.9f);
        Assert.AreEqual(6f, timer.SecondsLeft, 0.001f, "beda kecil tidak dikoreksi (bar tidak bergetar)");

        HostSays(cycle.SyncStamp, 3f);
        Assert.AreEqual(3f, timer.SecondsLeft, 0.001f, "meleset jauh: dikoreksi ke angka host");
    }

    [Test]
    public void Client_IgnoresOtherClientsTimer_AndDoesNotRestartAfterStop()
    {
        Setup(1);
        HostSays(cycle.SyncStamp + 5, 8f);
        Assert.IsFalse(timer.IsRunning, "timer milik klien lain diabaikan");

        HostSays(cycle.SyncStamp, 8f);
        timer.Stop();   // mis. Apply menang
        HostSays(cycle.SyncStamp, 7f);   // kiriman terlambat
        Assert.IsFalse(timer.IsRunning, "tidak jalan lagi setelah Stop");
    }
}
