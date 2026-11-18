using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Masuk main menu = sisa sesi sebelumnya dibersihkan: game ditandai selesai, save aktif dilepas, room ditinggalkan.
public class MainMenuSessionTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        service = LobbyService.Instance;
        Assert.IsNotNull(service);
        service.LeaveRoom();
        service.Players.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) Object.Destroy(o);
        yield return null;

        SaveService.ClearActive();
        service.LeaveRoom();
        service.Players.Clear();
    }

    private void StartSession()
    {
        var go = new GameObject("MainMenuSession");
        toDestroy.Add(go);
        go.AddComponent<MainMenuSession>();
    }

    [UnityTest]
    public IEnumerator Start_LeavesTheRoomTheLobbyWasStillIn()
    {
        service.JoinRoom("12345");

        StartSession();
        yield return null;

        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
        Assert.AreEqual(0, service.Players.Count);
    }

    [UnityTest]
    public IEnumerator Start_ReleasesTheActiveSaveFile()
    {
        SaveService.SetActive(new SaveFile { id = "unit-test-save-menu", roomName = "Test", nextDay = 3 });

        StartSession();
        yield return null;

        Assert.IsNull(SaveService.Active);
    }

    [UnityTest]
    public IEnumerator Start_MarksTheGameAsFinished()
    {
        service.CreateRoom("12345");
        service.SetInGame(true);

        StartSession();
        yield return null;

        Assert.IsFalse(service.InGame);
        Assert.IsFalse(MatchStarter.InGame);
    }

    [UnityTest]
    public IEnumerator Start_WithNothingToClean_DoesNotThrow()
    {
        StartSession();
        yield return null;

        LogAssert.NoUnexpectedReceived();
        Assert.AreEqual(LobbyMode.None, service.Mode);
    }
}
