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

public class IdleClockTests
{
    [Fact]
    public void Standing_idle_past_the_delay_freezes_the_clock()
    {
        var clock = IdleClock.Start.Advance(true, false, 0.6f).Advance(true, false, 0.6f);

        Assert.True(clock.IsFrozen(1f));
    }

    [Fact]
    public void A_short_pause_between_actions_does_not_freeze_the_clock()
    {
        var clock = IdleClock.Start.Advance(true, false, 0.5f);

        Assert.False(clock.IsFrozen(1f));
    }

    [Fact]
    public void Starting_an_action_unfreezes_at_once()
    {
        var clock = IdleClock.Start.Advance(true, false, 5f).Advance(true, true, 0.1f);

        Assert.False(clock.IsFrozen(1f));
    }

    [Fact]
    public void Idle_time_while_freezing_is_not_allowed_never_counts()
    {
        var clock = IdleClock.Start.Advance(false, false, 5f).Advance(true, false, 0.5f);

        Assert.False(clock.IsFrozen(1f));
    }

    [Fact]
    public void A_zero_delay_still_needs_a_moment_of_idling()
    {
        Assert.False(IdleClock.Start.IsFrozen(0f));
        Assert.True(IdleClock.Start.Advance(true, false, 0.1f).IsFrozen(0f));
    }
}

public class ClockPaceTests
{
    [Fact]
    public void A_frozen_day_clock_stops()
    {
        Assert.Equal(0f, ClockPace.Factor(0.5f, isFrozen: true, isDayClock: true));
    }

    [Fact]
    public void A_running_day_clock_follows_the_configured_rate()
    {
        Assert.Equal(0.5f, ClockPace.Factor(0.5f, isFrozen: false, isDayClock: true));
    }

    [Fact]
    public void Exploration_timers_keep_the_games_pace()
    {
        Assert.Equal(1f, ClockPace.Factor(0.5f, isFrozen: true, isDayClock: false));
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
