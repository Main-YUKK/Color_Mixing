using TMPro;
using UnityEngine;

public class DayCycleHUD : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text HappyClientText;
    [SerializeField] private GameObject gameOverPanel;

    private void OnEnable()
    {
        cycle.OnDayStarted += OnDayStarted;
        cycle.OnLivesChanged += OnLivesChanged;
        cycle.OnHappyClientChanged += OnHappyClientChanged;
        cycle.OnGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        cycle.OnDayStarted -= OnDayStarted;
        cycle.OnLivesChanged -= OnLivesChanged;
        cycle.OnHappyClientChanged -= OnHappyClientChanged;
        cycle.OnGameOver -= OnGameOver;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnDayStarted(int day) { if (dayText != null) dayText.text = $"Day {day}"; }
    private void OnLivesChanged(int lives) { if (livesText != null) livesText.text = $"Lives: {lives}"; }
    private void OnHappyClientChanged(int yes) { if (HappyClientText != null) HappyClientText.text = $"{yes}/{GameRules.YesToWinDay}"; }
    private void OnGameOver() { if (gameOverPanel != null) gameOverPanel.SetActive(true); }
}
