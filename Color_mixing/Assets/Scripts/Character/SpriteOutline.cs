using UnityEngine;

// Garis tepi putih (gaya stiker) di sekeliling sprite, tanpa ubah gambar:
// salinan sprite digeser melingkar di belakang sprite asli, digambar sebagai siluet satu warna
// (shader Resources/SpriteSolidColor) supaya garis hitam di gambar rambut tidak ikut terlihat.
// Mengikuti sprite, flip, transparansi, dan urutan render sprite asli, jadi aman untuk rambut yang
// ganti gaya / warna. Skala induk ikut mengecilkan / membesarkan garis (anak dari transform sprite).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    private const int Copies = 12;      // makin banyak makin halus di sudut tajam

    private SpriteRenderer source;
    private SpriteRenderer[] ring;
    private Material solid;             // siluet satu warna; null = pakai warna biasa (garis hitam sprite ikut terlihat)
    private float width = 0.04f;
    private Color color = Color.white;

    // Pasang garis tepi ke renderer ini. width dalam unit lokal renderer (sebelum skala induk).
    public static SpriteOutline Add(SpriteRenderer target, float width, Color color)
    {
        var outline = target.GetComponent<SpriteOutline>();
        if (outline == null) outline = target.gameObject.AddComponent<SpriteOutline>();
        outline.width = width;
        outline.color = color;
        outline.Build();
        return outline;
    }

    // Add() sudah membangun lebih dulu dengan setelannya; Start hanya untuk komponen yang dipasang manual.
    private void Start() => Build();

    private void Build()
    {
        if (ring != null) return;

        source = GetComponent<SpriteRenderer>();
        var shader = Resources.Load<Shader>("SpriteSolidColor");
        if (shader != null)
        {
            solid = new Material(shader) { name = "SpriteOutline (runtime)" };
            solid.SetColor("_Color", color);
        }
        else Debug.LogWarning("[Outline] Shader SpriteSolidColor tidak ditemukan di Resources; garis tepi tidak akan putih bersih.", this);

        ring = new SpriteRenderer[Copies];
        for (int i = 0; i < Copies; i++)
        {
            float angle = i * Mathf.PI * 2f / Copies;
            var go = new GameObject("Outline" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * width;
            ring[i] = go.AddComponent<SpriteRenderer>();
            if (solid != null) ring[i].sharedMaterial = solid;
        }
        Sync();
    }

    private void LateUpdate() => Sync();

    private void Sync()
    {
        if (ring == null || source == null) return;

        bool visible = source.enabled && source.sprite != null;
        // Dengan shader siluet, rgb diambil dari material; yang dipakai dari sini hanya transparansinya.
        var tint = solid != null ? new Color(1f, 1f, 1f, source.color.a) : new Color(color.r, color.g, color.b, color.a * source.color.a);
        foreach (var r in ring)
        {
            r.enabled = visible;
            if (!visible) continue;
            r.sprite = source.sprite;
            r.flipX = source.flipX;
            r.flipY = source.flipY;
            r.color = tint;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder - 1;   // tepat di bawah sprite asli
            r.maskInteraction = source.maskInteraction;
        }
    }

    private void OnDestroy()
    {
        if (solid != null) Destroy(solid);
    }
}
