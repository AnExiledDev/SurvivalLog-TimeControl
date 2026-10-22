using System;

namespace TimeControl.Core;

/// <summary>What happens when the player's order queue runs out.</summary>
public enum QueueEmptyAction
{
    Off,
    Slow,
    Pause,
}

/// <summary>The speed the queue slow-down drops the game to, named as the game's own speed buttons are.</summary>
public enum QueueSlowTarget
{
    Slow,
    Normal,
    Fast,
}

public static class QueueEmptyActions
{
    /// <summary>
    /// The on/off setting this one replaced paused the game when it ran out. A player who had it on keeps the pause, as
    /// that is what they chose; a value already picked for the new setting stays.
    /// </summary>
    /// <param name="retiredValue">The old setting's text as saved, or null when the file has none.</param>
    public static QueueEmptyAction FromRetired(string? retiredValue, QueueEmptyAction current) =>
        current == QueueEmptyAction.Off && bool.TryParse(retiredValue?.Trim(), out var wasOn) && wasOn ? QueueEmptyAction.Pause : current;
}

/// <summary>Whether the queue slow-down is on, and the speed it drops to; swapped whole when one changes.</summary>
public readonly record struct QueueSlowSettings(bool IsOn, QueueSlowTarget Target)
{
    public GameSpeed TargetSpeed => Target switch
    {
        QueueSlowTarget.Slow => new GameSpeed(PauseToggle.Slow, FastGear.GamesOwn),
        QueueSlowTarget.Fast => new GameSpeed(PauseToggle.Double, FastGear.GamesOwn),
        _ => new GameSpeed(PauseToggle.Normal, FastGear.GamesOwn),
    };
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="CanSlow">The game is where the mod's pauses apply, and not in a fast-forward such as sleep.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="IsBusy">The player's character has an order queued.</param>
/// <param name="Running">The speed the game runs at, or last ran at when paused.</param>
/// <param name="ElapsedSeconds">Real seconds since the last check.</param>
/// <param name="WasSpeedChangedByHand">The player or the game's own buttons changed the speed since the last check.</param>
public readonly record struct QueueSlowSnapshot(bool CanSlow, bool IsPaused, bool IsBusy, GameSpeed Running, float ElapsedSeconds, bool WasSpeedChangedByHand);

/// <param name="SlowTo">Drop the game to this speed now.</param>
/// <param name="RestoreTo">Put this speed back: at once while running, or on the next resume while paused.</param>
public readonly record struct QueueSlowStep(QueueSlow State, GameSpeed? SlowTo, GameSpeed? RestoreTo);

/// <summary>
/// Slow down on an empty queue: when the player's order queue runs out while the game runs faster than the chosen
/// speed, it drops to that speed, and the player's next order puts the old speed back. A speed change by hand in
/// between makes the speed the player's, and nothing is put back.
/// </summary>
public abstract record QueueSlow
{
    public static readonly QueueSlow Start = new Watching(null);

    private QueueSlow()
    {
    }

    /// <param name="QueueEmptySeconds">How long the queue has read empty since it last read busy, 0 while busy; null when no run-out is pending.</param>
    public sealed record Watching(float? QueueEmptySeconds) : QueueSlow;

    /// <param name="Before">The speed from before the slow-down, put back on the next order.</param>
    public sealed record Slowed(GameSpeed Before) : QueueSlow;

    public static QueueSlowStep Next(QueueSlow state, QueueSlowSnapshot seen, GameSpeed target) => state switch
    {
        Watching watching => Watch(watching, seen, target),
        Slowed slowed => HoldSlow(slowed, seen),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a queue slow state Next does not handle"),
    };

    /// <summary>Whether one speed is faster than another: slow, then normal, fast, fastest, and the fast gears above it.</summary>
    public static bool IsFaster(GameSpeed speed, GameSpeed than) => Rank(speed) > Rank(than);

    /// <summary>A run-out while paused, or where the mod does not slow the game, is spent.</summary>
    private static QueueSlowStep Watch(Watching watching, QueueSlowSnapshot seen, GameSpeed target)
    {
        var queue = QueueRunOut.Track(watching.QueueEmptySeconds, seen.IsBusy, seen.ElapsedSeconds);
        var isSlowing = queue.HasRunOut && seen.CanSlow && !seen.IsPaused && IsFaster(seen.Running, target);

        if (!isSlowing) return Stay(new Watching(queue.EmptySeconds));

        return new QueueSlowStep(new Slowed(seen.Running), target, RestoreTo: null);
    }

    /// <summary>The next order puts the old speed back, whether the game runs or a pause holds it.</summary>
    private static QueueSlowStep HoldSlow(Slowed slowed, QueueSlowSnapshot seen)
    {
        if (seen.WasSpeedChangedByHand || !seen.CanSlow) return Stay(new Watching(seen.IsBusy ? 0f : null));

        if (!seen.IsBusy) return Stay(slowed);

        return new QueueSlowStep(new Watching(0f), SlowTo: null, slowed.Before);
    }

    private static int Rank(GameSpeed speed) => speed.Level switch
    {
        PauseToggle.Slow => 0,
        PauseToggle.Double => 2,
        PauseToggle.Triple => 3 + Math.Max(speed.Gear, FastGear.GamesOwn),
        _ => 1,
    };

    private static QueueSlowStep Stay(QueueSlow state) => new(state, SlowTo: null, RestoreTo: null);
}
