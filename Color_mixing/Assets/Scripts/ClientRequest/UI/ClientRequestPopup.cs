using System.Collections;
using UnityEngine;

// Popup request klien (versi besar): muncul membesar saat tombol klien ditekan, tertutup saat layar ditekan di mana saja.
// Pasang di root popup (yang punya CanvasGroup + Image gelap layar penuh + Button yang memanggil Close).
// Waktu pakai unscaled, jadi tetap jalan walau game dijeda.
public class ClientRequestPopup : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;      // fade seluruh popup (latar gelap + isi)
    [SerializeField] private RectTransform content;  // lingkaran + bubble, yang di-scale
    [SerializeField] private float openSeconds = 0.25f;
    [SerializeField] private float closeSeconds = 0.15f;
    [SerializeField, Range(0f, 1f)] private float fromScale = 0.6f;

    public void Open()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Animate(true));
    }

    public void Close()
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(Animate(false));
    }

    private IEnumerator Animate(bool show)
    {
        float seconds = show ? openSeconds : closeSeconds;
        group.blocksRaycasts = show;   // klik saat menutup tidak memicu Close lagi
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            Apply(show ? k : 1f - k, show);
            yield return null;
        }
        Apply(show ? 1f : 0f, show);
        if (!show) gameObject.SetActive(false);
    }

    private void Apply(float k, bool opening)
    {
        group.alpha = k;
        // Membuka: sedikit memantul (ease-out back). Menutup: mengecil biasa.
        float s = opening ? EaseOutBack(k) : k;
        content.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, s);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
