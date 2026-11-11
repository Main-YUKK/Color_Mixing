using System;

public enum DaySnapshotKind
{
    ClientRequested = 0,   // klien baru diminta (bisa sekaligus hari baru)
    DayEnded = 1,          // target hari tercapai
    GameOver = 2,
}

// Foto keadaan hari dari host, dikirim tiap ada perubahan besar. Client memakainya untuk
// memutar ulang event DayCycleManager yang sama, jadi HUD / laporan tidak perlu tahu soal jaringan.
[Serializable]
public class DaySnapshot
{
    public int kind;           // DaySnapshotKind
    public int runId;          // id run (acak per StartSession); ganti = run baru, nomor seq mulai lagi
    public int seq;            // urutan kiriman host di dalam run
    public bool newDay;        // hari baru dimulai bersama kiriman ini
    public int day;            // DayEnded: hari yang baru selesai
    public int lives;
    public int yes;
    public int no;
    public int timeout;
    public int state;          // DayState
    public int clientIndex;
    public string requestId;   // kosong kalau tidak ada request baru
    public ClientLook look;    // rupa klien baru (valid = false kalau tidak ada)

    // Setelan playtest dari host (PlaytestSettings). 0 = Default; host lama tanpa angka ini juga terbaca 0.
    public int cfgDifficulty;
    public int cfgClientSeconds;
    public int cfgRotationSeconds;
    public int cfgPassingScore;
}
