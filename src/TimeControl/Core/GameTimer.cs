namespace TimeControl.Core;

/// <summary>Which of the game's timers is running, as far as this mod is concerned.</summary>
public enum GameTimer
{
    /// <summary>A timer the mod leaves at the game's pace, such as an exploration trip's countdown, or none yet.</summary>
    Other,

    /// <summary>The day clock at home after the disaster.</summary>
    DayClock,

    /// <summary>The countdown to the disaster, while the player prepares.</summary>
    PrologueCountdown,
}

public static class GameTimers
{
    /// <summary>
    /// The game runs the home day clock as a count up and both the prologue and an exploration trip as countdowns, so
    /// only the world state tells the two countdowns apart.
    /// </summary>
    public static GameTimer Classify(bool isCountingUp, bool isCountingDown, bool isBeforeDisaster)
    {
        if (isCountingUp) return GameTimer.DayClock;

        return isCountingDown && isBeforeDisaster ? GameTimer.PrologueCountdown : GameTimer.Other;
    }
}
