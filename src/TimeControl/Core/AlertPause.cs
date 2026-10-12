using System;

namespace TimeControl.Core;

/// <summary>Something that happened in the game which can pause it, in order of how much it needs the player.</summary>
public enum Alert
{
    Wave,
    Emergency,
    DroneArrival,
    QueueEmpty,
}

/// <summary>Which alerts pause the game; fixed for the session.</summary>
public readonly record struct AlertSettings(bool IsWaveOn, bool IsEmergencyOn, bool IsDroneArrivalOn, bool IsQueueEmptyOn)
{
    public bool IsAnyOn => IsWaveOn || IsEmergencyOn || IsDroneArrivalOn || IsQueueEmptyOn;
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="CanPause">The game is where the mod's pauses apply: under a timer the player waits out, and not in a fast-forward such as sleep.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="IsWaveActive">A zombie wave is under way.</param>
/// <param name="EmergenciesFired">Emergencies that started since the last check.</param>
/// <param name="DronesLanded">Trade drone trips that came home since the last check.</param>
/// <param name="IsBusy">The player's character has an order queued, the game's own test for "not idle".</param>
/// <param name="ElapsedSeconds">Real seconds since the last check.</param>
/// <param name="WasSpeedChangedByHand">The player or the game's own buttons changed the speed since the last check, so a pause seen now may be theirs.</param>
public readonly record struct AlertSnapshot(
    bool CanPause,
    bool IsPaused,
    bool IsWaveActive,
    int EmergenciesFired,
    int DronesLanded,
    bool IsBusy,
    float ElapsedSeconds,
    bool WasSpeedChangedByHand);

/// <param name="PauseFor">The alert to pause the game for now, or null to leave its speed alone.</param>
public readonly record struct AlertStep(AlertPause State, Alert? PauseFor);

/// <summary>
/// Pause on alerts: the game is paused once when a zombie wave starts, an emergency fires, a trade drone comes home or
/// the player's order queue runs out, each only if switched on. Every alert is one pause at the moment it starts and
/// never again: a player who resumes keeps time running, and an alert that starts while the game is already paused, or
/// where the mod does not pause, is let go rather than saved for later.
/// </summary>
public abstract record AlertPause
{
    /// <summary>
    /// About a second at ten checks a second. Counted in checks rather than seconds so one long loading frame cannot
    /// use it up before the load has put the world back together.
    /// </summary>
    public const int SettleChecks = 10;

    /// <summary>Longer than the moment between two queued orders, when the game reads the queue as empty.</summary>
    public const float QueueSettleSeconds = 0.3f;

    public static readonly AlertPause Start = new Unseen(0);

    private AlertPause()
    {
    }

    /// <summary>
    /// A new world settling. A load puts its wave back and finishes drone trips that ended while the game was closed
    /// over its first moments, so what the first <see cref="SettleChecks"/> checks see is how things stand, not news.
    /// </summary>
    public sealed record Unseen(int ChecksSeen) : AlertPause;

    /// <summary>Waiting for the next alert, remembering what the last check saw so only a change counts as one.</summary>
    /// <param name="QueueEmptySeconds">How long the order queue has read empty since it last read busy, 0 while busy; null when no run-out is pending.</param>
    public sealed record Watching(bool WasWaveActive, float? QueueEmptySeconds) : AlertPause;

    /// <summary>The game is paused for <paramref name="Reason"/> until anyone resumes it or pauses it again.</summary>
    public sealed record Holding(Alert Reason, bool WasWaveActive, float? QueueEmptySeconds) : AlertPause;

    /// <summary>
    /// The mod's pauses apply under the timers the player waits out, never in a fast-forward such as sleep, and never
    /// while an expedition is being finished, as the trip home waits on game time.
    /// </summary>
    public static bool CanPause(GameTimer timer, bool isFastForwarding, bool isLeavingExpedition) =>
        GameTimers.IsWaitedOut(timer) && !isFastForwarding && !isLeavingExpedition;

    /// <summary>
    /// A speed change by hand while held means the pause standing now was made after this one, by the player or the
    /// game, so it no longer holds the game for its alert.
    /// </summary>
    public static AlertStep Next(AlertPause state, AlertSnapshot seen, AlertSettings settings) => state switch
    {
        Unseen unseen => Settle(unseen, seen),
        Holding holding when seen.IsPaused && !seen.WasSpeedChangedByHand => Stay(Hold(holding, seen)),
        Holding holding => Watch(holding.WasWaveActive, holding.QueueEmptySeconds, seen, settings),
        Watching watching => Watch(watching.WasWaveActive, watching.QueueEmptySeconds, seen, settings),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "an alert pause state Next does not handle"),
    };

    /// <summary>Everything counted while the world settles is let go, and the last settling check is the baseline.</summary>
    private static AlertStep Settle(Unseen unseen, AlertSnapshot seen)
    {
        var checksSeen = unseen.ChecksSeen + 1;

        return Stay(checksSeen < SettleChecks ? new Unseen(checksSeen) : Remember(seen));
    }

    /// <summary>Alerts that start while held are spent, since the game is already paused.</summary>
    private static Holding Hold(Holding holding, AlertSnapshot seen) =>
        holding with { WasWaveActive = seen.IsWaveActive, QueueEmptySeconds = TrackQueue(holding.QueueEmptySeconds, seen).EmptySeconds };

    /// <summary>
    /// The first alert that started since the last check, by the order of <see cref="Alert"/>. A wave counts when it
    /// starts, not for as long as it lasts, and the queue when it runs out, not while it stays empty.
    /// </summary>
    private static Alert? Raised(bool wasWaveActive, bool hasQueueRunOut, AlertSnapshot seen, AlertSettings settings)
    {
        if (settings.IsWaveOn && seen.IsWaveActive && !wasWaveActive) return Alert.Wave;

        if (settings.IsEmergencyOn && seen.EmergenciesFired > 0) return Alert.Emergency;

        if (settings.IsDroneArrivalOn && seen.DronesLanded > 0) return Alert.DroneArrival;

        return settings.IsQueueEmptyOn && hasQueueRunOut ? Alert.QueueEmpty : null;
    }

    /// <summary>A pause someone else holds is theirs, and an alert where the mod does not pause is let go; either way it is spent.</summary>
    private static AlertStep Watch(bool wasWaveActive, float? queueEmptySeconds, AlertSnapshot seen, AlertSettings settings)
    {
        var queue = TrackQueue(queueEmptySeconds, seen);
        var raised = Raised(wasWaveActive, queue.HasRunOut, seen, settings);

        if (raised is not { } alert || seen.IsPaused || !seen.CanPause) return Stay(new Watching(seen.IsWaveActive, queue.EmptySeconds));

        return new AlertStep(new Holding(alert, seen.IsWaveActive, queue.EmptySeconds), alert);
    }

    /// <summary>
    /// The game reads the queue as empty for a moment between two queued orders, so it has run out only once it has
    /// stayed empty for <see cref="QueueSettleSeconds"/>; an order in that time means it never ran out. Each run-out
    /// counts once.
    /// </summary>
    private static (float? EmptySeconds, bool HasRunOut) TrackQueue(float? emptySeconds, AlertSnapshot seen)
    {
        if (seen.IsBusy) return (0f, false);

        if (emptySeconds is not { } before) return (null, false);

        var after = before + seen.ElapsedSeconds;

        return after >= QueueSettleSeconds ? (null, true) : (after, false);
    }

    private static Watching Remember(AlertSnapshot seen) => new(seen.IsWaveActive, seen.IsBusy ? 0f : null);

    private static AlertStep Stay(AlertPause state) => new(state, null);
}
