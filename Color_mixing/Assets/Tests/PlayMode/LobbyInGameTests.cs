using NUnit.Framework;

// Lobby selama dan sesudah game: ready dikosongkan saat game selesai.
public class LobbyInGameTests
{
    private LobbyService lobby;

    [SetUp]
    public void SetUp()
    {
        lobby = LobbyService.Instance;
        Assert.IsNotNull(lobby);
        lobby.LeaveRoom();
        lobby.Players.Clear();
        lobby.Players.Add(new LobbyPlayer { name = "Host", isHost = true, isLocal = true });
        lobby.Players.Add(new LobbyPlayer { name = "A", isReady = true });
        lobby.Players.Add(new LobbyPlayer { name = "B", isReady = true });
    }

    [TearDown]
    public void TearDown()
    {
        lobby.SetInGame(false);
        lobby.LeaveRoom();
        lobby.Players.Clear();
    }

    [Test]
    public void GameStart_KeepsReadyTicks()
    {
        lobby.SetInGame(true);

        Assert.IsTrue(lobby.InGame);
        Assert.IsTrue(lobby.Players.TrueForAll(p => p.isHost || p.isReady));
    }

    [Test]
    public void GameEnd_ClearsEveryReadyTick()
    {
        lobby.SetInGame(true);
        lobby.SetInGame(false);

        Assert.IsFalse(lobby.InGame);
        Assert.IsTrue(lobby.Players.TrueForAll(p => !p.isReady));
    }

    [Test]
    public void GameEnd_NotifiesTheLobbyScreen()
    {
        int changes = 0;
        lobby.OnPlayersChanged += () => changes++;
        lobby.SetInGame(true);

        lobby.SetInGame(false);

        Assert.AreEqual(1, changes);
    }

    [Test]
    public void EndGame_WhenNoGameWasRunning_ChangesNothing()
    {
        int changes = 0;
        lobby.OnPlayersChanged += () => changes++;

        MatchStarter.EndGame();

        Assert.AreEqual(0, changes);
        Assert.IsTrue(lobby.Players.Exists(p => p.isReady), "tanpa game, centang ready tidak disentuh");
    }

    [Test]
    public void LeavingTheRoom_ResetsTheFlag()
    {
        lobby.SetInGame(true);

        lobby.LeaveRoom();

        Assert.IsFalse(lobby.InGame);
    }
}
