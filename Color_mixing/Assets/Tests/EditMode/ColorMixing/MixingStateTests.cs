using NUnit.Framework;

public class MixingStateTests
{
    private const int Pigments = 5;
    private static readonly int[] Slots = { 0, 1, 2, 3, 4, 0 };   // P0: 0,1  P1: 2,3  P2: 4,0

    private MixingState NewState()
    {
        var s = new MixingState(Pigments);
        Assert.IsTrue(s.SetSlots(Slots));
        return s;
    }

    // ---------- Pembagian pigmen ----------

    [Test]
    public void GetPigment_ReadsTheAssignedSlots()
    {
        var s = NewState();
        Assert.AreEqual(0, s.GetPigment(0, 0));
        Assert.AreEqual(1, s.GetPigment(0, 1));
        Assert.AreEqual(3, s.GetPigment(1, 1));
        Assert.AreEqual(0, s.GetPigment(2, 1));
    }

    [Test]
    public void GetPigment_InvalidInput_ReturnsMinusOne()
    {
        var s = NewState();
        Assert.AreEqual(-1, s.GetPigment(3, 0));
        Assert.AreEqual(-1, s.GetPigment(-1, 0));
        Assert.AreEqual(-1, s.GetPigment(0, 2));
        Assert.AreEqual(-1, new MixingState(Pigments).GetPigment(0, 0), "belum dibagi");
    }

    [Test]
    public void SetSlots_RejectsWrongLengthOrUnknownPigment()
    {
        var s = new MixingState(Pigments);
        Assert.IsFalse(s.SetSlots(null));
        Assert.IsFalse(s.SetSlots(new[] { 0, 1, 2 }));
        Assert.IsFalse(s.SetSlots(new[] { 0, 1, 2, 3, 4, 5 }), "index 5 tidak ada di palet");
        Assert.IsFalse(s.SetSlots(new[] { 0, 1, 2, 3, 4, -1 }));
    }

    [Test]
    public void Slots_ReturnsACopy()
    {
        var s = NewState();
        s.Slots[0] = 4;
        Assert.AreEqual(0, s.GetPigment(0, 0));
    }

    // ---------- Tambah pigmen ----------

    [Test]
    public void TryAdd_PutsThePlayersPigmentInTheBowl()
    {
        var s = NewState();
        Assert.IsTrue(s.TryAdd(1, 0));      // pigmen 2
        Assert.IsTrue(s.TryAdd(1, 0, 2));
        Assert.AreEqual(3, s.Bowl.PigmentUnits[2]);
        Assert.AreEqual(3, s.Bowl.TotalUnits);
    }

    [Test]
    public void TryAdd_InvalidInput_IsRejected()
    {
        var s = NewState();
        Assert.IsFalse(s.TryAdd(5, 0));
        Assert.IsFalse(s.TryAdd(0, 2));
        Assert.IsFalse(s.TryAdd(0, 0, 0));
        Assert.IsFalse(s.TryAdd(0, 0, -3));
        Assert.IsTrue(s.Bowl.IsEmpty);
    }

    [Test]
    public void TryAdd_AfterMix_IsRejectedUntilUnmix()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        s.TrySetReady(0, true);

        Assert.IsFalse(s.TryAdd(0, 1));
        Assert.AreEqual(1, s.Bowl.TotalUnits);

        s.TrySetReady(0, false);
        Assert.IsTrue(s.TryAdd(0, 1));
    }

    [Test]
    public void TryAdd_OtherPlayersCanStillAddWhileOneIsReady()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        s.TrySetReady(0, true);
        Assert.IsTrue(s.TryAdd(1, 0));
    }

    // ---------- Mix ----------

    [Test]
    public void TrySetReady_EmptyBowl_IsRejected()
    {
        var s = NewState();
        Assert.IsFalse(s.TrySetReady(0, true));
        Assert.AreEqual(0, s.ReadyCount);
    }

    [Test]
    public void TrySetReady_SameValueTwice_ChangesNothing()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        Assert.IsTrue(s.TrySetReady(0, true));
        Assert.IsFalse(s.TrySetReady(0, true));
        Assert.IsFalse(s.TrySetReady(1, false), "belum Mix, Un-Mix tidak mengubah apa-apa");
    }

    [Test]
    public void AllThreeReady_ConfirmsTheMix()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        Assert.IsTrue(s.TrySetReady(0, true));
        Assert.IsTrue(s.TrySetReady(1, true));
        Assert.IsFalse(s.IsConfirmed);
        Assert.AreEqual(2, s.ReadyCount);

        Assert.IsTrue(s.TrySetReady(2, true));
        Assert.IsTrue(s.IsConfirmed);
    }

    [Test]
    public void AfterConfirm_NothingCanChange()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        for (int p = 0; p < 3; p++) s.TrySetReady(p, true);

        Assert.IsFalse(s.TrySetReady(0, false));
        Assert.IsFalse(s.TryAdd(0, 0));
        Assert.AreEqual(1, s.Bowl.TotalUnits);
    }

    [Test]
    public void InvalidPlayer_IsNeverReady()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        Assert.IsFalse(s.TrySetReady(9, true));
        Assert.IsFalse(s.IsReady(9));
        Assert.IsFalse(s.IsReady(-1));
    }

    // ---------- Redo ----------

    [Test]
    public void ResetForRedo_EmptiesTheBowlAndCancelsEveryMix()
    {
        var s = NewState();
        s.TryAdd(0, 0);
        for (int p = 0; p < 3; p++) s.TrySetReady(p, true);

        s.ResetForRedo();

        Assert.IsTrue(s.Bowl.IsEmpty);
        Assert.IsFalse(s.IsConfirmed);
        Assert.AreEqual(0, s.ReadyCount);
        Assert.IsTrue(s.TryAdd(0, 0), "bisa mixing lagi");
    }
}
