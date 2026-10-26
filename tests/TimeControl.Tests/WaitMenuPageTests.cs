using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The wait button's scripts, run from the root page against the fake HUD pages the way the plugin sends them.</summary>
public class WaitMenuPageTests
{
    /// <summary>Reads the wait button back off a page.</summary>
    private const string Helpers =
        "function waitOn(p) { return p.document.getElementById('tc-wait'); }" +
        "function waitItem(p, id) { return waitOn(p).querySelectorAll('.tc-wait-item').filter(function (x) { return x.getAttribute('data-id') === String(id); })[0]; }" +
        "function waitRows(p) { return waitOn(p).querySelectorAll('.tc-wait-row'); }" +
        "function isWaitOpen(p) { return waitOn(p).classList.contains('tc-open'); }";

    private const string Home = Helpers + "var home = openHome({ hasOwnButtons: true });";

    private const string BothPages = Home + "var countdown = openCountdown(); countdown.bottomLeft.appendChild(new FakeElement('div', 'speed-controls'));";

    private const int H = ClockHour.Hour;
    private const long Midnight = 10 * ClockHour.Day;

    private static readonly WaitMenuInput Idle = new(
        HudPage.Home,
        WaitUntil.Start,
        new ClockReading(Midnight + 6 * H, 6 * H),
        QuickAlarmAt: null,
        AlarmSecond: null,
        IsBrewing: true,
        IsBusy: true,
        CanStartWait: true,
        IsChinese: false);

    private static readonly string Draw = WaitMenu.Script(WaitMenu.View(Idle));

    [Fact]
    public void The_wait_button_sits_at_the_end_of_the_games_speed_buttons_with_its_menu_closed()
    {
        var ui = new FakeWebUi(Home);

        Assert.Equal("ok", ui.Send(Draw));
        Assert.True(ui.Is("waitOn(home).parentNode === home.document.querySelector('.speed-controls')"));
        Assert.True(ui.Is("home.document.getElementById('tc-wait-style')"));
        Assert.Equal("Wait until", ui.Text("waitOn(home).querySelector('.tc-wait-tip').textContent"));
        Assert.Equal(3.0, ui.Number("waitRows(home).length"));
        Assert.False(ui.Is("isWaitOpen(home)"));
        Assert.False(ui.Is("waitOn(home).querySelector('.tc-wait-btn').classList.contains('active')"));
    }

    [Fact]
    public void A_page_without_speed_buttons_gets_no_wait_button()
    {
        var ui = new FakeWebUi(Helpers + "var home = openHome();");

        Assert.Equal("ok", ui.Send(Draw));
        Assert.False(ui.Is("waitOn(home)"));
    }

    [Fact]
    public void A_closed_page_answers_that_it_is_not_open()
    {
        var ui = new FakeWebUi(Helpers);

        var reply = ui.Send(Draw);

        Assert.Equal("no CoreUI1 frame", reply);
        Assert.False(WaitMenu.IsOk(reply));
    }

    [Fact]
    public void The_button_opens_the_menu_and_a_target_clicked_in_it_comes_back_once_and_closes_it()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(Draw);
        ui.Run("waitOn(home).querySelector('.tc-wait-btn').click();");

        Assert.True(ui.Is("isWaitOpen(home)"));

        ui.Run($"waitItem(home, {WaitMenu.Morning}).click();");

