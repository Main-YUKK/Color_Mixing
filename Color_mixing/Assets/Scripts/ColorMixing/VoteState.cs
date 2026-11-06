using System.Linq;

public enum Vote { None, Apply, Redo }

// Aturan vote Apply / Redo tanpa Unity: sekali vote tidak bisa diubah, mayoritas menang.
public class VoteState
{
    private readonly Vote[] votes;
    private readonly int majority;

    public VoteState(int playerCount = PigmentAssignment.PlayerCount)
    {
        votes = new Vote[playerCount];
        majority = playerCount / 2 + 1;   // 3 pemain -> 2
    }

    public bool IsVoting { get; private set; }   // true selama menunggu vote
    public bool IsApplied { get; private set; }  // true setelah Apply menang
    public Vote Get(int player) => player >= 0 && player < votes.Length ? votes[player] : Vote.None;
    public int Count(Vote vote) => votes.Count(v => v == vote);

    // Semua vote dikosongkan dan sesi vote dibuka.
    public void Start()
    {
        System.Array.Clear(votes, 0, votes.Length);
        IsVoting = true;
        IsApplied = false;
    }

    // false = ditolak (tidak sedang vote, pemain tidak valid, vote None, atau pemain sudah vote).
    // winner = pemenang kalau suara ini menuntaskan vote, selain itu None.
    public bool TryCast(int player, Vote vote, out Vote winner)
    {
        winner = Vote.None;
        if (!IsVoting || vote == Vote.None || player < 0 || player >= votes.Length || votes[player] != Vote.None) return false;

        votes[player] = vote;
        winner = Count(Vote.Apply) >= majority ? Vote.Apply
               : Count(Vote.Redo) >= majority ? Vote.Redo
               : Vote.None;
        if (winner != Vote.None) IsVoting = false;
        IsApplied = winner == Vote.Apply;
        return true;
    }

    // Salinan keadaan untuk dikirim ke client (Vote sebagai angka).
    public int[] Votes => votes.Select(v => (int)v).ToArray();

    // Client: ganti seluruh keadaan dengan kiriman host. false kalau datanya tidak masuk akal.
    public bool Import(int[] newVotes, bool voting, bool applied)
    {
        if (newVotes == null || newVotes.Length != votes.Length || newVotes.Any(v => v < 0 || v > (int)Vote.Redo)) return false;

        for (int i = 0; i < votes.Length; i++) votes[i] = (Vote)newVotes[i];
        IsVoting = voting;
        IsApplied = applied;
        return true;
    }

    // Pemain sudah vote? (untuk log "sudah vote" yang membedakannya dari penolakan lain)
    public bool HasVoted(int player) => Get(player) != Vote.None;
}
