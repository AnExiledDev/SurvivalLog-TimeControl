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
public readonly record struct AlertSnapshot(bool CanPause, bool IsPaused, bool IsWaveActive, int EmergenciesFired, int DronesLanded, bool IsBusy);

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
    public static readonly AlertPause Start = new Unseen();

    private AlertPause()
    {
    }

    /// <summary>A new world: its first check is taken as how things stand, so a save loaded mid-wave does not pause.</summary>
    public sealed record Unseen : AlertPause;

    /// <summary>Waiting for the next alert, remembering what the last check saw so only a change counts as one.</summary>
    public sealed record Watching(bool WasWaveActive, bool WasBusy) : AlertPause;

    /// <summary>The game is paused for <paramref name="Reason"/> until anyone resumes it.</summary>
    public sealed record Holding(Alert Reason, bool WasWaveActive, bool WasBusy) : AlertPause;

    /// <summary>The mod's pauses apply under the timers the player waits out, and never in a fast-forward such as sleep.</summary>
    public static bool CanPause(GameTimer timer, bool isFastForwarding) => GameTimers.IsWaitedOut(timer) && !isFastForwarding;

    public static AlertStep Next(AlertPause state, AlertSnapshot seen, AlertSettings settings) => state switch
    {
        Unseen => Stay(Remember(seen)),
        Holding holding when seen.IsPaused => Stay(holding with { WasWaveActive = seen.IsWaveActive, WasBusy = seen.IsBusy }),
        Holding holding => Watch(holding.WasWaveActive, holding.WasBusy, seen, settings),
        Watching watching => Watch(watching.WasWaveActive, watching.WasBusy, seen, settings),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "an alert pause state Next does not handle"),
    };

    /// <summary>
    /// The first alert that started since the last check, by the order of <see cref="Alert"/>. A wave counts when it
    /// starts, not for as long as it lasts, and the queue when it runs out, not while it stays empty.
    /// </summary>
    private static Alert? Raised(bool wasWaveActive, bool wasBusy, AlertSnapshot seen, AlertSettings settings)
    {
        if (settings.IsWaveOn && seen.IsWaveActive && !wasWaveActive) return Alert.Wave;

        if (settings.IsEmergencyOn && seen.EmergenciesFired > 0) return Alert.Emergency;

        if (settings.IsDroneArrivalOn && seen.DronesLanded > 0) return Alert.DroneArrival;

        return settings.IsQueueEmptyOn && wasBusy && !seen.IsBusy ? Alert.QueueEmpty : null;
    }

    /// <summary>A pause someone else holds is theirs, and an alert where the mod does not pause is let go; either way it is spent.</summary>
    private static AlertStep Watch(bool wasWaveActive, bool wasBusy, AlertSnapshot seen, AlertSettings settings)
    {
        var raised = Raised(wasWaveActive, wasBusy, seen, settings);

        if (raised is not { } alert || seen.IsPaused || !seen.CanPause) return Stay(Remember(seen));

        return new AlertStep(new Holding(alert, seen.IsWaveActive, seen.IsBusy), alert);
    }

    private static Watching Remember(AlertSnapshot seen) => new(seen.IsWaveActive, seen.IsBusy);

    private static AlertStep Stay(AlertPause state) => new(state, null);
}
