using System;

namespace TimeControl.Core;

/// <summary>What the ticker saw on one check.</summary>
/// <param name="CanFreeze">Idle freeze is on, the day clock at home, the countdown to the disaster or an expedition is running, and no fast-forward such as sleep is.</param>
/// <param name="IsBusy">The player's character has an order queued, the game's own test for "not idle".</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="ElapsedSeconds">Real seconds since the last check.</param>
/// <param name="RunningLevel">The speed level the game last ran at, which a resume goes back to.</param>
/// <param name="IsLeavingExpedition">The expedition is being finished: the game's trip home waits on game time, so a pause would hold it forever.</param>
public readonly record struct IdleSnapshot(bool CanFreeze, bool IsBusy, bool IsPaused, float ElapsedSeconds, int RunningLevel, bool IsLeavingExpedition);

/// <summary>What the ticker has to do to the game after a step.</summary>
public abstract record IdleCommand
{
    public static readonly IdleCommand Nothing = new None();
    public static readonly IdleCommand PauseGame = new Pause();

    private IdleCommand()
    {
    }

    public sealed record None : IdleCommand;

    public sealed record Pause : IdleCommand;

    /// <param name="Level">The game speed level to resume at, always a running one.</param>
    public sealed record Resume(int Level) : IdleCommand;
}

public readonly record struct IdleStep(IdlePause State, IdleCommand Command);

/// <summary>
/// Idle freeze: the game is paused for real (zombies, plants, trades, the drone and the clock all stop) while the
/// player stands idle at home, while preparing for the disaster or on an expedition, and resumes the moment they give an
/// order or finish the expedition. A pause someone else made is never undone by this, and a player who resumes time while
/// idle keeps it running until their next order.
/// </summary>
public abstract record IdlePause
{
    /// <summary>Long enough that standing still for a moment between orders does not pause the game.</summary>
    public const float DefaultDelaySeconds = 3f;

    public static readonly IdlePause Start = new Watching(0f);

    private static readonly IdlePause LetRun = new Released();

    private IdlePause()
    {
    }

    /// <summary>Time runs and idle freeze is counting how long the player has stood idle.</summary>
    public sealed record Watching(float IdleSeconds) : IdlePause;

    /// <summary>Idle freeze paused the game, and resumes it at <paramref name="ResumeLevel"/> on the next order.</summary>
    public sealed record Frozen(int ResumeLevel) : IdlePause;

    /// <summary>The player resumed time while idle freeze held it, so time runs while they idle until their next order.</summary>
    public sealed record Released : IdlePause;

    /// <summary>A delay that is not a number means nothing was meant, so the default is kept.</summary>
    public static float ClampDelay(float seconds) => float.IsFinite(seconds) ? Math.Max(0f, seconds) : DefaultDelaySeconds;

    /// <summary>
    /// Idle freeze applies to the timers the player waits out by standing around: the day clock at home, the
    /// countdown to the disaster and an expedition's exposure meter. Any other timer keeps the game's pace, and a
    /// fast-forward such as sleep is the game deliberately running time, so neither freezes. Neither does holding the
    /// game's hold-to-speed-up key: the player is asking for time to run. Nor does a story scene, which counts its
    /// length in game time, so a freeze would hold it on a black screen.
    /// </summary>
    public static bool CanFreeze(bool isIdleFreezeOn, GameTimer timer, bool isFastForwarding, bool isHoldingSpeedUp = false, bool isStoryPlaying = false) =>
        isIdleFreezeOn && GameTimers.IsWaitedOut(timer) && !isFastForwarding && !isHoldingSpeedUp && !isStoryPlaying;

    public static IdleStep Next(IdlePause state, IdleSnapshot seen, float delaySeconds) => state switch
    {
        Watching watching => Watch(watching, seen, delaySeconds),
        Frozen frozen => Hold(frozen, seen),
        Released => Stay(seen.IsBusy || !seen.CanFreeze ? Start : LetRun),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "an idle pause state Next does not handle"),
    };

    /// <summary>
    /// Idling only counts while time runs and freezing is allowed, and never while the expedition is being finished; a
    /// player's own pause is theirs, so it neither counts as idling nor gets resumed. A zero delay still needs a moment
    /// of idling, or every gap between two orders would pause the game.
    /// </summary>
    private static IdleStep Watch(Watching watching, IdleSnapshot seen, float delaySeconds)
    {
        if (!seen.CanFreeze || seen.IsBusy || seen.IsPaused || seen.IsLeavingExpedition) return Stay(Start);

        if (seen.ElapsedSeconds <= 0f) return Stay(watching);

        var idleSeconds = watching.IdleSeconds + seen.ElapsedSeconds;

        if (idleSeconds < delaySeconds) return Stay(new Watching(idleSeconds));

        return new IdleStep(new Frozen(PauseToggle.RunningLevel(seen.RunningLevel)), IdleCommand.PauseGame);
    }

    /// <summary>
    /// Time running again while frozen means the player pressed play, so it is left running. Otherwise an order,
    /// finishing the expedition, or the freeze no longer applying (leaving home, sleeping), resumes the speed the game had.
    /// </summary>
    private static IdleStep Hold(Frozen frozen, IdleSnapshot seen)
    {
        if (!seen.IsPaused) return Stay(LetRun);

        if (seen.IsBusy || seen.IsLeavingExpedition || !seen.CanFreeze) return new IdleStep(Start, new IdleCommand.Resume(frozen.ResumeLevel));

        return Stay(frozen);
    }

    private static IdleStep Stay(IdlePause state) => new(state, IdleCommand.Nothing);
}
