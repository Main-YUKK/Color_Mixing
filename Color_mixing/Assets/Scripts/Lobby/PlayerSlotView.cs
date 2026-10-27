using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private GameObject readyIcon;
    [SerializeField] private GameObject character;
    [SerializeField] private GameObject silhouette;
    [SerializeField] private GameObject kickButton;

    [Tooltip("Teks saat slot kosong, mis. [player 2]")]
    [SerializeField] private string emptyLabel = "[player]";

    // Dipicu saat host menekan tombol Kick di slot ini.
    public event Action<LobbyPlayer> OnKickClicked;

    private LobbyPlayer current;

    private void Awake()
    {
        if (kickButton == null) return;

        // Button dicari di objek kickButton (atau anaknya), jadi Inspector tidak perlu diubah.
        var button = kickButton.GetComponent<Button>();
        if (button == null) button = kickButton.GetComponentInChildren<Button>(true);

        if (button != null)
            button.onClick.AddListener(() => { if (current != null) OnKickClicked?.Invoke(current); });
        else
            Debug.LogWarning("[PlayerSlotView] Kick Button tidak punya komponen Button.", this);
    }

    public void Show(LobbyPlayer player, bool viewerIsHost)
    {
        current = player;
        bool filled = player != null;

        SetActive(character, filled);
        SetActive(silhouette, !filled);
        // Centang hijau: hanya untuk client yang sudah ready (host tidak perlu ready).
        SetActive(readyIcon, filled && !player.isHost && player.isReady);
        SetActive(kickButton, filled && viewerIsHost && !player.isLocal);

        if (nameLabel != null)
            nameLabel.text = filled ? BuildLabel(player) : emptyLabel;
    }

    private static string BuildLabel(LobbyPlayer p)
    {
        string name = p.name.Length > 12 ? p.name.Substring(0, 12) + "…" : p.name;
        return p.isHost ? $"{name} (host)" : name;
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}