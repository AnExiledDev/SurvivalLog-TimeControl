using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Alarm clock: the game pauses when the clock reaches a time of day, every day or once, or a few hours on from the menu.</summary>
public class AlarmClockTests
{
    private const int H = ClockHour.Hour;

    /// <summary>Midnight on a day well into the save.</summary>
    private const long Midnight = 10 * ClockHour.Day;

    private static readonly AlarmSettings DailyAtSeven = new(7 * H, IsDaily: true);
    private static readonly AlarmSettings OnceAtSeven = new(7 * H, IsDaily: false);
    private static readonly AlarmSettings Off = new(null, IsDaily: true);

    [Fact]
    public void A_daily_alarm_rings_when_the_clock_reaches_it_and_again_the_next_day()
    {
        var state = Run(AlarmClock.Start, DailyAtSeven, 6 * H, out _);

        state = Run(state, DailyAtSeven, 7 * H, out var firstDay);
        state = Run(state, DailyAtSeven, 8 * H, out var laterThatDay);
        Run(state, DailyAtSeven, ClockHour.Day + 7 * H, out var nextDay);

        Assert.Equal(AlarmRing.Set, firstDay);
        Assert.Null(laterThatDay);
        Assert.Equal(AlarmRing.Set, nextDay);
    }

    [Fact]
    public void A_one_time_alarm_rings_once_and_stays_quiet_the_next_day()
    {
        var state = Run(AlarmClock.Start, OnceAtSeven, 6 * H, out _);

        state = Run(state, OnceAtSeven, 7 * H, out var first);
        Run(state, OnceAtSeven, ClockHour.Day + 7 * H, out var nextDay);

        Assert.Equal(AlarmRing.Set, first);
        Assert.Null(nextDay);
    }

    [Fact]
    public void A_new_alarm_time_in_the_settings_arms_a_spent_one_time_alarm_again()
    {
        var atEight = new AlarmSettings(8 * H, IsDaily: false);
        var state = Run(AlarmClock.Start, OnceAtSeven, 6 * H, out _);

        state = Run(state, OnceAtSeven, 7 * H, out _);
        state = Run(state, atEight, 7 * H + 30 * 60, out var beforeEight);
        Run(state, atEight, 8 * H, out var atEightRing);

        Assert.Null(beforeEight);
        Assert.Equal(AlarmRing.Set, atEightRing);
    }

    [Fact]
    public void A_save_loaded_after_the_alarm_time_waits_for_the_next_day()
    {
        var state = Run(AlarmClock.Start, DailyAtSeven, 9 * H, out var onLoad);

        Run(state, DailyAtSeven, 20 * H, out var thatEvening);
        Run(state, DailyAtSeven, ClockHour.Day + 7 * H, out var nextMorning);

        Assert.Null(onLoad);
        Assert.Null(thatEvening);
        Assert.Equal(AlarmRing.Set, nextMorning);
    }

    [Fact]
    public void No_alarm_time_never_rings()
    {
        var state = Run(AlarmClock.Start, Off, 6 * H, out _);

        Run(state, Off, 3 * ClockHour.Day, out var rang);

        Assert.Null(rang);
    }

    [Fact]
    public void An_alarm_due_while_the_character_sleeps_rings_once_they_wake()
    {
        var state = Run(AlarmClock.Start, DailyAtSeven, 6 * H, out _);

        state = Run(state, DailyAtSeven, 7 * H, out var whileAsleep, canRing: false);
        Run(state, DailyAtSeven, 9 * H, out var onWaking);

        Assert.Null(whileAsleep);
        Assert.Equal(AlarmRing.Set, onWaking);
    }

