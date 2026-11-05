using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Host menghasilkan foto keadaan hari, client memutar ulang event yang sama.
public class DayCycleSyncTests
{
    private GameObject hostObject, clientObject;
    private DayCycleManager host, client;
    private readonly List<DaySnapshot> sent = new List<DaySnapshot>();
    private readonly ClientRequest request = TestHelpers.Request("R1", RequestDifficulty.LevelOne);

    [SetUp]
    public void SetUp()
    {
        GameNet.End();   // tanpa sesi = pemegang aturan
        hostObject = new GameObject("host");
        host = hostObject.AddComponent<DayCycleManager>();
        clientObject = new GameObject("client");
        client = clientObject.AddComponent<DayCycleManager>();

        sent.Clear();
        GameSession.CurrentRequest = request;
        GameSession.CurrentLook = null;
        host.OnClientRequested += () => GameSession.CurrentRequest = request;   // seperti ClientRequestController
        host.OnStatePublished += kind => sent.Add(host.TakeSnapshot(kind));
    }

    [TearDown]
    public void TearDown()
    {
        GameNet.End();
        Object.DestroyImmediate(hostObject);
        Object.DestroyImmediate(clientObject);
    }

    // Setelah host menghasilkan foto, jadikan manager "client".
    private void BecomeClient()
    {
        var network = new FakeNetwork();
        GameNet.Begin(network.Nodes[1]);
    }

    [Test]
    public void StartSession_PublishesClientRequestedWithNewDay()
    {
        host.StartSession(false);

        Assert.AreEqual(1, sent.Count);
        Assert.AreEqual((int)DaySnapshotKind.ClientRequested, sent[0].kind);
        Assert.AreEqual(1, sent[0].seq);
        Assert.IsTrue(sent[0].newDay);
        Assert.AreEqual(GameRules.StartingLives, sent[0].lives);
        Assert.AreEqual("R1", sent[0].requestId);
    }

    [Test]
    public void Client_ReplaysDayStartedLivesAndRequest()
    {
        host.StartSession(false);
        var snapshot = sent[0];
        BecomeClient();

        int day = 0, lives = -1, happy = -1, requested = 0;
        client.OnDayStarted += d => day = d;
        client.OnLivesChanged += l => lives = l;
        client.OnHappyClientChanged += y => happy = y;
        client.OnClientRequested += () => requested++;

        Assert.IsTrue(client.ApplyRemote(snapshot, request));

        Assert.AreEqual(1, day);
        Assert.AreEqual(GameRules.StartingLives, lives);
        Assert.AreEqual(0, happy);
        Assert.AreEqual(1, requested);
        Assert.AreSame(request, GameSession.CurrentRequest);
        Assert.IsTrue(client.IsRunning);
    }

    [Test]
    public void Client_SeesLostLifeOnNextRequest()
    {
        host.StartSession(false);
        host.OnFeedbackRevealed(false);
        var second = sent[1];
        Assert.AreEqual(GameRules.StartingLives - 1, second.lives);
        Assert.IsFalse(second.newDay);

        BecomeClient();
        client.ApplyRemote(sent[0], request);
        int lives = -1;
        client.OnLivesChanged += l => lives = l;

        client.ApplyRemote(second, request);

        Assert.AreEqual(GameRules.StartingLives - 1, lives);
    }

    [Test]
    public void DuplicateAndOldSnapshots_AreIgnored()
    {
        host.StartSession(false);
        host.OnFeedbackRevealed(false);
        BecomeClient();

        Assert.IsTrue(client.ApplyRemote(sent[0], request));
        Assert.IsTrue(client.ApplyRemote(sent[1], request));
        Assert.IsFalse(client.ApplyRemote(sent[1], request), "dobel");
        Assert.IsFalse(client.ApplyRemote(sent[0], request), "lama");
    }

    [Test]
    public void ThreeWrongAnswers_ClientGetsGameOver()
    {
        host.StartSession(false);
        for (int i = 0; i < GameRules.StartingLives; i++) host.OnFeedbackRevealed(false);
        Assert.AreEqual((int)DaySnapshotKind.GameOver, sent[sent.Count - 1].kind);

        BecomeClient();
        bool gameOver = false;
        client.OnGameOver += () => gameOver = true;
        foreach (var s in sent) client.ApplyRemote(s, request);

        Assert.IsTrue(gameOver);
        Assert.IsFalse(client.IsRunning);
        Assert.AreEqual(0, client.Lives);
    }

    [Test]
    public void Client_ReplaysDayEnded()
    {
        BecomeClient();
        client.holdForReport = true;
        DayResult? ended = null;
        client.OnDayEnded += r => ended = r;

        client.ApplyRemote(new DaySnapshot { kind = (int)DaySnapshotKind.DayEnded, runId = 7, seq = 5, day = 2, lives = 2, yes = 3, no = 1 }, null);

        Assert.IsTrue(ended.HasValue);
        Assert.AreEqual(2, ended.Value.day);
        Assert.AreEqual(3, ended.Value.yesCount);
        Assert.AreEqual(3, client.CurrentDay);
        Assert.IsTrue(client.AwaitingNextDay);
    }

    [Test]
    public void Client_CannotDriveTheDay()
    {
        BecomeClient();
        client.StartSession(false);
        client.OnFeedbackRevealed(false);
        client.OnClientTimeout();

        Assert.IsFalse(client.IsRunning);
        Assert.AreEqual(0, client.NoCount);
        Assert.AreEqual(0, client.TimeoutCount);
    }

    [Test]
    public void Host_RejectsRemoteSnapshots()
    {
        Assert.IsFalse(host.ApplyRemote(new DaySnapshot { runId = 7, seq = 1 }, null));
    }

    [Test]
    public void Look_TravelsFromHostToClient()
    {
        var look = new ClientLook { valid = true, prefabIndex = 2, hasHair = true, hair = new HairLook { style = 3, variant = 1, front = true, color = Color.red } };
        host.OnClientRequested += () => GameSession.CurrentLook = look;   // seperti CharacterSpawner
        host.StartSession(false);
        var json = JsonUtility.ToJson(sent[0]);   // lewat jaringan = lewat JSON
        BecomeClient();

        client.ApplyRemote(JsonUtility.FromJson<DaySnapshot>(json), request);

        var got = GameSession.CurrentLook;
        Assert.IsNotNull(got);
        Assert.AreEqual(2, got.prefabIndex);
        Assert.AreEqual(3, got.hair.style);
        Assert.AreEqual(1, got.hair.variant);
        Assert.IsTrue(got.hair.front);
        Assert.AreEqual(Color.red, got.hair.color);
    }

    [Test]
    public void MissingLook_BecomesNullOnClient()
    {
        host.StartSession(false);   // tidak ada yang memasang CurrentLook
        var json = JsonUtility.ToJson(sent[0]);
        BecomeClient();

        client.ApplyRemote(JsonUtility.FromJson<DaySnapshot>(json), request);

        Assert.IsNull(GameSession.CurrentLook);
    }
}
