using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD siklus hari yang bisa dipakai di scene mana pun: day, lives, happy client, timer bar.
// Datanya ada di scene ClientArrival, jadi dibaca lewat Instance (referensi Inspector antar scene tidak bisa).
// Semua field opsional: isi yang dipakai saja.
public class GameHUDView : MonoBehaviour
{
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text happyClientText;
    [SerializeField] private Slider timerBar;

    [Header("Timer bar bergaya (opsional, bisa dipakai tanpa Slider di atas)")]
    [Tooltip("Image tipe Filled / Horizontal / Left. Anak dari Timer Track, ukurannya sama.")]
    [SerializeField] private Image timerFill;
    [Tooltip("Ikon maskot yang ikut bergerak di ujung fill. Anak dari Timer Track, Anchor Preset middle-left.")]
    [SerializeField] private Image timerIcon;
    [SerializeField] private Sprite greenFill;
    [SerializeField] private Sprite yellowFill;
    [SerializeField] private Sprite redFill;
    [SerializeField] private Sprite greenIcon;
    [SerializeField] private Sprite yellowIcon;
    [SerializeField] private Sprite redIcon;
    [Tooltip("Di bawah nilai ini bar jadi kuning.")]
    [SerializeField, Range(0f, 1f)] private float yellowBelow = 0.6f;
    [Tooltip("Di bawah nilai ini bar jadi merah.")]
    [SerializeField, Range(0f, 1f)] private float redBelow = 0.3f;

    [Header("Hearts (opsional, bisa dipakai bersama / menggantikan Lives text)")]
    [SerializeField] private Image[] heartSlots;      // satu Image per nyawa, urut dari kiri
    [SerializeField] private Image[] cloudPops;       // satu Image per slot, di atas heart (nonaktif di awal)
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;
    [SerializeField] private Sprite loseCloud;        // asap hitam saat nyawa hilang
    [SerializeField] private Sprite gainCloud;        // asap putih saat nyawa bertambah
    [SerializeField] private float popDuration = 0.9f;
    [Tooltip("Tunggu sebelum asap mulai. Naikkan kalau nyawa berubah saat layar mixing masih menutupi HUD ini.")]
    [SerializeField] private float popDelay = 0f;

    private float iconHeight;
    private DayCycleManager cycle;
    private ClientTimer timer;
    private int shownLives = -1;                      // <= 0 = belum mulai / game over, jadi tanpa animasi
    private Coroutine[] pops;

    private void Start()
    {
        cycle = DayCycleManager.Instance;
        timer = FindAnyObjectByType<ClientTimer>();
        if (cycle == null)
        {
            Debug.Log("[HUD] DayCycleManager tidak ada (scene di-Play sendirian), HUD dikosongkan");
            return;
        }

        cycle.OnDayStarted += ShowDay;
        cycle.OnLivesChanged += ShowLives;
        cycle.OnHappyClientChanged += ShowHappyClient;

        // Scene ini bisa dimuat di tengah hari: event awal sudah lewat, jadi tampilkan nilai sekarang dulu.
        ShowDay(cycle.CurrentDay);
        ShowLives(cycle.Lives);
        ShowHappyClient(cycle.YesCount);
    }

    private void OnDestroy()
    {
        if (cycle == null) return;
        cycle.OnDayStarted -= ShowDay;
        cycle.OnLivesChanged -= ShowLives;
        cycle.OnHappyClientChanged -= ShowHappyClient;
    }

    private void Update()
    {
        if (timer == null) return;
        float v = Mathf.Clamp01(timer.Normalized);
        if (timerBar != null) timerBar.value = v;
        ShowTimerStyled(v);
    }

