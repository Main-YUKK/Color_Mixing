using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class ClientTimerTests
{
    private GameObject cycleGo;
    private GameObject timerGo;
    private GameObject spawnerGo;
    private GameObject prefab;
    private GameObject barGo;
    private GameObject textGo;
    private DayCycleManager cycle;
    private ClientTimer timer;
    private SaveFile save;

    [SetUp]
    public void SetUp()
    {
        // Save sementara supaya hari selesai / game over tidak menulis file save asli.
        save = new SaveFile { id = "unit-test-save-timer", roomName = "Test", nextDay = 1 };
        SaveService.SetActive(save);

        cycleGo = new GameObject("Cycle");
        cycle = cycleGo.AddComponent<DayCycleManager>();
        cycle.StartSession(false);

        // Nonaktif dulu: OnEnable timer jalan saat field sudah terisi.
        timerGo = new GameObject("Timer");
        timerGo.SetActive(false);
        timer = timerGo.AddComponent<ClientTimer>();
        PlayTestHelpers.SetField(timer, "cycle", cycle);
        PlayTestHelpers.SetField(timer, "startManually", true);
        PlayTestHelpers.SetField(timer, "secondsPerClient", 1f);
    }

    [TearDown]
    public void TearDown()
    {
        if (spawnerGo != null)
        {
            var s = spawnerGo.GetComponent<CharacterSpawner>();
            if (s != null && s.Current != null) Object.Destroy(s.Current);
        }
        foreach (var o in new[] { timerGo, cycleGo, spawnerGo, prefab, barGo, textGo })
            if (o != null) Object.Destroy(o);

        SaveService.Delete(save);
        GameSession.StartRun(1);
    }

    private void Enable(float seconds)
    {
        PlayTestHelpers.SetField(timer, "secondsPerClient", seconds);
        timerGo.SetActive(true);
    }

    // ---------- Hitung mundur ----------

    [Test]
    public void StartTimer_StartsFull()
    {
        Enable(1f);

        timer.StartTimer();

        Assert.AreEqual(1f, timer.Normalized, 0.0001f);
    }

    [UnityTest]
    public IEnumerator Timer_CountsDownOverTime()
    {
        Enable(1f);
        timer.StartTimer();

        yield return new WaitForSeconds(0.3f);

        Assert.Less(timer.Normalized, 1f);
        Assert.Greater(timer.Normalized, 0f);
    }

    [UnityTest]
    public IEnumerator Timer_UpdatesSliderBar()
    {
        barGo = new GameObject("Bar");
        var bar = barGo.AddComponent<Slider>();
        PlayTestHelpers.SetField(timer, "timerBar", bar);
        Enable(1f);
        timer.StartTimer();
        Assert.AreEqual(1f, bar.value, 0.0001f);

        yield return new WaitForSeconds(0.3f);

        Assert.Less(bar.value, 1f);
    }

    [UnityTest]
    public IEnumerator Timer_ShowsRemainingSecondsRoundedUp()
    {
        textGo = new GameObject("Text");
        var text = textGo.AddComponent<TextMeshPro>();
        PlayTestHelpers.SetField(timer, "timerText", text);
        Enable(5f);
        timer.StartTimer();

        yield return null;

        Assert.AreEqual("5", text.text);
    }

    // ---------- Timeout ----------

    [UnityTest]
    public IEnumerator Timer_ReachingZero_CallsTimeoutOnce()
    {
        Enable(0.2f);
        timer.StartTimer();

        yield return new WaitForSeconds(0.6f);

        // Given the timer ran out without a submit
        // Then the client timed out exactly once and a life was lost
        Assert.AreEqual(1, cycle.TimeoutCount);
        Assert.AreEqual(GameRules.StartingLives - 1, cycle.Lives);
    }

    // ---------- Stop / reset ----------

    [UnityTest]
    public IEnumerator Stop_PausesCountdown()
    {
        Enable(1f);
        timer.StartTimer();
        yield return new WaitForSeconds(0.1f);

        timer.Stop();
        float frozen = timer.Normalized;
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(frozen, timer.Normalized, 0.0001f);
    }

    [UnityTest]
    public IEnumerator Stop_PreventsTimeout()
    {
        Enable(0.2f);
        timer.StartTimer();
        timer.Stop();

        yield return new WaitForSeconds(0.5f);

        Assert.AreEqual(0, cycle.TimeoutCount);
    }

    [UnityTest]
    public IEnumerator StartTimer_AgainResetsToFull()
    {
        Enable(1f);
        timer.StartTimer();
        yield return new WaitForSeconds(0.3f);
        Assert.Less(timer.Normalized, 1f);

        timer.StartTimer();

        Assert.AreEqual(1f, timer.Normalized, 0.0001f);
    }

    // ---------- Berhenti oleh event ----------

    [UnityTest]
    public IEnumerator NewClientRequested_StopsTimer()
    {
        Enable(0.3f);
        timer.StartTimer();

        cycle.OnFeedbackRevealed(false);   // klien berikutnya diminta
        yield return new WaitForSeconds(0.6f);

        Assert.AreEqual(0, cycle.TimeoutCount);
    }

    [UnityTest]
    public IEnumerator DayEnded_StopsTimer()
    {
        cycle.holdForReport = true;
        cycle.OnFeedbackRevealed(true);
        cycle.OnFeedbackRevealed(true);
        Enable(0.3f);
        timer.StartTimer();

        cycle.OnFeedbackRevealed(true);    // Yes ke-3: hari selesai
        yield return new WaitForSeconds(0.6f);

        Assert.IsTrue(cycle.AwaitingNextDay);
        Assert.AreEqual(0, cycle.TimeoutCount);
    }

    [UnityTest]
    public IEnumerator GameOver_StopsTimer()
    {
        cycle.OnFeedbackRevealed(false);
        cycle.OnFeedbackRevealed(false);
        Enable(0.3f);
        timer.StartTimer();

        cycle.OnFeedbackRevealed(false);   // nyawa habis
        yield return new WaitForSeconds(0.6f);

        Assert.AreEqual(DayState.GameOver, cycle.State);
        Assert.AreEqual(0, cycle.TimeoutCount);
    }

    // ---------- Mode mulai ----------

    private CharacterSpawner MakeSpawner()
    {
        prefab = new GameObject("ClientPrefab");
        spawnerGo = new GameObject("Spawner");
        spawnerGo.SetActive(false);
        var spawner = spawnerGo.AddComponent<CharacterSpawner>();
        spawner.characterPrefabs = new[] { prefab };
        spawner.walk = false;           // langsung tiba tanpa berjalan
        spawner.spawnOnStart = false;
        spawnerGo.SetActive(true);
        return spawner;
    }

    [Test]
    public void AutoMode_TimerStartsWhenClientArrives()
    {
        var spawner = MakeSpawner();
        PlayTestHelpers.SetField(timer, "spawner", spawner);
        PlayTestHelpers.SetField(timer, "startManually", false);
        Enable(1f);
        Assert.AreEqual(0f, timer.Normalized, 0.0001f);     // belum jalan

        spawner.SpawnNext();                                // klien tiba

        Assert.AreEqual(1f, timer.Normalized, 0.0001f);
    }

    [Test]
    public void ManualMode_ClientArrivalDoesNotStartTimer()
    {
        var spawner = MakeSpawner();
        PlayTestHelpers.SetField(timer, "spawner", spawner);
        PlayTestHelpers.SetField(timer, "startManually", true);
        Enable(1f);

        spawner.SpawnNext();

        Assert.AreEqual(0f, timer.Normalized, 0.0001f);
    }
}
