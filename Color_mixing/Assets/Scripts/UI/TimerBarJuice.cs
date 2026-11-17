using UnityEngine;
using UnityEngine.UI;

// Efek "juice" untuk timer bar klien. Pasang di TimerTrack (GameHUDView memasangnya otomatis kalau belum ada).
//   Intro     : bar terisi cepat (ClientTimer), lalu memantul squash & stretch saat penuh.
//   Detak     : di bawah Pulse Below, bar berdenyut "lub-dub", makin cepat mendekati 0.
//   Getar     : di bawah Shake Below, bar bergetar, makin kuat mendekati 0.
//   Kedip     : di detik-detik terakhir, fill (dan target lain) berkedip ke Flash Color.
//   Panik     : di detik-detik terakhir, vignette merah di tepi layar ikut detak, ikon timer bergoyang.
// Semua pakai Time.deltaTime, jadi ikut berhenti saat game dijeda.
public class TimerBarJuice : MonoBehaviour
{
    [Header("Referensi (kosong = dicari otomatis)")]
    [Tooltip("Yang di-scale dan digetarkan. Kosong = objek ini.")]
    [SerializeField] private RectTransform bar;
    [Tooltip("Yang berkedip. Kosong = Image Filled pertama di bawah objek ini.")]
    [SerializeField] private Graphic[] blinkTargets;
    [Tooltip("Ikon jam/maskot yang bergoyang saat panik. Opsional.")]
    [SerializeField] private RectTransform rattleIcon;

    [Header("Intro bounce")]
    [Tooltip("Seberapa kuat pantulan saat bar penuh.")]
    [SerializeField] private float popStrength = 0.18f;
    [SerializeField] private float popDamping = 7f;
    [SerializeField] private float popFrequency = 18f;

    [Header("Heartbeat")]
    [SerializeField, Range(0f, 1f)] private float pulseBelow = 0.3f;
    [SerializeField] private float pulseBpmStart = 70f;
    [SerializeField] private float pulseBpmEnd = 190f;
    [SerializeField] private float pulseScaleStart = 0.04f;
    [SerializeField] private float pulseScaleEnd = 0.1f;

    [Header("Shake")]
    [SerializeField, Range(0f, 1f)] private float shakeBelow = 0.15f;
    [Tooltip("Getaran maksimum (unit UI) saat waktu hampir habis.")]
    [SerializeField] private float shakeMax = 7f;

    [Header("Blink")]
    [Tooltip("Mulai berkedip saat sisa detik <= nilai ini.")]
    [SerializeField] private float blinkSeconds = 5f;
    [Tooltip("Warna Image dikalikan dengan sprite, jadi putih tidak bisa membuat sprite lebih terang.\n" +
             "Default: fill jadi transparan sebagian (berkedip hilang-muncul). Untuk kedip putih/merah sungguhan,\n" +
             "isi Blink Targets dengan Image outline yang spritenya putih, lalu pilih warnanya di sini.")]
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private float blinkHzStart = 3f;
    [SerializeField] private float blinkHzEnd = 9f;

    [Header("Icon twitch (sebelum panik)")]
    [Tooltip("Ikon sesekali bergoyang kecil selama timer berjalan, sebelum Panic Seconds.")]
    [SerializeField] private bool twitch = true;
    [Tooltip("Sudut goyangan (derajat) saat bar masih hijau / sudah kuning.")]
    [SerializeField] private float twitchAngleGreen = 4f;
    [SerializeField] private float twitchAngleYellow = 7f;
    [Tooltip("Jeda acak antar goyangan (detik), min-max, saat hijau / kuning.")]
    [SerializeField] private Vector2 twitchGapGreen = new Vector2(1.5f, 3f);
    [SerializeField] private Vector2 twitchGapYellow = new Vector2(0.6f, 1.4f);
    [Tooltip("Di bawah nilai ini goyangan pakai setelan kuning. Samakan dengan Yellow Below di GameHUDView.")]
    [SerializeField, Range(0f, 1f)] private float twitchYellowBelow = 0.6f;
    [SerializeField] private float twitchDuration = 0.3f;

    [Header("Panic (vignette + rattle)")]
    [SerializeField] private float panicSeconds = 5f;
    [Tooltip("Kosong = dibuat otomatis di Canvas paling atas.\nDiisi Image tanpa sprite = sprite vignette dibuatkan otomatis.")]
    [SerializeField] private Image vignette;
    [SerializeField] private bool createVignette = true;
    [SerializeField] private Color vignetteColor = new Color(0.9f, 0.05f, 0.05f, 1f);
    [SerializeField, Range(0f, 1f)] private float vignetteAlphaStart = 0.2f;
    [SerializeField, Range(0f, 1f)] private float vignetteAlphaEnd = 0.6f;
    [SerializeField] private float rattleAngleStart = 6f;
    [SerializeField] private float rattleAngleEnd = 18f;
    [SerializeField] private float rattleHz = 14f;

