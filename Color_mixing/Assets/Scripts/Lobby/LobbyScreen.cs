using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyScreen : MonoBehaviour
{
    [Serializable]
    public class SlotView
    {
        public TMP_Text nameLabel;
        public GameObject readyIcon;
        public GameObject character;
        public GameObject silhouette;
        public GameObject kickButton;
    }

    [Header("Teks")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Slot (kiri, tengah=host, kanan)")]
    [SerializeField] private SlotView leftSlot;
    [SerializeField] private SlotView hostSlot;
    [SerializeField] private SlotView rightSlot;

    [Header("Tombol")]
    [SerializeField] private Button playButton;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private LobbyService service;

    private void Start()
    {
        service = LobbyService.Instance;

        // Scene ini dibuka dari tombol Host Room (belum ada sesi) -> jadi host.
        if (service.Mode == LobbyMode.None)
        {
            service.CreateRoom();
        }

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

        SetSlot(hostSlot, host, isHost);
        SetSlot(leftSlot, others.Count > 0 ? others[0] : null, isHost);
        SetSlot(rightSlot, others.Count > 1 ? others[1] : null, isHost);

        roomCodeText.text = $"Room Code: {service.RoomCode}";
        roomNameText.text = host != null ? $"{host.name}'s Room" : "Joining...";

        statusText.text = players.Count < 3
            ? $"Must have a total of 3 players to start the game ({players.Count}/3)"
            : "";

        // Tahap C nanti: Play hanya untuk host dan saat semua siap.
        playButton.gameObject.SetActive(isHost);
        playButton.interactable = isHost && players.Count == 3;
    }

    private void SetSlot(SlotView slot, LobbyPlayer player, bool viewerIsHost)
    {
        bool filled = player != null;
        slot.character.SetActive(filled);
        slot.silhouette.SetActive(!filled);
        slot.readyIcon.SetActive(filled);
        slot.nameLabel.text = filled ? player.name : "[empty]";
        slot.kickButton.SetActive(filled && viewerIsHost && !player.isLocal);
    }
}