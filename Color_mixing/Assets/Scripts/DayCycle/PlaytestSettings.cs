using UnityEngine;

// Setelan yang bisa diubah host setelah build, untuk playtest (panel "Playtest" di lobby).
// Nilai 0 = "Default": pakai nilai yang sudah ada di scene / asset, jadi game berjalan persis seperti biasa
// selama tidak ada yang diubah.
//
// Host yang menentukan. Nilainya ikut di tiap DaySnapshot, jadi semua device memakai angka yang sama
// (timer klien dan skor lulus ikut tampil di client; level dan rotasi diputuskan host).
//
// Matikan fitur ini di build rilis: Enabled = false (panel tidak dibuat sama sekali).
public static class PlaytestSettings
{
    public const bool Enabled = true;

    public static readonly int[] ClientSecondsOptions = { 30, 60, 90 };
    public static readonly int[] PassingScoreOptions = { 70, 80, 85, 90 };

    public const int MinRotationSeconds = 3;
    public const int MaxRotationSeconds = 300;

    private const string KeyDifficulty = "playtest.difficulty";
    private const string KeyClientSeconds = "playtest.clientSeconds";
    private const string KeyRotation = "playtest.rotationSeconds";
    private const string KeyPassing = "playtest.passingScore";

    // 0 = jadwal normal; 1-3 = semua klien memakai level itu.
    public static int Difficulty { get; private set; }
    // 0 = nilai scene; selain itu detik per klien.
    public static int ClientSeconds { get; private set; }
    // 0 = nilai scene; selain itu detik antar pergantian pigmen.
    public static int RotationSeconds { get; private set; }
    // 0 = nilai scene; selain itu persen skor minimal agar klien puas.
    public static int PassingScore { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Difficulty = ClientSeconds = RotationSeconds = PassingScore = 0;
    }

    // ---------- Dipakai game ----------

    public static RequestDifficulty Level(RequestDifficulty scheduled)
    {
        switch (Difficulty)
        {
            case 1: return RequestDifficulty.LevelOne;
            case 2: return RequestDifficulty.LevelTwo;
            case 3: return RequestDifficulty.LevelThree;
            default: return scheduled;
        }
    }

    public static float ClientTime(float sceneValue) => ClientSeconds > 0 ? ClientSeconds : sceneValue;
    public static float Rotation(float sceneValue) => RotationSeconds > 0 ? RotationSeconds : sceneValue;
    public static float Passing(float sceneValue) => PassingScore > 0 ? PassingScore : sceneValue;

    // ---------- Diubah host (panel) ----------

    public static void SetDifficulty(int value) { Difficulty = Mathf.Clamp(value, 0, 3); Save(); }
    public static void SetClientSeconds(int value) { ClientSeconds = value <= 0 ? 0 : Mathf.Clamp(value, 5, 600); Save(); }
    public static void SetPassingScore(int value) { PassingScore = value <= 0 ? 0 : Mathf.Clamp(value, 1, 100); Save(); }
    public static void SetRotationSeconds(int value)
    {
        RotationSeconds = value <= 0 ? 0 : Mathf.Clamp(value, MinRotationSeconds, MaxRotationSeconds);
        Save();
    }

    // ---------- Simpan di device host ----------

    private static void Save()
    {
        try
        {
            PlayerPrefs.SetInt(KeyDifficulty, Difficulty);
            PlayerPrefs.SetInt(KeyClientSeconds, ClientSeconds);
            PlayerPrefs.SetInt(KeyRotation, RotationSeconds);
            PlayerPrefs.SetInt(KeyPassing, PassingScore);
            PlayerPrefs.Save();
        }
        catch (System.Exception e) { Debug.LogWarning("[PLAYTEST] Gagal menyimpan setelan: " + e.Message); }
    }

    // Dipanggil panel saat host membukanya pertama kali (bukan otomatis, supaya tes dan client tidak ikut terpengaruh).
    public static void LoadSaved()
    {
        try
        {
            Difficulty = Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 0), 0, 3);
            ClientSeconds = Mathf.Max(0, PlayerPrefs.GetInt(KeyClientSeconds, 0));
            RotationSeconds = Mathf.Max(0, PlayerPrefs.GetInt(KeyRotation, 0));
            PassingScore = Mathf.Clamp(PlayerPrefs.GetInt(KeyPassing, 0), 0, 100);
        }
        catch (System.Exception e) { Debug.LogWarning("[PLAYTEST] Gagal membaca setelan: " + e.Message); }
    }

    // ---------- Sinkronisasi (ikut di DaySnapshot) ----------

    public static void Write(DaySnapshot s)
    {
        s.cfgDifficulty = Difficulty;
        s.cfgClientSeconds = ClientSeconds;
        s.cfgRotationSeconds = RotationSeconds;
        s.cfgPassingScore = PassingScore;
    }

    // Client: pakai angka dari host. Kiriman dari host lama (tanpa angka ini) = semua 0 = Default.
    public static void Read(DaySnapshot s)
    {
        Difficulty = Mathf.Clamp(s.cfgDifficulty, 0, 3);
        ClientSeconds = Mathf.Max(0, s.cfgClientSeconds);
        RotationSeconds = Mathf.Max(0, s.cfgRotationSeconds);
        PassingScore = Mathf.Clamp(s.cfgPassingScore, 0, 100);
    }
}
