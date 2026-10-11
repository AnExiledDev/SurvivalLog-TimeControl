using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class SlowGearTests
{
    [Fact]
    public void Default_config_offers_half_then_quarter_speed()
    {
        Assert.Equal(new[] { 0.5f, 0.25f }, SlowGear.ParseSpeeds("0.5,0.25"));
    }

    [Fact]
    public void A_typo_drops_one_speed_not_the_whole_list()
    {
        Assert.Equal(new[] { 0.5f, 0.25f }, SlowGear.ParseSpeeds("0.5, half; 0.25"));
    }

    [Fact]
    public void Speeds_never_reach_a_stop_or_normal_speed()
    {
        Assert.Equal(new[] { SlowGear.Slowest, SlowGear.Fastest }, SlowGear.ParseSpeeds("0,1.5"));
    }

    [Fact]
    public void An_empty_setting_offers_no_speeds_so_the_game_keeps_its_own_slow_button()
    {
        Assert.Empty(SlowGear.ParseSpeeds(""));
    }

    [Fact]
    public void Pressing_slow_while_slow_cycles_to_the_next_speed_and_wraps()
    {
        Assert.Equal(1, SlowGear.NextIndex(0, 2, wasAlreadySlow: true));
        Assert.Equal(0, SlowGear.NextIndex(1, 2, wasAlreadySlow: true));
    }

    [Fact]
    public void Coming_back_to_slow_from_another_speed_keeps_the_speed_last_used()
    {
        Assert.Equal(1, SlowGear.NextIndex(1, 2, wasAlreadySlow: false));
    }

    [Fact]
    public void A_remembered_speed_that_no_longer_exists_falls_back_to_the_first()
    {
        Assert.Equal(0, SlowGear.NextIndex(5, 2, wasAlreadySlow: false));
        Assert.Equal(-1, SlowGear.NextIndex(0, 0, wasAlreadySlow: true));
    }

    [Fact]
    public void Labels_read_as_the_speed_multiplier()
    {
        Assert.Equal("0.25x", SlowGear.Label(0.25f));
        Assert.Equal("0.5x", SlowGear.Label(0.5f));
    }
}

public class IdlePauseTests
{
    private const float Delay = 1f;

