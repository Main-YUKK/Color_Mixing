using System;
using System.Linq;
using UnityEngine;

// Vote Apply/Redo setelah semua pemain Mix. Sekali vote tidak bisa diubah, mayoritas menang.
public class MixVoteController : MonoBehaviour
{
    public enum Vote { None, Apply, Redo }

    // MARK: - Inspector
    [SerializeField] private ColorMixingController colorMixingController;

    // MARK: - State
    private const int Majority = PigmentAssignment.PlayerCount / 2 + 1; // 3 pemain -> 2
    private readonly Vote[] votes = new Vote[PigmentAssignment.PlayerCount];

    // MARK: - Public Properties
    public bool IsVoting { get; private set; }   // true selama panel reveal menunggu vote
    public Vote GetVote(int player) => votes[player];
    public int CountVotes(Vote vote) => votes.Count(v => v == vote);

    // MARK: - Events
    public event Action StateChanged; // untuk update UI reveal (ikon vote, tombol)

    // MARK: - Unity Lifecycle
    private void OnEnable() => colorMixingController.MixConfirmed += StartVoting;
    private void OnDisable() => colorMixingController.MixConfirmed -= StartVoting;

    // MARK: - Voting
    private void StartVoting()
    {
        Array.Clear(votes, 0, votes.Length);   // semua jadi Vote.None
        IsVoting = true;
        Debug.Log("[MIX] Vote dimulai: Apply atau Redo?");
        StateChanged?.Invoke();
    }

    public void CastVote(int player, Vote vote)
    {
        if (!IsVoting || vote == Vote.None) return;
        if (votes[player] != Vote.None)
        {
            Debug.Log($"[MIX] Ditolak: Pemain {player + 1} sudah vote {votes[player]}");
            return;
        }

        votes[player] = vote;
        Vote winner = CountVotes(Vote.Apply) >= Majority ? Vote.Apply
                    : CountVotes(Vote.Redo) >= Majority ? Vote.Redo
                    : Vote.None;
        if (winner != Vote.None) IsVoting = false;

        Debug.Log($"[MIX] Pemain {player + 1} vote {vote} -> Apply {CountVotes(Vote.Apply)}, Redo {CountVotes(Vote.Redo)}");
        StateChanged?.Invoke();   // UI refresh SETELAH state final

        if (winner == Vote.Redo)
        {
            Debug.Log("[MIX] Redo menang");
            colorMixingController.ResetForRedo();
        }
        else if (winner == Vote.Apply)
        {
            Debug.Log("[MIX] Apply menang");   // langkah 4: GameSession, BeginReveal, Stop timer, sembunyikan UI
        }
    }

#if UNITY_EDITOR
    // MARK: - Debug (Editor Only)
    [ContextMenu("P1 Apply")] private void P1Apply() => CastVote(0, Vote.Apply);
    [ContextMenu("P1 Redo")] private void P1Redo() => CastVote(0, Vote.Redo);
    [ContextMenu("P2 Apply")] private void P2Apply() => CastVote(1, Vote.Apply);
    [ContextMenu("P2 Redo")] private void P2Redo() => CastVote(1, Vote.Redo);
    [ContextMenu("P3 Apply")] private void P3Apply() => CastVote(2, Vote.Apply);
    [ContextMenu("P3 Redo")] private void P3Redo() => CastVote(2, Vote.Redo);
#endif
}