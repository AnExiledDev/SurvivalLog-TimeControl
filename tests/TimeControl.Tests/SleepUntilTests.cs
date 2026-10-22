using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Sleep until: a real sleep ends early at a set time of day or for an alert, and a nap is never cut short.</summary>
public class SleepUntilTests
{
    private const int H = ClockHour.Hour;
    private const long Midnight = 10 * ClockHour.Day;

    private static readonly SleepUntilSettings WakeAtSix = new(6 * H, IsWakeOnAlertOn: false);
    private static readonly SleepUntilSettings WakeOnAlert = new(null, IsWakeOnAlertOn: true);
    private static readonly SleepUntilSettings Off = new(null, IsWakeOnAlertOn: false);

    [Fact]
    public void A_sleep_ends_at_the_wake_time_the_first_time_the_clock_shows_it()
    {
        var asleep = SleepUntil.Next(SleepUntil.Start, Sleeping(22 * H), WakeAtSix);

        Assert.Equal(new SleepUntil.Asleep(Midnight + ClockHour.Day + 6 * H), asleep.State);

        var night = SleepUntil.Next(asleep.State, Sleeping(ClockHour.Day + 5 * H), WakeAtSix);
        var morning = SleepUntil.Next(night.State, Sleeping(ClockHour.Day + 6 * H), WakeAtSix);

        Assert.Equal(SleepUntilCommand.None, night.Command);
        Assert.Equal(new SleepUntilStep(new SleepUntil.Woken(ByAlert: false), SleepUntilCommand.Wake), morning);
    }

    [Fact]
    public void A_wake_is_asked_for_once_and_a_wake_at_a_set_time_leaves_the_game_running()
    {
        var woken = new SleepUntil.Woken(ByAlert: false);

        var stillGettingUp = SleepUntil.Next(woken, Sleeping(6 * H), WakeAtSix);
        var up = SleepUntil.Next(woken, Awake(6 * H), WakeAtSix);

        Assert.Equal(new SleepUntilStep(woken, SleepUntilCommand.None), stillGettingUp);
        Assert.Equal(new SleepUntilStep(SleepUntil.Start, SleepUntilCommand.None), up);
    }

    [Fact]
    public void A_nap_is_never_cut_short()
    {
        var nap = Sleeping(5 * H) with { IsWakeable = false };

        var watched = SleepUntil.Next(SleepUntil.Start, nap, WakeAtSix);
        var pastSix = SleepUntil.Next(watched.State, nap with { Reading = Reading(7 * H), IsNewAlert = true }, new SleepUntilSettings(6 * H, true));

        Assert.Equal(SleepUntil.Start, watched.State);
        Assert.Equal(SleepUntilCommand.None, pastSix.Command);
    }

    [Fact]
    public void With_both_settings_off_a_sleep_is_left_to_the_game()
    {
        var step = SleepUntil.Next(SleepUntil.Start, Sleeping(22 * H) with { IsNewAlert = true }, Off);

        Assert.Equal(new SleepUntilStep(SleepUntil.Start, SleepUntilCommand.None), step);
    }

    [Fact]
    public void A_character_who_wakes_on_their_own_is_left_alone()
    {
        var asleep = SleepUntil.Next(SleepUntil.Start, Sleeping(22 * H), WakeAtSix).State;

        var up = SleepUntil.Next(asleep, Awake(ClockHour.Day + 6 * H), WakeAtSix);

        Assert.Equal(new SleepUntilStep(SleepUntil.Start, SleepUntilCommand.None), up);
    }

    [Fact]
    public void An_alert_wakes_the_character_and_the_game_is_paused_once_they_are_up_and_the_speed_up_is_done()
    {
        var asleep = SleepUntil.Next(SleepUntil.Start, Sleeping(1 * H), WakeOnAlert);

        Assert.Equal(new SleepUntil.Asleep(null), asleep.State);

        var quiet = SleepUntil.Next(asleep.State, Sleeping(2 * H), WakeOnAlert);
        var alert = SleepUntil.Next(quiet.State, Sleeping(3 * H) with { IsNewAlert = true }, WakeOnAlert);

        Assert.Equal(SleepUntilCommand.None, quiet.Command);
        Assert.Equal(new SleepUntilStep(new SleepUntil.Woken(ByAlert: true), SleepUntilCommand.Wake), alert);

        var speedingUp = SleepUntil.Next(alert.State, Awake(3 * H) with { IsGameFastForwarding = true }, WakeOnAlert);
        var up = SleepUntil.Next(speedingUp.State, Awake(3 * H), WakeOnAlert);

        Assert.Equal(SleepUntilCommand.None, speedingUp.Command);
        Assert.Equal(new SleepUntilStep(SleepUntil.Start, SleepUntilCommand.Pause), up);
    }

    [Fact]
    public void A_game_already_paused_when_the_character_is_up_is_left_paused()
    {
        var up = SleepUntil.Next(new SleepUntil.Woken(ByAlert: true), Awake(3 * H) with { IsPaused = true }, WakeOnAlert);

        Assert.Equal(new SleepUntilStep(SleepUntil.Start, SleepUntilCommand.None), up);
    }

    [Fact]
    public void An_alert_does_not_wake_the_character_unless_wake_on_alert_is_on()
    {
        var asleep = SleepUntil.Next(SleepUntil.Start, Sleeping(22 * H), WakeAtSix).State;

        var alert = SleepUntil.Next(asleep, Sleeping(23 * H) with { IsNewAlert = true }, WakeAtSix);

        Assert.Equal(new SleepUntilStep(asleep, SleepUntilCommand.None), alert);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(6 * H, false, true)]
    [InlineData(null, true, true)]
    public void Sleep_until_is_on_when_either_setting_is(int? wakeAt, bool isWakeOnAlertOn, bool expected)
    {
        Assert.Equal(expected, new SleepUntilSettings(wakeAt, isWakeOnAlertOn).IsAnyOn);
    }

    private static SleepUntilSnapshot Sleeping(long sinceMidnight) => new(
        IsAsleep: true,
        IsWakeable: true,
        Reading(sinceMidnight),
        IsNewAlert: false,
        IsGameFastForwarding: true,
        IsPaused: false);

    private static SleepUntilSnapshot Awake(long sinceMidnight) => Sleeping(sinceMidnight) with { IsAsleep = false, IsGameFastForwarding = false };

    private static ClockReading Reading(long sinceMidnight) => new(Midnight + sinceMidnight, ClockHour.Wrap(sinceMidnight));
}
