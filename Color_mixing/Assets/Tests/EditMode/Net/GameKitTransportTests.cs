using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

// GameKitTransport dengan "match" palsu: semua kiriman sampai ke semua pemain lain (seperti GKMatch.Send ke semua).
public class GameKitTransportTests
{
    private class Room
    {
        public readonly List<string> names = new List<string> { "Host", "Ani", "Budi" };
        public readonly List<FakeLink> links = new List<FakeLink>();
        public bool deliverTwice;   // GameKit kadang mengirim pesan yang sama lewat dua event
    }

    private class FakeLink : IGameLink
    {
        private readonly Room room;
        private readonly int index;
        public FakeLink(Room room, int index) { this.room = room; this.index = index; LocalName = room.names[index]; room.links.Add(this); }

        public bool IsHost => index == 0;
        public string LocalName { get; }   // tetap, walau daftar lobby berubah
        public IReadOnlyList<string> CurrentRoster() => room.names;

        public event Action<string, byte[]> RawReceived;
        public event Action<string> PlayerGone;

        public void SendRaw(byte[] data)
        {
            foreach (var other in room.links)
            {
                if (other == this) continue;
                other.RawReceived?.Invoke(LocalName, data);
                if (room.deliverTwice) other.RawReceived?.Invoke(LocalName, data);
            }
        }

        public void Receive(string from, byte[] data) => RawReceived?.Invoke(from, data);
        public void Gone(string name) => PlayerGone?.Invoke(name);
    }

    private Room room;
    private GameKitTransport[] transports;
    private List<(int from, string type)>[] received;

    [SetUp]
    public void SetUp()
    {
        room = new Room();
        transports = new GameKitTransport[3];
        received = new List<(int, string)>[3];
        for (int i = 0; i < 3; i++)
        {
            int me = i;
            transports[i] = new GameKitTransport(new FakeLink(room, i));
            received[i] = new List<(int, string)>();
            transports[i].DataReceived += (from, data) =>
            {
                Assert.IsTrue(NetMessage.TryDecode(data, out var msg));
                received[me].Add((from, msg.type));
            };
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var t in transports) t.Dispose();
    }

    private static byte[] Msg(string type) => NetMessage.Create(type).Encode();

    // ---------- Pengalamatan ----------

    [Test]
    public void HostBroadcast_ReachesBothClients_WithHostIndex()
    {
        transports[0].SendToAll(Msg("A"));

        Assert.AreEqual(new[] { (0, "A") }, received[1]);
        Assert.AreEqual(new[] { (0, "A") }, received[2]);
        Assert.IsEmpty(received[0], "pengirim tidak menerima kirimannya sendiri");
    }

    [Test]
    public void ClientToHost_OnlyHostHearsIt_WithTheClientsIndex()
    {
        transports[1].SendToHost(Msg("B"));
        transports[2].SendToHost(Msg("C"));

        Assert.AreEqual(new[] { (1, "B"), (2, "C") }, received[0]);
        Assert.IsEmpty(received[1]);
        Assert.IsEmpty(received[2]);
    }

    [Test]
    public void HostSendTo_OnlyThatClientHearsIt()
    {
        transports[0].SendTo(2, Msg("D"));

        Assert.IsEmpty(received[1]);
        Assert.AreEqual(new[] { (0, "D") }, received[2]);
    }

    [Test]
    public void HostSendToHost_IsANoOp()
    {
        transports[0].SendToHost(Msg("E"));
        Assert.IsEmpty(received[1]);
        Assert.IsEmpty(received[2]);
    }

    [Test]
    public void ClientIgnoresFramesFromAnotherClient()
    {
        // Budi mengirim ke semua: host mendengarnya, tapi client lain (Ani) membuangnya.
        transports[2].SendToAll(Msg("F"));

        Assert.IsEmpty(received[1], "client hanya mendengar host");
        Assert.AreEqual(new[] { (2, "F") }, received[0]);
    }

    // ---------- Dobel dan lama ----------

    [Test]
    public void DuplicateDelivery_ReachesTheGameOnlyOnce()
    {
        room.deliverTwice = true;

        transports[0].SendToAll(Msg("G"));
        transports[1].SendToHost(Msg("H"));

        Assert.AreEqual(1, received[1].Count);
        Assert.AreEqual(1, received[2].Count);
        Assert.AreEqual(1, received[0].Count);
    }

    [Test]
    public void OldSequenceNumber_IsDropped()
    {
        var frames = new List<byte[]>();
        room.links[1].RawReceived += (_, data) => frames.Add(data);
        transports[0].SendToAll(Msg("I"));
        transports[0].SendToAll(Msg("J"));
        Assert.AreEqual(2, received[1].Count);

        room.links[1].Receive("Host", frames[0]);   // bingkai lama datang lagi
        room.links[1].Receive("Host", frames[1]);   // dan yang terakhir juga

        Assert.AreEqual(2, received[1].Count);
    }

