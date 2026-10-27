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

    private void Awake() => Instance = this;

    public void StartSession(bool continueSaved)
    {
        if (continueSaved && SaveService.HasSave())
        {
            CurrentDay = SaveService.Load().nextDay;
        }
        else
        {
            SaveService.ResetProgress();
            CurrentDay = 1;
        }

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
    }

    // Panggil saat feedback Yes/No mulai tampil (opsional).
    public void BeginReveal()
    {
        if (IsRunning && State == DayState.Mixing) State = DayState.Reveal;
    }

    // Panggil saat reveal feedback selesai.
    public void OnFeedbackRevealed(bool isYes)
    {
        if (!IsRunning || AwaitingNextDay) return;

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
        if (!IsRunning || AwaitingNextDay) return;

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
        // Simpan dulu supaya laporan sudah bisa menampilkan rekor terbaru.
        var save = SaveService.Load();
        save.nextDay = CurrentDay + 1;
        save.bestDay = Mathf.Max(save.bestDay, save.nextDay);
        SaveService.Save(save);

        var result = new DayResult
        {
            day = CurrentDay,
            yesCount = YesCount,
            noCount = NoCount,
            timeoutCount = TimeoutCount,
            livesLeft = Lives,
            bestDay = save.bestDay,
        };
        History.Add(result);
        CurrentDay++;
        State = DayState.DayEnd;

        if (holdForReport)
        {
            AwaitingNextDay = true;
            OnDayEnded?.Invoke(result);
        }
        else
        {
            OnDayEnded?.Invoke(result);
            BeginDay();
        }
    }

    // Tutup laporan dan mulai hari berikutnya.
    public void StartNextDay()
    {
        if (!IsRunning || !AwaitingNextDay) return;
        BeginDay();
    }

    private void GameOver()
    {
        IsRunning = false;
        State = DayState.GameOver;
        SaveService.ResetProgress();   // sesi berikutnya mulai dari Day 1
        OnGameOver?.Invoke();
    }
}
