using UnityEngine;
using UnityEngine.UI;

// Background "pill" (kapsul) putih bergaris tepi hitam untuk Image UI, dibuat secara prosedural
// supaya tidak perlu aset PNG. Ujungnya selalu setengah lingkaran sempurna berapa pun ukuran RectTransform-nya,
// dan tebal garis tepi ikut proporsional dengan tinggi pill.
[RequireComponent(typeof(Image))]
public class PillBackground : MonoBehaviour
{
    // Tekstur: tinggi 2R, lebar 2R+2 (1 kolom tengah untuk di-stretch). Border 9-slice = R di semua sisi.
    private const int Radius = 128;
    private const int OutlinePx = 14;   // ~9 unit pada pill setinggi 150

    private static Sprite cachedSprite;

    private Image image;
    private Canvas canvas;

    private void OnEnable()
    {
        image = GetComponent<Image>();
        image.sprite = GetSprite();
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        UpdateCorners();
    }

    private void OnRectTransformDimensionsChange() => UpdateCorners();

    private void OnTransformParentChanged()
    {
        canvas = null;
        UpdateCorners();
    }

    // Sudut 9-slice dibuat persis setengah tinggi rect: R / (spritePPU / canvasPPU * multiplier) = h / 2.
    private void UpdateCorners()
    {
        if (image == null) return;

        float height = ((RectTransform)transform).rect.height;
        if (height <= 0f) return;

        if (canvas == null) canvas = image.canvas;
        float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        float spritePpu = image.sprite != null ? image.sprite.pixelsPerUnit : 100f;

        float multiplier = 2f * Radius * canvasPpu / (height * spritePpu);
        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
            image.pixelsPerUnitMultiplier = multiplier;
    }

    private static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        int h = Radius * 2;
        int w = Radius * 2 + 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "PillBackground",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[w * h];
        float cy = h * 0.5f;
        float segStart = Radius;
        float segEnd = w - Radius;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Jarak bertanda ke tepi kapsul (negatif = di dalam), dihitung di tengah piksel untuk anti-aliasing.
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dx = px - Mathf.Clamp(px, segStart, segEnd);
                float dy = py - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - Radius;

                float outer = Mathf.Clamp01(0.5f - d);               // seluruh bentuk (garis tepi + isi)
                float inner = Mathf.Clamp01(0.5f - (d + OutlinePx)); // isi putih

                byte c = (byte)Mathf.RoundToInt(255f * inner);       // hitam di tepi, putih di dalam
                pixels[y * w + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(255f * outer));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        cachedSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(Radius, Radius, Radius, Radius));
        cachedSprite.name = "PillBackground";
        cachedSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedSprite;
    }
}
