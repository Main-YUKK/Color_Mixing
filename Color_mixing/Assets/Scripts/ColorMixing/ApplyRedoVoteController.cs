using System;
using System.Linq;
using UnityEngine;

// Vote Apply/Redo setelah semua pemain Mix. Sekali vote tidak bisa diubah, mayoritas menang.
public class ApplyRedoVoteController : MonoBehaviour
{
    // MARK: - Inspector
    [SerializeField] private ColorMixingController colorMixingController;

    // MARK: - State
    private readonly VoteState state = new VoteState();   // aturan vote (mayoritas, sekali vote)

    // MARK: - Public Properties
    public bool IsVoting => state.IsVoting;   // true selama panel reveal menunggu vote
    public bool IsApplied => state.IsApplied;   // true setelah Apply menang
    public Vote GetVote(int player) => state.Get(player);
    public int CountVotes(Vote vote) => state.Count(vote);

    // MARK: - Events
    public event Action StateChanged; // untuk update UI reveal (ikon vote, tombol)

    // Controller yang sedang aktif di scene (dipakai MixNet). Null kalau scene mixing belum dimuat.
    public static ApplyRedoVoteController Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => Current = null;

    // MARK: - Unity Lifecycle
    private void Awake() => Current = this;
    private void OnDestroy() { if (Current == this) Current = null; }

    private void OnEnable() => colorMixingController.MixConfirmed += StartVoting;
    private void OnDisable() => colorMixingController.MixConfirmed -= StartVoting;

    // MARK: - Voting
    private void StartVoting()
    {
        state.Start();   // semua jadi Vote.None
        Debug.Log("[MIX] Vote dimulai: Apply atau Redo?");
        StateChanged?.Invoke();
    }

    public void CastVote(int player, Vote vote)
    {
        if (!GameNet.IsAuthority) { MixNet.RequestVote(vote); return; }   // client: minta host, hasilnya datang sebagai foto

        if (!state.TryCast(player, vote, out Vote winner))
        {
            if (IsVoting && state.HasVoted(player))
                Debug.Log($"[MIX] Ditolak: Pemain {player + 1} sudah vote {GetVote(player)}");
            return;
        }

        Debug.Log($"[MIX] Pemain {player + 1} vote {vote} -> Apply {CountVotes(Vote.Apply)}, Redo {CountVotes(Vote.Redo)}");
        StateChanged?.Invoke();   // UI refresh SETELAH state final

        if (winner == Vote.Redo)
        {
            Debug.Log("[MIX] Redo menang");
            colorMixingController.ResetForRedo();
        }
        else if (winner == Vote.Apply)
        {
            ApplyResult();
        }
    }

    // MARK: - Sinkronisasi
    // Host: isi bagian vote dari foto.
    public void FillSnapshot(MixSnapshot s)
    {
        s.votes = state.Votes;
        s.voting = state.IsVoting;
        s.applied = state.IsApplied;
        s.appliedColor = state.IsApplied ? GameSession.AppliedColor : Color.black;
    }

    // Client: pasang bagian vote dari foto host. Kalau Apply baru saja menang, jalankan hasilnya seperti di host.
    public bool ApplyRemote(MixSnapshot s)
    {
        if (GameNet.IsAuthority || s == null) return false;

        bool wasApplied = state.IsApplied;
        if (!state.Import(s.votes, s.voting, s.applied)) return false;

        StateChanged?.Invoke();
        if (s.applied && !wasApplied) ApplyResult(s.appliedColor);
        return true;
    }

    // Apply menang: simpan warna hasil, hentikan timer klien, masuk fase reveal.
    private void ApplyResult() =>
        ApplyResult(ColorMixer.Blend(colorMixingController.Palette, colorMixingController.MixingBowl.PigmentUnits));

    // Host menghitung warnanya sendiri; client memakai warna kiriman host (sama persis, dan tidak perlu isi mangkuk).
    private void ApplyResult(Color applied)
    {
        GameSession.AppliedColor = applied;

        var timer = FindAnyObjectByType<ClientTimer>();
        if (timer != null) timer.Stop();
        if (DayCycleManager.Instance != null) DayCycleManager.Instance.BeginReveal();

        Debug.Log($"[MIX] Apply menang: warna hasil #{ColorUtility.ToHtmlStringRGB(GameSession.AppliedColor)}");
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

#if UNITY_EDITOR
// Inspector saat Play: vote tiap pemain saat ini + tombol simulasi 3 pemain.
[UnityEditor.CustomEditor(typeof(ApplyRedoVoteController))]
public class ApplyRedoVoteControllerEditor : UnityEditor.Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;   // ikut berubah saat vote masuk

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;

        var controller = (ApplyRedoVoteController)target;
        GUILayout.Label(controller.IsVoting
            ? $"Voting: Apply {controller.CountVotes(Vote.Apply)}, Redo {controller.CountVotes(Vote.Redo)}"
            : "Belum ada sesi vote");

        for (int player = 0; player < PigmentAssignment.PlayerCount; player++)
        {
            Vote vote = controller.GetVote(player);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"P{player + 1}: {vote}", GUILayout.Width(80));
            GUI.enabled = controller.IsVoting && vote == Vote.None;
            if (GUILayout.Button("Apply")) controller.CastVote(player, Vote.Apply);
            if (GUILayout.Button("Redo")) controller.CastVote(player, Vote.Redo);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
#endif
