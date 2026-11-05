using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Layar hasil klien: ucapan klien, dan perbandingan warna kalau pemain sempat Apply.
// Pasang di root ClientResultPanel yang selalu aktif; yang dinyalakan dan dimatikan adalah content.
public class ClientResultView : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [SerializeField] private ClientTimer timer;
    [SerializeField] private ClientArrivalSequence sequence;
    [SerializeField] private GameObject content;
    [SerializeField] private GameObject comparison;    // warna dan persentase, disembunyikan saat timer habis
    [SerializeField] private GameObject requestCard;   // disembunyikan selama hasil tampil
    [SerializeField] private Image targetColor;
    [SerializeField] private Image mixColor;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private string successMessage = "Suka Banget sama warnanya, sayangku. Kew Kew <3";
    [SerializeField] private string failMessage = "Warna Najis ini mah tuh !!";
    [SerializeField] private string timeoutMessage = "Lelet kali kauu!!!";
    [SerializeField] private float showSeconds = 3f;
    [SerializeField, Range(0f, 100f)] private float passingScore = 80f;   // skor minimal agar klien puas (%)

    private void OnEnable()
    {
        cycle.OnRevealStarted += ShowResult;
        timer.TimedOut += ShowTimeout;
    }

    private void OnDisable()
    {
        cycle.OnRevealStarted -= ShowResult;
        timer.TimedOut -= ShowTimeout;
    }

    private void ShowResult()
    {
        Color target = GameSession.CurrentRequest.targetColor;
        Color applied = GameSession.AppliedColor;
        double score = ScoreCalculator.MatchPercentage(applied, target);
        bool success = score >= passingScore;

        targetColor.color = target;
        mixColor.color = applied;
        percentText.text = $"{Mathf.RoundToInt((float)score)}%";
        comparison.SetActive(true);

        ReactClient(success, applied);
        Debug.Log($"[REQ] Hasil klien: skor {score:0.0}% -> {(success ? "Yes" : "No")}");
        Open(success ? successMessage : failMessage, () => cycle.OnFeedbackRevealed(success));
    }

    private void ShowTimeout()
    {
        sequence.UnloadNextScene();   // Canvas ColorMixing menutupi layar ini
        comparison.SetActive(false);

        ReactClient(false, null);   // timeout: klien kesal, rambut tidak diwarnai
        Debug.Log("[REQ] Timer habis");
        Open(timeoutMessage, cycle.OnClientTimeout);
    }

    // Klien yang sedang berdiri: rambut jadi warna campuran (kalau ada), ekspresi senang / kesal sampai dia pergi.
    private void ReactClient(bool success, Color? hairColor)
    {
        GameObject client = sequence.Spawner.Current;
        if (client == null) return;

        if (hairColor.HasValue && client.TryGetComponent(out RandomCharacter character))
            character.SetHairColor(hairColor.Value);
        if (client.TryGetComponent(out ClientExpression expression))
            expression.Show(success);
    }

    private void Open(string message, Action finish)
    {
        speechText.text = message;
        requestCard.SetActive(false);
        content.SetActive(true);
        StartCoroutine(FinishAfterDelay(finish));
    }

    private IEnumerator FinishAfterDelay(Action finish)
    {
        yield return new WaitForSeconds(showSeconds);
        content.SetActive(false);   // tutup dulu: panel game over atau laporan hari bisa langsung muncul
        finish();
    }
}