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

    private DayCycleManager cycle;
    private ClientTimer timer;

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
    private void ShowLives(int lives) { if (livesText != null) livesText.text = $"Lives: {lives}"; }
    private void ShowHappyClient(int yes) { if (happyClientText != null) happyClientText.text = $"{yes}/{GameRules.YesToWinDay}"; }
}