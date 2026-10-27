using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hitung mundur untuk klien aktif. Habis tanpa submit = kehilangan 1 nyawa.
public class ClientTimer : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private float secondsPerClient = 30f;
    [SerializeField] private Slider timerBar;      // Min 0, Max 1, opsional
    [SerializeField] private TMP_Text timerText;   // opsional

    private float remaining;
    private bool running;

    private void OnEnable()
    {
        cycle.OnClientRequested += StartTimer;
        cycle.OnGameOver += Stop;
        cycle.OnDayEnded += OnDayEnded;
    }

    private void OnDisable()
    {
        cycle.OnClientRequested -= StartTimer;
        cycle.OnGameOver -= Stop;
        cycle.OnDayEnded -= OnDayEnded;
    }

    private void StartTimer()
    {
        remaining = secondsPerClient;
        running = true;
        if (timerBar != null) timerBar.value = 1f;
    }

    // Panggil saat pemain submit, supaya timeout susulan tidak ikut memotong nyawa.
    public void Stop() => running = false;

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
        cycle.OnClientTimeout();
    }
}
