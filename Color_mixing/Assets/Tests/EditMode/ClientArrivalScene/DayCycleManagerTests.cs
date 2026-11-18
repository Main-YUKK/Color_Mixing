using NUnit.Framework;
using UnityEngine;

public class DayCycleManagerTests
{
    private GameObject go;
    private DayCycleManager cycle;
    private SaveFile save;

    private int clientRequested;
    private int gameOver;
    private int dayEnded;
    private DayResult lastResult;
    private int lastLives;
    private int lastHappy;

    [SetUp]
    public void SetUp()
    {
        // Save sementara supaya SaveService tidak membuat file acak di persistentDataPath.
        save = new SaveFile { id = "unit-test-save", roomName = "Test", nextDay = 1 };
        SaveService.SetActive(save);

        go = new GameObject("DayCycleManager");
        cycle = go.AddComponent<DayCycleManager>();

        clientRequested = 0;
        gameOver = 0;
        dayEnded = 0;
        lastLives = -1;
        lastHappy = -1;
        cycle.OnClientRequested += () => clientRequested++;
        cycle.OnGameOver += () => gameOver++;
        cycle.OnDayEnded += r => { dayEnded++; lastResult = r; };
        cycle.OnLivesChanged += l => lastLives = l;
        cycle.OnHappyClientChanged += y => lastHappy = y;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        SaveService.Delete(save);   // hapus file test + ClearActive
    }

    private void Yes(int n = 1) { for (int i = 0; i < n; i++) cycle.OnFeedbackRevealed(true); }
    private void No(int n = 1) { for (int i = 0; i < n; i++) cycle.OnFeedbackRevealed(false); }

    // ---------- Mulai sesi ----------

    [Test]
    public void StartSession_NewGame_StartsDayOneWithFullLives()
    {
        // Given a new game
        // When the session starts
        cycle.StartSession(false);

        // Then day 1 starts with 3 lives and no results
        Assert.AreEqual(1, cycle.CurrentDay);
        Assert.AreEqual(GameRules.StartingLives, cycle.Lives);
        Assert.AreEqual(0, cycle.YesCount);
        Assert.IsTrue(cycle.IsRunning);
        Assert.AreEqual(DayState.Mixing, cycle.State);
    }

    [Test]
    public void StartSession_Continue_UsesSavedNextDay()
    {
        // Given a save whose next day is 5
        save.nextDay = 5;

        // When continuing
        cycle.StartSession(true);

        // Then the run starts at day 5
        Assert.AreEqual(5, cycle.CurrentDay);
        Assert.AreEqual(5, GameSession.Day);
    }

    [Test]
    public void StartSession_RequestsFirstClientAndUpdatesHud()
    {
        cycle.StartSession(false);

        Assert.AreEqual(1, clientRequested);
        Assert.AreEqual(GameRules.StartingLives, lastLives);
        Assert.AreEqual(0, lastHappy);
        Assert.AreEqual(0, GameSession.ClientIndex);
    }

    // ---------- Yes ----------

    [Test]
    public void Yes_IncreasesHappyClientsAndRequestsNextClient()
    {
        cycle.StartSession(false);

        Yes();

        Assert.AreEqual(1, cycle.YesCount);
        Assert.AreEqual(1, lastHappy);
        Assert.AreEqual(2, clientRequested);
        Assert.AreEqual(GameRules.StartingLives, cycle.Lives);
    }

    [Test]
    public void Yes_ClientIndexFollowsYesCount()
    {
        cycle.StartSession(false);

        Yes(2);

        Assert.AreEqual(2, GameSession.ClientIndex);
    }

    [Test]
    public void No_KeepsClientIndexSoSameSlotGetsNewRequest()
    {
        cycle.StartSession(false);
        Yes();

        No();

        Assert.AreEqual(1, GameSession.ClientIndex);
    }

    // ---------- No ----------

    [Test]
    public void No_LosesOneLifeAndRequestsNextClient()
    {
        cycle.StartSession(false);

        No();

        Assert.AreEqual(2, cycle.Lives);
        Assert.AreEqual(2, lastLives);
        Assert.AreEqual(1, cycle.NoCount);
        Assert.AreEqual(2, clientRequested);
    }

    // ---------- Timeout ----------

    [Test]
    public void Timeout_LosesOneLifeAndCountsTimeout()
    {
        cycle.StartSession(false);

        cycle.OnClientTimeout();

        Assert.AreEqual(2, cycle.Lives);
        Assert.AreEqual(1, cycle.TimeoutCount);
        Assert.AreEqual(0, cycle.NoCount);
        Assert.AreEqual(2, clientRequested);
    }

    // ---------- Game over ----------

    [Test]
    public void LastLifeLost_TriggersGameOver()
    {
        cycle.StartSession(false);

        No(GameRules.StartingLives);

        Assert.AreEqual(0, cycle.Lives);
        Assert.AreEqual(1, gameOver);
        Assert.IsFalse(cycle.IsRunning);
        Assert.AreEqual(DayState.GameOver, cycle.State);
    }

    [Test]
    public void GameOver_DoesNotRequestAnotherClient()
    {
        cycle.StartSession(false);
        int before = clientRequested;

        No(GameRules.StartingLives);

        // 2 No pertama meminta klien baru, No ke-3 (game over) tidak.
        Assert.AreEqual(before + GameRules.StartingLives - 1, clientRequested);
    }

