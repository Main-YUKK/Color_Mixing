using UnityEngine;

// Semua suara scene ClientArrival: klien masuk / pergi, suara hewan, feedback, timer, nyawa, hari, laporan, game over.
// Pasang di satu objek di scene ClientArrival lalu isi klipnya. Suara dimainkan lewat AudioManager (mengikuti SFX On/Off).
// Referensi yang kosong dicari otomatis di scene. Scene ColorMixing dimuat additive di atas scene ini,
// jadi suara tetap berbunyi selama pemain mencampur warna.
public class ClientArrivalAudio : MonoBehaviour
{
    [Header("Referensi (kosong = dicari otomatis)")]
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private CharacterSpawner spawner;
    [SerializeField] private ClientResultView resultView;
    [SerializeField] private ClientTimer timer;
    [SerializeField] private EndOfDayReport report;
    [SerializeField] private ClientArrivalSequence sequence;

    [Header("Klien")]
    [SerializeField] private AudioClip clientEnter;
    [SerializeField] private AudioClip clientLeave;
    [Tooltip("Urutan SAMA dengan Character Prefabs di CharacterSpawner:\nAyam, Bebek, Buaya, Kambing, Katak, Kucing, Lumba2, Tikus.\nDibunyikan saat kartu request muncul (klien mengucapkan request), berhenti saat pemain lanjut ke scene mixing.")]
    [SerializeField] private AudioClip[] customerSounds;

    [Header("Feedback")]
    [SerializeField] private AudioClip feedbackGood;
    [SerializeField] private AudioClip feedbackBad;

    [Header("Timer")]
    [Tooltip("Klip panjang (~12 detik) yang berakhir tepat saat waktu habis. Mulai otomatis sebanyak panjang klipnya sebelum waktu habis.")]
    [SerializeField] private AudioClip timerUp;

    [Header("Nyawa, hari, laporan")]
    [SerializeField] private AudioClip lostHeart;
    [SerializeField] private AudioClip lifeRegenerate;
    [SerializeField] private AudioClip changingDay;
    [SerializeField] private AudioClip successReport;
    [SerializeField] private AudioClip gameOver;

    private int lastLives = -1;      // <= 0 = belum mulai / game over, jadi nyawa penuh kembali tidak dibunyikan
    private bool warningPlayed;      // peringatan timer sudah dibunyikan untuk timer yang sedang berjalan

    private void Start()
    {
        if (cycle == null) cycle = DayCycleManager.Instance != null ? DayCycleManager.Instance : FindFirstObjectByType<DayCycleManager>();
        if (spawner == null) spawner = FindFirstObjectByType<CharacterSpawner>();
        if (resultView == null) resultView = FindFirstObjectByType<ClientResultView>();
        if (timer == null) timer = FindFirstObjectByType<ClientTimer>();
        if (report == null) report = FindFirstObjectByType<EndOfDayReport>();
        if (sequence == null) sequence = FindFirstObjectByType<ClientArrivalSequence>();

        if (spawner != null)
        {
            spawner.OnClientEntering += OnClientEntering;
            spawner.OnClientLeaving += StopVoice;
            spawner.OnClientNearExit += OnClientNearExit;
        }
        if (sequence != null)
        {
            sequence.CardShown += OnCardShown;
            sequence.NextSceneLoading += StopVoice;
        }
        if (resultView != null) resultView.FeedbackShown += OnFeedback;
        if (timer != null)
        {
            timer.Started += OnTimerStarted;
            timer.Stopped += StopTimerWarning;
        }
        if (cycle != null)
        {
            cycle.OnLivesChanged += OnLivesChanged;
            cycle.OnDayStarted += OnDayStarted;
            cycle.OnGameOver += OnGameOver;
            cycle.OnClientRequested += StopVoice;
            lastLives = cycle.Lives;
        }
        if (report != null) report.Stamped += OnReportStamped;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnClientEntering -= OnClientEntering;
            spawner.OnClientLeaving -= StopVoice;
            spawner.OnClientNearExit -= OnClientNearExit;
        }
        if (sequence != null)
        {
            sequence.CardShown -= OnCardShown;
            sequence.NextSceneLoading -= StopVoice;
        }
        if (resultView != null) resultView.FeedbackShown -= OnFeedback;
        if (timer != null)
        {
            timer.Started -= OnTimerStarted;
            timer.Stopped -= StopTimerWarning;
        }
        if (cycle != null)
        {
            cycle.OnLivesChanged -= OnLivesChanged;
            cycle.OnDayStarted -= OnDayStarted;
            cycle.OnGameOver -= OnGameOver;
            cycle.OnClientRequested -= StopVoice;
        }
        if (report != null) report.Stamped -= OnReportStamped;
        StopVoice();
        StopTimerWarning();   // keluar dari scene (kembali ke lobby): peringatan tidak boleh terus berbunyi
    }

    // Peringatan timer: mulai saat sisa waktu tinggal sepanjang klipnya, supaya ujung klip jatuh di waktu habis.
    private void Update()
    {
        if (timerUp == null || warningPlayed || timer == null || !timer.IsRunning) return;
        float left = timer.SecondsLeft;
        if (left > timerUp.length) return;

        warningPlayed = true;
        // Waktu klien lebih pendek dari klipnya: mulai di tengah klip.
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTimerWarning(timerUp, timerUp.length - left);
    }

    // Waktu habis: klip berakhir sendiri di 0. Sengaja TIDAK mendengar ClientTimer.TimedOut, karena adanya pendengar
    // di event itu mengambil alih tugas memanggil OnClientTimeout (lihat ClientTimer.Update).
    private void OnTimerStarted() => warningPlayed = false;

    private void StopTimerWarning()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopTimerWarning();
    }

    // ---------- Klien ----------

    private void OnClientEntering() => Play(clientEnter);
    // Bunyi pintu: saat klien hampir sampai di pintu. Jaraknya diatur di CharacterSpawner > Exit Sound Distance.
    private void OnClientNearExit() => Play(clientLeave);

    private void StopVoice()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopVoice();
    }

    // Kartu request muncul: klien "berbicara" sampai pemain lanjut ke mixing.
    private void OnCardShown()
    {
        var look = GameSession.CurrentLook;
        if (customerSounds == null || look == null || !look.valid) return;
        if (look.prefabIndex < 0 || look.prefabIndex >= customerSounds.Length) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayVoice(customerSounds[look.prefabIndex]);
    }

    private void OnFeedback(bool good) => Play(good ? feedbackGood : feedbackBad);

    // ---------- Nyawa, hari, laporan ----------

    private void OnLivesChanged(int lives)
    {
        if (lastLives > 0)
        {
            if (lives < lastLives) Play(lostHeart);
            else if (lives > lastLives) Play(lifeRegenerate);
        }
        lastLives = lives;
    }

    private void OnDayStarted(int day) => Play(changingDay);
    private void OnReportStamped() => Play(successReport);
    private void OnGameOver() => Play(gameOver);

    private static void Play(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip);
    }
}
