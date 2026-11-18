using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public class DayCycleHUDTests
{
    private readonly List<GameObject> toDestroy = new List<GameObject>();
    private DayCycleManager cycle;
    private DayCycleHUD hud;
    private TextMeshPro dayText;
    private TextMeshPro livesText;
    private TextMeshPro happyText;
    private GameObject panel;
    private SaveFile save;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        toDestroy.Clear();

        // Save sementara supaya hari selesai / game over tidak menulis file save asli.
        save = new SaveFile { id = "unit-test-save-hud", roomName = "Test", nextDay = 1 };
        SaveService.SetActive(save);

        cycle = New("Cycle").AddComponent<DayCycleManager>();
        dayText = NewText("DayText");
        livesText = NewText("LivesText");
        happyText = NewText("HappyText");
        panel = New("GameOverPanel");

        // Nonaktif dulu supaya OnEnable jalan setelah field terisi.
        var hudGo = New("HUD");
        hudGo.SetActive(false);
        hud = hudGo.AddComponent<DayCycleHUD>();
        PlayTestHelpers.SetField(hud, "cycle", cycle);
        PlayTestHelpers.SetField(hud, "dayText", dayText);
        PlayTestHelpers.SetField(hud, "livesText", livesText);
        PlayTestHelpers.SetField(hud, "HappyClientText", happyText);
        PlayTestHelpers.SetField(hud, "gameOverPanel", panel);
        hudGo.SetActive(true);

        yield return null;      // Start() menyembunyikan panel game over
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in toDestroy)
            if (go != null) Object.Destroy(go);

        SaveService.Delete(save);
        GameSession.StartRun(1);
    }

    private GameObject New(string name)
    {
        var go = new GameObject(name);
        toDestroy.Add(go);
        return go;
    }

    private TextMeshPro NewText(string name)
    {
        var text = New(name).AddComponent<TextMeshPro>();
        text.text = "-";
        return text;
    }

    // ---------- Awal ----------

    [Test]
    public void Start_HidesGameOverPanel()
    {
        Assert.IsFalse(panel.activeSelf);
    }

    // ---------- Hari mulai ----------

    [Test]
    public void SessionStart_ShowsDayLivesAndHappyClients()
    {
        cycle.StartSession(false);

        Assert.AreEqual("Day 1", dayText.text);
        Assert.AreEqual("Lives: 3", livesText.text);
        Assert.AreEqual("0/3", happyText.text);
    }

    [Test]
    public void SessionStart_Continue_ShowsSavedDay()
    {
        save.nextDay = 5;

        cycle.StartSession(true);

        Assert.AreEqual("Day 5", dayText.text);
    }

    // ---------- Selama bermain ----------

    [Test]
    public void ClientFailed_ShowsOneLifeLess()
    {
        cycle.StartSession(false);

        cycle.OnFeedbackRevealed(false);

        Assert.AreEqual("Lives: 2", livesText.text);
        Assert.AreEqual("0/3", happyText.text);
    }

    [Test]
    public void ClientTimedOut_ShowsOneLifeLess()
    {
        cycle.StartSession(false);

        cycle.OnClientTimeout();

        Assert.AreEqual("Lives: 2", livesText.text);
    }

    [Test]
    public void ClientSucceeded_ShowsOneMoreHappyClient()
    {
        cycle.StartSession(false);

        cycle.OnFeedbackRevealed(true);

        Assert.AreEqual("1/3", happyText.text);
        Assert.AreEqual("Lives: 3", livesText.text);
    }

    [Test]
    public void DuringNormalPlay_GameOverPanelStaysHidden()
    {
        cycle.StartSession(false);

        cycle.OnFeedbackRevealed(false);
        cycle.OnFeedbackRevealed(true);

        Assert.IsFalse(panel.activeSelf);
    }

    // ---------- Hari berikutnya ----------

    [Test]
    public void NextDay_ResetsTextsForNewDay()
    {
        cycle.StartSession(false);
        cycle.OnFeedbackRevealed(false);     // 2 nyawa

        for (int i = 0; i < GameRules.YesToWinDay; i++) cycle.OnFeedbackRevealed(true);

        Assert.AreEqual("Day 2", dayText.text);
        Assert.AreEqual("Lives: 3", livesText.text);
        Assert.AreEqual("0/3", happyText.text);
    }

    // ---------- Game over ----------

    [Test]
    public void GameOver_ShowsPanelAndZeroLives()
    {
        cycle.StartSession(false);

        for (int i = 0; i < GameRules.StartingLives; i++) cycle.OnFeedbackRevealed(false);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual("Lives: 0", livesText.text);
    }

    // ---------- Aman dan event ----------

    [Test]
    public void MissingReferences_DoNotThrow()
    {
        var emptyGo = New("EmptyHUD");
        emptyGo.SetActive(false);
        var empty = emptyGo.AddComponent<DayCycleHUD>();
        PlayTestHelpers.SetField(empty, "cycle", cycle);
        emptyGo.SetActive(true);

        Assert.DoesNotThrow(() =>
        {
            cycle.StartSession(false);
            for (int i = 0; i < GameRules.StartingLives; i++) cycle.OnFeedbackRevealed(false);
        });
    }

    [Test]
    public void WhenDisabled_TextsStopUpdating()
    {
        hud.enabled = false;

        cycle.StartSession(false);

        Assert.AreEqual("-", dayText.text);
        Assert.AreEqual("-", livesText.text);
        Assert.AreEqual("-", happyText.text);
    }
}
