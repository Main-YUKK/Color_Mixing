using System;
using System.Collections.Generic;
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
        CheckReferences();

        service = LobbyService.Instance;
        if (service == null)
        {
            Debug.LogError("[LobbyScreen] LobbyService.Instance kosong. Cek apakah LobbyService.cs ada error compile.");
            return;
        }

        // Scene ini dibuka dari tombol Host Room (belum ada sesi) -> jadi host.
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

        // 1) Teks utama diisi LEBIH DULU supaya tetap berubah walau slot bermasalah.

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

        // 2) Slot pemain
        SetSlot(hostSlot, host, isHost);
        SetSlot(leftSlot, others.Count > 0 ? others[0] : null, isHost);
        SetSlot(rightSlot, others.Count > 1 ? others[1] : null, isHost);

        // 3) Tombol Play
        if (playButton != null)
        {
            playButton.gameObject.SetActive(isHost);
            playButton.interactable = isHost && players.Count == 3;
        }
    }

    // Nama panjang dipotong supaya judul room tidak mendorong tombol edit terlalu jauh.
    private static string Shorten(string name, int maxLength = 14)
    {
        if (string.IsNullOrEmpty(name)) return "Player";
        return name.Length <= maxLength ? name : name.Substring(0, maxLength) + "…";
    }

    private void SetSlot(SlotView slot, LobbyPlayer player, bool viewerIsHost)
    {
        if (slot == null) return;
        bool filled = player != null;

        SetActive(slot.character, filled);
        SetActive(slot.silhouette, !filled);
        SetActive(slot.readyIcon, filled);
        SetActive(slot.kickButton, filled && viewerIsHost && !player.isLocal);
        if (slot.nameLabel != null) slot.nameLabel.text = filled ? player.name : "[empty]";
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    // Memberi tahu kolom Inspector mana yang masih kosong.
    private void CheckReferences()
    {
        var missing = new List<string>();
        if (roomNameText == null) missing.Add("Room Name Text");
        if (roomCodeText == null) missing.Add("Room Code Text");
        if (statusText == null) missing.Add("Status Text");
        if (playButton == null) missing.Add("Play Button");
        CheckSlot("Left Slot", leftSlot, missing);
        CheckSlot("Host Slot", hostSlot, missing);
        CheckSlot("Right Slot", rightSlot, missing);

        if (missing.Count > 0)
        {
            Debug.LogError("[LobbyScreen] Kolom Inspector yang masih kosong: " + string.Join(", ", missing), this);
        }
    }

    private static void CheckSlot(string label, SlotView slot, List<string> missing)
    {
        if (slot == null) { missing.Add(label); return; }
        if (slot.nameLabel == null) missing.Add(label + " > Name Label");
        if (slot.readyIcon == null) missing.Add(label + " > Ready Icon");
        if (slot.character == null) missing.Add(label + " > Character");
        if (slot.silhouette == null) missing.Add(label + " > Silhouette");
        if (slot.kickButton == null) missing.Add(label + " > Kick Button");
    }
}