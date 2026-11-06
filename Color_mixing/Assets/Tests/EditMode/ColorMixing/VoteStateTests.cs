using NUnit.Framework;

public class VoteStateTests
{
    private VoteState Started()
    {
        var v = new VoteState();
        v.Start();
        return v;
    }

    [Test]
    public void NotVoting_BeforeStart_RejectsEverything()
    {
        var v = new VoteState();
        Assert.IsFalse(v.TryCast(0, Vote.Apply, out _));
    }

    [Test]
    public void TwoApplyVotes_MakeApplyWin()
    {
        var v = Started();
        Assert.IsTrue(v.TryCast(0, Vote.Apply, out var first));
        Assert.AreEqual(Vote.None, first);
        Assert.IsTrue(v.IsVoting);

        Assert.IsTrue(v.TryCast(2, Vote.Apply, out var second));
        Assert.AreEqual(Vote.Apply, second);
        Assert.IsFalse(v.IsVoting);
        Assert.IsTrue(v.IsApplied);
    }

    [Test]
    public void TwoRedoVotes_MakeRedoWin()
    {
        var v = Started();
        v.TryCast(0, Vote.Redo, out _);
        v.TryCast(1, Vote.Redo, out var winner);

        Assert.AreEqual(Vote.Redo, winner);
        Assert.IsFalse(v.IsVoting);
        Assert.IsFalse(v.IsApplied);
    }

    [Test]
    public void SplitVotes_ThirdVoterDecides()
    {
        var v = Started();
        v.TryCast(0, Vote.Apply, out _);
        v.TryCast(1, Vote.Redo, out var afterTwo);
        Assert.AreEqual(Vote.None, afterTwo);
        Assert.IsTrue(v.IsVoting);

        v.TryCast(2, Vote.Redo, out var winner);
        Assert.AreEqual(Vote.Redo, winner);
    }

    [Test]
    public void ChangingAVote_IsRejected()
    {
        var v = Started();
        Assert.IsTrue(v.TryCast(0, Vote.Apply, out _));
        Assert.IsFalse(v.TryCast(0, Vote.Redo, out _));
        Assert.AreEqual(Vote.Apply, v.Get(0));
        Assert.AreEqual(1, v.Count(Vote.Apply));
        Assert.AreEqual(0, v.Count(Vote.Redo));
    }

    [Test]
    public void AfterTheWinner_LateVotesAreRejected()
    {
        var v = Started();
        v.TryCast(0, Vote.Apply, out _);
        v.TryCast(1, Vote.Apply, out _);
        Assert.IsFalse(v.TryCast(2, Vote.Redo, out _));
        Assert.AreEqual(Vote.None, v.Get(2));
    }

    [Test]
    public void InvalidVotes_AreRejected()
    {
        var v = Started();
        Assert.IsFalse(v.TryCast(0, Vote.None, out _));
        Assert.IsFalse(v.TryCast(3, Vote.Apply, out _));
        Assert.IsFalse(v.TryCast(-1, Vote.Apply, out _));
        Assert.AreEqual(Vote.None, v.Get(7));
    }

    [Test]
    public void Start_ClearsOldVotesForTheNextRound()
    {
        var v = Started();
        v.TryCast(0, Vote.Redo, out _);
        v.TryCast(1, Vote.Redo, out _);

        v.Start();

        Assert.IsTrue(v.IsVoting);
        Assert.IsFalse(v.IsApplied);
        Assert.AreEqual(0, v.Count(Vote.Redo));
        Assert.IsTrue(v.TryCast(0, Vote.Apply, out _));
    }
}
