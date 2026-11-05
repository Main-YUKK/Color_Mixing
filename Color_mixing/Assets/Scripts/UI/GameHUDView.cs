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
        if (timer != null && timerBar != null) timerBar.value = timer.Normalized;
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
