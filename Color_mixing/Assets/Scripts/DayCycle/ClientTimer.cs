using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClientTimer : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [Tooltip("Opsional. Diisi: timer mulai saat klien selesai berjalan masuk, dan berhenti saat klien baru diminta.")]
    [SerializeField] private CharacterSpawner spawner;
    [SerializeField] private float secondsPerClient = 30f;
    [SerializeField] private Slider timerBar; 
    [SerializeField] private TMP_Text timerText;

    private float remaining;
    private bool running;

    private void OnEnable()
    {
        if (spawner != null)
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
        cycle.OnClientRequested -= Stop;
        cycle.OnClientRequested -= StartTimer;
        if (spawner != null) spawner.OnClientArrived -= StartTimer;
        cycle.OnGameOver -= Stop;
        cycle.OnDayEnded -= OnDayEnded;
    }

    private void StartTimer()
    {
        remaining = secondsPerClient;
        running = true;
        if (timerBar != null) timerBar.value = 1f;
    }

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
