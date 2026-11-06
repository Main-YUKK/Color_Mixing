using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Foto hari dari host dikirim lewat jaringan yang buruk: hilang, dobel, terbalik urutan, terlambat.
// Client harus tetap berakhir di keadaan yang benar dan tidak pernah mundur.
public class BadNetworkTests
{
    private GameObject hostObject, clientObject;
    private DayCycleManager host, client;
    private readonly List<DaySnapshot> sent = new List<DaySnapshot>();
    private readonly ClientRequest request = TestHelpers.Request("R1", RequestDifficulty.LevelOne);
    private readonly List<int> livesSeen = new List<int>();
    private int requested, gameOvers;

    [SetUp]
    public void SetUp()
    {
        GameNet.End();
        hostObject = new GameObject("host");
        host = hostObject.AddComponent<DayCycleManager>();
        clientObject = new GameObject("client");
        client = clientObject.AddComponent<DayCycleManager>();

        sent.Clear();
        livesSeen.Clear();
        requested = gameOvers = 0;
        GameSession.CurrentRequest = request;
        host.OnClientRequested += () => GameSession.CurrentRequest = request;
        host.OnStatePublished += kind => sent.Add(Wire(host.TakeSnapshot(kind)));
    }

    [TearDown]
    public void TearDown()
    {
        GameNet.End();
        Object.DestroyImmediate(hostObject);
        Object.DestroyImmediate(clientObject);
    }

    private static DaySnapshot Wire(DaySnapshot s) => JsonUtility.FromJson<DaySnapshot>(JsonUtility.ToJson(s));

    // s0: hari mulai (nyawa 3) · s1: salah (2) · s2: timeout (1) · s3: salah lagi = game over
    private void PlayHostThroughGameOver()
    {
        host.StartSession(false);
        host.OnFeedbackRevealed(false);
        host.OnClientTimeout();
        host.OnFeedbackRevealed(false);
        Assert.AreEqual(4, sent.Count);
    }

    private void StartListening()
    {
        GameNet.Begin(new FakeNetwork().Nodes[1]);   // mulai dari sini manager ini adalah client
        client.OnLivesChanged += l => livesSeen.Add(l);
        client.OnClientRequested += () => requested++;
        client.OnGameOver += () => gameOvers++;
    }

    private void Deliver(params int[] order)
    {
        foreach (int i in order) client.ApplyRemote(sent[i], request);
    }

    [Test]
    public void InOrder_Baseline()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(0, 1, 2, 3);

        Assert.AreEqual(new[] { 3, 2, 1, 0 }, livesSeen);
        Assert.AreEqual(3, requested);
        Assert.AreEqual(1, gameOvers);
    }

    [Test]
    public void Duplicates_AreIgnored()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(0, 0, 1, 1, 1, 2, 3, 3);

        Assert.AreEqual(new[] { 3, 2, 1, 0 }, livesSeen);
        Assert.AreEqual(3, requested);
        Assert.AreEqual(1, gameOvers);
    }

    [Test]
    public void Reordered_LateSnapshotNeverMovesTheClientBackwards()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(0, 2, 1);   // s1 datang setelah s2

        Assert.AreEqual(1, client.Lives, "tetap di keadaan terbaru");
        Assert.AreEqual(new[] { 3, 1 }, livesSeen);
    }

    [Test]
    public void FullyReversed_EndsInTheLatestState()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(3, 2, 1, 0);

        Assert.AreEqual(0, client.Lives);
        Assert.IsFalse(client.IsRunning);
        Assert.AreEqual(1, gameOvers);
        Assert.AreEqual(0, requested, "snapshot lama tidak memunculkan klien lagi");
    }

    [Test]
    public void DroppedSnapshot_NextOneStillBringsTheClientUpToDate()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(0, 2);   // s1 hilang

        Assert.AreEqual(1, client.Lives);
        Assert.AreEqual(new[] { 3, 1 }, livesSeen);
        Assert.AreEqual(1, client.TimeoutCount);
        Assert.AreEqual(1, client.NoCount, "hitungan salah dari s1 ikut terbawa di s2");
    }

    [Test]
    public void OnlyGameOverArrives_ClientStillEnds()
    {
        PlayHostThroughGameOver();
        StartListening();

        Deliver(3);   // semua foto sebelumnya hilang, termasuk awal run

        Assert.IsFalse(client.IsRunning);
        Assert.AreEqual(1, gameOvers);
        Assert.AreEqual(0, client.Lives);
    }

    [Test]
    public void LateSnapshotFromAnOlderRun_IsIgnored()
    {
        host.StartSession(false);                // run A
        var runA = sent[0];
        host.StartSession(false);                // host mulai ulang: run B
        var runB = sent[1];
        Assert.Greater(runB.runId, runA.runId);

        StartListening();
        client.ApplyRemote(runB, request);
        livesSeen.Clear();

        bool applied = client.ApplyRemote(runA, request);   // kiriman run A datang terlambat

        Assert.IsFalse(applied);
        Assert.IsEmpty(livesSeen);
        Assert.IsTrue(client.IsRunning);
    }

    [Test]
    public void NewRun_ResetsSequenceNumbers()
    {
        host.StartSession(false);
        host.OnFeedbackRevealed(false);
        host.StartSession(false);   // seq mulai dari 1 lagi, runId lebih besar
        StartListening();

        Deliver(0, 1);               // run pertama
        Assert.IsTrue(client.ApplyRemote(sent[2], request), "seq 1 dari run baru harus diterima");
    }

    // ---------- Jaringan palsu: pesan GameNet sampai dobel ----------

    [Test]
    public void GameNet_DeliversDuplicateMessagesTwice_SoHandlersMustTolerateThem()
    {
        var network = new FakeNetwork();
        var nets = network.CreateGameNets();
        int received = 0;
        nets[1].Subscribe(NetMessageType.Ping, _ => received++);

        byte[] data = NetMessage.Create(NetMessageType.Ping).Encode();
        network.Nodes[1].InjectRaw(0, data);
        network.Nodes[1].InjectRaw(0, data);

        Assert.AreEqual(2, received, "GameNet tidak membuang dobel; yang menangani dobel adalah aturan di tiap pesan (seq / state)");
    }
}
