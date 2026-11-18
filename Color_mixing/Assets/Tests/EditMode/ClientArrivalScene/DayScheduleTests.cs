using NUnit.Framework;
using UnityEngine;

public class DayScheduleTests
{
    private const RequestDifficulty L1 = RequestDifficulty.LevelOne;
    private const RequestDifficulty L2 = RequestDifficulty.LevelTwo;
    private const RequestDifficulty L3 = RequestDifficulty.LevelThree;

    private DaySchedule schedule;

    // 6 hari tertulis, hari 5-6 (index 4-5) diputar terus.
    [SetUp]
    public void SetUp()
    {
        schedule = TestHelpers.Schedule(4,
            new[] { L1, L1, L1 },   // hari 1
            new[] { L1, L1, L2 },   // hari 2
            new[] { L2, L2, L3 },   // hari 3
            new[] { L3, L3, L3 },   // hari 4
            new[] { L1, L2, L3 },   // hari 5 (loop)
            new[] { L3, L2, L1 });  // hari 6 (loop)
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(schedule);

    [Test]
    public void WrittenDay_ReturnsLevelOfThatClientSlot()
    {
        // Given day 3 is written as L2, L2, L3
        // When asking each client slot
        // Then each returns its own level
        Assert.AreEqual(L2, schedule.GetLevel(3, 0));
        Assert.AreEqual(L2, schedule.GetLevel(3, 1));
        Assert.AreEqual(L3, schedule.GetLevel(3, 2));
    }

    [Test]
    public void FirstDay_UsesFirstEntry()
    {
        Assert.AreEqual(L1, schedule.GetLevel(1, 0));
        Assert.AreEqual(L1, schedule.GetLevel(1, 2));
    }

    [Test]
    public void LastWrittenDay_StillReadsFromList()
    {
        Assert.AreEqual(L3, schedule.GetLevel(6, 0));
        Assert.AreEqual(L1, schedule.GetLevel(6, 2));
    }

    [Test]
    public void DayAfterList_LoopsBackToLoopStart()
    {
        // Day 7 wraps to day 5, day 8 to day 6
        Assert.AreEqual(schedule.GetLevel(5, 0), schedule.GetLevel(7, 0));
        Assert.AreEqual(schedule.GetLevel(5, 2), schedule.GetLevel(7, 2));
        Assert.AreEqual(schedule.GetLevel(6, 0), schedule.GetLevel(8, 0));
        Assert.AreEqual(schedule.GetLevel(6, 2), schedule.GetLevel(8, 2));
    }

    [Test]
    public void LoopKeepsRepeating_ForFarFutureDays()
    {
        // Loop panjang 2 hari: hari 5, 7, 9, ... sama; hari 6, 8, 10, ... sama
        Assert.AreEqual(schedule.GetLevel(5, 1), schedule.GetLevel(101, 1));
        Assert.AreEqual(schedule.GetLevel(6, 1), schedule.GetLevel(100, 1));
    }

    [Test]
    public void DaysBeforeLoopStart_AreNeverRepeated()
    {
        // Hari 1-4 tidak ikut loop
        Assert.AreNotEqual(schedule.GetLevel(1, 2), schedule.GetLevel(7, 2));   // L1 vs L3
    }

    [Test]
    public void ClientIndexOutOfRange_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => schedule.GetLevel(1, 3));
    }
}
