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
    /// The countdown to the disaster is over, by the player completing preparations or by time running out. The game
    /// then moves into the disaster on game time, so the mod's pauses no longer apply.
    /// </summary>
    CountdownOver,

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
    /// expedition a frozen timer: the day stands still while away and the exposure meter does the counting. The game
    /// seals the countdown as preparations end, and the sealed timer stays until the next world makes a new one.
    /// </summary>
    public static GameTimer Classify(bool isCountingUp, bool isCountingDown, bool isFrozen, bool isBeforeDisaster, bool isCountdownOver = false)
    {
        if (isCountingUp) return GameTimer.DayClock;

        if (isFrozen) return GameTimer.Expedition;

        if (!isCountingDown || !isBeforeDisaster) return GameTimer.Other;

        return isCountdownOver ? GameTimer.CountdownOver : GameTimer.PrologueCountdown;
    }

    /// <summary>The countdown to the disaster, running or over; the HUD, the keys and the buttons stay as they were in it.</summary>
    public static bool IsCountdown(GameTimer timer) => timer is GameTimer.PrologueCountdown or GameTimer.CountdownOver;

    /// <summary>
    /// The timers the player waits out by standing around: the day clock at home, the countdown to the disaster and an
    /// expedition's exposure meter. The mod's own pauses only ever apply under these; any other timer keeps the game's pace.
    /// A countdown that is over is not one: the game waits on game time from then until the disaster has started.
    /// </summary>
    public static bool IsWaitedOut(GameTimer timer) =>
        timer is GameTimer.DayClock or GameTimer.PrologueCountdown or GameTimer.Expedition;

    /// <summary>With Time Control switched off on expeditions, an expedition is left at the game's pace like any other timer.</summary>
    public static GameTimer Controlled(GameTimer timer, bool isExpeditionControlOn) =>
        timer == GameTimer.Expedition && !isExpeditionControlOn ? GameTimer.Other : timer;
}
