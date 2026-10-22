using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class QueueSlowTests
{
    private static readonly GameSpeed SlowSpeed = new(PauseToggle.Slow, FastGear.GamesOwn);
    private static readonly GameSpeed Normal = new(PauseToggle.Normal, FastGear.GamesOwn);
    private static readonly GameSpeed Fast = new(PauseToggle.Double, FastGear.GamesOwn);
    private static readonly GameSpeed Fastest = new(PauseToggle.Triple, FastGear.GamesOwn);
    private static readonly GameSpeed SecondFastGear = new(PauseToggle.Triple, 2);

    // Shorter than the queue's settle time, but two in a row are longer.
    private static readonly QueueSlowSnapshot Empty = new(CanSlow: true, IsPaused: false, IsBusy: false, Running: Fast, ElapsedSeconds: 0.2f,
        WasSpeedChangedByHand: false);
    private static readonly QueueSlowSnapshot Busy = Empty with { IsBusy = true };

    private static List<QueueSlowStep> Steps(GameSpeed target, params QueueSlowSnapshot[] checks)
    {
        var state = QueueSlow.Start;
        var steps = new List<QueueSlowStep>();

        foreach (var seen in checks)
        {
            var step = QueueSlow.Next(state, seen, target);

            steps.Add(step);
            state = step.State;
        }

        return steps;
    }

    private static GameSpeed[] SlowedTo(GameSpeed target, params QueueSlowSnapshot[] checks) =>
        Steps(target, checks).Select(step => step.SlowTo).OfType<GameSpeed>().ToArray();

    private static QueueSlowSnapshot Paused(QueueSlowSnapshot seen) => seen with { IsPaused = true };

    [Fact]
    public void A_queue_that_stays_empty_slows_a_faster_game_to_the_chosen_speed_once()
    {
        var steps = Steps(Normal, Busy, Empty, Empty, Empty, Empty);

        Assert.Null(steps[1].SlowTo);
        Assert.Equal(Normal, steps[2].SlowTo);
        Assert.Equal(new QueueSlow.Slowed(Fast), steps[2].State);
        Assert.Null(steps[3].SlowTo);
        Assert.Null(steps[4].SlowTo);
    }

    [Fact]
    public void The_moment_between_two_queued_orders_is_not_a_run_out()
    {
        Assert.Empty(SlowedTo(Normal, Busy, Empty, Busy, Empty, Busy));
    }

    [Fact]
    public void A_queue_empty_from_the_first_check_has_not_run_out()
    {
        Assert.Empty(SlowedTo(Normal, Empty, Empty, Empty));
    }

    [Fact]
    public void A_game_already_at_or_below_the_chosen_speed_is_left_alone()
    {
        Assert.Empty(SlowedTo(Normal, Busy with { Running = Normal }, Empty with { Running = Normal }, Empty with { Running = Normal }));
        Assert.Empty(SlowedTo(Normal, Busy with { Running = SlowSpeed }, Empty with { Running = SlowSpeed }, Empty with { Running = SlowSpeed }));
    }

    [Fact]
    public void The_chosen_speed_can_be_fast_for_a_game_run_at_the_fastest()
    {
        var target = new QueueSlowSettings(IsOn: true, QueueSlowTarget.Fast).TargetSpeed;
        var steps = Steps(target, Busy with { Running = SecondFastGear }, Empty with { Running = SecondFastGear }, Empty with { Running = SecondFastGear });

        Assert.Equal(Fast, steps[2].SlowTo);
        Assert.Equal(new QueueSlow.Slowed(SecondFastGear), steps[2].State);
    }

    [Fact]
    public void The_next_order_puts_the_old_speed_back_once()
    {
        var slowed = Empty with { Running = Normal };
        var steps = Steps(Normal, Busy, Empty, Empty, slowed, Busy with { Running = Normal }, Busy);

        Assert.Null(steps[3].RestoreTo);
        Assert.Equal(Fast, steps[4].RestoreTo);
        Assert.Null(steps[5].RestoreTo);
    }

    [Fact]
    public void An_order_given_while_paused_still_brings_the_old_speed_back_for_the_resume()
    {
        var steps = Steps(Normal, Busy, Empty, Empty, Paused(Busy) with { Running = Normal });

        Assert.Equal(Fast, steps[3].RestoreTo);
    }

    [Fact]
    public void A_speed_picked_by_hand_after_the_slow_down_is_kept()
    {
        var picked = Empty with { Running = SlowSpeed, WasSpeedChangedByHand = true };
        var steps = Steps(Normal, Busy, Empty, Empty, picked, Busy with { Running = SlowSpeed });

        Assert.All(steps, step => Assert.Null(step.RestoreTo));
    }

    [Fact]
    public void Falling_asleep_while_slowed_ends_the_slow_down_without_putting_the_speed_back()
    {
        var asleep = Busy with { Running = Normal, CanSlow = false };
        var steps = Steps(Normal, Busy, Empty, Empty, asleep, Busy with { Running = Normal });

        Assert.All(steps, step => Assert.Null(step.RestoreTo));
    }

    [Fact]
    public void A_run_out_while_paused_is_spent_and_not_saved_for_the_resume()
    {
        Assert.Empty(SlowedTo(Normal, Paused(Busy), Paused(Empty), Paused(Empty), Empty, Empty, Empty));
    }

    [Fact]
    public void A_run_out_where_the_mod_does_not_slow_the_game_is_spent()
    {
        var asleep = Empty with { CanSlow = false };

        Assert.Empty(SlowedTo(Normal, Busy, asleep, asleep, Empty, Empty));
    }

    [Fact]
    public void Each_run_out_slows_once_after_the_old_speed_came_back()
    {
        var atNormal = Empty with { Running = Normal };

        Assert.Equal(new[] { Normal, Normal }, SlowedTo(Normal, Busy, Empty, Empty, atNormal, Busy with { Running = Normal }, Busy, Empty, Empty));
    }

    [Fact]
    public void Speeds_rank_slow_normal_fast_fastest_then_the_fast_gears()
    {
        GameSpeed[] slowestFirst = { SlowSpeed, Normal, Fast, Fastest, new(PauseToggle.Triple, 1), SecondFastGear };

        for (var i = 1; i < slowestFirst.Length; i++)
        {
            Assert.True(QueueSlow.IsFaster(slowestFirst[i], slowestFirst[i - 1]));
            Assert.False(QueueSlow.IsFaster(slowestFirst[i - 1], slowestFirst[i]));
        }

        Assert.False(QueueSlow.IsFaster(Normal, Normal));
    }

    [Fact]
    public void Each_target_names_the_game_speed_button_it_drops_to()
    {
        Assert.Equal(SlowSpeed, new QueueSlowSettings(IsOn: true, QueueSlowTarget.Slow).TargetSpeed);
        Assert.Equal(Normal, new QueueSlowSettings(IsOn: true, QueueSlowTarget.Normal).TargetSpeed);
        Assert.Equal(Fast, new QueueSlowSettings(IsOn: true, QueueSlowTarget.Fast).TargetSpeed);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData(" true ")]
    public void A_player_who_had_the_old_queue_pause_on_keeps_pausing(string retired)
    {
        Assert.Equal(QueueEmptyAction.Pause, QueueEmptyActions.FromRetired(retired, QueueEmptyAction.Off));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("yes")]
    public void Without_the_old_queue_pause_on_the_new_setting_stays_off(string? retired)
    {
        Assert.Equal(QueueEmptyAction.Off, QueueEmptyActions.FromRetired(retired, QueueEmptyAction.Off));
    }

    [Fact]
    public void A_choice_already_made_in_the_new_setting_is_never_overwritten()
    {
        Assert.Equal(QueueEmptyAction.Slow, QueueEmptyActions.FromRetired("true", QueueEmptyAction.Slow));
        Assert.Equal(QueueEmptyAction.Pause, QueueEmptyActions.FromRetired("false", QueueEmptyAction.Pause));
    }
}
