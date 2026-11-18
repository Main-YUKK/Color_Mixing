using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Satu slot pemain di lobby: kosong (siluet) atau terisi (karakter, nama, centang ready, tombol Kick).
public class PlayerSlotViewTests
{
    private readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
    private SlotRig slot;

    [SetUp]
    public void SetUp()
    {
        toDestroy.Clear();
        slot = SlotRig.Build("Slot", toDestroy);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy)
            if (o != null) UnityEngine.Object.Destroy(o);
    }

    private static LobbyPlayer Client(string name = "Budi", bool ready = false, bool local = false)
    {
        return new LobbyPlayer { name = name, isReady = ready, isLocal = local };
    }

    private static LobbyPlayer Host(string name = "Sari", bool local = false)
    {
        return new LobbyPlayer { name = name, isHost = true, isLocal = local };
    }

    // ---------- Slot kosong ----------

    [Test]
    public void EmptySlot_ShowsSilhouetteAndHidesTheRest()
    {
        slot.view.Show(null, viewerIsHost: true, "Pemain 2");

        Assert.IsTrue(slot.silhouette.activeSelf);
        Assert.IsFalse(slot.character.activeSelf);
        Assert.IsFalse(slot.readyIcon.activeSelf);
        Assert.IsFalse(slot.kick.activeSelf);
        Assert.AreEqual("Pemain 2", slot.label.text);
    }

    [Test]
    public void EmptySlot_WithoutCustomText_UsesTheInspectorLabel()
    {
        PlayTestHelpers.SetField(slot.view, "emptyLabel", "Pemain 3");

        slot.view.Show(null, viewerIsHost: false);

        Assert.AreEqual("Pemain 3", slot.label.text);
    }

    [Test]
    public void SlotThatBecomesEmpty_GoesBackToSilhouette()
    {
        slot.view.Show(Client(), viewerIsHost: true);

        slot.view.Show(null, viewerIsHost: true, "Pemain 2");

        Assert.IsTrue(slot.silhouette.activeSelf);
        Assert.IsFalse(slot.character.activeSelf);
        Assert.IsFalse(slot.kick.activeSelf);
    }

    // ---------- Slot terisi ----------

    [Test]
    public void FilledSlot_ShowsCharacterAndName()
    {
        slot.view.Show(Client("Budi"), viewerIsHost: false);

        Assert.IsTrue(slot.character.activeSelf);
        Assert.IsFalse(slot.silhouette.activeSelf);
        Assert.AreEqual("Budi", slot.label.text);
    }

    [Test]
    public void HostName_GetsAHostMarkOnASecondLine()
    {
        slot.view.Show(Host("Sari"), viewerIsHost: false);

        Assert.AreEqual("Sari\n(Host)", slot.label.text);
    }

    [Test]
    public void LongName_IsCutAtTwelveCharactersWithAnEllipsis()
    {
        slot.view.Show(Client("Abcdefghijklmnop"), viewerIsHost: false);

        Assert.AreEqual("Abcdefghijkl…", slot.label.text);
    }

    [Test]
    public void NameOfExactlyTwelveCharacters_IsNotCut()
    {
        slot.view.Show(Client("Abcdefghijkl"), viewerIsHost: false);

        Assert.AreEqual("Abcdefghijkl", slot.label.text);
    }

    [Test]
    public void LongHostName_IsCutBeforeTheHostMark()
    {
        slot.view.Show(Host("Abcdefghijklmnop"), viewerIsHost: false);

        Assert.AreEqual("Abcdefghijkl…\n(Host)", slot.label.text);
    }

    // ---------- Centang ready ----------

    [Test]
    public void ReadyIcon_ShowsOnlyForAReadyClient()
    {
        slot.view.Show(Client(ready: false), viewerIsHost: false);
        Assert.IsFalse(slot.readyIcon.activeSelf, "client belum ready");

        slot.view.Show(Client(ready: true), viewerIsHost: false);
        Assert.IsTrue(slot.readyIcon.activeSelf, "client sudah ready");
    }

    [Test]
    public void ReadyIcon_NeverShowsForTheHost()
    {
        var host = Host();
        host.isReady = true;

        slot.view.Show(host, viewerIsHost: true);

        Assert.IsFalse(slot.readyIcon.activeSelf);
    }

    // ---------- Tombol Kick ----------

    [Test]
    public void KickButton_ShowsToTheHostForOtherPlayers()
    {
        slot.view.Show(Client(), viewerIsHost: true);

        Assert.IsTrue(slot.kick.activeSelf);
    }

    [Test]
    public void KickButton_HiddenWhenTheViewerIsNotTheHost()
    {
        slot.view.Show(Client(), viewerIsHost: false);

        Assert.IsFalse(slot.kick.activeSelf);
    }

    [Test]
    public void KickButton_HiddenForTheLocalPlayerEvenOnTheHost()
    {
        slot.view.Show(Host(local: true), viewerIsHost: true);

        Assert.IsFalse(slot.kick.activeSelf, "host tidak bisa mengeluarkan dirinya sendiri");
    }

    [Test]
    public void KickClick_RaisesTheEventWithTheShownPlayer()
    {
        var budi = Client("Budi");
        LobbyPlayer kicked = null;
        int calls = 0;
        slot.view.OnKickClicked += p => { kicked = p; calls++; };
        slot.view.Show(budi, viewerIsHost: true);

        slot.kickButton.onClick.Invoke();

        Assert.AreSame(budi, kicked);
        Assert.AreEqual(1, calls);
    }

    [Test]
    public void KickClick_AfterTheSlotWentEmpty_RaisesNothing()
    {
        int calls = 0;
        slot.view.OnKickClicked += p => calls++;
        slot.view.Show(Client(), viewerIsHost: true);
        slot.view.Show(null, viewerIsHost: true);

        slot.kickButton.onClick.Invoke();

        Assert.AreEqual(0, calls);
    }

    [Test]
    public void KickClick_TargetsWhoeverIsInTheSlotNow()
    {
        var first = Client("Pertama");
        var second = Client("Kedua");
        LobbyPlayer kicked = null;
        slot.view.OnKickClicked += p => kicked = p;
        slot.view.Show(first, viewerIsHost: true);
        slot.view.Show(second, viewerIsHost: true);

        slot.kickButton.onClick.Invoke();

        Assert.AreSame(second, kicked);
    }

    // ---------- Salah setting di Inspector ----------

    [Test]
    public void KickObjectWithoutAButton_LogsAWarningButStillWorks()
    {
        LogAssert.Expect(LogType.Warning, new Regex("Kick Button tidak punya komponen Button"));
        var broken = SlotRig.Build("BrokenSlot", toDestroy, withKickButton: false);

        Assert.DoesNotThrow(() => broken.view.Show(Client(), viewerIsHost: true));
        Assert.IsTrue(broken.kick.activeSelf);
    }

    [Test]
    public void ViewWithNoReferencesAtAll_DoesNotThrow()
    {
        var go = new GameObject("EmptyView");
        toDestroy.Add(go);
        var view = go.AddComponent<PlayerSlotView>();

        Assert.DoesNotThrow(() => view.Show(Client(), viewerIsHost: true));
        Assert.DoesNotThrow(() => view.Show(null, viewerIsHost: false));
    }
}
