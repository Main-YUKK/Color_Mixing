using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ClientRequestBagTests
{
    private const RequestDifficulty L1 = RequestDifficulty.LevelOne;
    private const RequestDifficulty L2 = RequestDifficulty.LevelTwo;

    private ClientRequestDatabase db;
    private ClientRequestBag bag;

    [SetUp]
    public void SetUp()
    {
        db = TestHelpers.Database(
            TestHelpers.Request("a1", L1), TestHelpers.Request("a2", L1), TestHelpers.Request("a3", L1),
            TestHelpers.Request("b1", L2), TestHelpers.Request("b2", L2));
        bag = new ClientRequestBag(db);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(db);

    [Test]
    public void Next_ReturnsRequestOfRequestedDifficulty()
    {
        Assert.AreEqual(L1, bag.Next(L1).difficulty);
        Assert.AreEqual(L2, bag.Next(L2).difficulty);
    }

    [Test]
    public void Next_NoRepeatUntilAllRequestsUsed()
    {
        // Given 3 level-one requests
        // When drawing 3 times
        var seen = new HashSet<string>();
        for (int i = 0; i < 3; i++) seen.Add(bag.Next(L1).requestId);

        // Then all 3 are different
        Assert.AreEqual(3, seen.Count);
    }

    [Test]
    public void Next_AfterBagEmpty_RefillsAndKeepsGoing()
    {
        for (int i = 0; i < 3; i++) bag.Next(L1);

        var again = bag.Next(L1);

        Assert.AreEqual(L1, again.difficulty);
    }

    [Test]
    public void Next_EverySecondRound_AlsoHasNoRepeats()
    {
        for (int i = 0; i < 3; i++) bag.Next(L1);

        var seen = new HashSet<string>();
        for (int i = 0; i < 3; i++) seen.Add(bag.Next(L1).requestId);

        Assert.AreEqual(3, seen.Count);
    }

    [Test]
    public void Next_DifferentLevelsHaveSeparateBags()
    {
        // Drawing level one must not use up level two
        for (int i = 0; i < 3; i++) bag.Next(L1);

        var seen = new HashSet<string> { bag.Next(L2).requestId, bag.Next(L2).requestId };

        Assert.AreEqual(2, seen.Count);
    }

    [Test]
    public void Next_DoesNotChangeDatabase()
    {
        for (int i = 0; i < 6; i++) bag.Next(L1);

        Assert.AreEqual(3, db.GetByDifficulty(L1).Count);
    }
}
