using System;

namespace TimeControl.Core;

/// <summary>What the ticker saw on one check of a world the game just entered.</summary>
/// <param name="IsOn">StartPaused is on.</param>
/// <param name="Timer">The timer running, as the mod controls it.</param>
/// <param name="IsNormalMode">The game is in its normal mode, not a guided start or a story scene.</param>
public readonly record struct StartSnapshot(bool IsOn, GameTimer Timer, bool IsNormalMode);

/// <param name="ShouldPause">Pause the game now, keeping the speed it would have run at for the next resume.</param>
public readonly record struct StartStep(StartPause State, bool ShouldPause);

/// <summary>
/// Start paused: a save the game enters starts paused, at home or in the countdown to the disaster. The world takes a
/// moment to settle into its timer and mode after it loads, so the pause waits for them for a few checks and gives up
/// after that, which leaves a guided start, a story scene or an expedition running as the game starts it.
/// </summary>
public abstract record StartPause
{
    /// <summary>Two seconds at ten checks a second.</summary>
    public const int MostChecks = 20;

    public static readonly StartPause Done = new Spent();

    public static readonly StartPause Entered = new Armed(MostChecks);

    private StartPause()
    {
    }

    /// <summary>Nothing to do until the game enters another world.</summary>
    public sealed record Spent : StartPause;

    /// <param name="ChecksLeft">Checks left to wait for the timer and the mode to settle.</param>
    public sealed record Armed(int ChecksLeft) : StartPause;

    public static StartStep Next(StartPause state, StartSnapshot seen) => state switch
    {
        Spent => Stay(state),
        Armed armed => Settle(armed, seen),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a start pause state Next does not handle"),
    };

    private static StartStep Settle(Armed armed, StartSnapshot seen)
    {
        if (!seen.IsOn || seen.Timer == GameTimer.Expedition) return Stay(Done);

        if (WaitUntil.CanWait(seen.Timer) && seen.IsNormalMode) return new StartStep(Done, true);

        return Stay(armed.ChecksLeft > 1 ? new Armed(armed.ChecksLeft - 1) : Done);
    }

    private static StartStep Stay(StartPause state) => new(state, false);
}
