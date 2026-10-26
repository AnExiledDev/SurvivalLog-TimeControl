using System.Linq;
using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The wait menu: what it offers, what its clicks do, and the button the page draws for it.</summary>
public class WaitMenuTests
{
    private const int H = ClockHour.Hour;
    private const long Midnight = 10 * ClockHour.Day;

    private static readonly ClockReading SixAm = new(Midnight + 6 * H, 6 * H);
    private static readonly GameSpeed Normal = new(PauseToggle.Normal, FastGear.GamesOwn);

    private static readonly WaitMenuInput Idle = new(
        HudPage.Home,
        WaitUntil.Start,
        SixAm,
        QuickAlarmAt: null,
        AlarmSecond: null,
        IsBrewing: true,
        IsBusy: true,
        CanStartWait: true,
        IsChinese: false);

    [Fact]
    public void An_idle_menu_offers_the_targets_the_hours_to_wait_and_the_hours_an_alarm_can_be_set_ahead()
    {
        var view = WaitMenu.View(Idle);

        Assert.False(view.IsActive);
        Assert.Equal("Wait until", view.Tip);
        Assert.Equal(new[] { "Wait until", "", "Pause in" }, view.Lines.Select(line => line.Text));
        Assert.Equal("Morning|Brew done|Queue done", Labels(view.Lines[0]));
        Assert.Equal("+1h|+2h|+4h|+8h", Labels(view.Lines[1]));
        Assert.Equal("1h|2h|4h|8h", Labels(view.Lines[2]));
        Assert.All(view.Lines.SelectMany(line => line.Items), item => Assert.True(item.IsEnabled));
    }

    [Fact]
    public void The_alarm_time_from_the_settings_is_a_target_only_when_one_is_set()
    {
        var view = WaitMenu.View(Idle with { AlarmSecond = 21 * H + 30 * 60 });

        Assert.Equal("Morning|Alarm 21:30|Brew done|Queue done", Labels(view.Lines[0]));
    }

    [Fact]
    public void A_brew_or_the_queue_with_nothing_to_wait_for_is_greyed_out()
    {
        var view = WaitMenu.View(Idle with { IsBrewing = false, IsBusy = false });

        Assert.False(Item(view, WaitMenu.Brew).IsEnabled);
        Assert.False(Item(view, WaitMenu.Queue).IsEnabled);
        Assert.True(Item(view, WaitMenu.Morning).IsEnabled);
    }

    [Fact]
    public void While_no_wait_can_start_every_wait_is_greyed_out_but_alarms_can_still_be_set()
    {
        var view = WaitMenu.View(Idle with { CanStartWait = false, AlarmSecond = 7 * H });

        Assert.All(view.Lines[0].Items.Concat(view.Lines[1].Items), item => Assert.False(item.IsEnabled));
        Assert.All(view.Lines[2].Items, item => Assert.True(item.IsEnabled));
    }

    [Fact]
    public void A_running_wait_shows_first_with_a_cancel_button_and_lights_the_button()
    {
        var waiting = new WaitUntil.Waiting(new WaitGoal.At(Midnight + 7 * H, WaitKind.Morning), Normal, GameTimer.DayClock);

        var view = WaitMenu.View(Idle with { Wait = waiting });

        Assert.True(view.IsActive);
        Assert.Equal("Waiting: Morning", view.Lines[0].Text);
        Assert.Equal(new[] { new WaitMenuItem(WaitMenu.CancelWaitId, "Cancel") }, view.Lines[0].Items);
    }

    [Fact]
    public void A_wait_for_a_number_of_hours_shows_the_time_it_ends()
    {
        var waiting = new WaitUntil.Waiting(new WaitGoal.At(Midnight + 26 * H + 30 * 60, WaitKind.Hours), Normal, GameTimer.DayClock);

        Assert.Equal("Waiting: 02:30", WaitMenu.View(Idle with { Wait = waiting }).Lines[0].Text);
    }

    [Fact]
    public void An_alarm_from_the_menu_shows_the_time_it_rings_with_a_cancel_button_and_lights_the_button()
    {
        var view = WaitMenu.View(Idle with { QuickAlarmAt = Midnight + 8 * H });

        Assert.True(view.IsActive);
        Assert.Equal("Pause at 08:00", view.Lines[0].Text);
        Assert.Equal(new[] { new WaitMenuItem(WaitMenu.CancelAlarmId, "Cancel") }, view.Lines[0].Items);
    }

    [Fact]
    public void The_games_language_picks_the_menus_words()
    {
        var view = WaitMenu.View(Idle with { IsChinese = true, QuickAlarmAt = Midnight + 8 * H });

        Assert.Equal(WaitTexts.WaitButton.Zh, view.Tip);
        Assert.Equal(WaitTexts.AlarmAt.Zh + "08:00", view.Lines[0].Text);
        Assert.Equal(WaitTexts.Cancel.Zh, view.Lines[0].Items[0].Label);
        Assert.Equal(WaitTexts.Morning.Zh, view.Lines[1].Items[0].Label);
    }

