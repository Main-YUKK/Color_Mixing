using NUnit.Framework;
using UnityEngine;

// Format pesan lobby antar-device (JSON lewat JsonUtility) dan nilai awal LobbyPlayer.
// Kalau field LobbyMessage diubah/diganti nama, device dengan versi build lama tidak bisa saling bicara,
// jadi bentuk JSON-nya dijaga di sini.
public class LobbyMessageTests
{
    [Test]
    public void RoundTrip_KeepsEveryField()
    {
        var original = new LobbyMessage
        {
            type = "HELLO",
            name = "Budi",
            isHost = true,
            roomName = "Ruang Seru",
            ready = true,
            target = "Sari",
            slots = "A|B"
        };

        var copy = JsonUtility.FromJson<LobbyMessage>(JsonUtility.ToJson(original));

        Assert.AreEqual("HELLO", copy.type);
        Assert.AreEqual("Budi", copy.name);
        Assert.IsTrue(copy.isHost);
        Assert.AreEqual("Ruang Seru", copy.roomName);
        Assert.IsTrue(copy.ready);
        Assert.AreEqual("Sari", copy.target);
        Assert.AreEqual("A|B", copy.slots);
    }

    [Test]
    public void FieldNames_AreTheOnesOtherDevicesExpect()
    {
        string json = JsonUtility.ToJson(new LobbyMessage { type = "ACK", name = "X" });

        foreach (string key in new[] { "type", "name", "isHost", "roomName", "ready", "target", "slots" })
            StringAssert.Contains($"\"{key}\"", json);
    }

    [Test]
    public void MissingFields_FallBackToEmptyValues()
    {
        var msg = JsonUtility.FromJson<LobbyMessage>("{\"type\":\"READY\",\"name\":\"Budi\"}");

        Assert.AreEqual("READY", msg.type);
        Assert.IsFalse(msg.isHost);
        Assert.IsFalse(msg.ready);
        Assert.IsTrue(string.IsNullOrEmpty(msg.roomName));
        Assert.IsTrue(string.IsNullOrEmpty(msg.target));
        Assert.IsTrue(string.IsNullOrEmpty(msg.slots));
    }

    [Test]
    public void UnknownFields_AreIgnored()
    {
        var msg = JsonUtility.FromJson<LobbyMessage>("{\"type\":\"HELLO\",\"name\":\"Budi\",\"fieldBaru\":123}");

        Assert.AreEqual("HELLO", msg.type);
        Assert.AreEqual("Budi", msg.name);
    }

    [Test]
    public void LobbyPlayer_StartsAsPlainRemoteClientThatIsNotReady()
    {
        var player = new LobbyPlayer();

        Assert.IsFalse(player.isHost);
        Assert.IsFalse(player.isLocal);
        Assert.IsFalse(player.isReady);
    }
}
