using System;
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
    [SerializeField] private float outlineWidth = 9f;
    [SerializeField] private float highlightOffset = 27f;                // Sketch: Y 9 x 3
    [SerializeField, Range(0f, 1f)] private float highlightOpacity = 0.25f;
    [SerializeField] private float shadowOffset = 33f;                   // Sketch: Y -11 x 3
    [SerializeField, Range(0f, 1f)] private float shadowOpacity = 0.2f;
    [SerializeField] private float shadeBlur = 6f;                       // Sketch: blur 2 x 3
    [SerializeField, Range(0f, 0.06f)] private float wobbleRatio = 0.03f; // kedalaman gelombang tepi terhadap jari-jari blob
    [SerializeField] private float wobbleSpeed = 0f;                     // 0 = gelombang diam
    [SerializeField, Range(0, 2)] private int player;                    // cadangan nomor pemain saat tes tanpa jaringan
    [SerializeField] private Vector2 leftPour = new Vector2(-300f, 160f);  // tangan kiri (slot 0)
    [SerializeField] private Vector2 rightPour = new Vector2(320f, 120f);  // tangan kanan (slot 1)
    [SerializeField] private Vector2 friendPour = new Vector2(150f, 320f); // pemain lain, dari atas

    private readonly Vector2[] ring = new Vector2[RingCount];
    private readonly Vector2[] normals = new Vector2[RingCount];
    private SoftBlobSim sim;
    [SerializeField] private float swirlSpeed = 7f;        // kecepatan pusaran saat reveal (radian per detik)
    [SerializeField] private float revealDuration = 1.8f;  // lama pusaran dan perubahan warna saat reveal (detik)

    private bool revealing;     // fase reveal: semua blob dilebur jadi satu blob berwarna hasil campuran
    private Color revealColor;
    private float revealTime;
    private int[] knownUnits;   // isi mangkuk terakhir yang sudah digambar, untuk tahu pigmen mana yang bertambah
    private float accumulator;

    protected override void Awake()
    {
        base.Awake();
        if (!Application.isPlaying) return;
        sim = new SoftBlobSim(rectTransform.rect.width * 0.5f);
        knownUnits = new int[colorMixingController.Palette.Count];
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
            Array.Clear(knownUnits, 0, knownUnits.Length);
            revealing = false;
            sim.SwirlSpeed = 0f;
            return;
        }

        if (colorMixingController.IsMixConfirmed)
        {
            if (!revealing) StartReveal(bowl);
            return;
        }

        // Pigmen yang bertambah sejak terakhir digambar = baru saja dituang.
        for (int i = 0; i < knownUnits.Length; i++)
        {
            int units = bowl.PigmentUnits[i];
            if (units == knownUnits[i]) continue;

            Vector2 source = PourSource(i);
            sim.SetUnits(i, units, source.x, source.y);
            knownUnits[i] = units;
        }
    }

    // Semua pemain sudah Mix: blob paling besar menelan sisanya, lalu semuanya berwarna hasil campuran.
    private void StartReveal(MixingBowl bowl)
    {
        revealing = true;
        revealTime = 0f;
        revealColor = ColorMixer.Blend(colorMixingController.Palette, bowl.PigmentUnits);

        int keeper = 0;
        for (int i = 1; i < bowl.PigmentUnits.Count; i++)
            if (bowl.PigmentUnits[i] > bowl.PigmentUnits[keeper]) keeper = i;

        for (int i = 0; i < bowl.PigmentUnits.Count; i++)
            if (i != keeper) sim.SetUnits(i, 0, 0f, 0f);
        sim.SetUnits(keeper, bowl.TotalUnits, 0f, 0f);
    }

    // Host mencatat siapa yang terakhir menuang tiap pigmen. Tangan sendiri: kiri untuk slot 0, kanan untuk slot 1.
    // Selain itu (pemain lain, atau belum diketahui) dianggap dituang dari atas.
    private Vector2 PourSource(int pigment)
    {
        int pourer = colorMixingController.LastPourer(pigment);
        if (pourer < 0 || pourer / 2 != LocalPlayer.Index(player)) return friendPour;
        return pourer % 2 == 0 ? leftPour : rightPour;
    }

    private void Update()
    {
        if (sim == null) return;

        if (revealing)
        {
            // Pusaran naik lalu turun lagi (bentuk setengah sinus), selesai tepat di akhir revealDuration.
            revealTime += Time.deltaTime;
            float progress = Mathf.Clamp01(revealTime / revealDuration);
            sim.SwirlSpeed = swirlSpeed * Mathf.Sin(progress * Mathf.PI);
        }

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

        var style = new PaintStyle(outlineWidth, highlightOffset, highlightOpacity, shadowOffset, shadowOpacity, shadeBlur);

        foreach (SoftBlobSim.Blob blob in sim.Blobs)
        {
            BuildRing(blob);
            Color pigmentColor = colorMixingController.Palette.GetPigment(blob.Id).color;
            if (revealing) pigmentColor = Color.Lerp(pigmentColor, revealColor, Mathf.SmoothStep(0f, 1f, revealTime / revealDuration));
            PaintMesh.Draw(vh, ring, normals, RingCount, pigmentColor, style);
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

        float area = SoftBlobSim.Area(blob);
        float sign = area > 0f ? 1f : -1f;
        PaintMesh.ComputeNormals(ring, normals, RingCount, sign);

        // Gelombang hanya ke dalam, jadi tidak keluar piring dan tidak menimpa blob tetangga.
        float depth = wobbleRatio * Mathf.Sqrt(Mathf.Abs(area) / Mathf.PI);
        float phase = blob.Id * 1.9f;
        float time = Time.time * wobbleSpeed;
        for (int i = 0; i < RingCount; i++)
        {
            float u = i / (float)RingCount * 2f * Mathf.PI;
            float wave = 0.6f * Mathf.Sin(4f * u + phase + time) + 0.4f * Mathf.Sin(7f * u + phase * 1.7f - time);
            ring[i] -= normals[i] * depth * (1f - wave);
        }
        PaintMesh.ComputeNormals(ring, normals, RingCount, sign);
    }

    private static float CatmullRom(float a, float b, float c, float d, float t) =>
        0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t);
}