    private static TimerBarJuice vignetteOwner;   // dua HUD (ClientArrival + ColorMixing) jangan dobel vignette

    private ClientTimer timer;
    private Vector3 baseScale;
    private Vector2 basePos;
    private Quaternion iconBaseRot;
    private Color[] baseColors;
    private bool vignetteCreated;

    private bool popped;
    private float popT = -1f;   // < 0 = tidak memantul
    private float beatPhase;
    private float blinkPhase;
    private float twitchWait = -1f;   // detik sampai goyangan berikutnya (< 0 = belum dijadwalkan)
    private float twitchT = -1f;      // waktu sejak goyangan mulai (< 0 = diam)
    private float twitchAngle;
    private bool dirty;         // ada efek yang sedang mengubah bar, perlu dikembalikan

    // Dipanggil GameHUDView saat memasang komponen ini otomatis.
    public void Setup(Image fill, Image icon)
    {
        if ((blinkTargets == null || blinkTargets.Length == 0) && fill != null) blinkTargets = new Graphic[] { fill };
        if (rattleIcon == null && icon != null) rattleIcon = icon.rectTransform;
        CacheColors();
    }

    private void Awake()
    {
        if (bar == null) bar = (RectTransform)transform;
        baseScale = bar.localScale;
        basePos = bar.anchoredPosition;

        if (blinkTargets == null || blinkTargets.Length == 0)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
                if (img.type == Image.Type.Filled) { blinkTargets = new Graphic[] { img }; break; }
        }
        if (rattleIcon != null) iconBaseRot = rattleIcon.localRotation;
        CacheColors();
    }

    private void OnEnable()
    {
        vignetteOwner = this;
        timer = FindAnyObjectByType<ClientTimer>();
        if (timer != null) timer.Started += OnTimerStarted;
    }

    private void OnDisable()
    {
        if (timer != null) timer.Started -= OnTimerStarted;
        if (vignetteOwner == this) vignetteOwner = null;
        ResetVisuals();
    }

    private void OnDestroy()
    {
        if (vignetteCreated && vignette != null) Destroy(vignette.gameObject);
    }

    private void OnTimerStarted()
    {
        popped = false;
        popT = -1f;
        beatPhase = 0f;
        blinkPhase = 0f;
        twitchWait = -1f;
        twitchT = -1f;
    }

    private void CacheColors()
    {
        if (blinkTargets == null) return;
        baseColors = new Color[blinkTargets.Length];
        for (int i = 0; i < blinkTargets.Length; i++)
            if (blinkTargets[i] != null) baseColors[i] = blinkTargets[i].color;
        if (rattleIcon != null) iconBaseRot = rattleIcon.localRotation;
    }

    private void Update()
    {
        if (timer == null) { timer = FindAnyObjectByType<ClientTimer>(); if (timer != null) timer.Started += OnTimerStarted; }
        if (timer == null || !timer.IsActive)
        {
            if (dirty) ResetVisuals();
            return;
        }

        float dt = Time.deltaTime;
        float v = timer.Normalized;
        float secs = timer.SecondsLeft;
        bool draining = timer.IsRunning && !timer.InIntro;
        dirty = true;

        // --- intro bounce: mulai saat bar tampil penuh
        if (!popped && timer.InIntro && timer.DisplayNormalized >= 0.999f) { popped = true; popT = 0f; }
        if (!popped && draining) popped = true;   // intro 0 detik: tidak ada pantulan
        Vector2 scale = Vector2.one;
        if (popT >= 0f)
        {
            popT += dt;
            float a = popStrength * Mathf.Exp(-popDamping * popT) * Mathf.Cos(popFrequency * popT);
            scale.x += a;
            scale.y -= a * 0.8f;       // squash & stretch: melebar sambil memendek
            if (popT > 5f / popDamping) popT = -1f;
        }

        // --- heartbeat
        float beat = 0f;
        if (draining && v < pulseBelow && pulseBelow > 0f)
        {
            float u = 1f - v / pulseBelow;
            beatPhase += dt * Mathf.Lerp(pulseBpmStart, pulseBpmEnd, u) / 60f;
            beatPhase -= Mathf.Floor(beatPhase);
            beat = Beat(beatPhase);
            float s = Mathf.Lerp(pulseScaleStart, pulseScaleEnd, u) * beat;
            scale += new Vector2(s, s);
        }
        bar.localScale = new Vector3(baseScale.x * scale.x, baseScale.y * scale.y, baseScale.z);

        // --- shake
        Vector2 offset = Vector2.zero;
        if (draining && v < shakeBelow && shakeBelow > 0f)
        {
            float k = 1f - v / shakeBelow;
            offset = Random.insideUnitCircle * shakeMax * k * k;   // pelan dulu, lalu makin liar
        }
        bar.anchoredPosition = basePos + offset;

        // --- blink
        if (blinkTargets != null && baseColors != null)
        {
            bool on = false;
            if (draining && secs <= blinkSeconds && blinkSeconds > 0f)
            {
                float k = 1f - secs / blinkSeconds;
                blinkPhase += dt * Mathf.Lerp(blinkHzStart, blinkHzEnd, k);
                on = blinkPhase - Mathf.Floor(blinkPhase) < 0.5f;
            }
            for (int i = 0; i < blinkTargets.Length; i++)
                if (blinkTargets[i] != null) blinkTargets[i].color = on ? flashColor : baseColors[i];
        }

        // --- panic: vignette + rattle
        float panic = draining && secs <= panicSeconds && panicSeconds > 0f ? 1f - secs / panicSeconds : -1f;
        UpdateVignette(panic, beat);
        if (rattleIcon != null)
        {
            if (panic >= 0f)
            {
                float angle = Mathf.Lerp(rattleAngleStart, rattleAngleEnd, panic);
                float z = Mathf.Sin(Time.time * rattleHz * 2f * Mathf.PI) * angle + Random.Range(-2f, 2f);
                rattleIcon.localRotation = iconBaseRot * Quaternion.Euler(0f, 0f, z);
            }
            else rattleIcon.localRotation = iconBaseRot * Quaternion.Euler(0f, 0f, Twitch(dt, v, draining));
        }
    }

    // Goyangan kecil sesekali: tunggu jeda acak, lalu ayunan cepat yang mereda (kiri-kanan-kiri).
    private float Twitch(float dt, float v, bool draining)
    {
        if (!twitch || !draining) { twitchT = -1f; twitchWait = -1f; return 0f; }
        bool yellow = v < twitchYellowBelow;

        if (twitchT >= 0f)
        {
            twitchT += dt;
            float p = twitchT / Mathf.Max(0.01f, twitchDuration);
            if (p >= 1f) { twitchT = -1f; return 0f; }
            return twitchAngle * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
        }

        if (twitchWait < 0f) twitchWait = RandomGap(yellow);
        twitchWait -= dt;
        if (twitchWait <= 0f)
        {
            twitchT = 0f;
            twitchWait = -1f;
            float a = yellow ? twitchAngleYellow : twitchAngleGreen;
            twitchAngle = Random.value < 0.5f ? a : -a;   // arah acak supaya tidak monoton
        }
        return 0f;
    }

    private float RandomGap(bool yellow)
    {
        Vector2 g = yellow ? twitchGapYellow : twitchGapGreen;
        return Random.Range(g.x, g.y);
    }

    // Satu siklus detak "lub-dub": denyut besar lalu denyut kecil, sisanya diam.
    private static float Beat(float t)
    {
        float lub = Mathf.Exp(-Mathf.Pow((t - 0.05f) / 0.05f, 2f));
        float dub = Mathf.Exp(-Mathf.Pow((t - 0.25f) / 0.05f, 2f)) * 0.6f;
        return lub + dub;
    }

    private void UpdateVignette(float panic, float beat)
    {
        if (panic < 0f || vignetteOwner != this)
        {
            if (vignette != null && vignette.enabled) vignette.enabled = false;
            return;
        }
        if (vignette == null && createVignette) CreateVignette();
        if (vignette == null) return;
        if (vignette.sprite == null) vignette.sprite = MakeVignetteSprite();
        if (!vignette.gameObject.activeSelf) vignette.gameObject.SetActive(true);   // objeknya boleh dimatikan di prefab

        float alpha = Mathf.Lerp(vignetteAlphaStart, vignetteAlphaEnd, panic) * (0.65f + 0.35f * Mathf.Clamp01(beat));
        Color c = vignetteColor;
        c.a = alpha;
        vignette.color = c;
        if (!vignette.enabled) vignette.enabled = true;
    }

    private void ResetVisuals()
    {
        dirty = false;
        if (bar != null)
        {
            bar.localScale = baseScale;
            bar.anchoredPosition = basePos;
        }
        if (blinkTargets != null && baseColors != null)
            for (int i = 0; i < blinkTargets.Length; i++)
                if (blinkTargets[i] != null) blinkTargets[i].color = baseColors[i];
        if (rattleIcon != null) rattleIcon.localRotation = iconBaseRot;
        if (vignette != null) vignette.enabled = false;
    }

    // Vignette layar penuh: tengah transparan, tepi berwarna. Tekstur dibuat sendiri, tidak perlu aset.
    private void CreateVignette()
    {
        createVignette = false;   // coba sekali saja
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        var go = new GameObject("Panic Vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvas.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();   // di belakang HUD, jadi tidak menutupi bar

        vignette = go.GetComponent<Image>();
        vignette.sprite = MakeVignetteSprite();
        vignette.raycastTarget = false;
        vignette.enabled = false;
        vignetteCreated = true;
    }

    private static Sprite MakeVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PanicVignette" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;    // 0 di tengah, 1 di sudut
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, d));
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
