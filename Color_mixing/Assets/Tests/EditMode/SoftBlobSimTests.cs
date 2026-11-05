using System;
using NUnit.Framework;

public class SoftBlobSimTests
{
    [Test]
    public void LuasBlobMengikutiRasio()
    {
        const float radius = 346f;
        float paintArea = 0.92f * MathF.PI * radius * radius;
        var sim = new SoftBlobSim(radius);

        sim.SetUnits(2, 1, -100f, -150f);
        for (int i = 0; i < 120; i++) sim.Step(1f / 60f);
        sim.SetUnits(1, 1, 250f, -120f);
        for (int i = 0; i < 180; i++) sim.Step(1f / 60f);

        foreach (SoftBlobSim.Blob blob in sim.Blobs)
            Assert.AreEqual(0.5f, SoftBlobSim.Area(blob) / paintArea, 0.015f);
    }
}
