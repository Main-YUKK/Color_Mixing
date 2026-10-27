using System;

public static class GameRules
{
    public const int StartingLives = 3;
    public const int YesToWinDay = 3;
}

public enum DayState
{
    Inactive,
    Mixing,     // klien aktif, pemain sedang mencampur warna
    Reveal,     // feedback Yes/No sedang ditampilkan
    DayEnd,     // target tercapai, hasil hari dicatat
    GameOver,
}

// Progres yang disimpan di device host.
[Serializable]
public class SaveData
{
    public int nextDay = 1;   // hari awal saat Continue
    public int bestDay = 1;   // rekor hari tertinggi
}

[Serializable]
public struct DayResult
{
    public int day;
    public int yesCount;
    public int noCount;
    public int timeoutCount;
    public int livesLeft;
    public int bestDay;
}
