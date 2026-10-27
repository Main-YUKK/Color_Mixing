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
    [SerializeField] private Button playButton;                  // hanya tampil di host
    [SerializeField] private Button readyButton;                 // hanya tampil di client
    [SerializeField] private TMP_Text readyButtonLabel;          // opsional: "Ready" / "Cancel Ready"

    [Header("Edit nama room (khusus host)")]
    [SerializeField] private Button editRoomNameButton;          // ikon pensil
    [SerializeField] private TMP_InputField roomNameInput;       // awalnya boleh aktif, dimatikan otomatis

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Harus sama dengan LobbyService.RequiredPlayers (ubah ke 2 kalau tes dengan 2 device).
    private const int RequiredPlayers = 3;

    private LobbyService service;
    private bool leaving;   // mencegah LoadScene dua kali

    private void Start()
    {
        AutoWireMissingReferences();
        CheckReferences();

        service = LobbyService.Instance;
        if (service == null)
        {
            Debug.LogError("[LobbyScreen] LobbyService.Instance kosong. Cek error compile di LobbyService.cs.");
            return;
        }

        // Masuk lobby tanpa Join dulu = host baru. Client sudah punya Mode.Client dari JoinRoom().
        if (service.Mode == LobbyMode.None)
        {
            service.CreateRoom();
        }

        Debug.Log($"[LobbyScreen] Mode={service.Mode}, RoomCode={service.RoomCode}");

        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);

        if (editRoomNameButton != null) editRoomNameButton.onClick.AddListener(OnEditRoomNameClicked);
        if (roomNameInput != null)
        {
            roomNameInput.characterLimit = LobbyService.MaxRoomNameLength;
            roomNameInput.onEndEdit.AddListener(OnRoomNameEndEdit);
            roomNameInput.gameObject.SetActive(false);
        }

        if (leftSlot != null) leftSlot.OnKickClicked += HandleKick;
        if (hostSlot != null) hostSlot.OnKickClicked += HandleKick;
        if (rightSlot != null) rightSlot.OnKickClicked += HandleKick;

        service.OnPlayersChanged += Refresh;
        service.OnRoomClosed += HandleRoomClosed;
        Refresh();
    }

    private void OnDestroy()
    {
        if (readyButton != null) readyButton.onClick.RemoveListener(OnReadyClicked);
        if (editRoomNameButton != null) editRoomNameButton.onClick.RemoveListener(OnEditRoomNameClicked);
        if (roomNameInput != null) roomNameInput.onEndEdit.RemoveListener(OnRoomNameEndEdit);

        if (leftSlot != null) leftSlot.OnKickClicked -= HandleKick;
        if (hostSlot != null) hostSlot.OnKickClicked -= HandleKick;
        if (rightSlot != null) rightSlot.OnKickClicked -= HandleKick;

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
        if (leaving) return;
        leaving = true;

        // TODO (tiket ML-6): tampilkan alert popup di main menu memakai alasan ini
        // (termasuk "Kamu dikeluarkan oleh host.").
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ---------- Aksi tombol ----------

    // Toggle: klik pertama = ready (ReadyIcon menyala di semua device), klik lagi = batal.
    private void OnReadyClicked()
    {
        if (service == null || !service.CanSetReady) return;
        service.SetReady(!service.IsLocalReady);
    }

    private void HandleKick(LobbyPlayer player)
    {
        if (player == null) return;
        service.KickPlayer(player.name);
    }

    private void OnEditRoomNameClicked()
    {
        if (service.Mode != LobbyMode.Host || roomNameInput == null) return;

        roomNameInput.gameObject.SetActive(true);
        roomNameInput.text = service.RoomName;
        roomNameInput.ActivateInputField();
        Refresh();
    }

    // Keyboard ditutup / fokus hilang: simpan nama (kosong = kembali ke default), lalu sembunyikan input.
    private void OnRoomNameEndEdit(string text)
    {
        service.SetRoomName(text);
        roomNameInput.gameObject.SetActive(false);
        Refresh();
    }

    // ---------- Tampilan ----------

    private void Refresh()
    {
        if (service == null) return;

        var players = service.Players;
        bool isHost = service.Mode == LobbyMode.Host;
        bool isClient = service.Mode == LobbyMode.Client;

        var host = players.Find(p => p.isHost);

        // Urutan slot ditentukan host (urutan datang), jadi P2/P3 tidak bertukar tempat
        // ketika ada pemain baru dan posisinya sama di semua device.
        var others = service.GetOthersInSlotOrder();

        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {service.RoomCode}";

        bool editing = roomNameInput != null && roomNameInput.gameObject.activeSelf;

        if (roomNameText != null)
        {
            roomNameText.gameObject.SetActive(!editing);

            if (host == null)
                roomNameText.text = "Joining...";
            else if (!string.IsNullOrEmpty(service.RoomName))
                roomNameText.text = service.RoomName;
            else
                roomNameText.text = $"{Shorten(host.name)}'s Room";
        }

        if (editRoomNameButton != null)
            editRoomNameButton.gameObject.SetActive(isHost && !editing && roomNameInput != null);

        if (statusText != null)
        {
            bool allReady = players.TrueForAll(p => p.isHost || p.isReady);

            if (players.Count < RequiredPlayers)
                statusText.text = $"Must have a total of {RequiredPlayers} players to start the game ({players.Count}/{RequiredPlayers})";
            else if (!allReady)
                statusText.text = "Waiting for all players to be ready";
            else
                statusText.text = isHost ? "Everyone is ready! Press Play to start." : "Waiting for the host to start the game...";
        }

        // Slot: ReadyIcon / Kick diatur di PlayerSlotView.Show() berdasarkan LobbyPlayer.isReady.
        if (hostSlot != null) hostSlot.Show(host, isHost);
        if (leftSlot != null) leftSlot.Show(others.Count > 0 ? others[0] : null, isHost);
        if (rightSlot != null) rightSlot.Show(others.Count > 1 ? others[1] : null, isHost);

        // Client: ReadyButton (aktif setelah host terdengar). Host: PlayButton (aktif kalau semua siap).
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(isClient);
            readyButton.interactable = service.CanSetReady;
        }
        if (readyButtonLabel != null)
            readyButtonLabel.text = service.IsLocalReady ? "Cancel Ready" : "Ready";

        if (playButton != null)
        {
            playButton.gameObject.SetActive(isHost);
            playButton.interactable = service.CanStart;
            // TODO: onClick Play belum dipasang (scene gameplay belum ada).
        }
    }

    private static string Shorten(string name, int maxLength = 14)
    {
        if (string.IsNullOrEmpty(name)) return "Player";
        return name.Length <= maxLength ? name : name.Substring(0, maxLength) + "…";
    }

    // Kalau referensi di Inspector masih kosong, cari objeknya berdasarkan NAMA di scene
    // (nama sesuai Hierarchy: PlayButton, ReadyButton, EditNameButton, RoomNameInput).
    // Tanpa ini, tombol yang referensinya kosong tidak pernah disembunyikan/ditampilkan oleh script,
    // sehingga ReadyButton tetap terlihat di host.
    private void AutoWireMissingReferences()
    {
        if (playButton == null) playButton = FindInScene<Button>("PlayButton");
        if (readyButton == null) readyButton = FindInScene<Button>("ReadyButton");
        if (editRoomNameButton == null) editRoomNameButton = FindInScene<Button>("EditNameButton");
        if (roomNameInput == null) roomNameInput = FindInScene<TMP_InputField>("RoomNameInput");
        if (readyButtonLabel == null && readyButton != null)
            readyButtonLabel = readyButton.GetComponentInChildren<TMP_Text>(true);
    }

    private static T FindInScene<T>(string objectName) where T : Component
    {
        var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
            if (c.gameObject.name == objectName) return c;
        return null;
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
        if (playButton == null) missing.Add("Play Button (objek 'PlayButton' harus punya komponen Button)");
        if (readyButton == null) missing.Add("Ready Button (objek 'ReadyButton' harus punya komponen Button)");

        if (missing.Count > 0)
            Debug.LogError("[LobbyScreen] Referensi wajib yang kosong: " + string.Join(", ", missing), this);

        // PlayButton disembunyikan di client, jadi semua anaknya ikut hilang.
        if (readyButton != null && playButton != null && readyButton.transform.IsChildOf(playButton.transform))
            Debug.LogError("[LobbyScreen] ReadyButton berada di dalam PlayButton, jadi tidak akan terlihat di client. Pindahkan ReadyButton keluar dari PlayButton di Hierarchy.", this);

        // Opsional: tanpa ini fitur ganti nama room saja yang tidak aktif (ikon pensil disembunyikan).
        if (editRoomNameButton == null || roomNameInput == null)
            Debug.LogWarning("[LobbyScreen] Fitur edit nama room nonaktif: Edit Room Name Button / Room Name Input belum ada.", this);
    }
}