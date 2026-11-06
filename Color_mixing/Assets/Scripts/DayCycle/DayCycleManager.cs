using System;
using System.Collections.Generic;
using UnityEngine;

// Siklus hari: 3 nyawa, 3 "Yes" mengakhiri hari, nyawa 0 = game over.
// Host-authoritative: panggil method-nya hanya di host, lalu broadcast event ke device lain.
public class DayCycleManager : MonoBehaviour
{
    public static DayCycleManager Instance { get; private set; }

    // Nyalakan agar hari berikutnya menunggu StartNextDay() (mis. tombol Next Day di laporan).
    // EndOfDayReport menyalakannya sendiri.
    public bool holdForReport;

    public DayState State { get; private set; } = DayState.Inactive;
    public int CurrentDay { get; private set; }
    public int Lives { get; private set; }
    public int YesCount { get; private set; }
    public int NoCount { get; private set; }
    public int TimeoutCount { get; private set; }
    public bool IsRunning { get; private set; }
    public bool AwaitingNextDay { get; private set; }
    public List<DayResult> History { get; } = new List<DayResult>();

    // NET: event bertanda (NET) harus disinkronkan ke semua device.
    public event Action<int> OnDayStarted;
    public event Action<int> OnLivesChanged;        // NET
    public event Action<int> OnHappyClientChanged;     // NET
    public event Action OnClientRequested;          // klien baru harus muncul
    public event Action<DayResult> OnDayEnded;
    public event Action OnGameOver;                 // NET
    public event Action OnRevealStarted;            // layar hasil klien mulai tampil

    // Host: keadaan hari berubah besar, saatnya dikirim ke client (diurus DayCycleNet).
    public event Action<DaySnapshotKind> OnStatePublished;

    // Tanpa sesi jaringan (Editor / satu device) atau host = pemegang aturan. Client hanya menerima foto dari host.
    public bool IsAuthority => GameNet.Instance == null || GameNet.Instance.IsHost;

    private int snapshotSeq;       // host: nomor kiriman terakhir
    private int runId;             // host: id run ini
    private static int nextRunId;  // host: id run berikutnya (naik tiap StartSession)
    private int lastRunId;         // client: run yang sedang diikuti
    private int lastAppliedSeq;    // client: nomor kiriman terakhir yang dipakai
    private bool dayJustStarted;

    // Nomor foto hari terakhir (host: yang dikirim, client: yang dipakai). Foto mixing membawa angka ini,
    // supaya foto klien sebelumnya tidak dipasang ke klien yang baru.
    public int SyncStamp => IsAuthority ? snapshotSeq : lastAppliedSeq;

    private void Awake() => Instance = this;

    public void StartSession(bool continueSaved)
    {
        if (!IsAuthority) return;
        snapshotSeq = 0;
        // Naik terus (bukan acak penuh): client bisa membedakan run yang lebih baru dari kiriman lama yang terlambat.
        if (nextRunId == 0) nextRunId = UnityEngine.Random.Range(1, 1_000_000);
        runId = ++nextRunId;
        CurrentDay = continueSaved && SaveService.Active != null ? SaveService.Active.nextDay : 1;

        History.Clear();
        GameSession.StartRun(CurrentDay);
        IsRunning = true;
        BeginDay();
    }

    private void BeginDay()
    {
        Lives = GameRules.StartingLives;
        YesCount = 0;
        NoCount = 0;
        TimeoutCount = 0;
        AwaitingNextDay = false;
        dayJustStarted = true;
        GameSession.Day = CurrentDay;

        OnDayStarted?.Invoke(CurrentDay);
        OnLivesChanged?.Invoke(Lives);
        OnHappyClientChanged?.Invoke(YesCount);
        RequestClient();
    }

    private void RequestClient()
    {
        // Slot klien di jadwal = jumlah Yes hari ini (klien yang gagal diganti request lain di slot yang sama).
        GameSession.ClientIndex = YesCount;
        State = DayState.Mixing;
        OnClientRequested?.Invoke();
        OnStatePublished?.Invoke(DaySnapshotKind.ClientRequested);   // setelah request dipilih oleh pendengar di atas
    }

    // Panggil saat feedback Yes/No mulai tampil (opsional).
    public void BeginReveal()
    {
        if (!IsRunning || State != DayState.Mixing) return;
        State = DayState.Reveal;
        OnRevealStarted?.Invoke();
    }

    // Panggil saat reveal feedback selesai.
    public void OnFeedbackRevealed(bool isYes)
    {
        if (!IsAuthority || !IsRunning || AwaitingNextDay) return;

        if (isYes)
        {
            YesCount++;
            OnHappyClientChanged?.Invoke(YesCount);
            if (YesCount >= GameRules.YesToWinDay) { EndDay(); return; }
        }
        else
        {
            NoCount++;
            if (LoseLife()) return;
        }

        RequestClient();
    }

    // Panggil saat timer klien habis tanpa submit.
    public void OnClientTimeout()
    {
        if (!IsAuthority || !IsRunning || AwaitingNextDay) return;

        TimeoutCount++;
        if (LoseLife()) return;
        RequestClient();
    }

    // true kalau itu nyawa terakhir (game over).
    private bool LoseLife()
    {
        Lives--;
        OnLivesChanged?.Invoke(Lives);
        if (Lives > 0) return false;

        GameOver();
        return true;
    }

