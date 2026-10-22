using System;

namespace TimeControl.Core;

/// <summary>When a sleep is cut short; fixed until the settings change.</summary>
/// <param name="WakeAtSecond">The time of day the character wakes at, or null to leave waking to the game.</param>
/// <param name="IsWakeOnAlertOn">An alert that pauses the game wakes the character too.</param>
public readonly record struct SleepUntilSettings(int? WakeAtSecond, bool IsWakeOnAlertOn)
{
    public bool IsAnyOn => WakeAtSecond != null || IsWakeOnAlertOn;
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsAsleep">The player's character is sleeping or napping.</param>
/// <param name="IsWakeable">The character is in a real sleep, the kind the game speeds up; a nap is not.</param>
/// <param name="Reading">The running clock, or null when it could not be read.</param>
/// <param name="IsNewAlert">An alert paused the game on this check.</param>
/// <param name="IsGameFastForwarding">The game's own sleep speed-up is still running; it ends a moment after the character wakes.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
public readonly record struct SleepUntilSnapshot(
    bool IsAsleep,
    bool IsWakeable,
    ClockReading? Reading,
    bool IsNewAlert,
    bool IsGameFastForwarding,
    bool IsPaused);

/// <summary>What the ticker has to do to the game after a step.</summary>
public enum SleepUntilCommand
{
    None,

    /// <summary>Wake the character, the way the player cancelling the sleep does.</summary>
    Wake,

    /// <summary>The character woke for an alert and the game runs: pause it, as the alert would have.</summary>
    Pause,
}

public readonly record struct SleepUntilStep(SleepUntil State, SleepUntilCommand Command);

/// <summary>
/// Sleep until: a real sleep ends at a set time of day, or when an alert pauses the game. Waking goes the way the
/// player's own cancel does, so the game's wake, the sleep fast-forward and the pause on waking all follow as usual.
/// Naps are never cut short. The game decides when a sleep may end on its own, so a wake time can only end it early.
/// </summary>
public abstract record SleepUntil
{
    public static readonly SleepUntil Start = new Awake();

    private SleepUntil()
    {
    }

    public sealed record Awake : SleepUntil;

    /// <param name="WakeAt">When the character wakes, on the clock running when the sleep started; null to leave it to the game or an alert.</param>
    public sealed record Asleep(long? WakeAt) : SleepUntil;

    /// <summary>The character was woken and the ticker waits for the game to finish waking them.</summary>
    /// <param name="ByAlert">An alert woke them, so the game should be paused once they are up.</param>
    public sealed record Woken(bool ByAlert) : SleepUntil;

    public static SleepUntilStep Next(SleepUntil state, SleepUntilSnapshot seen, SleepUntilSettings settings) => state switch
    {
        Awake => Watch(seen, settings),
        Asleep asleep => Sleep(asleep, seen, settings),
        Woken woken => Rise(woken, seen),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a sleep until state Next does not handle"),
    };

    /// <summary>The wake time is the next time the clock shows it after the sleep starts.</summary>
    private static SleepUntilStep Watch(SleepUntilSnapshot seen, SleepUntilSettings settings)
    {
        if (!seen.IsAsleep || !seen.IsWakeable || !settings.IsAnyOn) return Stay(Start);

        long? wakeAt = settings.WakeAtSecond is { } second && seen.Reading is { } reading ? reading.Next(second) : null;

        return Stay(new Asleep(wakeAt));
    }

    private static SleepUntilStep Sleep(Asleep asleep, SleepUntilSnapshot seen, SleepUntilSettings settings)
    {
        if (!seen.IsAsleep) return Stay(Start);

        if (settings.IsWakeOnAlertOn && seen.IsNewAlert) return new SleepUntilStep(new Woken(ByAlert: true), SleepUntilCommand.Wake);

        if (asleep.WakeAt is { } wakeAt && seen.Reading is { } reading && reading.Total >= wakeAt) return new SleepUntilStep(new Woken(ByAlert: false), SleepUntilCommand.Wake);

        return Stay(asleep);
    }

    /// <summary>
    /// A wake is asked for once and never pressed again. Once the character is up and the game's own speed-up has put
    /// its speed back, which can undo a pause made during it, a wake for an alert pauses the game if it runs.
    /// </summary>
    private static SleepUntilStep Rise(Woken woken, SleepUntilSnapshot seen)
    {
        if (seen.IsAsleep || seen.IsGameFastForwarding) return Stay(woken);

        return new SleepUntilStep(Start, woken.ByAlert && !seen.IsPaused ? SleepUntilCommand.Pause : SleepUntilCommand.None);
    }

    private static SleepUntilStep Stay(SleepUntil state) => new(state, SleepUntilCommand.None);
}
