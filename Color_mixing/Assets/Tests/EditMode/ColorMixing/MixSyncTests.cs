using NUnit.Framework;
using UnityEngine;

// Keadaan host dikirim sebagai foto (lewat JSON) dan client harus punya keadaan yang sama persis.
public class MixSyncTests
{
    private static readonly int[] Slots = { 0, 1, 2, 3, 4, 0 };

    private static MixingState HostState()
    {
        var s = new MixingState(5);
        s.SetSlots(Slots);
        s.TryAdd(0, 0);
        s.TryAdd(1, 1, 2);
        s.TrySetReady(0, true);
        return s;
    }

    private static MixSnapshot Roundtrip(MixSnapshot s) => JsonUtility.FromJson<MixSnapshot>(JsonUtility.ToJson(s));

    private static MixSnapshot Snapshot(MixingState m, VoteState v)
    {
        return new MixSnapshot
        {
            slots = m.Slots, units = m.Units, ready = m.ReadyFlags, confirmed = m.IsConfirmed,
            votes = v.Votes, voting = v.IsVoting, applied = v.IsApplied,
        };
    }

    // ---------- Mixing ----------

    [Test]
    public void ClientState_MatchesHost_AfterJson()
    {
        var host = HostState();
        var s = Roundtrip(Snapshot(host, new VoteState()));

        var client = new MixingState(5);
        Assert.IsTrue(client.Import(s.slots, s.units, s.ready, s.confirmed));

        Assert.AreEqual(host.Slots, client.Slots);
        Assert.AreEqual(host.Units, client.Units);
        Assert.AreEqual(host.ReadyFlags, client.ReadyFlags);
        Assert.AreEqual(host.Bowl.TotalUnits, client.Bowl.TotalUnits);
        Assert.AreEqual(host.IsConfirmed, client.IsConfirmed);
        Assert.AreEqual(host.GetPigment(2, 1), client.GetPigment(2, 1));
    }

    [Test]
    public void Import_ReplacesOldClientState_NotMerges()
    {
        var client = new MixingState(5);
        client.SetSlots(Slots);
        client.TryAdd(2, 0, 4);
        client.TrySetReady(2, true);

        var host = new MixingState(5);
        host.SetSlots(Slots);
        var s = Snapshot(host, new VoteState());
        client.Import(s.slots, s.units, s.ready, s.confirmed);

        Assert.IsTrue(client.Bowl.IsEmpty, "foto host adalah kebenaran: mangkuk kosong");
        Assert.AreEqual(0, client.ReadyCount);
    }

    [Test]
    public void Import_RejectsBrokenData_AndChangesNothing()
    {
        var client = HostState();
        var before = Snapshot(client, new VoteState());

        Assert.IsFalse(client.Import(Slots, new[] { 1, 2, 3 }, new[] { false, false, false }, false), "jumlah jenis salah");
        Assert.IsFalse(client.Import(Slots, new[] { 0, 0, 0, 0, -1 }, new[] { false, false, false }, false), "unit negatif");
        Assert.IsFalse(client.Import(Slots, new[] { 0, 0, 0, 0, 0 }, new[] { false }, false), "jumlah pemain salah");
        Assert.IsFalse(client.Import(new[] { 9, 9, 9, 9, 9, 9 }, new[] { 0, 0, 0, 0, 0 }, new[] { false, false, false }, false), "pigmen tidak ada");
        Assert.IsFalse(client.Import(null, null, null, false));

        Assert.AreEqual(before.units, client.Units);
        Assert.AreEqual(before.ready, client.ReadyFlags);
    }

    [Test]
    public void ConfirmedState_TravelsToClient()
    {
        var host = HostState();
        host.TrySetReady(1, true);
        host.TrySetReady(2, true);
        Assert.IsTrue(host.IsConfirmed);

        var s = Roundtrip(Snapshot(host, new VoteState()));
        var client = new MixingState(5);
        client.Import(s.slots, s.units, s.ready, s.confirmed);

        Assert.IsTrue(client.IsConfirmed);
        Assert.IsFalse(client.TryAdd(0, 0), "client juga tidak bisa menambah setelah dikonfirmasi");
    }

    // ---------- Vote ----------

    [Test]
    public void VoteState_TravelsToClient()
    {
        var host = new VoteState();
        host.Start();
        host.TryCast(0, Vote.Apply, out _);
        host.TryCast(2, Vote.Apply, out _);   // Apply menang

        var s = Roundtrip(Snapshot(new MixingState(5), host));
        var client = new VoteState();
        Assert.IsTrue(client.Import(s.votes, s.voting, s.applied));

        Assert.IsTrue(client.IsApplied);
        Assert.IsFalse(client.IsVoting);
        Assert.AreEqual(Vote.Apply, client.Get(0));
        Assert.AreEqual(Vote.None, client.Get(1));
        Assert.AreEqual(Vote.Apply, client.Get(2));
    }

    [Test]
    public void VoteImport_RejectsBrokenData()
    {
        var v = new VoteState();
        Assert.IsFalse(v.Import(new[] { 0, 0 }, true, false));
        Assert.IsFalse(v.Import(new[] { 0, 5, 0 }, true, false));
        Assert.IsFalse(v.Import(new[] { 0, -1, 0 }, true, false));
        Assert.IsFalse(v.Import(null, true, false));
        Assert.IsFalse(v.IsVoting, "tidak ada yang berubah");
    }

    // ---------- Mangkuk ----------

    [Test]
    public void BowlSet_UpdatesUnitsAndTotal()
    {
        var bowl = new MixingBowl(5);
        bowl.AddPigment(0, 9);

        Assert.IsTrue(bowl.Set(new[] { 1, 0, 2, 0, 3 }));

        Assert.AreEqual(6, bowl.TotalUnits);
        Assert.AreEqual(2, bowl.PigmentUnits[2]);
        Assert.IsFalse(bowl.IsEmpty);
    }

    [Test]
    public void BowlSet_RejectsWrongSize()
    {
        var bowl = new MixingBowl(5);
        Assert.IsFalse(bowl.Set(new[] { 1, 2 }));
        Assert.IsFalse(bowl.Set(null));
        Assert.IsTrue(bowl.IsEmpty);
    }

    // ---------- Aturan host terhadap permintaan client ----------

    [Test]
    public void HostChecksRequests_UsingSenderIndex_NotClaimedPlayer()
    {
        // Host memanggil TryAdd(message.from, ...): client 1 hanya bisa memakai pigmen miliknya sendiri.
        var host = new MixingState(5);
        host.SetSlots(Slots);

        int sender = 1;
        Assert.IsTrue(host.TryAdd(sender, 0));
        Assert.AreEqual(1, host.Units[2], "pigmen slot 0 milik pemain 1 = pigmen 2");
        Assert.AreEqual(0, host.Units[0]);
    }
}
