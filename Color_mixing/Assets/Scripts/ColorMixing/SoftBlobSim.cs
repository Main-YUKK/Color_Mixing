using System;
using System.Collections.Generic;

// Simulasi blob cat: tiap blob = ring titik yang dijaga luasnya, tanpa ketergantungan ke Unity.
// Koordinat berpusat di tengah piring. Rasio ditentukan lewat jumlah unit tiap blob.
public sealed class SoftBlobSim
{
    public sealed class Blob
    {
        public readonly int Id;
        public readonly float[] X, Y;
        internal readonly float[] PrevX, PrevY, VelX, VelY;
        public int Units { get; internal set; }
        internal float TargetArea, GoalArea;

        internal Blob(int id, int pointCount)
        {
            Id = id;
            X = new float[pointCount]; Y = new float[pointCount];
            PrevX = new float[pointCount]; PrevY = new float[pointCount];
            VelX = new float[pointCount]; VelY = new float[pointCount];
        }
    }

    // MARK: - Tuning
    public const int PointCount = 36;
    private const float FillRatio = 0.92f;      // total luas cat terhadap luas piring
    private const int SubSteps = 2;
    private const int Iterations = 6;
    private const float Damping = 0.95f;        // naikkan supaya goyangan lebih lama
    private const float SkinStiffness = 0.2f;
    private const float Tension = 0.04f;
    private const float PressureStiffness = 0.15f;
    private const float GrowRate = 3f;          // kecepatan luas menuju target
    private const float MaxPush = 2f;           // batas dorongan tekanan per iterasi
    private const float MaxSpeed = 900f;
    private const float PokeStrength = 260f;    // kekuatan goyangan saat cat masuk
    private const float CreaseCos = 0.8f;       // sudut lebih tajam dari ini dihaluskan kuat
    private const float CreaseStrength = 0.8f;
    private const float SpawnRadiusRatio = 0.05f;

    private readonly float plateRadius;
    private readonly float paintArea;
    private readonly List<Blob> blobs = new List<Blob>();
    private readonly float[] gradX = new float[PointCount];
    private readonly float[] gradY = new float[PointCount];
    private readonly Random random = new Random();

    public IReadOnlyList<Blob> Blobs => blobs;

    public SoftBlobSim(float plateRadius)
    {
        this.plateRadius = plateRadius;
        paintArea = FillRatio * MathF.PI * plateRadius * plateRadius;
    }

    // MARK: - Public API
    // Samakan jumlah unit satu pigmen. Blob baru muncul di (spawnX, spawnY).
    public void SetUnits(int id, int units, float spawnX, float spawnY)
    {
        Blob blob = Find(id);
        if (blob == null)
        {
            if (units <= 0) return;
            blob = Spawn(id, spawnX, spawnY);
        }
        if (blob.Units == units) return;

        bool added = units > blob.Units;
        blob.Units = units;
        UpdateGoals();
        if (added) Poke(blob);
    }

    public void Clear() => blobs.Clear();

    public void Step(float dt)
    {
        float h = dt / SubSteps;
        for (int s = 0; s < SubSteps; s++)
        {
            foreach (Blob b in blobs)
            {
                b.TargetArea += (b.GoalArea - b.TargetArea) * MathF.Min(1f, GrowRate * h);
                for (int i = 0; i < PointCount; i++)
                {
                    b.PrevX[i] = b.X[i];
                    b.PrevY[i] = b.Y[i];
                    b.X[i] += b.VelX[i] * h;
                    b.Y[i] += b.VelY[i] * h;
                }
            }

            for (int it = 0; it < Iterations; it++) Solve();

            foreach (Blob b in blobs)
            {
                for (int i = 0; i < PointCount; i++)
                {
                    float vx = (b.X[i] - b.PrevX[i]) / h * Damping;
                    float vy = (b.Y[i] - b.PrevY[i]) / h * Damping;
                    float speed = MathF.Sqrt(vx * vx + vy * vy);
                    if (speed > MaxSpeed) { vx *= MaxSpeed / speed; vy *= MaxSpeed / speed; }
                    b.VelX[i] = vx;
                    b.VelY[i] = vy;
                }
            }
        }
    }

