using System;

namespace TimeControl.Core;

/// <summary>What a wait is for, which the menu names it by.</summary>
public enum WaitKind
{
    /// <summary>The next seven in the morning.</summary>
    Morning,

    /// <summary>The next time the clock shows the alarm's time.</summary>
    AlarmHour,

    /// <summary>A number of hours from when the wait started.</summary>
    Hours,

    /// <summary>The first brew in a brewing barrel finishing.</summary>
    Brew,

    /// <summary>The player's character finishing every order queued.</summary>
    Queue,
}

/// <summary>Where a wait ends.</summary>
public abstract record WaitGoal
{
    private WaitGoal()
    {
    }

    /// <summary>A moment on the running clock.</summary>
    public sealed record At(long Total, WaitKind Kind) : WaitGoal;

    /// <param name="EmptySeconds">How long the queue has read empty since it last read busy.</param>
    public sealed record QueueDone(float EmptySeconds) : WaitGoal;
}

/// <summary>What the ticker saw on one check.</summary>
/// <param name="Timer">The timer running, as the mod controls it.</param>
/// <param name="Reading">The running clock, or null when it could not be read.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="WasSpeedChangedByHand">The player, or the game itself, changed the speed since the last check.</param>
/// <param name="IsBusy">The player's character has an order queued.</param>
/// <param name="IsAsleep">The player's character is sleeping or napping.</param>
/// <param name="IsSleepWatched">The mod's sleep handling is watching the sleep, and puts back the speed from before it on waking.</param>
/// <param name="IsGameFastForwarding">The game's own sleep speed-up is still running; it ends a moment after the character wakes.</param>
/// <param name="ElapsedSeconds">Real seconds since the last check.</param>
public readonly record struct WaitSnapshot(
    GameTimer Timer,
    ClockReading? Reading,
    bool IsPaused,
    bool WasSpeedChangedByHand,
    bool IsBusy,
    bool IsAsleep,
    bool IsSleepWatched,
    bool IsGameFastForwarding,
    float ElapsedSeconds);

/// <summary>What the ticker has to do to the game after a step.</summary>
public abstract record WaitCommand
{
    public static readonly WaitCommand Nothing = new None();

    private WaitCommand()
    {
    }

    public sealed record None : WaitCommand;

    /// <summary>Run at this speed until the wait ends.</summary>
    public sealed record FastForward(GameSpeed Speed) : WaitCommand;

    /// <summary>The wait is over: pause the game, and make the next resume go back to <paramref name="Before"/>.</summary>
    public sealed record Arrive(GameSpeed Before) : WaitCommand;

    /// <summary>Someone else paused the game: leave the pause, and make the next resume go back to <paramref name="Before"/>.</summary>
    public sealed record ResumeTo(GameSpeed Before) : WaitCommand;

    /// <summary>Put back the speed from before the wait: at once while running, or on the next resume while paused.</summary>
    public sealed record Restore(GameSpeed Before) : WaitCommand;

    /// <summary>The character fell asleep: the mod's sleep handling takes over, and puts back <paramref name="Before"/> on waking.</summary>
    public sealed record HandToSleep(GameSpeed Before) : WaitCommand;
}

public readonly record struct WaitStep(WaitUntil State, WaitCommand Command);

/// <summary>
/// Wait until: the game runs at the fastest fast speed until a chosen moment, then pauses there. The speed from
/// before the wait is what play or the pause key resumes afterwards. A speed the player picks during the wait is
/// theirs and ends it, as does anything that pauses the game first, such as an alert.
/// </summary>
public abstract record WaitUntil
{
    public static readonly WaitUntil Start = new Idle();

    private WaitUntil()
    {
    }

    public sealed record Idle : WaitUntil;

    /// <param name="Goal">Where the wait ends.</param>
    /// <param name="Before">The speed the game ran at, or would resume to, when the wait started.</param>
    /// <param name="Timer">The timer the wait was started under; the clock it counts on.</param>
    public sealed record Waiting(WaitGoal Goal, GameSpeed Before, GameTimer Timer) : WaitUntil;

    /// <summary>
    /// The character fell asleep during a wait and the mod's sleep handling is off: the wait is over, and the speed
    /// from before it comes back once they are up and the game's own sleep speed-up is done.
    /// </summary>
    public sealed record AfterSleep(GameSpeed Before) : WaitUntil;

    /// <summary>Waits run on the clock of the day at home and on the countdown to the disaster, where the game shows the time of day.</summary>
    public static bool CanWait(GameTimer timer) => timer is GameTimer.DayClock or GameTimer.PrologueCountdown;

