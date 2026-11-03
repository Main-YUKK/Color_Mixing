using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tombol vote milik local player: Ulangi (Redo) atau Warnai (Apply). Menampilkan ikon jumlah vote.
[RequireComponent(typeof(Button))]
public class VoteButton : MonoBehaviour
{
    [SerializeField] private MixVoteController mixVoteController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    [SerializeField] private MixVoteController.Vote vote;
    [SerializeField] private GameObject[] icons;   // 3 ikon (1 per pemain)
    [SerializeField] private TMP_Text label; 

    private Button button;
    private Sprite disabledSprite;   // sprite Disabled asli dari Inspector

    private void Awake()
    {
        button = GetComponent<Button>();
        disabledSprite = button.spriteState.disabledSprite;
        button.onClick.AddListener(() => mixVoteController.CastVote(player, vote));
    }

    private void OnEnable()
    {
        mixVoteController.StateChanged += Refresh;
        Refresh();
    }

    private void Start() => label.text = vote == MixVoteController.Vote.Redo ? "Ulangi" : "Warnai";
    private void OnDisable() => mixVoteController.StateChanged -= Refresh;

    private void Refresh()
    {
        MixVoteController.Vote myVote = mixVoteController.GetVote(player);

        // Setelah vote tombol nonaktif; tombol pilihan pemain tetap tampil Pressed.
        SpriteState state = button.spriteState;
        state.disabledSprite = myVote == vote ? state.pressedSprite : disabledSprite;
        button.spriteState = state;
        button.interactable = myVote == MixVoteController.Vote.None;

        int count = mixVoteController.CountVotes(vote);
        for (int i = 0; i < icons.Length; i++)
            icons[i].SetActive(i < count);
    }
}