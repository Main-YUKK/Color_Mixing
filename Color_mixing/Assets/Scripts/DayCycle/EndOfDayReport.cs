using System.Collections;
using TMPro;
using UnityEngine;

// Kartu hasil hari ("HARI KE-1: SELESAI"). Progres sudah disimpan oleh DayCycleManager sebelum kartu ini tampil.
// Hari berikutnya mulai otomatis setelah Show Seconds, atau saat NextDay() dipanggil (mis. dari tombol).
public class EndOfDayReport : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text targetText;
    [Tooltip("0 = tidak otomatis, menunggu NextDay() dari tombol.")]
    [SerializeField] private float showSeconds = 3f;

    private Coroutine autoNext;

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
        if (titleText != null) titleText.text = $"HARI KE-{r.day}: SELESAI";
        if (targetText != null) targetText.text = $"Target: {r.yesCount}/{GameRules.YesToWinDay}";
        panel.SetActive(true);

        if (autoNext != null) StopCoroutine(autoNext);
        if (showSeconds > 0f) autoNext = StartCoroutine(AutoNext());
    }

    private IEnumerator AutoNext()
    {
        yield return new WaitForSeconds(showSeconds);
        NextDay();
    }

    private void Hide()
    {
        if (autoNext != null) StopCoroutine(autoNext);
        autoNext = null;
        if (panel != null) panel.SetActive(false);
    }

    public void NextDay()
    {
        Hide();
        cycle.StartNextDay();
    }
}
