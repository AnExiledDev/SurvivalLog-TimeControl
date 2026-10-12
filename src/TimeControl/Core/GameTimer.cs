namespace TimeControl.Core;

/// <summary>Which of the game's timers is running, as far as this mod is concerned.</summary>
public enum GameTimer
{
    /// <summary>A timer the mod leaves at the game's pace, or none yet.</summary>
    Other,

    /// <summary>The day clock at home after the disaster.</summary>
    DayClock,

    /// <summary>The countdown to the disaster, while the player prepares.</summary>
    PrologueCountdown,

    /// <summary>
    /// An expedition: the day clock stands still and the exposure meter fills on game time instead, so pausing or
    /// speeding up the game pauses or speeds up the trip.
    /// </summary>
    Expedition,
}

public static class GameTimers
{
    /// <summary>
    /// The game runs the home day clock as a count up, the prologue as a countdown before the disaster, and gives an
    /// expedition a frozen timer: the day stands still while away and the exposure meter does the counting.
    /// </summary>
    public static GameTimer Classify(bool isCountingUp, bool isCountingDown, bool isFrozen, bool isBeforeDisaster)
    {
        if (isCountingUp) return GameTimer.DayClock;

        if (isFrozen) return GameTimer.Expedition;

        return isCountingDown && isBeforeDisaster ? GameTimer.PrologueCountdown : GameTimer.Other;
    }

    /// <summary>With Time Control switched off on expeditions, an expedition is left at the game's pace like any other timer.</summary>
    public static GameTimer Controlled(GameTimer timer, bool isExpeditionControlOn) =>
        timer == GameTimer.Expedition && !isExpeditionControlOn ? GameTimer.Other : timer;
}