        Assert.False(ui.Is("isWaitOpen(home)"));
        Assert.Equal($"ok:{WaitMenu.Morning}", ui.Send(Draw));
        Assert.Equal("ok", ui.Send(Draw));
    }

    [Fact]
    public void A_greyed_out_target_does_nothing_when_clicked()
    {
        var ui = new FakeWebUi(Home);
        var draw = WaitMenu.Script(WaitMenu.View(Idle with { IsBrewing = false }));

        ui.Send(draw);
        ui.Run($"waitOn(home).querySelector('.tc-wait-btn').click(); waitItem(home, {WaitMenu.Brew}).click();");

        Assert.True(ui.Is($"waitItem(home, {WaitMenu.Brew}).disabled"));
        Assert.True(ui.Is("isWaitOpen(home)"));
        Assert.Equal("ok", ui.Send(draw));
    }

    [Fact]
    public void Drawing_the_same_menu_again_keeps_it_open_and_its_rows_in_place()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(Draw);
        ui.Run("waitOn(home).querySelector('.tc-wait-btn').click(); var firstRow = waitRows(home)[0];");
        ui.Send(Draw);

        Assert.True(ui.Is("isWaitOpen(home)"));
        Assert.True(ui.Is("waitRows(home)[0] === firstRow"));
        Assert.Equal(1.0, ui.Number("home.document.querySelectorAll('#tc-wait').length"));
    }

    [Fact]
    public void A_running_wait_lights_the_button_and_adds_its_row_with_a_cancel_button()
    {
        var ui = new FakeWebUi(Home);
        var waiting = new WaitUntil.Waiting(new WaitGoal.At(Midnight + 7 * H, WaitKind.Morning), new GameSpeed(PauseToggle.Normal, FastGear.GamesOwn), GameTimer.DayClock);

        ui.Send(Draw);
        ui.Send(WaitMenu.Script(WaitMenu.View(Idle with { Wait = waiting })));

        Assert.True(ui.Is("waitOn(home).querySelector('.tc-wait-btn').classList.contains('active')"));
        Assert.Equal(4.0, ui.Number("waitRows(home).length"));
        Assert.Equal("Waiting: Morning", ui.Text("waitRows(home)[0].querySelector('.tc-wait-text').textContent"));
        Assert.True(ui.Is($"waitItem(home, {WaitMenu.CancelWaitId})"));

        ui.Send(Draw);

        Assert.False(ui.Is("waitOn(home).querySelector('.tc-wait-btn').classList.contains('active')"));
        Assert.Equal(3.0, ui.Number("waitRows(home).length"));
    }

    [Fact]
    public void A_click_on_a_button_the_page_lost_is_never_applied()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(Draw);
        ui.Run($"waitItem(home, {WaitMenu.Morning}).click(); var lost = waitOn(home); lost.parentNode.removeChild(lost);");

        Assert.Equal("ok", ui.Send(Draw));
        Assert.True(ui.Is("waitOn(home)"));
    }

    [Fact]
    public void Speed_buttons_that_go_away_take_the_wait_button_and_its_clicks_with_them()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(Draw);
        ui.Run($"waitItem(home, {WaitMenu.Morning}).click(); var group = home.document.querySelector('.speed-controls'); group.parentNode.removeChild(group);");

        Assert.Equal("ok", ui.Send(Draw));
        Assert.False(ui.Is("waitOn(home)"));
    }

    [Fact]
    public void In_the_countdown_the_button_moves_off_the_home_page_and_its_menu_opens_to_the_right()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);

        Assert.Equal("ok", ui.Send(WaitMenu.Script(WaitMenu.View(Idle with { Page = HudPage.Countdown }))));
        Assert.False(ui.Is("waitOn(home)"));
        Assert.True(ui.Is("waitOn(countdown).parentNode === countdown.document.querySelector('.speed-controls')"));
        Assert.True(ui.Is("waitOn(countdown).classList.contains('tc-left')"));
    }

    [Fact]
    public void At_home_the_menu_opens_to_the_left()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(Draw);

        Assert.False(ui.Is("waitOn(home).classList.contains('tc-left')"));
    }

    [Fact]
    public void Removal_takes_the_wait_button_off_every_page()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Send(WaitMenu.Script(WaitMenu.View(Idle with { Page = HudPage.Countdown })));
        ui.Run("home.document.querySelector('.speed-controls').appendChild(home.document.createElement('div')).id = 'tc-wait';");

        Assert.Equal("ok", ui.Send(WaitMenu.RemovalScript()));
        Assert.False(ui.Is("waitOn(home)"));
        Assert.False(ui.Is("waitOn(countdown)"));
    }

    [Fact]
    public void Chinese_words_reach_the_page_as_written()
    {
        var ui = new FakeWebUi(Home);

        ui.Send(WaitMenu.Script(WaitMenu.View(Idle with { IsChinese = true })));

        Assert.Equal(WaitTexts.WaitButton.Zh, ui.Text("waitOn(home).querySelector('.tc-wait-tip').textContent"));
        Assert.Equal(WaitTexts.Morning.Zh, ui.Text($"waitItem(home, {WaitMenu.Morning}).textContent"));
    }

    [Fact]
    public void Between_draws_the_small_script_hands_back_a_click_once()
    {
        var ui = new FakeWebUi(Home);
        var collect = WaitMenu.CollectScript(HudPage.Home);

        ui.Send(Draw);
        ui.Run($"waitOn(home).querySelector('.tc-wait-btn').click(); waitItem(home, {WaitMenu.Morning}).click();");

        Assert.Equal($"ok:{WaitMenu.Morning}", ui.Send(collect));
        Assert.Equal("ok", ui.Send(collect));
    }

    [Fact]
    public void A_page_that_lost_the_wait_button_answers_so_and_drops_its_clicks()
    {
        var ui = new FakeWebUi(Home);
        var collect = WaitMenu.CollectScript(HudPage.Home);

        ui.Send(Draw);
        ui.Run($"waitOn(home).querySelector('.tc-wait-btn').click(); waitItem(home, {WaitMenu.Morning}).click();" +
               "waitOn(home).parentNode.removeChild(waitOn(home));");

        var reply = ui.Send(collect);

        Assert.Equal(WaitMenu.NotOnPage, reply);
        Assert.False(WaitMenu.IsOk(reply));
        Assert.Equal("ok", ui.Send(Draw));
        Assert.True(ui.Is("waitOn(home)"));
    }

    [Fact]
    public void A_page_with_no_speed_buttons_has_no_wait_button_to_miss()
    {
        var ui = new FakeWebUi(Helpers + "var home = openHome();");

        Assert.Equal("ok", ui.Send(WaitMenu.CollectScript(HudPage.Home)));
    }

    [Fact]
    public void Collecting_with_the_page_closed_answers_that_it_is_not_open()
    {
        var ui = new FakeWebUi(Helpers);

        Assert.True(HudPages.IsClosedReply(ui.Send(WaitMenu.CollectScript(HudPage.Home))));
    }
}
