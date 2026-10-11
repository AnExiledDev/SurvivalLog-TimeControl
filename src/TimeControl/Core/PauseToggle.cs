namespace TimeControl.Core;

/// <summary>The speed level one press of the pause key goes to: pause when running, back to the last speed when paused.</summary>
public static class PauseToggle
{
    public const int Paused = 0;
    public const int Normal = 1;
    public const int Slow = 4;

    public static int Target(bool isPaused, int lastRunningLevel)
    {
        if (!isPaused) return Paused;

        return lastRunningLevel is >= Normal and <= Slow ? lastRunningLevel : Normal;
    }
}