    public static float Area(Blob b)
    {
        float a = 0f;
        for (int i = 0; i < PointCount; i++)
        {
            int j = (i + 1) % PointCount;
            a += b.X[i] * b.Y[j] - b.X[j] * b.Y[i];
        }
        return a * 0.5f;
    }

    // MARK: - Setup
    private Blob Find(int id)
    {
        foreach (Blob b in blobs) if (b.Id == id) return b;
        return null;
    }

    private Blob Spawn(int id, float cx, float cy)
    {
        var b = new Blob(id, PointCount);
        float r = plateRadius * SpawnRadiusRatio;
        for (int i = 0; i < PointCount; i++)
        {
            float t = i / (float)PointCount * 2f * MathF.PI;
            b.X[i] = cx + r * MathF.Cos(t);
            b.Y[i] = cy + r * MathF.Sin(t);
        }
        b.TargetArea = Area(b);
        blobs.Add(b);
        return b;
    }

    private void UpdateGoals()
    {
        int total = 0;
        foreach (Blob b in blobs) total += b.Units;
        foreach (Blob b in blobs) b.GoalArea = total == 0 ? 0f : (float)b.Units / total * paintArea;
    }

    // Dorongan lokal ke arah tengah blob, supaya bergoyang saat cat masuk.
    private void Poke(Blob b)
    {
        int center = random.Next(PointCount);
        float mx = 0f, my = 0f;
        for (int i = 0; i < PointCount; i++) { mx += b.X[i]; my += b.Y[i]; }
        mx /= PointCount; my /= PointCount;

        for (int o = -4; o <= 4; o++)
        {
            int i = (center + o + PointCount) % PointCount;
            float dx = mx - b.X[i], dy = my - b.Y[i];
            float d = MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 1f);
            float w = MathF.Exp(-o * o / 6f) * PokeStrength;
            b.VelX[i] += dx / d * w;
            b.VelY[i] += dy / d * w;
        }
    }

    // MARK: - Solver
    private void Solve()
    {
        foreach (Blob b in blobs)
        {
            SolveSkin(b);
            SolveTension(b);
            SolvePressure(b);
        }
        for (int m = 0; m < blobs.Count; m++)
            for (int n = 0; n < blobs.Count; n++)
                if (m != n) SolveCollision(blobs[m], blobs[n]);
        foreach (Blob b in blobs) KeepInsidePlate(b);
    }

    // Ratakan jarak antar titik.
    private static void SolveSkin(Blob b)
    {
        float perimeter = 0f;
        for (int i = 0; i < PointCount; i++)
        {
            int j = (i + 1) % PointCount;
            float dx = b.X[j] - b.X[i], dy = b.Y[j] - b.Y[i];
            perimeter += MathF.Sqrt(dx * dx + dy * dy);
        }
        float rest = perimeter / PointCount;

        for (int i = 0; i < PointCount; i++)
        {
            int j = (i + 1) % PointCount;
            float dx = b.X[j] - b.X[i], dy = b.Y[j] - b.Y[i];
            float d = MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 1e-4f);
            float c = (d - rest) / d * 0.5f * SkinStiffness;
            b.X[i] += dx * c; b.Y[i] += dy * c;
            b.X[j] -= dx * c; b.Y[j] -= dy * c;
        }
    }

    // Tarik titik ke tengah tetangganya; sudut tajam dihaluskan lebih kuat.
    private static void SolveTension(Blob b)
    {
        for (int i = 0; i < PointCount; i++)
        {
            int a = (i + PointCount - 1) % PointCount, c = (i + 1) % PointCount;
            float ux = b.X[i] - b.X[a], uy = b.Y[i] - b.Y[a];
            float vx = b.X[c] - b.X[i], vy = b.Y[c] - b.Y[i];
            float len = MathF.Max(MathF.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy)), 1e-4f);
            float k = (ux * vx + uy * vy) / len < CreaseCos ? CreaseStrength : Tension;
            b.X[i] += ((b.X[a] + b.X[c]) * 0.5f - b.X[i]) * k;
            b.Y[i] += ((b.Y[a] + b.Y[c]) * 0.5f - b.Y[i]) * k;
        }
    }

    // Dorong titik sepanjang normal sampai luas blob mendekati target.
    private void SolvePressure(Blob b)
    {
        float error = b.TargetArea - Area(b);
        float gradSq = 0f;
        for (int i = 0; i < PointCount; i++)
        {
            int a = (i + PointCount - 1) % PointCount, c = (i + 1) % PointCount;
            gradX[i] = (b.Y[c] - b.Y[a]) * 0.5f;
            gradY[i] = (b.X[a] - b.X[c]) * 0.5f;
            gradSq += gradX[i] * gradX[i] + gradY[i] * gradY[i];
        }
        gradSq = MathF.Max(gradSq, 1e-4f);

        float gradSize = MathF.Sqrt(gradSq / PointCount);
        float limit = MaxPush / gradSize;
        float lambda = Math.Clamp(error / gradSq * PressureStiffness, -limit, limit);
        for (int i = 0; i < PointCount; i++)
        {
            b.X[i] += gradX[i] * lambda;
            b.Y[i] += gradY[i] * lambda;
        }
    }

    // Titik `a` yang masuk ke `b` didorong ke tepi terdekat `b`, dibagi dua sisi.
    private static void SolveCollision(Blob a, Blob b)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < PointCount; i++)
        {
            minX = MathF.Min(minX, b.X[i]); maxX = MathF.Max(maxX, b.X[i]);
            minY = MathF.Min(minY, b.Y[i]); maxY = MathF.Max(maxY, b.Y[i]);
        }

        for (int i = 0; i < PointCount; i++)
        {
            float x = a.X[i], y = a.Y[i];
            if (x < minX || x > maxX || y < minY || y > maxY || !Contains(b, x, y)) continue;

            float best = float.MaxValue, bestT = 0f, qx = 0f, qy = 0f;
            int bestEdge = 0;
            for (int j = 0; j < PointCount; j++)
            {
                int k = (j + 1) % PointCount;
                float ex = b.X[k] - b.X[j], ey = b.Y[k] - b.Y[j];
                float t = ((x - b.X[j]) * ex + (y - b.Y[j]) * ey) / MathF.Max(ex * ex + ey * ey, 1e-4f);
                t = Math.Clamp(t, 0f, 1f);
                float cx = b.X[j] + ex * t, cy = b.Y[j] + ey * t;
                float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d < best) { best = d; bestEdge = j; bestT = t; qx = cx; qy = cy; }
            }

            float dx = (qx - x) * 0.5f, dy = (qy - y) * 0.5f;
            int next = (bestEdge + 1) % PointCount;
            a.X[i] += dx; a.Y[i] += dy;
            b.X[bestEdge] -= dx * (1f - bestT); b.Y[bestEdge] -= dy * (1f - bestT);
            b.X[next] -= dx * bestT; b.Y[next] -= dy * bestT;
        }
    }

    private static bool Contains(Blob b, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = PointCount - 1; i < PointCount; j = i++)
        {
            float yi = b.Y[i], yj = b.Y[j];
            if ((yi > y) != (yj > y) && x < (b.X[j] - b.X[i]) * (y - yi) / (yj - yi) + b.X[i])
                inside = !inside;
        }
        return inside;
    }

    private void KeepInsidePlate(Blob b)
    {
        for (int i = 0; i < PointCount; i++)
        {
            float d = MathF.Sqrt(b.X[i] * b.X[i] + b.Y[i] * b.Y[i]);
            if (d <= plateRadius) continue;
            b.X[i] *= plateRadius / d;
            b.Y[i] *= plateRadius / d;
        }
    }
}
