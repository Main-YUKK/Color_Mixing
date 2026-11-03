using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menukar layar mixing <-> reveal, lalu menampilkan warna hasil dan teks status.
// Pasang di objek yang selalu aktif (ColorMixingScreen), bukan di RevealPanel.
public class RevealPanelView : MonoBehaviour
{
    // MARK: - Inspector
    [SerializeField] private ColorMixingController colorMixingController;
    [SerializeField] private MixVoteController mixVoteController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    [SerializeField] private GameObject mixingPanel;
    [SerializeField] private GameObject revealPanel;
    [SerializeField] private Image resultColor;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject canvasRoot;   
    [SerializeField] private string notVotedMessage = "Kamu punya (1) pilihan";
    [SerializeField] private string votedMessage = "menunggu semua pemain untuk memilih";

    // MARK: - Unity Lifecycle
    private void OnEnable() => mixVoteController.StateChanged += Refresh;
    private void OnDisable() => mixVoteController.StateChanged -= Refresh;
    private void Start() => Refresh();

    // MARK: - Refresh
    private void Refresh()
    {
        if (mixVoteController.IsApplied)
        {
            canvasRoot.SetActive(false);
            return;
        }
        bool voting = mixVoteController.IsVoting;
        mixingPanel.SetActive(!voting);
        revealPanel.SetActive(voting);
        if (!voting) return;

        resultColor.color = ColorMixer.Blend(colorMixingController.Palette, colorMixingController.MixingBowl.PigmentUnits);

        bool hasVoted = mixVoteController.GetVote(player) != MixVoteController.Vote.None;
        statusText.text = hasVoted ? votedMessage : notVotedMessage;
    }
}