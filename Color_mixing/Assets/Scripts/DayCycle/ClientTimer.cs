using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class ClientTimer : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [Tooltip("Opsional. Diisi: timer mulai saat klien selesai berjalan masuk, dan berhenti saat klien baru diminta.")]
    [SerializeField] private CharacterSpawner spawner;
    [Tooltip("Mati: timer mulai saat klien selesai masuk.\nNyala: timer menunggu StartTimer() dari layar mixing (waktu baca kartu tidak terpotong).\nWajib dipakai bersama Next Scene Name = ColorMixing di Client Arrival Sequence, kalau tidak timer tidak pernah jalan.")]
    [SerializeField] private bool startManually;
    [Tooltip("Hanya untuk Start Manually + Spawner.\nNyala: timer muncul dan langsung berjalan saat kartu request muncul (klien tiba).\nMati: timer muncul dan terisi saat klien tiba, lalu diam penuh menunggu StartTimer() (mis. tombol ke layar mixing).")]
    [SerializeField] private bool countdownOnCard = true;
    [Tooltip("Jeda (detik) setelah klien tiba / kartu request muncul sebelum timer tampil dan mulai. 0 = langsung.")]
    [SerializeField] private float startDelay = 3f;
    [SerializeField] private float secondsPerClient = 30f;
    [SerializeField] private Slider timerBar;
    [SerializeField] private TMP_Text timerText;
    [Tooltip("Intro: bar terisi cepat dari 0 lalu memantul, baru mulai berkurang. 0 = langsung jalan.")]
    [SerializeField] private float introSeconds = 0.8f;
    [Tooltip("Bagian intro (0-1) yang dipakai untuk mengisi bar. Sisanya bar diam penuh sambil memantul.")]
    [SerializeField, Range(0.1f, 1f)] private float introFillPortion = 0.55f;

    private float remaining;
    private bool running;
    private bool holding;     // intro sudah diputar saat klien datang, bar penuh menunggu StartTimer()
    private float introLeft;
    private float delayLeft = -1f;     // > 0 = menunggu Start Delay sebelum timer tampil
    private bool delayedHold;          // setelah jeda: PlayIntro (diam penuh) bukan StartTimer
    private bool startRequested;       // StartTimer() dipanggil selama jeda: langsung berjalan setelah jeda

    // Timer yang sedang aktif di scene, dan kabar "waktu habis" dari timer host (dikirim MixNet ke client).
    public static ClientTimer Current { get; private set; }
    public static event Action HostTimedOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        Current = null;
        HostTimedOut = null;
    }

    // Timer dihentikan sebelum habis (mis. pemain sudah Apply) atau disembunyikan karena klien berganti.
    public event Action Stopped;

    public event Action TimedOut;   // kalau ada pendengar, dia yang memanggil OnClientTimeout setelah ucapan tampil

    // Sisa waktu klien 0-1 (1 = penuh, 0 = habis), untuk timer bar di scene lain (ColorMixing).
    public float Normalized => Mathf.Clamp01(remaining / Seconds);

    // Nilai untuk tampilan bar: sama dengan Normalized, kecuali saat intro (bar sedang terisi).
    public float DisplayNormalized
    {
        get
        {
            if (!InIntro) return Normalized;
            float p = 1f - introLeft / introSeconds;
            float k = Mathf.Clamp01(p / introFillPortion);
            return 1f - (1f - k) * (1f - k) * (1f - k);   // ease-out: cepat di awal, melambat di ujung
        }
    }

    public bool IsRunning => running;
    public bool IsActive => running || holding;             // berjalan, atau penuh menunggu mulai
    public bool InIntro => IsActive && introLeft > 0f;
    public float SecondsLeft => Mathf.Max(0f, remaining);

    // Bar boleh terlihat: dari kartu request muncul sampai klien berikutnya diminta. Sebelum itu disembunyikan HUD.
    public bool IsShown { get; private set; }

    // Timer baru mulai (intro dimulai). Dipakai efek timer bar untuk reset.
    public event Action Started;

    // Lama waktu per klien: setelan playtest host kalau ada, kalau tidak nilai di Inspector.
    private float Seconds => PlaytestSettings.ClientTime(secondsPerClient);

    private void OnEnable()
    {
        Current = this;
        if (GetComponent<TimerHaptics>() == null) gameObject.AddComponent<TimerHaptics>();   // getar HP saat hampir habis
        if (startManually)
        {
            cycle.OnClientRequested += Hide;
            // Kartu request muncul tepat saat klien tiba: timer tampil di situ.
            if (spawner != null)
            {
                spawner.OnClientArrived += OnArrived;
            }
        }
        else if (spawner != null)
        {
            cycle.OnClientRequested += Hide;
            spawner.OnClientArrived += OnArrived;
        }
        else cycle.OnClientRequested += StartTimer;
        cycle.OnGameOver += Hide;
        cycle.OnDayEnded += OnDayEnded;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
        cycle.OnClientRequested -= Hide;
        cycle.OnClientRequested -= StartTimer;
        if (spawner != null)
        {
            spawner.OnClientArrived -= OnArrived;
        }
        cycle.OnGameOver -= Hide;
        cycle.OnDayEnded -= OnDayEnded;
    }

    // Klien tiba (kartu request muncul): tunggu Start Delay, lalu timer tampil.
    private void OnArrived()
    {
        delayedHold = startManually && !countdownOnCard;
        startRequested = false;
        if (startDelay > 0f) { delayLeft = startDelay; return; }
        delayLeft = -1f;
        if (delayedHold) PlayIntro(); else StartTimer();
    }

    // Klien datang: bar terisi dengan pantulan lalu diam penuh. Waktu belum berkurang sampai StartTimer().
    public void PlayIntro()
    {
        remaining = Seconds;
        running = false;
        holding = true;
        IsShown = true;
        introLeft = Mathf.Max(0f, introSeconds);
        if (timerBar != null) timerBar.value = DisplayNormalized;
        Started?.Invoke();
    }

    public void StartTimer()
    {
        if (delayLeft > 0f)
        {
            startRequested = true;   // masih jeda: mulai berjalan begitu jeda selesai
            return;
        }
        if (holding)
        {
            // Intro sudah diputar saat klien datang: tinggal mulai berkurang (setelah intro selesai kalau masih jalan).
            holding = false;
            running = true;
            return;
        }
        if (running) return;   // sudah berjalan sejak kartu muncul: panggilan dari layar mixing tidak mengulang timer
        remaining = Seconds;
        running = true;
        IsShown = true;
        introLeft = Mathf.Max(0f, introSeconds);
        if (timerBar != null) timerBar.value = DisplayNormalized;
        Started?.Invoke();
    }

    public void Stop()
    {
        running = false;
        holding = false;
        introLeft = 0f;
        delayLeft = -1f;
        startRequested = false;
        Stopped?.Invoke();
    }

    // Klien baru diminta / hari selesai: timer berhenti dan disembunyikan sampai kartu berikutnya muncul.
    public void Hide()
    {
        Stop();
        IsShown = false;
    }

    // Client: host bilang waktu klien habis.
    public void ForceTimeout()
    {
        running = false;
        holding = false;
        introLeft = 0f;
        delayLeft = -1f;
        remaining = 0f;
        if (timerBar != null) timerBar.value = 0f;
        if (timerText != null) timerText.text = "0";
        TimedOut?.Invoke();
    }

    private void OnDayEnded(DayResult _) => Hide();

    private void Update()
    {
        if (delayLeft > 0f)
        {
            delayLeft -= Time.deltaTime;
            if (delayLeft > 0f) return;
            delayLeft = -1f;
            if (delayedHold && !startRequested) PlayIntro(); else StartTimer();
            startRequested = false;
        }
        if (!running && !holding) return;

        // Intro: bar terisi dulu, waktu belum berkurang.
        if (introLeft > 0f)
        {
            introLeft -= Time.deltaTime;
            if (timerBar != null) timerBar.value = DisplayNormalized;
            if (timerText != null) timerText.text = Mathf.CeilToInt(remaining).ToString();
            return;
        }
        if (holding)
        {
            if (timerBar != null) timerBar.value = 1f;
            return;   // penuh, menunggu StartTimer()
        }

        remaining -= Time.deltaTime;
        if (timerBar != null)
            timerBar.value = Mathf.Clamp01(remaining / Seconds);
        if (timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
        if (remaining > 0f) return;

        running = false;

        // Client tidak memutuskan sendiri: waktu habis menurut host datang lewat ForceTimeout().
        if (!cycle.IsAuthority) return;

        HostTimedOut?.Invoke();
        if (TimedOut != null) TimedOut.Invoke();
        else cycle.OnClientTimeout();
    }
}