    [Fact]
    public void An_alarm_set_from_the_menu_rings_once_that_many_hours_on()
    {
        var state = Run(AlarmClock.Start, Off, 6 * H, out _);

        state = AlarmClock.SetQuick(state, Reading(6 * H), GameTimer.DayClock, 2);
        state = Run(state, Off, 8 * H - 1, out var justBefore);
        state = Run(state, Off, 8 * H, out var onTime);
        Run(state, Off, 10 * H, out var later);

        Assert.Null(justBefore);
        Assert.Equal(AlarmRing.Quick, onTime);
        Assert.Null(later);
    }

    [Fact]
    public void A_new_alarm_from_the_menu_replaces_the_one_set_before()
    {
        var state = AlarmClock.SetQuick(AlarmClock.Start, Reading(6 * H), GameTimer.DayClock, 1);

        state = AlarmClock.SetQuick(state, Reading(6 * H), GameTimer.DayClock, 4);

        Assert.Equal(Midnight + 10 * H, state.QuickAlarmAt);
    }

    [Fact]
    public void A_cancelled_alarm_from_the_menu_never_rings()
    {
        var state = AlarmClock.SetQuick(AlarmClock.Start, Reading(6 * H), GameTimer.DayClock, 1);

        state = AlarmClock.CancelQuick(state);
        Run(state, Off, 8 * H, out var rang);

        Assert.Null(rang);
    }

    [Fact]
    public void A_new_clock_drops_the_alarms_counted_on_the_old_one()
    {
        var state = Run(AlarmClock.Start, DailyAtSeven, 6 * H, out _);

        state = AlarmClock.SetQuick(state, Reading(6 * H), GameTimer.DayClock, 1);

        var countdown = AlarmClock.Next(state, new AlarmSnapshot(GameTimer.PrologueCountdown, Reading(6 * H + 60), CanRing: true), DailyAtSeven);

        Assert.Null(countdown.Rang);
        Assert.Null(countdown.State.QuickAlarmAt);
        Assert.Equal(Midnight + 7 * H, countdown.State.SetAlarmAt);
    }

    [Fact]
    public void Nothing_rings_on_an_expedition_and_nothing_set_before_it_carries_over()
    {
        var state = Run(AlarmClock.Start, DailyAtSeven, 6 * H, out _);

        state = AlarmClock.SetQuick(state, Reading(6 * H), GameTimer.DayClock, 1);

        var away = AlarmClock.Next(state, new AlarmSnapshot(GameTimer.Expedition, Reading(8 * H), CanRing: true), DailyAtSeven);

        Assert.Null(away.Rang);
        Assert.Null(away.State.QuickAlarmAt);
        Assert.Null(away.State.SetAlarmAt);
    }

    [Fact]
    public void A_one_time_alarm_already_rung_stays_quiet_in_another_save()
    {
        var state = Run(AlarmClock.Start, OnceAtSeven, 6 * H, out _);

        state = Run(state, OnceAtSeven, 7 * H, out _);
        state = AlarmClock.ForNewWorld(state);
        Run(state, OnceAtSeven, 6 * H, out var onLoad);
        Run(Run(state, OnceAtSeven, 6 * H, out _), OnceAtSeven, 7 * H, out var atSeven);

        Assert.Null(onLoad);
        Assert.Null(atSeven);
    }

    [Fact]
    public void Another_save_drops_the_alarm_from_the_menu()
    {
        var state = AlarmClock.SetQuick(AlarmClock.Start, Reading(6 * H), GameTimer.DayClock, 1);

        Assert.Null(AlarmClock.ForNewWorld(state).QuickAlarmAt);
    }

    private static AlarmClock Run(AlarmClock state, AlarmSettings settings, long sinceMidnight, out AlarmRing? rang, bool canRing = true)
    {
        var step = AlarmClock.Next(state, new AlarmSnapshot(GameTimer.DayClock, Reading(sinceMidnight), canRing), settings);

        rang = step.Rang;

        return step.State;
    }

    private static ClockReading Reading(long sinceMidnight) => new(Midnight + sinceMidnight, ClockHour.Wrap(sinceMidnight));
}
