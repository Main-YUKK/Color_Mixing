using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Buat room (host), masuk room (client), keluar room, dan ganti nama room.
// Pakai LobbyService baru per test (bukan singleton). Di Editor tidak ada matchmaking, jadi yang dites murni logika lobby.
public class LobbyServiceRoomTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private LobbyService service;
    private int changes;

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        service = LobbyTestHelpers.NewService(toDestroy);
        changes = 0;
        service.OnPlayersChanged += () => changes++;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) UnityEngine.Object.Destroy(o);
    }

    // ---------- CreateRoom ----------

    [Test]
    public void CreateRoom_BecomesHostWithLocalPlayerAndFiveDigitCode()
    {
        string code = service.CreateRoom();

        Assert.AreEqual(LobbyMode.Host, service.Mode);
        Assert.AreEqual(code, service.RoomCode);
        Assert.AreEqual(5, code.Length);
        Assert.IsTrue(code.All(char.IsDigit), "kode room harus angka semua");

        Assert.AreEqual(1, service.Players.Count);
        Assert.IsTrue(service.Players[0].isHost);
        Assert.IsTrue(service.Players[0].isLocal);
        Assert.IsFalse(service.Players[0].isReady);
    }

    [Test]
    public void CreateRoom_NotifiesPlayersChangedOnce()
    {
        service.CreateRoom();

        Assert.AreEqual(1, changes);
    }

    [Test]
    public void CreateRoom_WithValidSavedCode_KeepsThatCode()
    {
        string code = service.CreateRoom("48213");

        Assert.AreEqual("48213", code);
        Assert.AreEqual("48213", service.RoomCode);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("1234")]
    [TestCase("123456")]
    [TestCase("abcde")]
    public void CreateRoom_WithInvalidSavedCode_FallsBackToRandomCode(string saved)
    {
        string code = service.CreateRoom(saved);

        Assert.AreNotEqual(saved, code);
        Assert.AreEqual(5, code.Length);
        Assert.IsTrue(code.All(char.IsDigit));
    }

    [Test]
    public void CreateRoom_WithCustomName_SetsRoomName()
    {
        service.CreateRoom("12345", "  Warung Biru  ");

        Assert.AreEqual("Warung Biru", service.RoomName);
    }

    [Test]
    public void CreateRoom_WithoutCustomName_LeavesDefaultName()
    {
        service.CreateRoom("12345");

        Assert.AreEqual("", service.RoomName);
    }

    [Test]
    public void CreateRoom_CalledAgain_StartsFromScratch()
    {
        service.CreateRoom("11111", "Lama");
        LobbyTestHelpers.AddPlayer(service, "A");

        service.CreateRoom("22222");

        Assert.AreEqual("22222", service.RoomCode);
        Assert.AreEqual("", service.RoomName, "nama room lama tidak ikut terbawa");
        Assert.AreEqual(1, service.Players.Count, "hanya pemain lokal yang tersisa");
    }

    [Test]
    public void CreateRoom_ClearsThePreviousFailureMessage()
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.KickLocal(service);
        Assert.IsNotNull(service.LastMessage, "syarat awal: ada pesan gagal");

        service.CreateRoom();

        Assert.IsNull(service.LastMessage);
    }

    // ---------- JoinRoom ----------

    [TestCase(null)]
    [TestCase("")]
    [TestCase("1234")]
    [TestCase("123456")]
    [TestCase("abcde")]
    public void JoinRoom_WithInvalidCode_ReturnsFalseAndStaysIdle(string code)
    {
        bool ok = service.JoinRoom(code);

        Assert.IsFalse(ok);
        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
        Assert.AreEqual(0, service.Players.Count);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void JoinRoom_WithValidCode_BecomesClientWithLocalPlayer()
    {
        bool ok = service.JoinRoom("12345");

        Assert.IsTrue(ok);
        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.AreEqual("12345", service.RoomCode);
        Assert.AreEqual(1, service.Players.Count);
        Assert.IsFalse(service.Players[0].isHost);
        Assert.IsTrue(service.Players[0].isLocal);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void JoinRoom_SameCodeWhileStillSearching_IsIgnored()
    {
        service.JoinRoom("12345");
        var me = service.Players[0];

        bool ok = service.JoinRoom("12345");

        Assert.IsTrue(ok);
        Assert.AreSame(me, service.Players[0], "pencarian yang sama tidak diulang dari awal");
        Assert.AreEqual(1, changes, "tidak ada notifikasi tambahan");
    }

    [Test]
    public void JoinRoom_DifferentCode_RestartsWithTheNewCode()
    {
        service.JoinRoom("12345");

        service.JoinRoom("54321");

        Assert.AreEqual("54321", service.RoomCode);
        Assert.AreEqual(1, service.Players.Count);
    }

    [Test]
    public void JoinRoom_SameCodeAfterJoinWasConfirmed_StartsOver()
    {
        service.JoinRoom("12345");
        LobbyTestHelpers.Receive(service, "HELLO", "HostX", isHost: true);
        Assert.AreEqual(2, service.Players.Count, "syarat awal: host sudah terdengar");

        service.JoinRoom("12345");

        Assert.AreEqual(1, service.Players.Count, "sudah masuk sebelumnya, jadi join ulang membersihkan daftar");
    }

    [Test]
    public void JoinRoom_AfterBeingHost_SwitchesToClient()
    {
        service.CreateRoom("11111");

        service.JoinRoom("22222");

        Assert.AreEqual(LobbyMode.Client, service.Mode);
        Assert.IsFalse(service.Players.Exists(p => p.isHost));
    }

    // ---------- LeaveRoom ----------

    [Test]
    public void LeaveRoom_ResetsEverything()
    {
        service.CreateRoom("12345", "Ruang");
        LobbyTestHelpers.AddPlayer(service, "A");

        service.LeaveRoom();

        Assert.AreEqual(LobbyMode.None, service.Mode);
        Assert.IsNull(service.RoomCode);
        Assert.AreEqual("", service.RoomName);
        Assert.AreEqual(0, service.Players.Count);
        Assert.IsFalse(service.InGame);
    }

    [Test]
    public void LeaveRoom_WhenNotInARoom_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => service.LeaveRoom());
        Assert.AreEqual(LobbyMode.None, service.Mode);
    }

    // ---------- SetRoomName ----------

    [Test]
    public void SetRoomName_AsHost_TrimsAndNotifies()
    {
        service.CreateRoom("12345");
        changes = 0;

        service.SetRoomName("  Warung Biru ");

        Assert.AreEqual("Warung Biru", service.RoomName);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void SetRoomName_LongerThanLimit_IsCut()
    {
        service.CreateRoom("12345");

        service.SetRoomName(new string('x', LobbyService.MaxRoomNameLength + 10));

        Assert.AreEqual(LobbyService.MaxRoomNameLength, service.RoomName.Length);
    }

    [Test]
    public void SetRoomName_ExactlyAtLimit_IsKeptWhole()
    {
        service.CreateRoom("12345");
        string name = new string('y', LobbyService.MaxRoomNameLength);

        service.SetRoomName(name);

        Assert.AreEqual(name, service.RoomName);
    }

    [Test]
    public void SetRoomName_SameNameAgain_DoesNotNotify()
    {
        service.CreateRoom("12345");
        service.SetRoomName("Warung");
        changes = 0;

        service.SetRoomName("  Warung ");

        Assert.AreEqual(0, changes);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void SetRoomName_EmptyOrNull_GoesBackToDefault(string empty)
    {
        service.CreateRoom("12345", "Warung");
        changes = 0;

        service.SetRoomName(empty);

        Assert.AreEqual("", service.RoomName);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void SetRoomName_AsClient_IsIgnored()
    {
        service.JoinRoom("12345");
        changes = 0;

        service.SetRoomName("Curang");

        Assert.AreEqual("", service.RoomName);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void SetRoomName_WithoutARoom_IsIgnored()
    {
        service.SetRoomName("Curang");

        Assert.AreEqual("", service.RoomName);
    }

    // ---------- Event yang aman ----------

    [Test]
    public void PlayersChanged_SubscriberThatThrows_DoesNotStopOthers()
    {
        var other = LobbyTestHelpers.NewService(toDestroy);
        int good = 0;
        other.OnPlayersChanged += () => throw new InvalidOperationException("boom");
        other.OnPlayersChanged += () => good++;
        LogAssert.Expect(LogType.Exception, new Regex("boom"));

        other.CreateRoom();

        Assert.AreEqual(1, good, "subscriber kedua tetap dipanggil");
    }

    [Test]
    public void RoomClosed_SubscriberThatThrows_DoesNotStopOthers()
    {
        string reason = null;
        service.OnRoomClosed += r => throw new InvalidOperationException("boom");
        service.OnRoomClosed += r => reason = r;
        service.JoinRoom("12345");
        LogAssert.Expect(LogType.Exception, new Regex("boom"));

        LobbyTestHelpers.KickLocal(service);

        Assert.AreEqual("Kamu dikeluarkan oleh host.", reason);
    }
}
