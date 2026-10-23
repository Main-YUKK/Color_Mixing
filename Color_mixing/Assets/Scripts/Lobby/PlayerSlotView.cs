using TMPro;
using UnityEngine;

public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private GameObject readyIcon;
    [SerializeField] private GameObject character;
    [SerializeField] private GameObject silhouette;
    [SerializeField] private GameObject kickButton;

    [Tooltip("Teks saat slot kosong, mis. [player 2]")]
    [SerializeField] private string emptyLabel = "[player]";

    public void Show(LobbyPlayer player, bool viewerIsHost)
    {
        bool filled = player != null;

        SetActive(character, filled);
        SetActive(silhouette, !filled);
        SetActive(readyIcon, filled);
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