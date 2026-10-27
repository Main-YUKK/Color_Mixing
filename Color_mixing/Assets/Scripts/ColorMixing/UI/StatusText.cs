using TMPro;
using UnityEngine;

// Teks status di bawah mangkuk:
public class StatusText : MonoBehaviour
{
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    [SerializeField] private TMP_Text text;
    [SerializeField] private float rotationAlertDuration = 1.5f;
    [SerializeField] private string rotationMessage = "*Time to change colors!*";
    [SerializeField] private string waitingMessage = "*Waiting all players to vote*";

    private float alertTimeLeft;

    private void OnEnable()
    {
        colorMixingController.StateChanged += Refresh;
        colorMixingController.Rotated += ShowRotationAlert;
    }

    private void OnDisable()
    {
        colorMixingController.StateChanged -= Refresh;
        colorMixingController.Rotated -= ShowRotationAlert;
    }

    private void Start() => Refresh();

    private void ShowRotationAlert()
    {
        alertTimeLeft = rotationAlertDuration;
        Refresh();
    }

    private void Update()
    {
        if (alertTimeLeft <= 0f) return;
        alertTimeLeft -= Time.deltaTime;
        if (alertTimeLeft <= 0f) Refresh();   // alert habis -> kembali ke waiting / kosong
    }

private void Refresh()
{
    if (colorMixingController.IsReady(player) && !colorMixingController.IsMixConfirmed)
        text.text = waitingMessage;          // prioritas 1: sudah Mix -> selalu waiting
    else if (alertTimeLeft > 0f)
        text.text = rotationMessage;         // prioritas 2: baru rotasi
    else
        text.text = "";
}
}