using NUnit.Framework;

// Test pertama: memastikan assembly test bisa melihat kode game di ColorMixing.Runtime.
public class MixingBowlTests
{
    [Test]
    public void NewBowl_IsEmpty()
    {
        var bowl = new MixingBowl(5);

        Assert.That(bowl.IsEmpty, Is.True);
        Assert.That(bowl.TotalUnits, Is.EqualTo(0));
    }
}
