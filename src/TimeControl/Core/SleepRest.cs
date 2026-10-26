namespace TimeControl.Core;

/// <summary>
/// Sleep until ends a sleep the way the game ends a finished one, never as a cancel, so the character gets what a
/// finished sleep gives. The game grants Vim and Vigor at every finished sleep, but its own early wake only comes once
/// the character has slept its minimum hours, so a sleep the mod ends sooner goes without the buff, as one the player
/// cancels does.
/// </summary>
public static class SleepRest
{
    /// <summary>The fewest hours slept the game's own early wake asks for before the character may get up.</summary>
    public const float MinHours = 3f;

    /// <param name="slept">How long the character has slept, on the sleep's own timer.</param>
    /// <param name="needed">The minimum hours, on that same timer.</param>
    public static bool EarnsBuff(float slept, float needed) => slept >= needed;

    /// <summary>
    /// What waking gives. Most beds give stamina and Vim and Vigor in the step the game runs when a sleep finishes. One
    /// bed has no such step: it gives them on a timer at the 6 hour mark instead, so ending it early would skip them.
    /// That bed gets its timer's reward fired once the minimum is slept, if the timer has not fired yet.
    /// </summary>
    /// <param name="earnsBuff">Whether the character slept the minimum hours.</param>
    /// <param name="hasFinishStep">Whether the sleep has the step the game runs when a sleep finishes.</param>
    /// <param name="rewardPending">Whether the sleep's timed reward is still waiting to fire.</param>
    public static WakeReward Plan(bool earnsBuff, bool hasFinishStep, bool rewardPending)
    {
        if (hasFinishStep) return earnsBuff ? WakeReward.Finished : WakeReward.BuffHeldBack;

        return earnsBuff && rewardPending ? WakeReward.TimedRewardFired : WakeReward.NothingMore;
    }
}

public enum WakeReward
{
    /// <summary>The finish step runs and gives what it always gives.</summary>
    Finished,

    /// <summary>The finish step runs, with its Vim and Vigor held back.</summary>
    BuffHeldBack,

    /// <summary>The sleep's timed reward is fired before it ends.</summary>
    TimedRewardFired,

    /// <summary>The sleep only ends; nothing more is given.</summary>
    NothingMore,
}

/// <summary>The one ended sleep whose Vim and Vigor is held back, by the game's id for that sleep, used once.</summary>
public sealed class BuffHold
{
    private long? _held;

    public bool IsAny => _held != null;

    public void Hold(long sleep) => _held = sleep;

    public void Clear() => _held = null;

    /// <summary>Whether this sleep's buff is held back; true at most once per hold.</summary>
    public bool Take(long sleep)
    {
        if (_held != sleep) return false;

        _held = null;

        return true;
    }
}
