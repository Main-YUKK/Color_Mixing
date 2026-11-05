using NUnit.Framework;

public class StartHandshakeTests
{
    [Test]
    public void NotAllReadyAtFirst()
    {
        var h = new StartHandshake(3);
        Assert.IsFalse(h.AllReady);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, h.Missing());
    }

    [Test]
    public void AllReadyAfterEveryoneReports()
    {
        var h = new StartHandshake(3);
        h.MarkReady(0); h.MarkReady(2);
        Assert.IsFalse(h.AllReady);
        CollectionAssert.AreEqual(new[] { 1 }, h.Missing());
        h.MarkReady(1);
        Assert.IsTrue(h.AllReady);
    }

    [Test]
    public void DuplicateReportDoesNotCountTwice()
    {
        var h = new StartHandshake(3);
        Assert.IsTrue(h.MarkReady(1));
        Assert.IsFalse(h.MarkReady(1));
        Assert.IsFalse(h.AllReady);
    }

    [Test]
    public void InvalidIndexIsIgnored()
    {
        var h = new StartHandshake(3);
        Assert.IsFalse(h.MarkReady(-1));
        Assert.IsFalse(h.MarkReady(3));
        Assert.IsFalse(h.IsReady(7));
    }

    [Test]
    public void SinglePlayerIsReadyAfterSelf()
    {
        var h = new StartHandshake(1);
        h.MarkReady(0);
        Assert.IsTrue(h.AllReady);
    }
}