    [Test]
    public void NewTransportSession_IsAcceptedEvenWithLowSequenceNumbers()
    {
        transports[0].SendToAll(Msg("K"));
        transports[0].SendToAll(Msg("L"));
        transports[0].Dispose();

        transports[0] = new GameKitTransport(room.links[0]);   // host membuka sesi baru, urutan mulai dari 1
        transports[0].SendToAll(Msg("M"));

        Assert.AreEqual(3, received[1].Count);
        Assert.AreEqual("M", received[1][2].type);
    }

    // ---------- Data yang bukan milik game ----------

    [Test]
    public void LobbyJsonAndGarbage_AreIgnored()
    {
        byte[] lobbyJson = Encoding.UTF8.GetBytes("{\"type\":\"HELLO\",\"name\":\"Host\"}");
        room.links[1].Receive("Host", lobbyJson);
        room.links[1].Receive("Host", new byte[0]);
        room.links[1].Receive("Host", new byte[] { GameKitTransport.FrameMarker, 0xFF, 1, 2 });   // header terpotong
        room.links[1].Receive("Host", null);

        Assert.IsEmpty(received[1]);
    }

    [Test]
    public void UnknownSender_IsDropped()
    {
        transports[0].SendToAll(Msg("N"));
        var frame = new byte[12];
        frame[0] = GameKitTransport.FrameMarker;
        frame[1] = 0xFF;
        frame[6] = 99;
        room.links[1].Receive("Orang Asing", frame);

        Assert.AreEqual(1, received[1].Count);
    }

    // ---------- Nomor pemain ----------

    [Test]
    public void LocalIndexAndCount_FollowTheRoster()
    {
        Assert.AreEqual(0, transports[0].LocalPlayerIndex);
        Assert.AreEqual(1, transports[1].LocalPlayerIndex);
        Assert.AreEqual(2, transports[2].LocalPlayerIndex);
        Assert.AreEqual(3, transports[0].PlayerCount);
        Assert.IsTrue(transports[0].IsHost);
        Assert.IsFalse(transports[1].IsHost);
    }

    [Test]
    public void Freeze_KeepsIndexesWhenTheLobbyChanges()
    {
        transports[2].Freeze(new[] { "Host", "Ani", "Budi" });
        room.names.RemoveAt(1);   // Ani keluar dari daftar lobby: Budi bergeser ke index 1 di daftar langsung

        Assert.AreEqual(2, transports[2].LocalPlayerIndex, "nomor pemain tetap selama game");
        Assert.AreEqual(3, transports[2].PlayerCount);

        transports[2].Unfreeze();
        Assert.AreEqual(1, transports[2].LocalPlayerIndex, "setelah game, daftar lobby berlaku lagi");
    }

    [Test]
    public void Freeze_WithoutNames_TakesTheCurrentLobby()
    {
        transports[0].Freeze();
        room.names.Add("Cici");

        Assert.AreEqual(3, transports[0].Roster.Count);
    }

    // ---------- Pemain terputus ----------

    [Test]
    public void PlayerGone_OnHost_ReportsTheClientIndex_OnlyDuringAGame()
    {
        var lost = new List<int>();
        transports[0].PeerLost += lost.Add;

        room.links[0].Gone("Ani");          // di lobby: bukan urusan game
        Assert.IsEmpty(lost);

        transports[0].Freeze();
        room.links[0].Gone("Ani");
        CollectionAssert.AreEqual(new[] { 1 }, lost);
    }

    [Test]
    public void PlayerGone_OnClient_ReportsOnlyTheHost()
    {
        var lost = new List<int>();
        transports[1].PeerLost += lost.Add;
        transports[1].Freeze();

        room.links[1].Gone("Budi");   // client lain: host yang akan memberi tahu
        Assert.IsEmpty(lost);

        room.links[1].Gone("Host");
        CollectionAssert.AreEqual(new[] { 0 }, lost);
    }

    [Test]
    public void PlayerGone_ForUnknownNameOrMyself_IsIgnored()
    {
        var lost = new List<int>();
        transports[0].PeerLost += lost.Add;
        transports[0].Freeze();

        room.links[0].Gone("Orang Asing");
        room.links[0].Gone("Host");

        Assert.IsEmpty(lost);
    }

    // ---------- Bersama GameNet ----------

    [Test]
    public void GameNet_WorksOverTheTransport()
    {
        var hostNet = new GameNet(transports[0]);
        var clientNet = new GameNet(transports[1]);
        NetMessage got = null;
        clientNet.Subscribe(NetMessageType.Start, m => got = m);

        hostNet.SendToAll(NetMessage.Create(NetMessageType.Start));

        Assert.IsNotNull(got);
        Assert.AreEqual(0, got.from);
        Assert.IsFalse(clientNet.IsHost);
        Assert.AreEqual(1, clientNet.LocalPlayerIndex);
    }
}
