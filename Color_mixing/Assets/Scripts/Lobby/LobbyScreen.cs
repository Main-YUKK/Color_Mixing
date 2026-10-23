using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyScreen : MonoBehaviour
{
    [Header("Teks")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Slot (kiri, tengah=host, kanan)")]
    [SerializeField] private PlayerSlotView leftSlot;
    [SerializeField] private PlayerSlotView hostSlot;
    [SerializeField] private PlayerSlotView rightSlot;

    [Header("Tombol")]
    [SerializeField] private Button playButton;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private LobbyService service;

    private void Start()
    {
        CheckReferences();

        service = LobbyService.Instance;
        if (service == null)
        {
            Debug.LogError("[LobbyScreen] LobbyService.Instance kosong. Cek error compile di LobbyService.cs.");
            return;
        }

        if (service.Mode == LobbyMode.None)
        {
            service.CreateRoom();
        }

        Debug.Log($"[LobbyScreen] Mode={service.Mode}, RoomCode={service.RoomCode}");

        service.OnPlayersChanged += Refresh;
        service.OnRoomClosed += HandleRoomClosed;
        Refresh();
    }

    private void OnDestroy()
    {
        if (service == null) return;
        service.OnPlayersChanged -= Refresh;
        service.OnRoomClosed -= HandleRoomClosed;
    }

    // Dipanggil dari BackButton (On Before Back)
    public void LeaveRoom()
    {
        LobbyService.Instance.LeaveRoom();
    }

    private void HandleRoomClosed(string reason)
    {
        // TODO (tiket ML-6): tampilkan alert popup di main menu memakai alasan ini.
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void Refresh()
    {
        var players = service.Players;
        bool isHost = service.Mode == LobbyMode.Host;

        var host = players.Find(p => p.isHost);
        var others = players.FindAll(p => !p.isHost);
        // Urutan stabil agar P2/P3 menempati posisi yang sama di semua device.
        others.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {service.RoomCode}";

        if (roomNameText != null)
            roomNameText.text = host != null ? $"{Shorten(host.name)}'s Room" : "Joining...";

        if (statusText != null)
        {
            statusText.text = players.Count < 3
                ? $"Must have a total of 3 players to start the game ({players.Count}/3)"
                : "";
        }

        if (hostSlot != null) hostSlot.Show(host, isHost);
        if (leftSlot != null) leftSlot.Show(others.Count > 0 ? others[0] : null, isHost);
        if (rightSlot != null) rightSlot.Show(others.Count > 1 ? others[1] : null, isHost);

        if (playButton != null)
        {
            playButton.gameObject.SetActive(isHost);
            playButton.interactable = isHost && players.Count == 3;
        }
    }

    private static string Shorten(string name, int maxLength = 14)
    {
        if (string.IsNullOrEmpty(name)) return "Player";
        return name.Length <= maxLength ? name : name.Substring(0, maxLength) + "…";
    }

    private void CheckReferences()
    {
        var missing = new List<string>();
        if (roomNameText == null) missing.Add("Room Name Text");
        if (roomCodeText == null) missing.Add("Room Code Text");
        if (statusText == null) missing.Add("Status Text");
        if (leftSlot == null) missing.Add("Left Slot");
        if (hostSlot == null) missing.Add("Host Slot");
        if (rightSlot == null) missing.Add("Right Slot");
        if (playButton == null) missing.Add("Play Button");

        if (missing.Count > 0)
            Debug.LogError("[LobbyScreen] Kolom Inspector yang masih kosong: " + string.Join(", ", missing), this);
    }
}