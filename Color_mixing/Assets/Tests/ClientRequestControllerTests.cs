using NUnit.Framework;
using TMPro;
using UnityEngine;

public class ClientRequestControllerTests
{
    private const RequestDifficulty L1 = RequestDifficulty.LevelOne;
    private const RequestDifficulty L2 = RequestDifficulty.LevelTwo;
    private const RequestDifficulty L3 = RequestDifficulty.LevelThree;

    private GameObject go;
    private GameObject cardGo;
    private ClientRequestDatabase db;
    private DaySchedule schedule;
    private TextMeshPro text;
    private ClientRequestController controller;

    [SetUp]
    public void SetUp()
    {
        db = TestHelpers.Database(
            TestHelpers.Request("a1", L1, "desc a1"), TestHelpers.Request("a2", L1, "desc a2"),
            TestHelpers.Request("b1", L2, "desc b1"),
            TestHelpers.Request("c1", L3, "desc c1"));
        schedule = TestHelpers.Schedule(0,
            new[] { L1, L1, L2 },   // hari 1
            new[] { L3, L2, L1 });  // hari 2

        // Card dengan teks
        cardGo = new GameObject("Card");
        text = cardGo.AddComponent<TextMeshPro>();
        var card = cardGo.AddComponent<ClientRequestCardView>();
        TestHelpers.SetField(card, "descriptionText", text);

        // Nonaktif dulu supaya OnEnable (butuh DayCycleManager) tidak jalan; method dipanggil langsung.
        go = new GameObject("Controller");
        go.SetActive(false);
        controller = go.AddComponent<ClientRequestController>();
        TestHelpers.SetField(controller, "database", db);
        TestHelpers.SetField(controller, "schedule", schedule);
        TestHelpers.SetField(controller, "card", card);

        GameSession.Reset(db);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(cardGo);
        Object.DestroyImmediate(db);
        Object.DestroyImmediate(schedule);
        GameSession.StartRun(1);
    }

    private void ShowClient() => TestHelpers.Invoke(controller, "ShowCurrentClient");

    [Test]
    public void ShowCurrentClient_PicksRequestFromScheduledLevel()
    {
        // Given day 1, client slot 2 is scheduled as level two
        GameSession.Day = 1;
        GameSession.ClientIndex = 2;

        ShowClient();

        Assert.AreEqual(L2, GameSession.CurrentRequest.difficulty);
        Assert.AreEqual("b1", GameSession.CurrentRequest.requestId);
    }

    [Test]
    public void ShowCurrentClient_FollowsDayOfSession()
    {
        // Given day 2, client slot 0 is scheduled as level three
        GameSession.Day = 2;
        GameSession.ClientIndex = 0;

        ShowClient();

        Assert.AreEqual(L3, GameSession.CurrentRequest.difficulty);
    }

    [Test]
    public void ShowCurrentClient_ShowsDescriptionOnCard()
    {
        GameSession.Day = 2;
        GameSession.ClientIndex = 0;

        ShowClient();

        Assert.AreEqual("desc c1", text.text);
    }

    [Test]
    public void ShowCurrentClient_SameSlotTwice_GivesDifferentRequest()
    {
        // Klien gagal (No): slot sama, request harus beda selama bag belum habis
        GameSession.Day = 1;
        GameSession.ClientIndex = 0;

        ShowClient();
        string first = GameSession.CurrentRequest.requestId;
        ShowClient();

        Assert.AreNotEqual(first, GameSession.CurrentRequest.requestId);
    }

    [Test]
    public void ShowCurrentClient_CreatesBagWhenMissing()
    {
        GameSession.StartRun(1);    // bag = null
        Assert.IsNull(GameSession.Bag);

        ShowClient();

        Assert.IsNotNull(GameSession.Bag);
        Assert.IsNotNull(GameSession.CurrentRequest);
    }
}
