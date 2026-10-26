using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Holding a key runs the game fast or slow, and letting go puts back where the game was.</summary>
public class HoldKeysTests
{
    private static readonly HeldFrom RunningNormal = new(false, false, new SpeedTarget.Level(new GameSpeed(PauseToggle.Normal, FastGear.GamesOwn)));
    private static readonly HeldFrom PausedByKey = new(true, true, new SpeedTarget.Custom(1.5f));
    private static readonly HeldFrom RunningFast = new(false, false, new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, 1)));

    private static HoldSnapshot Seen(bool fast = false, bool slow = false, bool canHold = true, bool changed = false, HeldFrom? now = null, bool gameKey = false) =>
        new(fast, slow, canHold, changed, now ?? RunningNormal, gameKey);

    private static HoldStep Run(HoldKeys state, params HoldSnapshot[] checks)
    {
        var step = new HoldStep(state, HoldCommand.Nothing);

        foreach (var seen in checks) step = HoldKeys.Next(step.State, seen);

        return step;
    }

    [Fact]
    public void Pressing_the_fast_key_starts_a_fast_hold_and_keeps_where_the_game_was()
    {
        var step = HoldKeys.Next(HoldKeys.Start, Seen(fast: true));

        Assert.Equal(new HoldCommand.Start(HoldKind.Fast), step.Command);
        Assert.Equal(new HoldKeys.Holding(HoldKind.Fast, RunningNormal), step.State);
    }

    [Fact]
    public void Letting_go_puts_back_where_the_game_was_when_the_hold_started_not_where_it_was_during_it()
    {
        var step = Run(HoldKeys.Start, Seen(slow: true, now: PausedByKey), Seen(slow: true, now: RunningFast), Seen(now: RunningFast));

        Assert.Equal(new HoldCommand.Release(PausedByKey), step.Command);
        Assert.Equal(HoldKeys.Start, step.State);
    }

    [Fact]
    public void Holding_the_games_speed_up_key_while_paused_runs_time_and_letting_go_pauses_again()
    {
        var started = HoldKeys.Next(HoldKeys.Start, Seen(gameKey: true, now: PausedByKey));

        Assert.Equal(new HoldCommand.Start(HoldKind.GameKey), started.Command);

        var released = Run(started.State, Seen(gameKey: true, now: RunningFast), Seen(now: RunningFast));

        Assert.Equal(new HoldCommand.Release(PausedByKey), released.Command);
        Assert.Equal(HoldKeys.Start, released.State);
    }

    [Fact]
    public void The_mods_own_fast_key_wins_over_the_games_speed_up_key()
    {
        Assert.Equal(new HoldCommand.Start(HoldKind.Fast), HoldKeys.Next(HoldKeys.Start, Seen(fast: true, gameKey: true)).Command);
    }

    [Fact]
    public void While_both_keys_are_down_fast_wins()
    {
        var step = HoldKeys.Next(HoldKeys.Start, Seen(fast: true, slow: true));

        Assert.Equal(new HoldCommand.Start(HoldKind.Fast), step.Command);
    }

    [Fact]
    public void Letting_go_of_fast_while_slow_is_still_down_switches_to_slow_and_still_returns_to_the_first_speed()
    {
        var switched = Run(HoldKeys.Start, Seen(fast: true, slow: true), Seen(slow: true, now: RunningFast));

        Assert.Equal(new HoldCommand.Start(HoldKind.Slow), switched.Command);

        var released = HoldKeys.Next(switched.State, Seen());

        Assert.Equal(new HoldCommand.Release(RunningNormal), released.Command);
    }

    [Fact]
    public void Holding_the_same_key_on_later_checks_does_nothing_more()
    {
        var step = Run(HoldKeys.Start, Seen(fast: true), Seen(fast: true));

        Assert.Equal(HoldCommand.Nothing, step.Command);
        Assert.IsType<HoldKeys.Holding>(step.State);
    }

    [Fact]
    public void Another_speed_change_during_a_hold_ends_it_where_it_is_and_a_key_still_held_never_takes_the_speed_back()
    {
        var cancelled = Run(HoldKeys.Start, Seen(fast: true), Seen(fast: true, changed: true));

        Assert.Equal(new HoldCommand.Cancel(RunningNormal), cancelled.Command);

        var stillHeld = HoldKeys.Next(cancelled.State, Seen(fast: true));

        Assert.Equal(HoldCommand.Nothing, stillHeld.Command);

        var keysUp = HoldKeys.Next(stillHeld.State, Seen());

        Assert.Equal(HoldCommand.Nothing, keysUp.Command);
        Assert.Equal(HoldKeys.Start, keysUp.State);
        Assert.Equal(new HoldCommand.Start(HoldKind.Fast), HoldKeys.Next(keysUp.State, Seen(fast: true)).Command);
    }

    [Fact]
    public void A_hold_ends_where_it_is_once_the_speed_stops_being_the_players_to_change()
    {
        var step = Run(HoldKeys.Start, Seen(fast: true), Seen(fast: true, canHold: false));

        Assert.Equal(new HoldCommand.Cancel(RunningNormal), step.Command);
    }

    [Fact]
    public void A_key_pressed_where_no_hold_can_start_does_nothing_even_once_a_hold_could_start()
    {
        var refused = HoldKeys.Next(HoldKeys.Start, Seen(fast: true, canHold: false));

        Assert.Equal(HoldCommand.Nothing, refused.Command);
        Assert.Equal(HoldCommand.Nothing, HoldKeys.Next(refused.State, Seen(fast: true)).Command);
    }

    [Fact]
    public void With_no_key_down_nothing_happens()
    {
        var step = HoldKeys.Next(HoldKeys.Start, Seen(changed: true));

        Assert.Equal(HoldCommand.Nothing, step.Command);
        Assert.Equal(HoldKeys.Start, step.State);
    }
}