    private static IdleSnapshot Idle(float seconds, int runningLevel = PauseToggle.Normal) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: false, seconds, runningLevel);

    private static IdleSnapshot IdleWhilePaused(float seconds) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: true, seconds, PauseToggle.Normal);

    private static IdleSnapshot OrderGiven(bool isPaused) =>
        new(CanFreeze: true, IsBusy: true, IsPaused: isPaused, 0.1f, PauseToggle.Normal);

    private static IdleStep Run(IdlePause state, params IdleSnapshot[] checks)
    {
        var step = new IdleStep(state, IdleCommand.Nothing);

        foreach (var seen in checks) step = IdlePause.Next(step.State, seen, Delay);

        return step;
    }

    [Fact]
    public void Standing_idle_past_the_delay_pauses_the_game_and_remembers_the_speed_it_ran_at()
    {
        var step = Run(IdlePause.Start, Idle(0.6f, runningLevel: 2), Idle(0.6f, runningLevel: 2));

        Assert.Equal(IdleCommand.PauseGame, step.Command);
        Assert.Equal(new IdlePause.Frozen(2), step.State);
    }

    [Fact]
    public void Idling_shorter_than_the_delay_leaves_the_game_running()
    {
        var step = Run(IdlePause.Start, Idle(0.5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.IsType<IdlePause.Watching>(step.State);
    }

    [Fact]
    public void An_order_between_two_short_idles_starts_the_count_over()
    {
        var step = Run(IdlePause.Start, Idle(0.6f), OrderGiven(isPaused: false), Idle(0.6f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
    }

    [Fact]
    public void An_order_while_paused_by_idling_resumes_at_the_remembered_speed()
    {
        var step = Run(new IdlePause.Frozen(2), OrderGiven(isPaused: true));

        Assert.Equal(new IdleCommand.Resume(2), step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void A_remembered_speed_that_is_not_a_running_one_resumes_at_normal_speed()
    {
        var step = Run(IdlePause.Start, Idle(2f, runningLevel: PauseToggle.Paused));

        Assert.Equal(new IdlePause.Frozen(PauseToggle.Normal), step.State);
    }

    [Fact]
    public void The_players_own_pause_never_counts_as_idling_and_is_never_undone()
    {
        var step = Run(IdlePause.Start, IdleWhilePaused(5f), OrderGiven(isPaused: true), IdleWhilePaused(5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void Pressing_play_while_paused_by_idling_lets_time_run_until_the_next_order()
    {
        var released = Run(new IdlePause.Frozen(PauseToggle.Normal), Idle(0.1f), Idle(10f));

        Assert.Equal(IdleCommand.Nothing, released.Command);
        Assert.IsType<IdlePause.Released>(released.State);

        var afterOrder = Run(released.State, OrderGiven(isPaused: false), Idle(0.6f), Idle(0.6f));

        Assert.Equal(IdleCommand.PauseGame, afterOrder.Command);
    }

    [Fact]
    public void Leaving_home_or_sleeping_while_paused_by_idling_resumes_the_game()
    {
        var leftHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: true, 0.1f, PauseToggle.Normal);

        var step = Run(new IdlePause.Frozen(3), leftHome);

        Assert.Equal(new IdleCommand.Resume(3), step.Command);
    }

    [Fact]
    public void Idle_time_where_pausing_is_not_allowed_never_counts()
    {
        var awayFromHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: false, 5f, PauseToggle.Normal);

        var step = Run(IdlePause.Start, awayFromHome, Idle(0.5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
    }

    [Fact]
    public void A_zero_delay_still_needs_a_moment_of_idling()
    {
        var noTimePassed = IdlePause.Next(IdlePause.Start, Idle(0f), delaySeconds: 0f);
        var aMomentPassed = IdlePause.Next(IdlePause.Start, Idle(0.1f), delaySeconds: 0f);

        Assert.Equal(IdleCommand.Nothing, noTimePassed.Command);
        Assert.Equal(IdleCommand.PauseGame, aMomentPassed.Command);
    }

    [Fact]
    public void A_broken_delay_setting_falls_back_to_something_sane()
    {
        Assert.Equal(1f, IdlePause.ClampDelay(float.NaN));
        Assert.Equal(1f, IdlePause.ClampDelay(float.PositiveInfinity));
        Assert.Equal(0f, IdlePause.ClampDelay(-3f));
        Assert.Equal(2.5f, IdlePause.ClampDelay(2.5f));
    }
}

public class ClockPaceTests
{
    [Fact]
    public void The_day_clock_follows_the_configured_rate()
    {
        Assert.Equal(0.5f, ClockPace.Factor(0.5f, isDayClock: true));
    }

    [Fact]
    public void Exploration_timers_keep_the_games_pace()
    {
        Assert.Equal(1f, ClockPace.Factor(0.5f, isDayClock: false));
    }

    [Fact]
    public void A_broken_rate_setting_cannot_stop_or_race_the_clock()
    {
        Assert.Equal(ClockPace.SlowestRate, ClockPace.ClampRate(0f));
        Assert.Equal(ClockPace.FastestRate, ClockPace.ClampRate(100f));
        Assert.Equal(1f, ClockPace.ClampRate(float.NaN));
    }
}

public class PauseToggleTests
{
    [Fact]
    public void Pressing_pause_while_running_pauses()
    {
        Assert.Equal(PauseToggle.Paused, PauseToggle.Target(isPaused: false, lastRunningLevel: 3));
    }

    [Fact]
    public void Pressing_pause_while_paused_resumes_the_last_speed()
    {
        Assert.Equal(3, PauseToggle.Target(isPaused: true, lastRunningLevel: 3));
        Assert.Equal(PauseToggle.Slow, PauseToggle.Target(isPaused: true, lastRunningLevel: 4));
    }

    [Fact]
    public void Resuming_with_no_known_speed_goes_to_normal_speed()
    {
        Assert.Equal(PauseToggle.Normal, PauseToggle.Target(isPaused: true, lastRunningLevel: 0));
    }

    [Fact]
    public void A_speed_level_the_game_does_not_have_resumes_at_normal_speed()
    {
        Assert.Equal(PauseToggle.Normal, PauseToggle.RunningLevel(7));
        Assert.Equal(PauseToggle.Normal, PauseToggle.RunningLevel(-1));
    }
}

public class SpeedChangeFanOutTests
{
    [Fact]
    public void A_speed_change_reaching_two_time_managers_in_one_frame_counts_two()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 10, timeManager: 222);

        Assert.Equal(2, fanOut.TakeLargest());
    }

    [Fact]
    public void One_time_manager_changing_twice_in_a_frame_counts_once()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 10, timeManager: 111);

        Assert.Equal(1, fanOut.TakeLargest());
    }

    [Fact]
    public void Changes_in_separate_frames_are_not_added_together()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 11, timeManager: 222);

        Assert.Equal(1, fanOut.TakeLargest());
    }

    [Fact]
    public void Taking_the_count_starts_it_over()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.TakeLargest();

        Assert.Equal(0, fanOut.TakeLargest());
    }
}

public class PageClockTests
{
    [Fact]
    public void A_slowed_clock_is_drawn_at_the_slowed_pace()
    {
        var update = PageClock.Next(0.5f, 2f, "", wasNeutralLastSent: true);

        Assert.Equal(new PageClockUpdate(1f, ""), update);
    }

    [Fact]
    public void A_frozen_clock_is_drawn_stopped()
    {
        Assert.Equal(0f, PageClock.Next(0f, 1f, "", wasNeutralLastSent: true)?.ClockScale);
    }

    [Fact]
    public void Holding_a_slow_speed_only_relabels_and_leaves_the_clock_to_the_game()
    {
        Assert.Equal(new PageClockUpdate(null, "0.25x"), PageClock.Next(1f, 0.25f, "0.25x", wasNeutralLastSent: true));
    }

    [Fact]
    public void Nothing_is_sent_while_the_mod_changes_nothing()
    {
        Assert.Null(PageClock.Next(1f, 1f, "", wasNeutralLastSent: true));
    }

    [Fact]
    public void Turning_everything_off_puts_the_clock_back_at_game_speed_once()
    {
        Assert.Equal(new PageClockUpdate(2f, ""), PageClock.Next(1f, 2f, "", wasNeutralLastSent: false));
    }

    [Fact]
    public void Scripts_use_a_dot_decimal_whatever_the_system_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            Assert.Contains("var k=0.25;", PageClock.Script(new PageClockUpdate(0.25f, "")));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_label_cannot_break_out_of_the_script_string()
    {
        var script = PageClock.Script(new PageClockUpdate(null, "0.5x');alert(1);//"));

        Assert.DoesNotContain("alert", script);
        Assert.Contains("n=b+' 0.5x1'", script);
    }
}
