using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ClientArrivalSequenceTests
{
    private readonly List<GameObject> toDestroy = new List<GameObject>();
    private DayCycleManager cycle;
    private CharacterSpawner spawner;
    private ClientArrivalSequence sequence;
    private GameObject card;
    private SaveFile save;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        toDestroy.Clear();

        // Save sementara supaya hari selesai / game over tidak menulis file save asli.
        save = new SaveFile { id = "unit-test-save-sequence", roomName = "Test", nextDay = 1 };
        SaveService.SetActive(save);

        cycle = New("Cycle").AddComponent<DayCycleManager>();

        var prefabs = new[] { New("ClientA"), New("ClientB") };
        var spawnerGo = New("Spawner");
        spawnerGo.SetActive(false);
        spawner = spawnerGo.AddComponent<CharacterSpawner>();
        spawner.characterPrefabs = prefabs;
        spawner.spawnPoint = New("SpawnPoint").transform;
        spawner.spawnOnStart = false;
        spawner.walk = false;           // klien langsung tiba; test jalan memakai walk = true
        spawner.walkDistance = 1f;
        spawner.walkSpeed = 10f;
        spawnerGo.SetActive(true);

        card = New("Card");

        // Nonaktif dulu supaya OnEnable jalan setelah field terisi.
        var seqGo = New("Sequence");
        seqGo.SetActive(false);
        sequence = seqGo.AddComponent<ClientArrivalSequence>();
        PlayTestHelpers.SetField(sequence, "cycle", cycle);
        PlayTestHelpers.SetField(sequence, "spawner", spawner);
        PlayTestHelpers.SetField(sequence, "cardRoot", card);
        PlayTestHelpers.SetField(sequence, "flipOnArrival", true);
        PlayTestHelpers.SetField(sequence, "nextSceneName", "");
        PlayTestHelpers.SetField(sequence, "cardSeconds", 0.2f);
        seqGo.SetActive(true);

        yield return null;              // Start() menyembunyikan kartu
    }

    [TearDown]
    public void TearDown()
    {
        if (spawner != null && spawner.Current != null) Object.Destroy(spawner.Current);
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

    private IEnumerator WaitUntilIdle(float timeout = 2f)
    {
        float t = 0f;
        while (spawner.IsBusy && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    // ---------- Awal ----------

    [Test]
    public void Start_HidesCard()
    {
        Assert.IsFalse(card.activeSelf);
    }

    // ---------- Klien tiba ----------

    [Test]
    public void ClientArrives_ShowsCard()
    {
        spawner.SpawnNext();

        Assert.IsTrue(card.activeSelf);
    }

    [Test]
    public void ClientArrives_FlipsClientToFaceCounter()
    {
        spawner.SpawnNext();

        Assert.Less(spawner.Current.transform.localScale.x, 0f);
    }

    [Test]
    public void ClientArrives_FlipOff_KeepsClientScale()
    {
        PlayTestHelpers.SetField(sequence, "flipOnArrival", false);

        spawner.SpawnNext();

        Assert.Greater(spawner.Current.transform.localScale.x, 0f);
    }

    [UnityTest]
    public IEnumerator NoNextScene_CardStaysVisible()
    {
        spawner.SpawnNext();

        yield return new WaitForSeconds(0.5f);

        Assert.IsTrue(card.activeSelf);
    }

    // ---------- Klien baru diminta ----------

    [Test]
    public void SessionStart_RequestsClientAndSpawnsOne()
    {
        cycle.StartSession(false);

        Assert.IsNotNull(spawner.Current);
        Assert.IsTrue(card.activeSelf);      // walk mati: klien langsung tiba
    }

    [UnityTest]
    public IEnumerator NewClientRequested_HidesCardUntilNextClientArrives()
    {
        spawner.walk = true;
        cycle.StartSession(false);
        yield return WaitUntilIdle();
        Assert.IsTrue(card.activeSelf);

        cycle.OnFeedbackRevealed(false);     // klien gagal: klien baru diminta

        Assert.IsFalse(card.activeSelf, "Kartu harus tertutup selama klien baru berjalan");
        yield return WaitUntilIdle();
        Assert.IsTrue(card.activeSelf, "Kartu muncul lagi saat klien baru tiba");
    }

    [UnityTest]
    public IEnumerator NewClientRequested_ReplacesCurrentClient()
    {
        cycle.StartSession(false);
        var first = spawner.Current;

        cycle.OnFeedbackRevealed(true);
        yield return null;

        Assert.IsTrue(first == null);
        Assert.IsNotNull(spawner.Current);
        Assert.AreNotSame(first, spawner.Current);
    }

    [Test]
    public void NewClientRequested_WithNextSceneNotLoaded_DoesNotThrow()
    {
        PlayTestHelpers.SetField(sequence, "nextSceneName", "SceneYangBelumDimuat");

        Assert.DoesNotThrow(() => cycle.StartSession(false));
    }

    // ---------- Pindah scene ----------

    [UnityTest]
    public IEnumerator UnknownNextScene_LogsErrorAfterCardSeconds()
    {
        PlayTestHelpers.SetField(sequence, "nextSceneName", "SceneYangTidakAda");
        LogAssert.Expect(LogType.Error, new Regex("tidak ditemukan"));

        spawner.SpawnNext();
        yield return new WaitForSeconds(0.5f);

        // Expect di atas gagal otomatis kalau error tidak muncul
    }

    [UnityTest]
    public IEnumerator DayEnded_CancelsPendingSceneLoad()
    {
        // Given a scene load is pending (kartu sedang dibaca)
        cycle.holdForReport = true;
        PlayTestHelpers.SetField(sequence, "nextSceneName", "SceneYangTidakAda");
        PlayTestHelpers.SetField(sequence, "cardSeconds", 0.3f);
        cycle.StartSession(false);
        cycle.OnFeedbackRevealed(true);
        cycle.OnFeedbackRevealed(true);

        // When the day ends
        cycle.OnFeedbackRevealed(true);
        yield return new WaitForSeconds(0.6f);

        // Then the routine was stopped: no "scene tidak ditemukan" error was logged
        // (log error yang tidak diharapkan membuat test ini gagal otomatis)
        Assert.IsTrue(cycle.AwaitingNextDay);
    }

    [UnityTest]
    public IEnumerator GameOver_CancelsPendingSceneLoad()
    {
        PlayTestHelpers.SetField(sequence, "nextSceneName", "SceneYangTidakAda");
        PlayTestHelpers.SetField(sequence, "cardSeconds", 0.3f);
        cycle.StartSession(false);
        cycle.OnFeedbackRevealed(false);
        cycle.OnFeedbackRevealed(false);

        cycle.OnFeedbackRevealed(false);     // nyawa habis
        yield return new WaitForSeconds(0.6f);

        Assert.AreEqual(DayState.GameOver, cycle.State);
    }

    // ---------- Event ----------

    [Test]
    public void WhenDisabled_StopsReactingToClientArrival()
    {
        sequence.enabled = false;

        spawner.SpawnNext();

        Assert.IsFalse(card.activeSelf);
        Assert.Greater(spawner.Current.transform.localScale.x, 0f);   // tidak di-flip
    }
}
