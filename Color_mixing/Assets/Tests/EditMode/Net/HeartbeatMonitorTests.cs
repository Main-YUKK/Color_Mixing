using System.Collections.Generic;
using NUnit.Framework;

public class HeartbeatMonitorTests
{
    private HeartbeatMonitor monitor;
    private readonly List<int> unstable = new List<int>();
    private readonly List<int> recovered = new List<int>();

    [SetUp]
    public void SetUp()
    {
        monitor = new HeartbeatMonitor(unstableAfter: 4f, lostAfter: 10f);
        unstable.Clear();
        recovered.Clear();
        monitor.Unstable += unstable.Add;
        monitor.Recovered += recovered.Add;
    }

    [Test]
    public void SteadyHeartbeats_StayGood()
    {
        monitor.Track(1, 0f);
        for (float t = 1f; t <= 30f; t += 1f)
        {
            monitor.Heard(1, t);
            Assert.IsEmpty(monitor.Check(t));
        }
        Assert.AreEqual(PeerHealth.Good, monitor.HealthOf(1));
        Assert.IsEmpty(unstable);
    }

    [Test]
    public void SilenceBeyondUnstableThreshold_RaisesUnstableOnce()
    {
        monitor.Track(1, 0f);

        monitor.Check(3.9f);
        Assert.IsEmpty(unstable);

        monitor.Check(4f);
        monitor.Check(6f);
        Assert.AreEqual(new[] { 1 }, unstable);
        Assert.AreEqual(PeerHealth.Unstable, monitor.HealthOf(1));
    }

    [Test]
    public void HearingAgainWhileUnstable_Recovers()
    {
        monitor.Track(1, 0f);
        monitor.Check(5f);

        monitor.Heard(1, 6f);

        Assert.AreEqual(new[] { 1 }, recovered);
        Assert.AreEqual(PeerHealth.Good, monitor.HealthOf(1));
        Assert.IsEmpty(monitor.Check(9f), "hitungan diam dimulai lagi dari 6");
    }

    [Test]
    public void SilenceBeyondLostThreshold_ReportsLostExactlyOnce()
    {
        monitor.Track(1, 0f);

        CollectionAssert.AreEqual(new[] { 1 }, monitor.Check(10f));
        Assert.IsEmpty(monitor.Check(11f), "sudah dilaporkan");
        Assert.AreEqual(PeerHealth.Lost, monitor.HealthOf(1));
    }

    [Test]
    public void LostPeer_IsNotRevivedByLateNews()
    {
        monitor.Track(1, 0f);
        monitor.Check(10f);

        monitor.Heard(1, 11f);

        Assert.AreEqual(PeerHealth.Lost, monitor.HealthOf(1));
        Assert.IsEmpty(recovered);
    }

    [Test]
    public void PeersAreJudgedIndependently()
    {
        monitor.Track(1, 0f);
        monitor.Track(2, 0f);
        monitor.Heard(2, 8f);

        var lost = monitor.Check(10f);

        CollectionAssert.AreEqual(new[] { 1 }, lost);
        Assert.AreEqual(PeerHealth.Good, monitor.HealthOf(2));
    }

    [Test]
    public void BothSilentAtTheSameCheck_AreReportedTogether()
    {
        monitor.Track(1, 0f);
        monitor.Track(2, 0f);

        var lost = monitor.Check(10f);

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, lost);
    }

    [Test]
    public void Resync_ForgivesOurOwnStall()
    {
        monitor.Track(1, 0f);
        monitor.Check(5f);                    // sempat tidak stabil

        monitor.Resync(30f);                  // device ini macet 30 detik, bukan salah pemain 1
        Assert.IsEmpty(monitor.Check(30.5f));
        Assert.IsEmpty(monitor.Check(33f));
    }

    [Test]
    public void UntrackedPeer_IsIgnored()
    {
        monitor.Heard(5, 1f);
        Assert.IsEmpty(monitor.Check(100f));
        Assert.AreEqual(0, monitor.TrackedCount);

        monitor.Track(1, 0f);
        monitor.Untrack(1);
        Assert.IsEmpty(monitor.Check(100f));
    }

    [Test]
    public void TrackingAgain_StartsFresh()
    {
        monitor.Track(1, 0f);
        monitor.Check(10f);

        monitor.Track(1, 50f);

        Assert.AreEqual(PeerHealth.Good, monitor.HealthOf(1));
        Assert.IsEmpty(monitor.Check(52f));
    }
}
