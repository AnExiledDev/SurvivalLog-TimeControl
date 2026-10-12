namespace TimeControl.Core;

/// <summary>The speed level one press of the pause key goes to: pause when running, back to the last speed when paused.</summary>
public static class PauseToggle
{
    public const int Paused = 0;
    public const int Normal = 1;
    public const int Double = 2;
    public const int Triple = 3;
    public const int Slow = 4;

    public static int Target(bool isPaused, int lastRunningLevel) => isPaused ? RunningLevel(lastRunningLevel) : Paused;

    /// <summary>The level to resume at; anything that is not a running speed resumes at normal speed.</summary>
    public static int RunningLevel(int level) => level is >= Normal and <= Slow ? level : Normal;
}
