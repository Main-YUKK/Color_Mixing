using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Kartu awal hari ("Hari ke-2 / Target Hari ini: 3 puas"). Tampil sebentar tiap hari baru dimulai, lalu menutup sendiri.
// Pasang di akar prefab kartunya (objek ini tetap aktif; tampil / sembunyi lewat CanvasGroup, supaya event tidak terlepas).
//
// Waktu klien tidak terpotong: timer baru jalan setelah klien selesai masuk dan layar mixing terbuka.
// Semua isian boleh kosong; yang kosong dilewati. Waktu pakai unscaled.
public class StartOfDayCard : MonoBehaviour
{
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private DayCycleManager cycle;
    [Tooltip("Kartu yang membesar saat muncul. Kosong = tanpa efek membesar, hanya memudar.")]
    [SerializeField] private Transform card;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Bagian angkanya saja (\"3 puas\"). Tulisan \"Target Hari ini:\" dan ikonnya objek terpisah di prefab.")]
    [SerializeField] private TMP_Text targetText;
    [Tooltip("{0} = nomor hari.")]
    [SerializeField] private string titleFormat = "Hari ke-{0}";
    [Tooltip("{0} = target klien puas.")]
    [SerializeField] private string targetFormat = "{0} puas";

    [Header("Waktu (detik)")]
    [SerializeField] private float inSeconds = 0.35f;
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float outSeconds = 0.25f;

    [Tooltip("Titik pasang suara nanti (belum ada audio).")]
    [SerializeField] private UnityEvent onShown;

    private CanvasGroup group;
    private Coroutine routine;

    // true dari saat kartu diminta tampil sampai selesai menghilang (klien menunggu kartu ini sebelum masuk).
    public bool IsShowing => routine != null;
    private Vector3 cardBaseScale = Vector3.one;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (card != null) cardBaseScale = card.localScale;
        SetVisible(false);
    }

    private void OnEnable()
    {
        // Prefab tidak bisa menunjuk objek scene, jadi kalau kosong dicari otomatis.
        if (cycle == null) cycle = FindFirstObjectByType<DayCycleManager>();
        if (cycle == null) { Debug.LogError("StartOfDayCard: DayCycleManager tidak ditemukan di scene.", this); enabled = false; return; }
        cycle.OnDayStarted += Show;
        cycle.OnGameOver += Hide;
    }

    private void OnDisable()
    {
        if (cycle == null) return;
        cycle.OnDayStarted -= Show;
        cycle.OnGameOver -= Hide;
    }

    private void Show(int day)
    {
        if (titleText != null) titleText.text = string.Format(titleFormat, day);
        if (targetText != null) targetText.text = string.Format(targetFormat, GameRules.YesToWinDay);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        SetVisible(true);
        group.alpha = 0f;
        onShown?.Invoke();

        for (float t = 0f; t < inSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / inSeconds);
            group.alpha = Mathf.Clamp01(k * 2f);
            if (card != null) card.localScale = cardBaseScale * Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(k));
            yield return null;
        }
        group.alpha = 1f;
        if (card != null) card.localScale = cardBaseScale;

        for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime) yield return null;

        for (float t = 0f; t < outSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - Mathf.Clamp01(t / outSeconds);
            yield return null;
        }

        routine = null;
        SetVisible(false);
    }

    private void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (card != null) card.localScale = cardBaseScale;
        SetVisible(false);
    }

    // Tampil = menangkap tap (layar di bawah tidak bisa ditekan saat kartu muncul).
    private void SetVisible(bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