    [Test]
    public void MixedNoAndTimeout_AlsoEndsGame()
    {
        cycle.StartSession(false);

        No();
        cycle.OnClientTimeout();
        cycle.OnClientTimeout();

        Assert.AreEqual(1, gameOver);
        Assert.AreEqual(DayState.GameOver, cycle.State);
    }

    [Test]
    public void AfterGameOver_InputIsIgnored()
    {
        cycle.StartSession(false);
        No(GameRules.StartingLives);

        Yes();
        cycle.OnClientTimeout();

        Assert.AreEqual(0, cycle.YesCount);
        Assert.AreEqual(0, cycle.Lives);
        Assert.AreEqual(1, gameOver);
    }

    [Test]
    public void GameOver_ResetsSavedProgressToDayOne()
    {
        save.nextDay = 4;
        save.completedDay = 3;
        cycle.StartSession(true);

        No(GameRules.StartingLives);

        Assert.AreEqual(1, save.nextDay);
        Assert.AreEqual(0, save.completedDay);
    }

    // ---------- Hari selesai ----------

    [Test]
    public void ThreeYes_EndsDayWithCorrectResult()
    {
        cycle.StartSession(false);
        No();                       // 1 No sebelum menang

        Yes(GameRules.YesToWinDay);

        Assert.AreEqual(1, dayEnded);
        Assert.AreEqual(1, lastResult.day);
        Assert.AreEqual(GameRules.YesToWinDay, lastResult.yesCount);
        Assert.AreEqual(1, lastResult.noCount);
        Assert.AreEqual(0, lastResult.timeoutCount);
        Assert.AreEqual(2, lastResult.livesLeft);
        Assert.AreEqual(1, cycle.History.Count);
    }

    [Test]
    public void DayEnd_SavesProgress()
    {
        cycle.StartSession(false);

        Yes(GameRules.YesToWinDay);

        Assert.AreEqual(1, save.completedDay);
        Assert.AreEqual(2, save.nextDay);
    }

    [Test]
    public void DayEnd_WithoutHold_StartsNextDayImmediately()
    {
        cycle.holdForReport = false;
        cycle.StartSession(false);
        No();

        Yes(GameRules.YesToWinDay);

        Assert.AreEqual(2, cycle.CurrentDay);
        Assert.AreEqual(GameRules.StartingLives, cycle.Lives);
        Assert.AreEqual(0, cycle.YesCount);
        Assert.AreEqual(0, cycle.NoCount);
        Assert.IsFalse(cycle.AwaitingNextDay);
        Assert.AreEqual(DayState.Mixing, cycle.State);
    }

    [Test]
    public void DayEnd_WithHold_WaitsForStartNextDay()
    {
        cycle.holdForReport = true;
        cycle.StartSession(false);
        int requestedBefore = clientRequested;

        Yes(GameRules.YesToWinDay);

        Assert.IsTrue(cycle.AwaitingNextDay);
        Assert.AreEqual(DayState.DayEnd, cycle.State);
        Assert.AreEqual(requestedBefore + GameRules.YesToWinDay - 1, clientRequested);
    }

    [Test]
    public void DayEnd_WithHold_IgnoresFeedbackAndTimeoutWhileWaiting()
    {
        cycle.holdForReport = true;
        cycle.StartSession(false);
        Yes(GameRules.YesToWinDay);
        int lives = cycle.Lives;

        Yes();
        No();
        cycle.OnClientTimeout();

        Assert.AreEqual(GameRules.YesToWinDay, cycle.YesCount);
        Assert.AreEqual(lives, cycle.Lives);
    }

    [Test]
    public void StartNextDay_AfterReport_BeginsNewDay()
    {
        cycle.holdForReport = true;
        cycle.StartSession(false);
        Yes(GameRules.YesToWinDay);

        cycle.StartNextDay();

        Assert.AreEqual(2, cycle.CurrentDay);
        Assert.AreEqual(GameRules.StartingLives, cycle.Lives);
        Assert.AreEqual(0, cycle.YesCount);
        Assert.IsFalse(cycle.AwaitingNextDay);
        Assert.AreEqual(DayState.Mixing, cycle.State);
    }

    [Test]
    public void StartNextDay_WhenNotWaiting_DoesNothing()
    {
        cycle.StartSession(false);
        int requested = clientRequested;

        cycle.StartNextDay();

        Assert.AreEqual(1, cycle.CurrentDay);
        Assert.AreEqual(requested, clientRequested);
    }

    // ---------- Reveal ----------

    [Test]
    public void BeginReveal_MovesStateFromMixingToReveal()
    {
        cycle.StartSession(false);

        cycle.BeginReveal();

        Assert.AreEqual(DayState.Reveal, cycle.State);
    }

    [Test]
    public void BeginReveal_BeforeSessionStarts_DoesNothing()
    {
        cycle.BeginReveal();

        Assert.AreEqual(DayState.Inactive, cycle.State);
    }

    [Test]
    public void NewClientRequested_ResetsStateToMixing()
    {
        cycle.StartSession(false);
        cycle.BeginReveal();

        No();

        Assert.AreEqual(DayState.Mixing, cycle.State);
    }

    // ---------- Riwayat ----------

    [Test]
    public void StartSession_ClearsHistory()
    {
        cycle.StartSession(false);
        Yes(GameRules.YesToWinDay);
        Assert.AreEqual(1, cycle.History.Count);

        cycle.StartSession(false);

        Assert.AreEqual(0, cycle.History.Count);
    }
}
