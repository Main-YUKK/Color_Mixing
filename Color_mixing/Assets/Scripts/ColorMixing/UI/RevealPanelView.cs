using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menukar layar mixing <-> reveal, lalu menampilkan warna hasil dan teks status.
// Pasang di objek yang selalu aktif (ColorMixingScreen), bukan di RevealPanel.
public class RevealPanelView : MonoBehaviour
{
    // MARK: - Inspector
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private ApplyRedoVoteController applyRedoVoteController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    private int Player => LocalPlayer.Index(player);   // nomor pemain di device ini
    [SerializeField] private GameObject mixingPanel;
    [SerializeField] private GameObject revealPanel;
    [SerializeField] private Image resultColor;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject canvasRoot;   
    [SerializeField] private string notVotedMessage = "Kamu punya (1) pilihan";
    [SerializeField] private string votedMessage = "menunggu semua pemain untuk memilih";

    // MARK: - Unity Lifecycle
    private void OnEnable() => applyRedoVoteController.StateChanged += Refresh;
    private void OnDisable() => applyRedoVoteController.StateChanged -= Refresh;
    private void Start() => Refresh();

    // MARK: - Refresh
    private void Refresh()
    {
        if (applyRedoVoteController.IsApplied)
        {
            canvasRoot.SetActive(false);
            return;
        }
        bool voting = applyRedoVoteController.IsVoting;
        mixingPanel.SetActive(!voting);
        revealPanel.SetActive(voting);
        if (!voting) return;

        resultColor.color = ColorMixer.Blend(colorMixingController.Palette, colorMixingController.MixingBowl.PigmentUnits);

        bool hasVoted = applyRedoVoteController.GetVote(Player) != Vote.None;
        statusText.text = hasVoted ? votedMessage : notVotedMessage;
    }
}