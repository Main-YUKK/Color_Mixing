using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Kartu hasil hari. Progres sudah disimpan oleh DayCycleManager sebelum kartu ini tampil.
//
// Tanpa objek animasi (Card / Stamp / Saved Message kosong): kartu tampil, lalu hari berikutnya mulai
// otomatis setelah Show Seconds, atau saat NextDay() dipanggil (mis. dari tombol).
// Dengan objek animasi, urutannya:  kartu muncul -> cap SUKSES -> "Progressmu tersimpan" -> hari berikutnya.
// Isi hanya yang sudah ada aset-nya; langkah yang kosong dilewati.
public class EndOfDayReport : MonoBehaviour
{
    [Tooltip("Kosong = dicari otomatis di scene.")]
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text targetText;
    [Tooltip("{0} = nomor hari.")]
    [SerializeField] private string titleFormat = "Hari ke-{0}";
    [Tooltip("{0} = klien puas, {1} = target.")]
    [SerializeField] private string targetFormat = "{0}/{1} puas";
    [Tooltip("Klien terakhir pergi dulu, baru laporan tampil. Kosong = dicari otomatis di scene; kalau tidak ada, laporan langsung tampil.")]
    [SerializeField] private CharacterSpawner spawner;
    [Tooltip("0 = tidak otomatis, menunggu NextDay() dari tombol.\nTanpa objek animasi: lama kartu tampil. Dengan animasi: tidak dipakai lamanya, hanya menentukan otomatis atau tidak (lama tahan = Hold After Saved).")]
    [SerializeField] private float showSeconds = 3f;

    [Header("Animasi (opsional)")]
    [Tooltip("Kartu yang muncul dengan efek membesar.")]
    [SerializeField] private Transform card;
    [Tooltip("Ikon klien puas (mis. 3 kepala). Yang tampil = jumlah klien puas hari itu.")]
    [SerializeField] private GameObject[] satisfiedHeads;
    [Tooltip("Cap SUKSES: muncul setelah kartu, dengan efek menghantam.")]
    [SerializeField] private GameObject stamp;
    [Tooltip("\"Progressmu tersimpan\": muncul setelah cap.")]
    [SerializeField] private GameObject savedMessage;
    [SerializeField] private float cardInSeconds = 0.35f;
    [SerializeField] private float stampDelay = 0.5f;       // jeda setelah kartu sebelum cap
    [SerializeField] private float stampSeconds = 0.25f;
    [SerializeField] private float savedDelay = 0.7f;       // jeda setelah cap sebelum pesan tersimpan
    [SerializeField] private float savedSeconds = 0.3f;
    [SerializeField] private float holdAfterSaved = 1.5f;   // lama pesan tersimpan terbaca sebelum hari berikutnya
    [Tooltip("Titik pasang suara nanti (belum ada audio).")]
    [SerializeField] private UnityEvent onCardShown;
    [SerializeField] private UnityEvent onStamped;
    [SerializeField] private UnityEvent onSaved;

    private Coroutine autoNext;
    private Coroutine pendingShow;
    private Coroutine sequence;
    private Vector3 cardBaseScale = Vector3.one;
    private Vector2 savedBasePosition;

    private bool HasAnimation => card != null || stamp != null || savedMessage != null;

    private void OnEnable()
    {
        // Prefab tidak bisa menunjuk objek scene, jadi kalau kosong dicari otomatis.
        if (cycle == null) cycle = FindFirstObjectByType<DayCycleManager>();
        if (cycle == null) { Debug.LogError("EndOfDayReport: DayCycleManager tidak ditemukan di scene.", this); enabled = false; return; }
        cycle.holdForReport = true;
        cycle.OnDayEnded += OnDayEnded;
        cycle.OnGameOver += Hide;
        cycle.OnClientRequested += Hide;   // hari berikutnya sudah mulai (mis. perintah host datang lebih dulu): tutup laporan
    }

    private void OnDisable()
    {
        if (cycle == null) return;
        cycle.holdForReport = false;
        cycle.OnDayEnded -= OnDayEnded;
        cycle.OnGameOver -= Hide;
        cycle.OnClientRequested -= Hide;
    }

