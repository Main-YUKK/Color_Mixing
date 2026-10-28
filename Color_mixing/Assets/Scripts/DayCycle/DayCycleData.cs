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

[Serializable]
public struct DayResult
{
    public int day;
    public int yesCount;
    public int noCount;
    public int timeoutCount;
    public int livesLeft;
}
