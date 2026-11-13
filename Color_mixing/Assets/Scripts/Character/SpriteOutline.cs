using UnityEngine;

// Garis tepi putih (gaya stiker) di sekeliling sprite, tanpa ubah gambar:
// salinan sprite digeser melingkar di belakang sprite asli, digambar sebagai siluet satu warna
// (shader Resources/SpriteSolidColor) supaya garis hitam di gambar rambut tidak ikut terlihat.
// Mengikuti sprite, flip, transparansi, dan urutan render sprite asli, jadi aman untuk rambut yang
// ganti gaya / warna. Tebal garis dalam unit dunia (tidak terpengaruh skala induk / animasi skala rambut).
// Dengan partner (mis. badan <-> rambut), garis keduanya digambar di bawah SEMUA sprite yang dipartnerkan,
// jadi hanya tepi luar gabungan yang terlihat (gaya cutout) dan tidak ada garis di antara badan dan rambut.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    private const int Copies = 12;      // makin banyak makin halus di sudut tajam

    private SpriteRenderer source;
    private SpriteRenderer[] ring;
    private Material solid;             // siluet satu warna; null = pakai warna biasa (garis hitam sprite ikut terlihat)
    private int depth;                   // 0 = garis dalam; 1 = garis luar (digambar satu lapis lebih bawah)
    private SpriteRenderer partner;     // sprite lain yang digabung dalam satu siluet
    private float width = 0.04f;
    private Color color = Color.white;
    private bool shown = true;

    // Garis tepi bisa dimatikan sementara (mis. rambut sheet berwarna yang sudah punya warna sendiri).
    public bool Shown
    {
        get => shown;
        set { shown = value; Sync(); }
    }

    // Pasang garis tepi ke renderer ini. width dalam unit dunia. partner (opsional) = sprite lain yang
    // satu siluet dengan ini: garis digambar di bawah yang paling bawah dari keduanya.
    public static SpriteOutline Add(SpriteRenderer target, float width, Color color, SpriteRenderer partner = null, int depth = 0)
    {
        SpriteOutline outline = null;
        foreach (var o in target.GetComponents<SpriteOutline>()) if (o.depth == depth) outline = o;
        if (outline == null) outline = target.gameObject.AddComponent<SpriteOutline>();
        outline.depth = depth;
        outline.width = width;
        outline.color = color;
        outline.partner = partner;
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
            go.transform.localPosition = Offset(i);
            ring[i] = go.AddComponent<SpriteRenderer>();
            if (solid != null) ring[i].sharedMaterial = solid;
        }
        Sync();
    }

    private void LateUpdate() => Sync();

    // Geser tiap salinan sejauh `width` di dunia: dibagi skala induk supaya tebal garis tetap walau skala berubah.
    private Vector3 Offset(int i)
    {
        float angle = i * Mathf.PI * 2f / Copies;
        var s = transform.lossyScale;
        float sx = Mathf.Abs(s.x), sy = Mathf.Abs(s.y);
        if (sx < 1e-4f || sy < 1e-4f) return Vector3.zero;    // rambut sedang skala 0 (belum muncul)
        return new Vector3(Mathf.Cos(angle) * width / sx, Mathf.Sin(angle) * width / sy, 0f);
    }

    private void Sync()
    {
        if (ring == null || source == null) return;

        bool visible = shown && source.enabled && source.sprite != null;
        // Dengan shader siluet, rgb diambil dari material; yang dipakai dari sini hanya transparansinya.
        var tint = solid != null ? new Color(1f, 1f, 1f, source.color.a) : new Color(color.r, color.g, color.b, color.a * source.color.a);
        int order = source.sortingOrder;
        if (partner != null) order = Mathf.Min(order, partner.sortingOrder);
        for (int i = 0; i < ring.Length; i++)
        {
            var r = ring[i];
            r.enabled = visible;
            if (!visible) continue;
            r.transform.localPosition = Offset(i);
            r.sprite = source.sprite;
            r.flipX = source.flipX;
            r.flipY = source.flipY;
            r.color = tint;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = order - 1 - depth;         // tepat di bawah sprite paling bawah
            r.maskInteraction = source.maskInteraction;
        }
    }

    private void OnDestroy()
    {
        if (solid != null) Destroy(solid);
    }
}
