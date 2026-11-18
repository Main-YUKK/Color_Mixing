using NUnit.Framework;
using UnityEngine;

public class GameSessionTests
{
    private ClientRequestDatabase db;

    [SetUp]
    public void SetUp()
    {
        db = TestHelpers.Database(TestHelpers.Request("a1", RequestDifficulty.LevelOne));
        GameSession.StartRun(1);    // bersihkan state static dari test lain
    }

    [TearDown]
    public void TearDown()
    {
        GameSession.StartRun(1);
        Object.DestroyImmediate(db);
    }

    [Test]
    public void StartRun_SetsDayAndClearsRunState()
    {
        GameSession.ClientIndex = 2;
        GameSession.CurrentRequest = TestHelpers.Request("x", RequestDifficulty.LevelOne);

        GameSession.StartRun(5);

        Assert.AreEqual(5, GameSession.Day);
        Assert.AreEqual(0, GameSession.ClientIndex);
        Assert.IsNull(GameSession.CurrentRequest);
        Assert.IsNull(GameSession.Bag);
    }

    [Test]
    public void Reset_CreatesBagAndGoesBackToDayOne()
    {
        GameSession.StartRun(7);
        GameSession.ClientIndex = 2;

        GameSession.Reset(db);

        Assert.IsNotNull(GameSession.Bag);
        Assert.AreEqual(1, GameSession.Day);
        Assert.AreEqual(0, GameSession.ClientIndex);
        Assert.IsNull(GameSession.CurrentRequest);
    }

    [Test]
    public void EnsureBag_WhenNull_CreatesBag()
    {
        Assert.IsNull(GameSession.Bag);

        GameSession.EnsureBag(db);

        Assert.IsNotNull(GameSession.Bag);
    }

    [Test]
    public void EnsureBag_WhenBagExists_KeepsSameBag()
    {
        GameSession.EnsureBag(db);
        var first = GameSession.Bag;

        GameSession.EnsureBag(db);

        Assert.AreSame(first, GameSession.Bag);
    }
}
