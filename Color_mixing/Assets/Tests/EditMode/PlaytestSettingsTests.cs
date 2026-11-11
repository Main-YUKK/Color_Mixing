using NUnit.Framework;

public class PlaytestSettingsTests
{
    [SetUp]
    public void SetUp() => PlaytestSettings.Reset();

    [TearDown]
    public void TearDown() => PlaytestSettings.Reset();

    [Test]
    public void Default_UsesSceneValuesAndSchedule()
    {
        Assert.AreEqual(30f, PlaytestSettings.ClientTime(30f));
        Assert.AreEqual(10f, PlaytestSettings.Rotation(10f));
        Assert.AreEqual(80f, PlaytestSettings.Passing(80f));
        Assert.AreEqual(RequestDifficulty.LevelTwo, PlaytestSettings.Level(RequestDifficulty.LevelTwo));
    }

    [Test]
    public void Difficulty_ForcesEveryClientToThatLevel()
    {
        PlaytestSettings.SetDifficulty(3);
        Assert.AreEqual(RequestDifficulty.LevelThree, PlaytestSettings.Level(RequestDifficulty.LevelOne));

        PlaytestSettings.SetDifficulty(1);
        Assert.AreEqual(RequestDifficulty.LevelOne, PlaytestSettings.Level(RequestDifficulty.LevelThree));

        PlaytestSettings.SetDifficulty(0);
        Assert.AreEqual(RequestDifficulty.LevelTwo, PlaytestSettings.Level(RequestDifficulty.LevelTwo));
    }

    [Test]
    public void Values_AreUsedAndClamped()
    {
        PlaytestSettings.SetClientSeconds(90);
        PlaytestSettings.SetPassingScore(85);
        PlaytestSettings.SetRotationSeconds(20);
        Assert.AreEqual(90f, PlaytestSettings.ClientTime(30f));
        Assert.AreEqual(85f, PlaytestSettings.Passing(80f));
        Assert.AreEqual(20f, PlaytestSettings.Rotation(10f));

        PlaytestSettings.SetRotationSeconds(1);                       // terlalu kecil -> batas bawah
        Assert.AreEqual(PlaytestSettings.MinRotationSeconds, PlaytestSettings.RotationSeconds);
        PlaytestSettings.SetRotationSeconds(100000);                  // terlalu besar -> batas atas
        Assert.AreEqual(PlaytestSettings.MaxRotationSeconds, PlaytestSettings.RotationSeconds);
        PlaytestSettings.SetRotationSeconds(0);                       // kosong -> Default
        Assert.AreEqual(10f, PlaytestSettings.Rotation(10f));
    }

    [Test]
    public void Snapshot_CarriesSettingsToClients()
    {
        PlaytestSettings.SetDifficulty(2);
        PlaytestSettings.SetClientSeconds(60);
        PlaytestSettings.SetRotationSeconds(15);
        PlaytestSettings.SetPassingScore(90);

        var snapshot = new DaySnapshot();
        PlaytestSettings.Write(snapshot);

        PlaytestSettings.Reset();                                     // device client mulai dari Default
        PlaytestSettings.Read(snapshot);

        Assert.AreEqual(2, PlaytestSettings.Difficulty);
        Assert.AreEqual(60, PlaytestSettings.ClientSeconds);
        Assert.AreEqual(15, PlaytestSettings.RotationSeconds);
        Assert.AreEqual(90, PlaytestSettings.PassingScore);
    }

    [Test]
    public void SnapshotFromOldHost_MeansDefault()
    {
        PlaytestSettings.SetClientSeconds(60);
        PlaytestSettings.Read(new DaySnapshot());                     // semua 0
        Assert.AreEqual(30f, PlaytestSettings.ClientTime(30f));
    }
}
