using System.Collections;
using UnityEngine;

// Animasi masuk: objek naik dari bawah ke posisi aslinya setiap kali diaktifkan (mis. kertas daftar pigmen di reveal).
public class SlideUpOnEnable : MonoBehaviour
{
    [SerializeField] private float distance = 1200f;   // seberapa jauh di bawah posisi akhir saat mulai
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float delay = 0f;

    private RectTransform rect;
    private Vector2 target;

    private void Awake()
    {
        rect = (RectTransform)transform;
        target = rect.anchoredPosition;   // posisi yang diatur di Inspector = posisi akhir
    }

    private void OnEnable() => StartCoroutine(Slide());

    private IEnumerator Slide()
    {
        Vector2 start = target + Vector2.down * distance;
        rect.anchoredPosition = start;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);   // ease-out: cepat lalu melambat
            rect.anchoredPosition = Vector2.LerpUnclamped(start, target, k);
            yield return null;
        }
        rect.anchoredPosition = target;
    }
}
