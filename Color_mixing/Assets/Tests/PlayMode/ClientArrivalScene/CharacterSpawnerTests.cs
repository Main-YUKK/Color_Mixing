using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CharacterSpawnerTests
{
    private readonly List<GameObject> toDestroy = new List<GameObject>();
    private CharacterSpawner spawner;
    private Transform spawnPoint;
    private int arrived;

    [SetUp]
    public void SetUp()
    {
        arrived = 0;
        toDestroy.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (spawner != null && spawner.Current != null) Object.Destroy(spawner.Current);
        foreach (var go in toDestroy)
            if (go != null) Object.Destroy(go);
    }

    private GameObject NewObject(string name, Vector3 position = default)
    {
        var go = new GameObject(name) { transform = { position = position } };
        toDestroy.Add(go);
        return go;
    }

    // Spawner dibuat nonaktif, diisi, lalu diaktifkan supaya Start / Update jalan dengan setelan test.
    private CharacterSpawner MakeSpawner(int prefabCount = 2, bool walk = false, bool spawnOnStart = false)
    {
        var prefabs = new GameObject[prefabCount];
        for (int i = 0; i < prefabCount; i++) prefabs[i] = NewObject("Client" + i);

        var go = NewObject("Spawner");
        go.SetActive(false);
        spawner = go.AddComponent<CharacterSpawner>();
        spawnPoint = NewObject("SpawnPoint", new Vector3(3f, 0f, 0f)).transform;

        spawner.characterPrefabs = prefabs;
        spawner.spawnPoint = spawnPoint;
        spawner.spawnOnStart = spawnOnStart;
        spawner.walk = walk;
        spawner.walkDistance = 1f;
        spawner.walkSpeed = 10f;
        spawner.OnClientArrived += () => arrived++;

        go.SetActive(true);
        return spawner;
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

    // ---------- Muncul ----------

    [Test]
    public void SpawnNext_WithoutWalking_ClientAppearsAtSpawnPointAndArrives()
    {
        MakeSpawner();

        spawner.SpawnNext();

        Assert.IsNotNull(spawner.Current);
        Assert.AreEqual(spawnPoint.position, spawner.Current.transform.position);
        Assert.IsFalse(spawner.IsBusy);
        Assert.AreEqual(1, arrived);
    }

    [Test]
    public void SpawnNext_WithoutSpawnPoint_UsesSpawnerPosition()
    {
        MakeSpawner();
        spawner.spawnPoint = null;
        spawner.transform.position = new Vector3(-2f, 1f, 0f);

        spawner.SpawnNext();

        Assert.AreEqual(spawner.transform.position, spawner.Current.transform.position);
    }

    [Test]
    public void SpawnNext_EmptyPrefabList_DoesNothing()
    {
        MakeSpawner();
        spawner.characterPrefabs = new GameObject[0];

        spawner.SpawnNext();

        Assert.IsNull(spawner.Current);
        Assert.AreEqual(0, arrived);
        Assert.IsFalse(spawner.IsBusy);
    }

    [Test]
    public void SpawnNext_NullPrefabList_DoesNothing()
    {
        MakeSpawner();
        spawner.characterPrefabs = null;

        spawner.SpawnNext();

        Assert.IsNull(spawner.Current);
        Assert.AreEqual(0, arrived);
    }

    [UnityTest]
    public IEnumerator SpawnOnStart_SpawnsOneClientWithoutCall()
    {
        MakeSpawner(spawnOnStart: true);

        yield return null;

        Assert.IsNotNull(spawner.Current);
        Assert.AreEqual(1, arrived);
    }

    [UnityTest]
    public IEnumerator SpawnOnStartOff_SpawnsNothingUntilCalled()
    {
        MakeSpawner(spawnOnStart: false);

        yield return null;

        Assert.IsNull(spawner.Current);
        Assert.AreEqual(0, arrived);
    }

    // ---------- Pergantian klien ----------

    [UnityTest]
    public IEnumerator SpawnNext_ReplacesOldClient()
    {
        MakeSpawner();
        spawner.SpawnNext();
        var old = spawner.Current;

        spawner.SpawnNext();
        yield return null;      // Destroy dijalankan di akhir frame

        Assert.IsTrue(old == null, "Klien lama harus dihancurkan");
        Assert.IsNotNull(spawner.Current);
        Assert.AreNotSame(old, spawner.Current);
        Assert.AreEqual(2, arrived);
    }

    [Test]
    public void SpawnNext_NeverPicksSamePrefabTwiceInARow()
    {
        MakeSpawner(prefabCount: 3);
        string previous = null;

        for (int i = 0; i < 30; i++)
        {
            spawner.SpawnNext();
            string name = spawner.Current.name;
            Assert.AreNotEqual(previous, name, $"Spawn ke-{i + 1} memakai prefab yang sama dengan sebelumnya");
            previous = name;
        }
    }

    [Test]
    public void SpawnNext_SinglePrefab_StillSpawnsEveryTime()
    {
        MakeSpawner(prefabCount: 1);

        spawner.SpawnNext();
        spawner.SpawnNext();
        spawner.SpawnNext();

        Assert.IsNotNull(spawner.Current);
        Assert.AreEqual(3, arrived);
    }

    // ---------- Berjalan ----------

    [UnityTest]
    public IEnumerator Walking_ClientComesFromEntrySideAndIsBusy()
    {
        MakeSpawner(walk: true);

        spawner.SpawnNext();

        // Given walk is on, the client starts to the right of the spot and has not arrived yet
        Assert.IsTrue(spawner.IsBusy);
        Assert.Greater(spawner.Current.transform.position.x, spawnPoint.position.x);
        Assert.AreEqual(0, arrived);

        yield return WaitUntilIdle();
    }

    [UnityTest]
    public IEnumerator Walking_ClientLandsExactlyOnSpawnPointAndArrives()
    {
        MakeSpawner(walk: true);

        spawner.SpawnNext();
        yield return WaitUntilIdle();

        Assert.IsFalse(spawner.IsBusy);
        Assert.AreEqual(1, arrived);
        Assert.AreEqual(spawnPoint.position.x, spawner.Current.transform.position.x, 0.0001f);
        Assert.AreEqual(spawnPoint.position.y, spawner.Current.transform.position.y, 0.0001f);
    }

    [UnityTest]
    public IEnumerator SpawnNext_WhileWalking_IsIgnored()
    {
        MakeSpawner(walk: true);
        spawner.SpawnNext();
        var first = spawner.Current;

        spawner.SpawnNext();        // masih berjalan, harus diabaikan
        yield return WaitUntilIdle();

        Assert.AreSame(first, spawner.Current);
        Assert.AreEqual(1, arrived);
    }

    [UnityTest]
    public IEnumerator Walking_OldClientTurnsToFaceDoorThenLeaves()
    {
        MakeSpawner(walk: true);
        spawner.SpawnNext();
        yield return WaitUntilIdle();
        var old = spawner.Current;
        Assert.Greater(old.transform.localScale.x, 0f);

        spawner.SpawnNext();

        // Klien lama berbalik menghadap pintu (kanan), lawan arah saat masuk
        Assert.Less(old.transform.localScale.x, 0f);

        yield return WaitUntilIdle();
        yield return null;

        Assert.IsTrue(old == null, "Klien lama harus hilang setelah keluar");
        Assert.AreNotSame(old, spawner.Current);
        Assert.AreEqual(2, arrived);
    }

    // ---------- Otomatis ----------

    [UnityTest]
    public IEnumerator StayTime_SpawnsNextClientAutomatically()
    {
        MakeSpawner(spawnOnStart: true);
        spawner.stayTime = new Vector2(0.1f, 0.1f);

        yield return new WaitForSeconds(0.6f);

        Assert.GreaterOrEqual(arrived, 2);
    }

    [UnityTest]
    public IEnumerator StayTimeZero_NeverSpawnsAutomatically()
    {
        MakeSpawner(spawnOnStart: true);
        spawner.stayTime = Vector2.zero;

        yield return new WaitForSeconds(0.4f);

        Assert.AreEqual(1, arrived);
    }
}