    private void EndDay()
    {
        // Simpan dulu, baru laporan hari tampil ("progressmu telah disimpan").
        SaveService.RecordDayCompleted(CurrentDay);

        var result = new DayResult
        {
            day = CurrentDay,
            yesCount = YesCount,
            noCount = NoCount,
            timeoutCount = TimeoutCount,
            livesLeft = Lives,
        };
        History.Add(result);
        CurrentDay++;
        State = DayState.DayEnd;

        if (holdForReport)
        {
            AwaitingNextDay = true;
            OnDayEnded?.Invoke(result);
            OnStatePublished?.Invoke(DaySnapshotKind.DayEnded);
        }
        else
        {
            OnDayEnded?.Invoke(result);
            OnStatePublished?.Invoke(DaySnapshotKind.DayEnded);
            BeginDay();
        }
    }

    // Tutup laporan dan mulai hari berikutnya.
    public void StartNextDay()
    {
        if (!IsAuthority || !IsRunning || !AwaitingNextDay) return;
        BeginDay();
    }

    private void GameOver()
    {
        IsRunning = false;
        State = DayState.GameOver;
        SaveService.ResetActiveProgress();   // sesi berikutnya mulai dari Day 1
        OnGameOver?.Invoke();
        OnStatePublished?.Invoke(DaySnapshotKind.GameOver);
    }

    // ---------- Sinkronisasi ----------

    // Host: foto keadaan sekarang. ClientRequested memakai GameSession.CurrentRequest (sudah dipilih pendengar event).
    public DaySnapshot TakeSnapshot(DaySnapshotKind kind)
    {
        var s = new DaySnapshot
        {
            kind = (int)kind,
            runId = runId,
            seq = ++snapshotSeq,
            day = kind == DaySnapshotKind.DayEnded && History.Count > 0 ? History[History.Count - 1].day : CurrentDay,
            lives = Lives,
            yes = YesCount,
            no = NoCount,
            timeout = TimeoutCount,
            state = (int)State,
            clientIndex = GameSession.ClientIndex,
        };

        if (kind == DaySnapshotKind.ClientRequested)
        {
            s.newDay = dayJustStarted;
            dayJustStarted = false;
            s.requestId = GameSession.CurrentRequest != null ? GameSession.CurrentRequest.requestId : null;
            s.look = GameSession.CurrentLook ?? new ClientLook();   // dipasang CharacterSpawner saat klien baru dipilih
        }
        return s;
    }

    // Client: pakai foto dari host dan putar ulang event yang sama seperti di host.
    // request = hasil ClientRequestDatabase.GetById(snapshot.requestId) oleh pemanggil (null kalau tidak ada).
    // false = foto ditolak (ini host, atau foto lama / dobel).
    public bool ApplyRemote(DaySnapshot s, ClientRequest request)
    {
        if (IsAuthority || s == null) return false;

        if (s.runId < lastRunId) return false;   // kiriman dari run lama yang terlambat
        bool newRun = s.runId != lastRunId;
        if (newRun) { lastRunId = s.runId; lastAppliedSeq = 0; }
        if (s.seq <= lastAppliedSeq) return false;
        lastAppliedSeq = s.seq;
        if (newRun) { History.Clear(); GameSession.StartRun(s.kind == (int)DaySnapshotKind.DayEnded ? s.day + 1 : s.day); }

        switch ((DaySnapshotKind)s.kind)
        {
            case DaySnapshotKind.ClientRequested: ApplyClientRequested(s, request); break;
            case DaySnapshotKind.DayEnded: ApplyDayEnded(s); break;
            case DaySnapshotKind.GameOver: ApplyGameOver(s); break;
        }
        return true;
    }

    private void ApplyClientRequested(DaySnapshot s, ClientRequest request)
    {
        IsRunning = true;
        AwaitingNextDay = false;
        CurrentDay = s.day;
        NoCount = s.no;
        TimeoutCount = s.timeout;

        if (s.newDay)
        {
            GameSession.Day = s.day;
            OnDayStarted?.Invoke(s.day);
        }

        if (s.newDay || s.lives != Lives) { Lives = s.lives; OnLivesChanged?.Invoke(Lives); }
        if (s.newDay || s.yes != YesCount) { YesCount = s.yes; OnHappyClientChanged?.Invoke(YesCount); }

        GameSession.ClientIndex = s.clientIndex;
        GameSession.CurrentRequest = request;
        GameSession.CurrentLook = s.look != null && s.look.valid ? s.look : null;
        State = (DayState)s.state;
        OnClientRequested?.Invoke();
    }

    private void ApplyDayEnded(DaySnapshot s)
    {
        Lives = s.lives;
        NoCount = s.no;
        TimeoutCount = s.timeout;
        if (s.yes != YesCount) { YesCount = s.yes; OnHappyClientChanged?.Invoke(YesCount); }

        var result = new DayResult { day = s.day, yesCount = s.yes, noCount = s.no, timeoutCount = s.timeout, livesLeft = s.lives };
        History.Add(result);
        CurrentDay = s.day + 1;
        State = DayState.DayEnd;
        AwaitingNextDay = holdForReport;
        OnDayEnded?.Invoke(result);
    }

    private void ApplyGameOver(DaySnapshot s)
    {
        if (s.lives != Lives) { Lives = s.lives; OnLivesChanged?.Invoke(Lives); }
        IsRunning = false;
        State = DayState.GameOver;
        OnGameOver?.Invoke();
    }
}