    /// <summary>
    /// Starts a wait, or moves one already running to a new goal; a wait that replaces another keeps the speed from
    /// before the first, so the fast speed of the wait is never the one put back. A speed still to be put back after a
    /// sleep is the one from before that wait.
    /// </summary>
    /// <param name="running">The speed the game runs at now, or would resume to while paused.</param>
    /// <param name="fastestGear">The fast gear the wait runs at.</param>
    public static WaitStep Begin(WaitUntil state, WaitGoal goal, GameSpeed running, GameTimer timer, int fastestGear)
    {
        var before = state switch
        {
            Waiting waiting => waiting.Before,
            AfterSleep afterSleep => afterSleep.Before,
            _ => running,
        };
        var fast = new GameSpeed(PauseToggle.Triple, Math.Max(fastestGear, FastGear.GamesOwn));

        return new WaitStep(new Waiting(goal, before, timer), new WaitCommand.FastForward(fast));
    }

    /// <summary>The player cancelled the wait: the speed from before it comes back.</summary>
    public static WaitStep Cancel(WaitUntil state) => state is Waiting waiting
        ? new WaitStep(Start, new WaitCommand.Restore(waiting.Before))
        : Stay(state);

    /// <summary>A wait is running, which the menu shows and idle freeze leaves alone.</summary>
    public static bool IsWaiting(WaitUntil state) => state is Waiting;

    /// <summary>
    /// A wait for a brew follows the first brew to finish as it is read again: another mod can move a brew along, and a
    /// brew finished early, or none left brewing, ends the wait on the next check.
    /// </summary>
    /// <param name="brewDoneAt">When the first brew in a barrel is done, on the running clock; null when none is brewing.</param>
    public static WaitUntil FollowBrew(WaitUntil state, long? brewDoneAt, ClockReading reading)
    {
        if (state is not Waiting { Goal: WaitGoal.At { Kind: WaitKind.Brew } at } waiting) return state;

        var doneAt = brewDoneAt ?? reading.Total;

        return doneAt < at.Total ? waiting with { Goal = at with { Total = doneAt } } : state;
    }

    /// <summary>
    /// Sleep is checked first: the game changes the speed when the character lies down, which would otherwise read as
    /// the player's own change and leave the wait's fast speed behind after the sleep.
    /// </summary>
    public static WaitStep Next(WaitUntil state, WaitSnapshot seen) => state switch
    {
        Idle => Stay(state),
        Waiting waiting => Wait(waiting, seen),
        AfterSleep afterSleep => WakeUp(afterSleep, seen),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a wait state Next does not handle"),
    };

    private static WaitStep Wait(Waiting waiting, WaitSnapshot seen)
    {
        if (seen.IsAsleep) return seen.IsSleepWatched ? new WaitStep(Start, new WaitCommand.HandToSleep(waiting.Before)) : Stay(new AfterSleep(waiting.Before));

        if (seen.WasSpeedChangedByHand) return Stay(Start);

        if (!CanWait(seen.Timer) || seen.Timer != waiting.Timer || seen.Reading is not { } reading) return new WaitStep(Start, new WaitCommand.Restore(waiting.Before));

        if (seen.IsPaused) return new WaitStep(Start, new WaitCommand.ResumeTo(waiting.Before));

        return waiting.Goal switch
        {
            WaitGoal.At at when reading.Total >= at.Total => new WaitStep(Start, new WaitCommand.Arrive(waiting.Before)),
            WaitGoal.At => Stay(waiting),
            WaitGoal.QueueDone queue => WaitForQueue(waiting, queue, seen),
            _ => throw new ArgumentOutOfRangeException(nameof(seen), waiting.Goal, "a wait goal Next does not handle"),
        };
    }

    /// <summary>
    /// The game changes the speed itself as the character lies down and gets up, so a change seen while asleep is not
    /// the player's. One made once they are up is, and keeps their speed.
    /// </summary>
    private static WaitStep WakeUp(AfterSleep afterSleep, WaitSnapshot seen)
    {
        if (seen.IsAsleep || seen.IsGameFastForwarding) return Stay(afterSleep);

        if (seen.WasSpeedChangedByHand) return Stay(Start);

        return new WaitStep(Start, new WaitCommand.Restore(afterSleep.Before));
    }

    /// <summary>
    /// The game reads the queue as empty for a moment between two queued orders, so it is done only once it has stayed
    /// empty for <see cref="AlertPause.QueueSettleSeconds"/>.
    /// </summary>
    private static WaitStep WaitForQueue(Waiting waiting, WaitGoal.QueueDone queue, WaitSnapshot seen)
    {
        if (seen.IsBusy) return Stay(waiting with { Goal = new WaitGoal.QueueDone(0f) });

        var emptySeconds = queue.EmptySeconds + seen.ElapsedSeconds;

        if (emptySeconds >= AlertPause.QueueSettleSeconds) return new WaitStep(Start, new WaitCommand.Arrive(waiting.Before));

        return Stay(waiting with { Goal = new WaitGoal.QueueDone(emptySeconds) });
    }

    private static WaitStep Stay(WaitUntil state) => new(state, WaitCommand.Nothing);
}
