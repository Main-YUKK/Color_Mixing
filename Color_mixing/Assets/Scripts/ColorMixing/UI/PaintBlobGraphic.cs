using UnityEngine;
using UnityEngine.UI;

// Menggambar isi mangkuk sebagai blob lunak. Luas tiap blob mengikuti rasio pigmen.
[RequireComponent(typeof(CanvasRenderer))]
public class PaintBlobGraphic : MaskableGraphic
{
    private const float StepTime = 1f / 60f;
    private const int Smooth = 3;                                        // titik gambar per titik simulasi
    private const int RingCount = SoftBlobSim.PointCount * Smooth;

    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private float outlineWidth = 8f;
    [SerializeField] private Vector2[] spawnPoints;                      // urutan = PigmentPalette

    private readonly Vector2[] ring = new Vector2[RingCount];
    private readonly Vector2[] normals = new Vector2[RingCount];
    private SoftBlobSim sim;
    private float accumulator;

    protected override void Awake()
    {
        base.Awake();
        if (Application.isPlaying) sim = new SoftBlobSim(rectTransform.rect.width * 0.5f);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Application.isPlaying) colorMixingController.StateChanged += RefreshUnits;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Application.isPlaying) colorMixingController.StateChanged -= RefreshUnits;
    }

    protected override void Start()
    {
        base.Start();
        if (Application.isPlaying) RefreshUnits();
    }

    private void RefreshUnits()
    {
        MixingBowl bowl = colorMixingController.MixingBowl;
        if (bowl.IsEmpty)
        {
            sim.Clear();
            return;
        }

        for (int i = 0; i < bowl.PigmentUnits.Count; i++)
            sim.SetUnits(i, bowl.PigmentUnits[i], spawnPoints[i].x, spawnPoints[i].y);
    }

    private void Update()
    {
        if (sim == null) return;

        accumulator += Mathf.Min(Time.deltaTime, 0.1f);
        while (accumulator >= StepTime)
        {
            sim.Step(StepTime);
            accumulator -= StepTime;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (sim == null) return;

        foreach (SoftBlobSim.Blob blob in sim.Blobs)
        {
            BuildRing(blob);
            AddFan(vh, 0f, Color.black);
            AddFan(vh, outlineWidth, colorMixingController.Palette.GetPigment(blob.Id).color);
        }
    }

    // Haluskan titik simulasi dengan Catmull-Rom, lalu hitung normal yang menghadap keluar.
    private void BuildRing(SoftBlobSim.Blob blob)
    {
        int n = SoftBlobSim.PointCount;
        for (int i = 0; i < n; i++)
        {
            int a = (i + n - 1) % n, c = (i + 1) % n, d = (i + 2) % n;
            for (int s = 0; s < Smooth; s++)
            {
                float t = s / (float)Smooth;
                ring[i * Smooth + s] = new Vector2(
                    CatmullRom(blob.X[a], blob.X[i], blob.X[c], blob.X[d], t),
                    CatmullRom(blob.Y[a], blob.Y[i], blob.Y[c], blob.Y[d], t));
            }
        }

        float sign = SoftBlobSim.Area(blob) > 0f ? 1f : -1f;
        for (int i = 0; i < RingCount; i++)
        {
            Vector2 tangent = ring[(i + 1) % RingCount] - ring[(i + RingCount - 1) % RingCount];
            normals[i] = new Vector2(tangent.y, -tangent.x).normalized * sign;
        }
    }

    private static float CatmullRom(float a, float b, float c, float d, float t) =>
        0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t);

    private void AddFan(VertexHelper vh, float inset, Color32 fanColor)
    {
        int start = vh.currentVertCount;
        Vector2 center = Vector2.zero;
        for (int i = 0; i < RingCount; i++) center += ring[i];
        center /= RingCount;

        vh.AddVert(center, fanColor, Vector2.zero);
        for (int i = 0; i < RingCount; i++)
            vh.AddVert(ring[i] - normals[i] * inset, fanColor, Vector2.zero);
        for (int i = 0; i < RingCount; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % RingCount);
    }
}