    [Fact]
    public void Every_button_in_the_menu_does_something()
    {
        var waiting = new WaitUntil.Waiting(new WaitGoal.QueueDone(0f), Normal, GameTimer.DayClock);
        var view = WaitMenu.View(Idle with { Wait = waiting, QuickAlarmAt = Midnight + 8 * H, AlarmSecond = 7 * H });

        Assert.All(view.Lines.SelectMany(line => line.Items), item => Assert.NotNull(WaitMenu.ChoiceFor(item.Id)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void The_hour_buttons_wait_or_set_an_alarm_that_many_hours_on(int place, int hours)
    {
        var view = WaitMenu.View(Idle);

        Assert.Equal(new WaitChoice.WaitFor(WaitKind.Hours, hours), WaitMenu.ChoiceFor(view.Lines[1].Items[place - 1].Id));
        Assert.Equal(new WaitChoice.AlarmIn(hours), WaitMenu.ChoiceFor(view.Lines[2].Items[place - 1].Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(99)]
    public void An_id_no_button_has_does_nothing(int id)
    {
        Assert.Null(WaitMenu.ChoiceFor(id));
    }

    [Fact]
    public void Morning_is_the_next_seven_oclock()
    {
        var evening = new ClockReading(Midnight + 20 * H, 20 * H);

        Assert.Equal(new WaitGoal.At(Midnight + 31 * H, WaitKind.Morning), Goal(WaitKind.Morning, 0, evening));
        Assert.Equal(new WaitGoal.At(Midnight + 7 * H, WaitKind.Morning), Goal(WaitKind.Morning, 0, SixAm));
    }

    [Fact]
    public void The_alarm_target_is_the_next_time_the_clock_shows_the_alarm_and_nothing_without_one()
    {
        Assert.Equal(new WaitGoal.At(Midnight + 21 * H, WaitKind.AlarmHour), Goal(WaitKind.AlarmHour, 0, SixAm, alarmSecond: 21 * H));
        Assert.Null(Goal(WaitKind.AlarmHour, 0, SixAm));
    }

    [Fact]
    public void Hours_count_on_from_now()
    {
        Assert.Equal(new WaitGoal.At(SixAm.Total + 4 * H, WaitKind.Hours), Goal(WaitKind.Hours, 4, SixAm));
    }

    [Fact]
    public void A_brew_or_the_queue_with_nothing_to_wait_for_starts_no_wait()
    {
        Assert.Equal(new WaitGoal.At(Midnight + 9 * H, WaitKind.Brew), Goal(WaitKind.Brew, 0, SixAm, brewDoneAt: Midnight + 9 * H));
        Assert.Null(Goal(WaitKind.Brew, 0, SixAm));
        Assert.Equal(new WaitGoal.QueueDone(0f), Goal(WaitKind.Queue, 0, SixAm, isBusy: true));
        Assert.Null(Goal(WaitKind.Queue, 0, SixAm));
    }

    [Fact]
    public void The_pages_answer_carries_the_clicks_and_drops_anything_no_button_has()
    {
        Assert.True(WaitMenu.IsOk("ok"));
        Assert.True(WaitMenu.IsOk("ok:1,30"));
        Assert.False(WaitMenu.IsOk("error: boom"));
        Assert.False(WaitMenu.IsOk(null));
        Assert.Equal(new[] { 1, 30, 21 }, WaitMenu.Clicks("ok:1,30,x,99,21"));
        Assert.Empty(WaitMenu.Clicks("ok"));
        Assert.Empty(WaitMenu.Clicks("error: ok:1"));
    }

    [Fact]
    public void A_clock_that_only_ran_on_draws_the_same_menu_so_the_one_built_is_kept()
    {
        var waiting = new WaitUntil.Waiting(new WaitGoal.At(Midnight + 9 * H, WaitKind.AlarmHour), Normal, GameTimer.DayClock);
        var before = Idle with { Wait = waiting, QuickAlarmAt = Midnight + 8 * H };
        var later = before with { Reading = new ClockReading(SixAm.Total + H / 2, SixAm.SecondOfDay + H / 2) };

        Assert.True(WaitMenu.IsSameMenu(before, later));
        Assert.Equal(WaitMenu.Script(WaitMenu.View(before)), WaitMenu.Script(WaitMenu.View(later)));
    }

    [Fact]
    public void A_clock_reading_that_moved_the_time_of_day_against_the_game_clock_is_a_different_menu()
    {
        var before = Idle with { QuickAlarmAt = Midnight + 8 * H };
        var shifted = before with { Reading = new ClockReading(SixAm.Total, SixAm.SecondOfDay + H) };

        Assert.False(WaitMenu.IsSameMenu(before, shifted));
        Assert.NotEqual(WaitMenu.Script(WaitMenu.View(before)), WaitMenu.Script(WaitMenu.View(shifted)));
    }

    [Fact]
    public void An_alarm_set_or_a_change_of_language_is_a_different_menu()
    {
        Assert.False(WaitMenu.IsSameMenu(Idle, Idle with { QuickAlarmAt = Midnight + 8 * H }));
        Assert.False(WaitMenu.IsSameMenu(Idle, Idle with { IsChinese = true }));
        Assert.False(WaitMenu.IsSameMenu(Idle, Idle with { IsBrewing = false }));
    }

    private static string Labels(WaitMenuLine line) => string.Join("|", line.Items.Select(item => item.Label));

    private static WaitMenuItem Item(WaitMenuView view, int id) => view.Lines.SelectMany(line => line.Items).Single(item => item.Id == id);

    private static WaitGoal? Goal(WaitKind kind, int hours, ClockReading reading, int? alarmSecond = null, long? brewDoneAt = null, bool isBusy = false) =>
        WaitMenu.GoalFor(new WaitChoice.WaitFor(kind, hours), reading, alarmSecond, brewDoneAt, isBusy);
}
