using NUnit.Framework;

public class ConnectionVerdictTests
{
    [Test]
    public void Client_HostSilent_WithInternet_HostDropped()
    {
        Assert.AreEqual(Verdict.PeersDropped, ConnectionVerdict.Decide(false, 1, 1, true));
    }

    [Test]
    public void Client_HostSilent_NoInternet_OwnConnection()
    {
        Assert.AreEqual(Verdict.OwnConnection, ConnectionVerdict.Decide(false, 1, 1, false));
    }

    [Test]
    public void Host_OneClientSilent_ThatClientDropped()
    {
        Assert.AreEqual(Verdict.PeersDropped, ConnectionVerdict.Decide(true, 1, 2, true));
    }

    [Test]
    public void Host_BothClientsSilentTogether_OwnConnection()
    {
        Assert.AreEqual(Verdict.OwnConnection, ConnectionVerdict.Decide(true, 2, 2, true));
    }

    [Test]
    public void Host_NoInternet_OwnConnectionEvenForOneClient()
    {
        Assert.AreEqual(Verdict.OwnConnection, ConnectionVerdict.Decide(true, 1, 2, false));
    }

    [Test]
    public void Host_WithSingleTrackedClient_CannotTellSoItBlamesTheClient()
    {
        // Tes dua device: satu-satunya client diam. Tidak bisa dibedakan dari host sendiri yang putus.
        Assert.AreEqual(Verdict.PeersDropped, ConnectionVerdict.Decide(true, 1, 1, true));
    }
}
