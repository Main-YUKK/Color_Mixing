using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

// Asap "poof" di atas rambut klien: awan besar muncul memantul, beberapa awan kecil menyusul, perubahan (warna rambut,
// ekspresi) terjadi saat rambut tertutup awan, lalu awan mengembang dan menghilang.
// Dipakai lewat HairPoof.Play(...) dari coroutine; tidak perlu dipasang di objek mana pun.
public static class HairPoof
{
    [Serializable]
    public class Settings
    {
        [Tooltip("Sprite awan (Models/UI/ClientArrivalScene/poof).")]
        public Sprite sprite;
        [Tooltip("Ukuran awan dibanding rambut (1 = selebar rambut).")]
        public float coverScale = 1.5f;
        [Tooltip("Detik awan membesar sampai menutupi rambut.")]
        public float growSeconds = 0.25f;
        [Tooltip("Detik awan diam menutupi (perubahan terjadi di awal bagian ini).")]
        public float holdSeconds = 0.3f;
        [Tooltip("Detik awan mengembang dan menghilang.")]
        public float fadeSeconds = 0.35f;
        [Tooltip("Awan kecil tambahan di sekitar awan utama.")]
        public int extraPuffs = 3;
        [Tooltip("Urutan render di atas badan / rambut klien.")]
        public int sortingAbove = 20;
    }

    /// <summary>
    /// Mainkan poof di rambut klien. onCovered dipanggil saat rambut tertutup awan (ganti warna di sini).
    /// Selesai saat awan sudah hilang.
    /// </summary>
    public static IEnumerator Play(MonoBehaviour host, GameObject client, Settings s, Action onCovered)
    {
        if (s == null || s.sprite == null || client == null)
        {
            onCovered?.Invoke();   // tanpa sprite awan: langsung ganti, tanpa animasi
            yield break;
        }

        Bounds area = HairArea(client, out int layer, out int order);
        float size = Mathf.Max(area.size.x, area.size.y) * s.coverScale;
        float spriteSize = Mathf.Max(s.sprite.bounds.size.x, s.sprite.bounds.size.y);
        float baseScale = spriteSize > 0f ? size / spriteSize : 1f;

        // awan utama + awan kecil yang menyusul sedikit terlambat
        int count = 1 + Mathf.Max(0, s.extraPuffs);
        var puffs = new SpriteRenderer[count];
        var delays = new float[count];
        var scales = new float[count];
        var spins = new float[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject(i == 0 ? "Poof" : "Poof (small)");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s.sprite;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + s.sortingAbove + (i == 0 ? 1 : 0);   // awan utama di depan
            Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * size * Random.Range(0.3f, 0.45f);
            go.transform.position = area.center + (Vector3)offset;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
            go.transform.localScale = Vector3.zero;
            if (Random.value < 0.5f) sr.flipX = true;   // variasi supaya tidak terlihat sama
            puffs[i] = sr;
            delays[i] = i == 0 ? 0f : Random.Range(0.03f, 0.15f);
            scales[i] = baseScale * (i == 0 ? 1f : Random.Range(0.35f, 0.55f));
            spins[i] = Random.Range(-25f, 25f);
        }

        float total = s.growSeconds + s.holdSeconds + s.fadeSeconds + 0.15f;
        bool covered = false;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < count; i++)
            {
                if (puffs[i] == null) continue;
                float lt = t - delays[i];
                Transform tr = puffs[i].transform;
                float k;
                float alpha = 1f;
                if (lt < 0f) k = 0f;
                else if (lt < s.growSeconds) k = EaseOutBack(lt / s.growSeconds);                         // muncul memantul
                else if (lt < s.growSeconds + s.holdSeconds)
                    k = 1f + 0.04f * Mathf.Sin((lt - s.growSeconds) * 25f);                                // bergolak sedikit
                else
                {
                    float p = Mathf.Clamp01((lt - s.growSeconds - s.holdSeconds) / Mathf.Max(0.01f, s.fadeSeconds));
                    k = 1f + 0.35f * p;                                                                    // mengembang
                    alpha = 1f - p * p;                                                                    // lalu hilang
                }
                tr.localScale = Vector3.one * (scales[i] * k);
                tr.Rotate(0f, 0f, spins[i] * Time.deltaTime);
                Color c = puffs[i].color;
                c.a = alpha;
                puffs[i].color = c;
            }

            // rambut sudah tertutup awan utama: waktunya ganti
            if (!covered && t >= s.growSeconds * 0.8f)
            {
                covered = true;
                onCovered?.Invoke();
            }
            yield return null;
        }

        if (!covered) onCovered?.Invoke();
        foreach (var p in puffs) if (p != null) UnityEngine.Object.Destroy(p.gameObject);
    }

    // Area rambut (atau kepala kalau rambut kosong / tersembunyi) + sorting untuk menaruh awan di depannya.
    private static Bounds HairArea(GameObject client, out int layer, out int order)
    {
        var body = client.GetComponent<SpriteRenderer>();
        client.TryGetComponent(out RandomCharacter character);
        var hair = character != null ? character.hairRenderer : null;

        layer = body != null ? body.sortingLayerID : 0;
        order = body != null ? body.sortingOrder : 0;
        if (hair != null) order = Mathf.Max(order, hair.sortingOrder);

        if (hair != null && hair.enabled && hair.sprite != null && hair.bounds.size.sqrMagnitude > 0.0001f)
            return hair.bounds;

        // tanpa rambut: sepertiga atas badan
        if (body != null)
        {
            Bounds b = body.bounds;
            return new Bounds(new Vector3(b.center.x, b.max.y - b.size.y / 6f, b.center.z), new Vector3(b.size.x * 0.5f, b.size.y / 3f, 0f));
        }
        return new Bounds(client.transform.position, Vector3.one);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
