using UnityEngine;
using UnityEngine.UI;

// Satu tetes cat berbentuk tetesan/daun dengan gaya yang sama seperti blob mangkuk (outline + highlight + shadow).
// Warna diambil dari Graphic.color. Bentuk diputar di mesh (bukan RectTransform), jadi cahaya tetap dari atas.
[RequireComponent(typeof(CanvasRenderer))]
public class PaintDropGraphic : MaskableGraphic
{
    private const int PointCount = 48;

    [SerializeField] private float length = 56f;                       // ujung lancip -> ujung bulat
    [SerializeField] private float width = 40f;
    [SerializeField, Range(0.5f, 4f)] private float tipSharpness = 1.5f; // makin besar makin lancip
    [SerializeField, Range(-0.5f, 0.5f)] private float bend = 0.15f;     // lengkung seperti kelopak
    [SerializeField] private float angle;                                // arah ujung lancip (derajat, 0 = kanan)
    [SerializeField] private PaintStyle style = new PaintStyle(4f, 8f, 0.25f, 10f, 0.2f, 3f);

    private readonly Vector2[] ring = new Vector2[PointCount];
    private readonly Vector2[] normals = new Vector2[PointCount];

    public float Angle
    {
        get => angle;
        set { angle = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        // Kurva tetesan: x = cos t, y = sin t * sin(t/2)^k. Ujung lancip di x = 1, ujung bulat di x = -1. Urutan berlawanan jarum jam.
        for (int i = 0; i < PointCount; i++)
        {
            float t = i / (float)PointCount * 2f * Mathf.PI;
            float x = Mathf.Cos(t);
            float y = Mathf.Sin(t) * Mathf.Pow(Mathf.Sin(t * 0.5f), tipSharpness);
            var point = new Vector2(x * length * 0.5f, y * width * 0.5f + bend * width * (1f - x * x));
            ring[i] = rotation * point;
        }

        PaintMesh.ComputeNormals(ring, normals, PointCount, 1f);
        PaintMesh.Draw(vh, ring, normals, PointCount, color, style);
    }
}
