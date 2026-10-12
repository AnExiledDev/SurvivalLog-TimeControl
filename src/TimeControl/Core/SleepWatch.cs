using System;

namespace TimeControl.Core;

/// <summary>A running speed: the game's level, and on the high speed level which of the fast gears was in use.</summary>
public readonly record struct GameSpeed(int Level, int Gear)
{
    /// <summary>Anything that is not a running level is normal speed, and only the high speed level carries a gear.</summary>
    public static GameSpeed Of(int level, int gear)
    {
        var running = PauseToggle.RunningLevel(level);

        return new GameSpeed(running, running == PauseToggle.Triple ? Math.Max(gear, FastGear.GamesOwn) : FastGear.GamesOwn);
    }
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsAsleep">The player's character is sleeping or napping.</param>
/// <param name="IsGameFastForwarding">The game's own sleep speed-up is still running; it ends a moment after the character wakes.</param>
/// <param name="IsBusy">The player's character has an order queued.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="Running">The speed the game runs at, or last ran at when paused.</param>
/// <param name="WasSpeedChangedByHand">The player, or the game's own speed buttons, changed the speed since the last check.</param>
public readonly record struct SleepSnapshot(bool IsAsleep, bool IsGameFastForwarding, bool IsBusy, bool IsPaused, GameSpeed Running, bool WasSpeedChangedByHand);

/// <param name="IsFastForwardOn">Sleep runs at the fastest fast gear, in place of the game's own sleep speed-up.</param>
/// <param name="IsPauseOnWakeOn">The game pauses when the character wakes.</param>
/// <param name="FastestGear">The fast gear sleep runs at; the game's own high speed when there are no gears.</param>
public readonly record struct SleepSettings(bool IsFastForwardOn, bool IsPauseOnWakeOn, int FastestGear);

/// <summary>What the ticker has to do to the game after a step.</summary>
public abstract record SleepCommand
{
    public static readonly SleepCommand Nothing = new None();
    public static readonly SleepCommand LetTimeRun = new LetRun();

    private SleepCommand()
    {
    }

    public sealed record None : SleepCommand;

    /// <summary>Run at the high speed level in this fast gear.</summary>
    public sealed record FastForward(int Gear) : SleepCommand;

    /// <summary>Pause the game, and make the next resume go back to <paramref name="ResumeAt"/>.</summary>
    public sealed record PauseOnWake(GameSpeed ResumeAt) : SleepCommand;

    /// <summary>An order was given while paused on waking: resume at <paramref name="At"/> if still paused.</summary>
    public sealed record Resume(GameSpeed At) : SleepCommand;

    /// <summary>Put back the speed from before the sleep: at once while running, or on the next resume while paused.</summary>
    public sealed record Restore(GameSpeed To) : SleepCommand;

    /// <summary>The player pressed play on waking, so time runs while they idle until their next order.</summary>
    public sealed record LetRun : SleepCommand;
}

public readonly record struct SleepStep(SleepWatch State, SleepCommand Command);

/// <summary>
/// Sleep and naps run at the fastest fast gear, and the game pauses when the character wakes, for any reason. The
/// speed from before the sleep is what the next order, play or the pause key resumes, never the sleep's own speed. A
/// speed the player picks during the sleep is theirs until they wake.
/// </summary>
public abstract record SleepWatch
{
    public static readonly SleepWatch Start = new Awake();

    private SleepWatch()
    {
    }

    public sealed record Awake : SleepWatch;

    /// <param name="Before">The speed the game ran at when the sleep started.</param>
    /// <param name="IsHeld">This mod set the sleep's speed and the player has not changed it since.</param>
    public sealed record Asleep(GameSpeed Before, bool IsHeld) : SleepWatch;

    /// <param name="ResumeAt">The speed from before the sleep.</param>
    /// <param name="WasBusy">The character had an order queued on the last check, so only a new one resumes.</param>
    public sealed record WakePaused(GameSpeed ResumeAt, bool WasBusy) : SleepWatch;

    public static SleepStep Next(SleepWatch state, SleepSnapshot seen, SleepSettings settings) => state switch
    {
        Awake => Watch(seen, settings),
        Asleep asleep => Sleep(asleep, seen, settings),
        WakePaused paused => HoldOnWaking(paused, seen),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a sleep state Next does not handle"),
    };

    /// <summary>
    /// A sleep that starts while paused is left until time runs, so the speed remembered is one the game really ran
    /// at rather than the pause.
    /// </summary>
    private static SleepStep Watch(SleepSnapshot seen, SleepSettings settings)
    {
        if (!seen.IsAsleep || seen.IsPaused) return Stay(Start);

        if (!settings.IsFastForwardOn && !settings.IsPauseOnWakeOn) return Stay(Start);

        var asleep = new Asleep(seen.Running, IsHeld: settings.IsFastForwardOn);

        return settings.IsFastForwardOn
            ? new SleepStep(asleep, new SleepCommand.FastForward(Math.Max(settings.FastestGear, FastGear.GamesOwn)))
            : Stay(asleep);
    }

    /// <summary>
    /// The game's own speed-up, when it runs, ends a moment after the character wakes and puts its speed back as it
    /// goes, so waking counts only once that is done; pausing earlier would have the game undo the pause.
    /// </summary>
    private static SleepStep Sleep(Asleep asleep, SleepSnapshot seen, SleepSettings settings)
    {
        if (seen.IsAsleep || seen.IsGameFastForwarding)
        {
            return asleep.IsHeld && seen.WasSpeedChangedByHand ? Stay(asleep with { IsHeld = false }) : Stay(asleep);
        }

        if (settings.IsPauseOnWakeOn) return new SleepStep(new WakePaused(asleep.Before, seen.IsBusy), new SleepCommand.PauseOnWake(asleep.Before));

        return new SleepStep(Start, asleep.IsHeld ? new SleepCommand.Restore(asleep.Before) : SleepCommand.Nothing);
    }

    /// <summary>
    /// Time running again means the player pressed play or the pause key, which resume the speed from before the
    /// sleep themselves. Otherwise a new order resumes it; one already queued when the character woke does not, or
    /// a queued order would skip the pause altogether.
    /// </summary>
    private static SleepStep HoldOnWaking(WakePaused paused, SleepSnapshot seen)
    {
        if (!seen.IsPaused) return new SleepStep(Start, SleepCommand.LetTimeRun);

        if (seen.IsBusy && !paused.WasBusy) return new SleepStep(Start, new SleepCommand.Resume(paused.ResumeAt));

        return Stay(paused with { WasBusy = seen.IsBusy });
    }

    private static SleepStep Stay(SleepWatch state) => new(state, SleepCommand.Nothing);
}
