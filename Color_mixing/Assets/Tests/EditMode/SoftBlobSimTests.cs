using System;
using NUnit.Framework;

public class SoftBlobSimTests
{
    [Test]
    public void LuasBlobMengikutiRasio()
    {
        const float radius = 346f;
        float paintArea = 0.95f * MathF.PI * radius * radius;
        var sim = new SoftBlobSim(radius);

        sim.SetUnits(2, 1, -100f, -150f);
        for (int i = 0; i < 120; i++) sim.Step(1f / 60f);
        sim.SetUnits(1, 1, 250f, -120f);
        for (int i = 0; i < 180; i++) sim.Step(1f / 60f);

        foreach (SoftBlobSim.Blob blob in sim.Blobs)
            Assert.AreEqual(0.5f, SoftBlobSim.Area(blob) / paintArea, 0.015f);
    }

    [Test]
    public void LimaBlobBerhentiBergerakSetelahTenang()
    {
        var sim = new SoftBlobSim(346f);
        sim.SetUnits(0, 1, 260f, 100f);
        sim.SetUnits(1, 2, 250f, -120f);
        sim.SetUnits(2, 10, -100f, -150f);
        sim.SetUnits(3, 1, -220f, 170f);
        sim.SetUnits(4, 4, 0f, 280f);
        for (int i = 0; i < 480; i++) sim.Step(1f / 60f);

        float[][] x = new float[sim.Blobs.Count][], y = new float[sim.Blobs.Count][];
        for (int b = 0; b < sim.Blobs.Count; b++)
        {
            x[b] = (float[])sim.Blobs[b].X.Clone();
            y[b] = (float[])sim.Blobs[b].Y.Clone();
        }
        sim.Step(1f / 60f);

        for (int b = 0; b < sim.Blobs.Count; b++)
            for (int i = 0; i < SoftBlobSim.PointCount; i++)
                Assert.Less(MathF.Abs(sim.Blobs[b].X[i] - x[b][i]) + MathF.Abs(sim.Blobs[b].Y[i] - y[b][i]), 0.5f);
    }

    // Regresi: blob baru yang lahir di dalam blob lain pernah melilit dua putaran.
    [Test]
    public void BlobBaruTidakMelilitDuaPutaran()
    {
        var sim = new SoftBlobSim(346f);
        sim.SetUnits(3, 1, -220f, 170f);
        for (int i = 0; i < 180; i++) sim.Step(1f / 60f);
        sim.SetUnits(2, 1, -100f, -150f);
        for (int i = 0; i < 240; i++) sim.Step(1f / 60f);

        foreach (SoftBlobSim.Blob blob in sim.Blobs)
            Assert.AreEqual(1, WindingCount(blob));
    }

    [Test]
    public void BlobBaruMunculDariArahTuangan()
    {
        var sim = new SoftBlobSim(346f);
        sim.SetUnits(0, 1, -300f, 160f);
        sim.Step(1f / 60f);

        float x = 0f, y = 0f;
        foreach (SoftBlobSim.Blob blob in sim.Blobs)
            for (int i = 0; i < SoftBlobSim.PointCount; i++) { x += blob.X[i]; y += blob.Y[i]; }
        Assert.Less(x, 0f);
        Assert.Greater(y, 0f);
    }

    // Reveal: blob terbesar menelan semua, yang lain mengecil lalu hilang.
    [Test]
    public void RevealMenyisakanSatuBlobPenuh()
    {
        const float radius = 346f;
        var sim = new SoftBlobSim(radius);
        sim.SetUnits(2, 10, -100f, -150f);
        sim.SetUnits(1, 2, 250f, -120f);
        sim.SetUnits(4, 4, 0f, 280f);
        for (int i = 0; i < 240; i++) sim.Step(1f / 60f);

        sim.SetUnits(1, 0, 0f, 0f);
        sim.SetUnits(4, 0, 0f, 0f);
        sim.SetUnits(2, 16, 0f, 0f);
        for (int i = 0; i < 240; i++) sim.Step(1f / 60f);

        Assert.AreEqual(1, sim.Blobs.Count);
        Assert.AreEqual(1, WindingCount(sim.Blobs[0]));
        Assert.AreEqual(1f, SoftBlobSim.Area(sim.Blobs[0]) / (0.95f * MathF.PI * radius * radius), 0.02f);
    }

    private static int WindingCount(SoftBlobSim.Blob blob)
    {
        int n = SoftBlobSim.PointCount;
        double turning = 0d;
        for (int i = 0; i < n; i++)
        {
            int a = (i + n - 1) % n, c = (i + 1) % n;
            double ux = blob.X[i] - blob.X[a], uy = blob.Y[i] - blob.Y[a];
            double vx = blob.X[c] - blob.X[i], vy = blob.Y[c] - blob.Y[i];
            turning += Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }
        return (int)Math.Round(Math.Abs(turning) / (2d * Math.PI));
    }
}
