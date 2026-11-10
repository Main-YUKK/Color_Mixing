using System.Collections;
using UnityEngine;

// Tetesan cat dari mulut botol ke mangkuk.
// Pasang di layer kosong (stretch) di dalam MixingPanel: di atas mangkuk, di bawah tangan dan botol.
// Satu tuangan = satu tetes per varian (anak nonaktif), jadi bentuk/ukuran/gaya tiap tetes diatur di variannya masing-masing.
public class PaintDrops : MonoBehaviour
{
    [SerializeField] private PaintDropGraphic[] variants;  // tetes ke-1, ke-2, ... (nonaktif, disalin tiap tuangan)
    [SerializeField] private RectTransform target;         // mangkuk: tetesan jatuh ke sekitar pusatnya
    [SerializeField] private float flightTime = 0.35f;
    [SerializeField] private float stagger = 0.06f;        // jeda antar tetes dalam satu tuangan
    [SerializeField] private float spread = 60f;           // sebaran titik jatuh di mangkuk
    [SerializeField] private float arcHeight = 80f;        // tinggi lengkungan lintasan
    [SerializeField] private bool tipLeads;                // false = ujung lancip di belakang (ekor ke arah botol)

    public void Drop(Vector3 worldFrom, Color color)
    {
        for (int i = 0; i < variants.Length; i++)
            StartCoroutine(Fly(variants[i], worldFrom, color, i * stagger));
    }

    // Panel disembunyikan di tengah animasi: buang tetesan yang tertinggal (varian dibiarkan).
    private void OnDisable()
    {
        foreach (Transform child in transform)
            if (System.Array.FindIndex(variants, v => v.transform == child) < 0) Destroy(child.gameObject);
    }

    // ponytail: Instantiate/Destroy per tetes, ganti pool kalau profiler menunjukkan GC spike.
    private IEnumerator Fly(PaintDropGraphic variant, Vector3 worldFrom, Color color, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        PaintDropGraphic drop = Instantiate(variant, transform);
        drop.gameObject.SetActive(true);
        drop.color = color;
        RectTransform rect = drop.rectTransform;

        // Hitung di ruang lokal layer ini supaya busur dan sebaran dalam satuan UI.
        Vector2 from = transform.InverseTransformPoint(worldFrom);
        Vector2 to = (Vector2)transform.InverseTransformPoint(target.position) + Random.insideUnitCircle * spread;
        float startScale = variant.transform.localScale.x;

        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = t / flightTime;
            rect.localPosition = Vector2.Lerp(from, to, k) + Vector2.up * (arcHeight * 4f * k * (1f - k));
            rect.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 0.5f, k);

            // Ujung tetesan mengikuti arah gerak (turunan lintasan).
            Vector2 velocity = (to - from) + Vector2.up * (arcHeight * 4f * (1f - 2f * k));
            float heading = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            drop.Angle = tipLeads ? heading : heading + 180f;
            yield return null;
        }
        Destroy(drop.gameObject);
    }
}
