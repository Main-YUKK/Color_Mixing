using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tombol vote milik local player: Ulangi (Redo) atau Warnai (Apply). Menampilkan ikon jumlah vote.
[RequireComponent(typeof(Button))]
public class VoteButton : MonoBehaviour
{
    [SerializeField] private ApplyRedoVoteController applyRedoVoteController;
    [SerializeField, Range(0, 2)] private int player;   // sementara 0, nanti dari local player
    [SerializeField] private ApplyRedoVoteController.Vote vote;
    [SerializeField] private GameObject[] icons;   // 3 ikon (1 per pemain)
    [SerializeField] private TMP_Text label; 

    private Button button;
    private Sprite disabledSprite;   // sprite Disabled asli dari Inspector

    private void Awake()
    {
        button = GetComponent<Button>();
        disabledSprite = button.spriteState.disabledSprite;
        button.onClick.AddListener(() => applyRedoVoteController.CastVote(player, vote));
    }

    private void OnEnable()
    {
        applyRedoVoteController.StateChanged += Refresh;
        Refresh();
    }

    private void Start() => label.text = vote == ApplyRedoVoteController.Vote.Redo ? "Ulangi" : "Warnai";
    private void OnDisable() => applyRedoVoteController.StateChanged -= Refresh;

    private void Refresh()
    {
        ApplyRedoVoteController.Vote myVote = applyRedoVoteController.GetVote(player);

        // Setelah vote tombol nonaktif; tombol pilihan pemain tetap tampil Pressed.
        SpriteState state = button.spriteState;
        state.disabledSprite = myVote == vote ? state.pressedSprite : disabledSprite;
        button.spriteState = state;
        button.interactable = myVote == ApplyRedoVoteController.Vote.None;

        int count = applyRedoVoteController.CountVotes(vote);
        for (int i = 0; i < icons.Length; i++)
            icons[i].SetActive(i < count);
    }
}