    // Fill dipotong dari kiri sesuai sisa waktu; ikon menempel di ujung fill; warna ganti per ambang.
    private void ShowTimerStyled(float v)
    {
        bool red = v < redBelow, yellow = !red && v < yellowBelow;
        if (timerFill != null)
        {
            timerFill.fillAmount = v;
            Sprite fill = red ? redFill : yellow ? yellowFill : greenFill;
            if (fill != null && timerFill.sprite != fill) timerFill.sprite = fill;
        }
        if (timerIcon != null)
        {
            Sprite icon = red ? redIcon : yellow ? yellowIcon : greenIcon;
            var rt = timerIcon.rectTransform;
            if (iconHeight <= 0f) iconHeight = rt.sizeDelta.y;     // tinggi dari yang diatur di prefab
            if (icon != null && timerIcon.sprite != icon)
            {
                timerIcon.sprite = icon;
                // Tinggi tetap, lebar mengikuti rasio sprite supaya tidak gepeng.
                rt.sizeDelta = new Vector2(iconHeight * icon.rect.width / icon.rect.height, iconHeight);
            }
            rt.anchorMin = new Vector2(v, rt.anchorMin.y);
            rt.anchorMax = new Vector2(v, rt.anchorMax.y);
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }
    }

    private void ShowDay(int day) { if (dayText != null) dayText.text = $"Day {day}"; }
    private void ShowLives(int lives)
    {
        if (livesText != null) livesText.text = $"Lives: {lives}";
        ShowHearts(lives);
    }
    private void ShowHappyClient(int yes) { if (happyClientText != null) happyClientText.text = $"{yes}/{GameRules.YesToWinDay}"; }

    // Heart penuh untuk tiap nyawa tersisa, heart kosong untuk yang hilang. Slot yang berubah mendapat asap.
    private void ShowHearts(int lives)
    {
        if (heartSlots == null) return;
        if (pops == null || pops.Length != heartSlots.Length) pops = new Coroutine[heartSlots.Length];

        for (int i = 0; i < heartSlots.Length; i++)
        {
            if (heartSlots[i] == null) continue;
            bool full = i < lives;
            bool changed = shownLives > 0 && (i < shownLives) != full;
            heartSlots[i].sprite = full ? fullHeart : emptyHeart;
            if (changed) Pop(i, full ? gainCloud : loseCloud);
        }
        shownLives = lives;
    }

    // Uji tanpa main sungguhan: klik kanan komponen ini di Inspector saat Play.
    [ContextMenu("Test: Lose Heart")]
    private void TestLoseHeart()
    {
        if (shownLives <= 0) ShowHearts(heartSlots.Length);     // mulai dari heart penuh supaya perubahan terbaca
        ShowHearts(Mathf.Max(1, shownLives - 1));
    }

    [ContextMenu("Test: Gain Heart")]
    private void TestGainHeart()
    {
        if (shownLives <= 0) ShowHearts(1);
        ShowHearts(Mathf.Min(heartSlots.Length, shownLives + 1));
    }

    private void Pop(int slot, Sprite sprite)
    {
        if (cloudPops == null || slot >= cloudPops.Length || cloudPops[slot] == null || sprite == null)
        {
            Debug.LogWarning($"[HUD] Asap slot {slot} tidak bisa tampil: cek Cloud Pops, Lose Cloud, dan Gain Cloud di GameHUDView.", this);
            return;
        }
        if (pops[slot] != null) StopCoroutine(pops[slot]);
        pops[slot] = StartCoroutine(PopRoutine(cloudPops[slot], sprite));
    }

    // Asap membesar cepat, tetap terlihat jelas di paruh pertama, lalu memudar dan hilang.
    private IEnumerator PopRoutine(Image cloud, Sprite sprite)
    {
        if (popDelay > 0f) yield return new WaitForSecondsRealtime(popDelay);
        cloud.sprite = sprite;
        cloud.gameObject.SetActive(true);
        var rt = cloud.rectTransform;
        for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / popDuration;
            float grow = 1f - (1f - k) * (1f - k);          // ease-out
            rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, grow);
            cloud.color = new Color(1f, 1f, 1f, k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f);
            yield return null;
        }
        cloud.gameObject.SetActive(false);
    }
}