    private void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<CharacterSpawner>();
        if (card != null) cardBaseScale = card.localScale;
        if (savedMessage != null && savedMessage.transform is RectTransform savedRect) savedBasePosition = savedRect.anchoredPosition;
        Hide();
    }

    // Klien terakhir pergi dulu, baru laporan tampil.
    private void OnDayEnded(DayResult r)
    {
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = StartCoroutine(ShowAfterClientLeaves(r));
    }

    private IEnumerator ShowAfterClientLeaves(DayResult r)
    {
        if (spawner != null)
        {
            while (spawner.IsBusy) yield return null;   // klien masih berjalan
            spawner.Dismiss();
            yield return null;
            while (spawner.IsBusy) yield return null;   // tunggu sampai keluar pintu
        }

        pendingShow = null;
        Show(r);
    }

    private void Show(DayResult r)
    {
        if (titleText != null) titleText.text = string.Format(titleFormat, r.day);
        if (targetText != null) targetText.text = string.Format(targetFormat, r.yesCount, GameRules.YesToWinDay);
        for (int i = 0; i < satisfiedHeads.Length; i++)
            if (satisfiedHeads[i] != null) satisfiedHeads[i].SetActive(i < r.yesCount);
        SetVisible(true);

        if (autoNext != null) StopCoroutine(autoNext);
        if (sequence != null) StopCoroutine(sequence);

        if (HasAnimation) sequence = StartCoroutine(PlaySequence());
        else if (showSeconds > 0f) autoNext = StartCoroutine(AutoNext());
    }

    // Kartu -> cap SUKSES -> "Progressmu tersimpan" -> hari berikutnya.
    private IEnumerator PlaySequence()
    {
        if (stamp != null) stamp.SetActive(false);
        if (savedMessage != null) savedMessage.SetActive(false);

        if (card != null) yield return ScaleIn(card, cardBaseScale, 0.7f, cardInSeconds);
        onCardShown?.Invoke();

        if (stamp != null)
        {
            yield return Wait(stampDelay);
            stamp.SetActive(true);
            onStamped?.Invoke();
            yield return Slam(stamp.transform, 2.2f, stampSeconds);
        }

        if (savedMessage != null)
        {
            yield return Wait(savedDelay);
            savedMessage.SetActive(true);
            onSaved?.Invoke();
            yield return SlideUp(savedMessage, savedSeconds);
        }

        sequence = null;
        if (showSeconds > 0f)
        {
            yield return Wait(holdAfterSaved);
            NextDay();
        }
    }

    // ---------- Gerakan (waktu unscaled, tidak terpengaruh timeScale) ----------

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // Membesar dari "from" ke ukuran asli, sedikit melewati lalu kembali.
    private static IEnumerator ScaleIn(Transform target, Vector3 baseScale, float from, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            target.localScale = baseScale * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t / seconds));
            yield return null;
        }
        target.localScale = baseScale;
    }

    // Cap: besar dan transparan -> ukuran asli dan jelas, cepat.
    private static IEnumerator Slam(Transform target, float fromScale, float seconds)
    {
        Vector3 baseScale = target.localScale;
        var group = EnsureGroup(target.gameObject);
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / seconds);
            target.localScale = baseScale * Mathf.Lerp(fromScale, 1f, k * k);
            group.alpha = Mathf.Clamp01(k * 3f);
            yield return null;
        }
        target.localScale = baseScale;
        group.alpha = 1f;
    }

    // Pesan: naik sedikit dari bawah sambil muncul.
    private IEnumerator SlideUp(GameObject target, float seconds)
    {
        var group = EnsureGroup(target);
        var rect = target.transform as RectTransform;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = EaseOutBack(t / seconds);
            if (rect != null) rect.anchoredPosition = savedBasePosition + Vector2.down * 60f * (1f - Mathf.Clamp01(k));
            group.alpha = Mathf.Clamp01(t / seconds * 2f);
            yield return null;
        }
        if (rect != null) rect.anchoredPosition = savedBasePosition;
        group.alpha = 1f;
    }

    private static CanvasGroup EnsureGroup(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        return group != null ? group : go.AddComponent<CanvasGroup>();
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private IEnumerator AutoNext()
    {
        yield return new WaitForSeconds(showSeconds);
        NextDay();
    }

    private void Hide()
    {
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = null;
        if (autoNext != null) StopCoroutine(autoNext);
        autoNext = null;
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;

        // Kembalikan ke keadaan awal untuk laporan berikutnya.
        if (stamp != null) stamp.SetActive(false);
        if (savedMessage != null) savedMessage.SetActive(false);
        if (card != null) card.localScale = cardBaseScale;
        SetVisible(false);
    }

    // Kalau Panel diisi objek script ini sendiri, mematikannya akan mematikan script (OnDisable melepas event
    // dan holdForReport), jadi laporan tidak pernah tampil. Untuk kasus itu pakai CanvasGroup.
    private void SetVisible(bool visible)
    {
        if (panel == null) return;

        if (panel != gameObject)
        {
            panel.SetActive(visible);
            return;
        }

        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    public void NextDay()
    {
        Hide();
        cycle.StartNextDay();
    }
}
