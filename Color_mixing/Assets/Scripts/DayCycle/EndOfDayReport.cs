using TMPro;
using UnityEngine;

// Panel hasil hari. Hari berikutnya baru mulai saat NextDay() dipanggil (hubungkan ke tombol Next Day).
public class EndOfDayReport : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text yesText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text bestDayText;

    private void OnEnable()
    {
        cycle.holdForReport = true;
        cycle.OnDayEnded += Show;
        cycle.OnGameOver += Hide;
    }

    private void OnDisable()
    {
        cycle.holdForReport = false;
        cycle.OnDayEnded -= Show;
        cycle.OnGameOver -= Hide;
    }

    private void Start() => Hide();

    private void Show(DayResult r)
    {
        if (titleText != null) titleText.text = $"Day {r.day} Complete";
        if (yesText != null) yesText.text = $"Yes: {r.yesCount}";
        if (livesText != null) livesText.text = $"Lives left: {r.livesLeft}/{GameRules.StartingLives}";
        if (bestDayText != null) bestDayText.text = $"Best day: {r.bestDay}";
        panel.SetActive(true);
    }

    private void Hide() { if (panel != null) panel.SetActive(false); }

    // Dipanggil dari tombol Next Day.
    public void NextDay()
    {
        Hide();
        cycle.StartNextDay();
    }
}
