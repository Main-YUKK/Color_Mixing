using NUnit.Framework;

public class LoadGateTests
{
    [Test]
    public void NewGate_CannotHide()
    {
        var gate = new LoadGate();
        gate.Begin(false);

        Assert.IsFalse(gate.CanHide);   // scene belum termuat
    }

    [Test]
    public void LoadedWithoutHold_CanHide()
    {
        var gate = new LoadGate();
        gate.Begin(false);
        gate.MarkLoaded();

        Assert.IsTrue(gate.CanHide);
    }

    [Test]
    public void LoadedButHeld_CannotHideUntilReleased()
    {
        var gate = new LoadGate();
        gate.Begin(true);
        gate.MarkLoaded();
        Assert.IsFalse(gate.CanHide);

        gate.Release();
        Assert.IsTrue(gate.CanHide);
    }

    [Test]
    public void ReleaseBeforeLoaded_StillWaitsForLoad()
    {
        var gate = new LoadGate();
        gate.Begin(true);
        gate.Release();   // GO datang sebelum scene selesai dimuat
        Assert.IsFalse(gate.CanHide);

        gate.MarkLoaded();
        Assert.IsTrue(gate.CanHide);
    }

    [Test]
    public void Begin_ResetsPreviousLoad()
    {
        var gate = new LoadGate();
        gate.Begin(false);
        gate.MarkLoaded();

        gate.Begin(true);

        Assert.IsFalse(gate.Loaded);
        Assert.IsTrue(gate.Held);
        Assert.IsFalse(gate.CanHide);
    }
}
