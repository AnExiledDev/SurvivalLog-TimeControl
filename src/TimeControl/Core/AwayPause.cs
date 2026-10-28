using System;

namespace TimeControl.Core;

/// <summary>Why the player is away from the running game.</summary>
public enum AwayReason
{
    /// <summary>The game's window lost focus, as after Alt+Tab.</summary>
    Background,

    /// <summary>One of the game's menus is open, such as the bag, the workbench, the phone, trade, a stove or a shop.</summary>
    Menu,
}

/// <summary>Which ways of being away pause the game; swapped whole when one changes.</summary>
/// <param name="IsOtherMenusOn">Every other menu the player opens to use something: stoves, machines, shops, the codex, the map and the like.</param>
public readonly record struct AwaySettings(bool IsFocusLossOn, bool IsBagOn, bool IsWorkbenchOn, bool IsPhoneOn, bool IsTradeOn, bool IsOtherMenusOn)
{
    public bool IsAnyOn => IsFocusLossOn || IsAnyMenuOn;

    public bool IsAnyMenuOn => IsBagOn || IsWorkbenchOn || IsPhoneOn || IsTradeOn || IsOtherMenusOn;

    /// <summary>The window being in the background wins over a menu, as a menu cannot be used from there.</summary>
    /// <param name="isWatchedMenuOpen">A menu whose setting is on is open.</param>
    public AwayReason? Reason(bool isInBackground, bool isWatchedMenuOpen)
    {
        if (IsFocusLossOn && isInBackground) return AwayReason.Background;

        return isWatchedMenuOpen ? AwayReason.Menu : null;
    }
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="Away">Why the player is away now, or null when they are back.</param>
/// <param name="CanPause">The game is where the mod's pauses apply.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="Running">The speed the game runs at, or last ran at when paused.</param>
/// <param name="WasSpeedChangedByHand">The player or the game's own buttons changed the speed since the last check.</param>
/// <param name="IsBusy">The player's character has an order queued or a workbench is crafting, read only while a menu is open.</param>
/// <param name="HasNewOrder">An order was given or a craft started since the last check, read only while a menu is open.</param>
/// <param name="Elapsed">Real seconds since the last check.</param>
/// <param name="HasNewJob">
/// A machine started a job since the last check, such as a stove cooking or a barrel brewing, read only while a menu is
/// open. Such a job runs on the machine's own timer for game hours, so it is not waited out like an order.
/// </param>
public readonly record struct AwaySnapshot(AwayReason? Away, bool CanPause, bool IsPaused, GameSpeed Running, bool WasSpeedChangedByHand, bool IsBusy = false, bool HasNewOrder = false, float Elapsed = 0f, bool HasNewJob = false);

/// <param name="IsPausing">Pause the game now.</param>
/// <param name="ResumeAt">Resume the game at this speed now, if it is still paused.</param>
public readonly record struct AwayStep(AwayPause State, bool IsPausing, GameSpeed? ResumeAt);

/// <summary>
/// Pause while away: the game pauses when the player leaves it running, for another window or a menu, and resumes at
/// the speed it ran at when they come back. Being away pauses at most once: one that starts while the game is already
/// paused, or where the mod does not pause, is let go until the player is back. A speed change by hand, or anyone
/// resuming, makes the speed the player's again, and nothing is resumed on their return. An order given from a menu,
/// such as a craft at the workbench, runs the game until the character has no orders left and no craft is running, then
/// the menu pauses again once that has held for <see cref="SettleSeconds"/>, so the next craft of a batch or a queue
/// starting right after one ends keeps the game running. A machine job started from a menu, such as a stove cooking,
/// runs the game until the menu closes, as the job itself runs for game hours.
/// </summary>
public abstract record AwayPause
{
    /// <summary>Real seconds with nothing to do before the menu pauses again after an order.</summary>
    public const float SettleSeconds = 1f;

    public static readonly AwayPause Start = new Watching();

    private AwayPause()
    {
    }

    /// <summary>The player is at the game.</summary>
    public sealed record Watching : AwayPause;

    /// <summary>The game is paused while the player is away for <paramref name="Reason"/>, and resumes at <paramref name="ResumeAt"/>.</summary>
    public sealed record Holding(AwayReason Reason, GameSpeed ResumeAt) : AwayPause;

    /// <summary>
    /// An order given from a menu runs the game; the menu pauses again once there has been nothing to do for
    /// <see cref="SettleSeconds"/>. <paramref name="IdleFor"/> is how long there has been nothing to do so far.
    /// </summary>
    public sealed record RunningOrder(float IdleFor = 0f) : AwayPause;

    /// <summary>The player is away, but the speed is theirs or a machine job started in the menu runs; nothing happens until they are back.</summary>
    public sealed record Released : AwayPause;

    public static AwayStep Next(AwayPause state, AwaySnapshot seen) => state switch
    {
        Watching => Watch(seen),
        Holding holding => Hold(holding, seen),
        RunningOrder running => RunOrder(running, seen),
        Released => seen.Away == null ? Stay(Start) : Stay(state),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "an away pause state Next does not handle"),
    };

    /// <summary>
    /// The badge for the pause while it holds the game: the alert badge setting covers it, as it is a pause the player
    /// did not make by hand. Null when this pause holds nothing, so the other badges decide.
    /// </summary>
    public static string? Badge(AwayPause state, BadgeSettings badges, bool isPaused, bool isChinese)
    {
        if (state is not Holding holding || !isPaused) return null;

        return badges.IsAlertBadgeOn ? BadgeText(holding.Reason).Pick(isChinese) : "";
    }

    public static Phrase BadgeText(AwayReason reason) => reason switch
    {
        AwayReason.Background => Texts.PausedInBackground,
        AwayReason.Menu => Texts.PausedMenuOpen,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "an away reason with no badge text"),
    };

    private static AwayStep Watch(AwaySnapshot seen)
    {
        if (seen.Away is not { } reason) return Stay(Start);

        if (seen.IsPaused || !seen.CanPause) return Stay(new Released());

        return new AwayStep(new Holding(reason, seen.Running), IsPausing: true, ResumeAt: null);
    }

    /// <summary>
    /// Going from the background into a menu, or back, keeps the game held until the player is fully back. An order
    /// given from a menu resumes the game for it, even when an older order was already queued.
    /// </summary>
    private static AwayStep Hold(Holding holding, AwaySnapshot seen)
    {
        if (seen.WasSpeedChangedByHand || !seen.IsPaused) return Stay(seen.Away == null ? Start : new Released());

        if (seen.Away is not { } reason) return new AwayStep(Start, IsPausing: false, holding.ResumeAt);

        if (reason == AwayReason.Menu && seen.HasNewJob) return new AwayStep(new Released(), IsPausing: false, holding.ResumeAt);

        if (reason == AwayReason.Menu && seen.HasNewOrder) return new AwayStep(new RunningOrder(), IsPausing: false, holding.ResumeAt);

        return Stay(holding with { Reason = reason });
    }

    /// <summary>
    /// Leaving the menu ends it with nothing to resume, as the game is running. A speed change by hand, or a pause by
    /// anyone else, makes the speed the player's. Going to the background pauses as it would from the game.
    /// </summary>
    private static AwayStep RunOrder(RunningOrder running, AwaySnapshot seen)
    {
        if (seen.Away is not { } reason) return Stay(Start);

        if (seen.WasSpeedChangedByHand || seen.IsPaused || !seen.CanPause) return Stay(new Released());

        if (reason == AwayReason.Background) return new AwayStep(new Holding(reason, seen.Running), IsPausing: true, ResumeAt: null);

        if (seen.HasNewJob) return Stay(new Released());

        if (seen.IsBusy || seen.HasNewOrder) return Stay(new RunningOrder());

        var idleFor = running.IdleFor + seen.Elapsed;

        if (idleFor < SettleSeconds) return Stay(new RunningOrder(idleFor));

        return new AwayStep(new Holding(reason, seen.Running), IsPausing: true, ResumeAt: null);
    }

    private static AwayStep Stay(AwayPause state) => new(state, IsPausing: false, ResumeAt: null);
}
