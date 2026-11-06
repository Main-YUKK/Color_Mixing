using System.Linq;
using NUnit.Framework;

public class PigmentAssignmentTests
{
    [Test]
    public void Shuffle_UsesEveryPigmentAndExactlyOneDuplicate()
    {
        for (int round = 0; round < 200; round++)
        {
            int[] slots = PigmentAssignment.Shuffle(5);

            Assert.AreEqual(6, slots.Length);
            Assert.AreEqual(5, slots.Distinct().Count(), "5 jenis, 1 di antaranya muncul dua kali");
            Assert.IsTrue(slots.All(p => p >= 0 && p < 5));
        }
    }

    [Test]
    public void Shuffle_NoPlayerHoldsTheSamePigmentTwice()
    {
        for (int round = 0; round < 500; round++)
        {
            int[] slots = PigmentAssignment.Shuffle(5);
            for (int p = 0; p < PigmentAssignment.PlayerCount; p++)
                Assert.AreNotEqual(slots[2 * p], slots[2 * p + 1], $"ronde {round}, pemain {p}");
        }
    }

    [Test]
    public void Shuffle_IsAcceptedByMixingState()
    {
        var state = new MixingState(5);
        Assert.IsTrue(state.SetSlots(PigmentAssignment.Shuffle(5)));
    }
}
