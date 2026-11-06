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
    [SerializeField] private float secondsPerClient = 30f;
    [SerializeField] private Slider timerBar;
    [SerializeField] private TMP_Text timerText;

    private float remaining;
    private bool running;

    // Timer yang sedang aktif di scene, dan kabar "waktu habis" dari timer host (dikirim MixNet ke client).
    public static ClientTimer Current { get; private set; }
    public static event Action HostTimedOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay()
    {
        Current = null;
        HostTimedOut = null;
    }

    public event Action TimedOut;   // kalau ada pendengar, dia yang memanggil OnClientTimeout setelah ucapan tampil

    // Sisa waktu klien 0-1 (1 = penuh, 0 = habis), untuk timer bar di scene lain (ColorMixing).
    public float Normalized => Mathf.Clamp01(remaining / secondsPerClient);

    private void OnEnable()
    {
        Current = this;
        if (startManually)
        {
            cycle.OnClientRequested += Stop;
        }
        else if (spawner != null)
        {
            cycle.OnClientRequested += Stop;
            spawner.OnClientArrived += StartTimer;
        }
        else cycle.OnClientRequested += StartTimer;
        cycle.OnGameOver += Stop;
        cycle.OnDayEnded += OnDayEnded;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
        cycle.OnClientRequested -= Stop;
        cycle.OnClientRequested -= StartTimer;
        if (spawner != null) spawner.OnClientArrived -= StartTimer;
        cycle.OnGameOver -= Stop;
        cycle.OnDayEnded -= OnDayEnded;
    }

    public void StartTimer()
    {
        remaining = secondsPerClient;
        running = true;
        if (timerBar != null) timerBar.value = 1f;
    }

    public void Stop() => running = false;

    // Client: host bilang waktu klien habis.
    public void ForceTimeout()
    {
        running = false;
        remaining = 0f;
        if (timerBar != null) timerBar.value = 0f;
        if (timerText != null) timerText.text = "0";
        TimedOut?.Invoke();
    }

    private void OnDayEnded(DayResult _) => Stop();

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (timerBar != null)
            timerBar.value = Mathf.Clamp01(remaining / secondsPerClient);
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
