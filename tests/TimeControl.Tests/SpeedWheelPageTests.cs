using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The mouse wheel over the speed buttons, run against the fake HUD pages the way the plugin sends it.</summary>
public class SpeedWheelPageTests
{
    // The fake page has no events of its own: listeners are kept on the element, and a wheel turn calls them in order.
    private const string WheelEvents =
        "FakeElement.prototype.addEventListener = function (type, listener, options) {" +
        "  (this.listeners = this.listeners || []).push({ type: type, listener: listener, options: options }); };" +
        "function wheelListeners(element) { return (element.listeners || []).filter(function (l) { return l.type === 'wheel'; }); }" +
        "function wheelOver(element, deltaY) {" +
        "  var e = { deltaY: deltaY, isPrevented: false, isStopped: false," +
        "    preventDefault: function () { this.isPrevented = true; }, stopPropagation: function () { this.isStopped = true; } };" +
        "  wheelListeners(element).forEach(function (l) { l.listener(e); });" +
        "  return e; }";

    private const string HomeWithGameButtons = WheelEvents + " var home = openHome({ hasOwnButtons: true }); var group = home.document.querySelector('.speed-controls');";

    private const int Up = -100;
    private const int Down = 100;

    private static readonly string On = SpeedWheel.Script(isOn: true);
    private static readonly string Off = SpeedWheel.Script(isOn: false);

    [Fact]
    public void The_games_speed_buttons_get_one_wheel_listener_that_may_stop_the_page_scrolling()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        Assert.Equal("ok", ui.Send(On));
        Assert.Equal("ok", ui.Send(On));
        Assert.Equal(1.0, ui.Number("wheelListeners(group).length"));
        Assert.Equal("false", ui.Text("wheelListeners(group)[0].options.passive"));
    }

    [Fact]
    public void Notches_up_count_up_and_notches_down_count_down_and_are_taken_once()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        ui.Send(On);
        ui.Run($"wheelOver(group, {Up}); wheelOver(group, {Up}); wheelOver(group, {Up}); wheelOver(group, {Down});");

        Assert.Equal("ok:2", ui.Send(On));
        Assert.Equal("ok", ui.Send(On));

        ui.Run($"wheelOver(group, {Down});");

        Assert.Equal("ok:-1", ui.Send(On));
    }

    [Fact]
    public void A_sideways_scroll_with_no_turn_counts_nothing()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        ui.Send(On);
        ui.Run("wheelOver(group, 0);");

        Assert.Equal("ok", ui.Send(On));
    }

    [Fact]
    public void While_on_a_turn_over_the_buttons_does_not_reach_the_camera()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        ui.Send(On);
        ui.Run($"var turn = wheelOver(group, {Up});");

        Assert.True(ui.Is("turn.isPrevented"));
        Assert.True(ui.Is("turn.isStopped"));
    }

    [Fact]
    public void While_off_a_turn_counts_nothing_and_goes_on_to_the_page()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        ui.Send(On);
        ui.Send(Off);
        ui.Run($"var turn = wheelOver(group, {Up});");

        Assert.False(ui.Is("turn.isPrevented"));
        Assert.False(ui.Is("turn.isStopped"));
        Assert.Equal("ok", ui.Send(Off));
        Assert.Equal("ok", ui.Send(On));
    }

    [Fact]
    public void The_mods_own_buttons_in_the_countdown_take_the_wheel_too()
    {
        var ui = new FakeWebUi(WheelEvents + " var countdown = openCountdown();");

        ui.Send(SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Normal, "")));
        ui.Send(On);
        ui.Run($"wheelOver(buttonsOn(countdown), {Up});");

        Assert.Equal(1.0, ui.Number("wheelListeners(buttonsOn(countdown)).length"));
        Assert.Equal("ok:1", ui.Send(On));
    }

    [Fact]
    public void Turns_on_both_hud_pages_add_up()
    {
        var ui = new FakeWebUi(HomeWithGameButtons + " var countdown = openCountdown();");

        ui.Send(SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Normal, "")));
        ui.Send(On);
        ui.Run($"wheelOver(group, {Up}); wheelOver(buttonsOn(countdown), {Up});");

        Assert.Equal("ok:2", ui.Send(On));
    }

    [Fact]
    public void Between_hookings_the_small_poll_takes_the_turns_on_both_pages_once()
    {
        var ui = new FakeWebUi(HomeWithGameButtons + " var countdown = openCountdown();");

        ui.Send(SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Normal, "")));
        ui.Send(On);
        ui.Run($"wheelOver(group, {Up}); wheelOver(group, {Up}); wheelOver(buttonsOn(countdown), {Down}); wheelOver(buttonsOn(countdown), {Up});");

        Assert.Equal("ok:2", ui.Send(SpeedWheel.PollScript));
        Assert.Equal("ok", ui.Send(SpeedWheel.PollScript));
        Assert.Equal("ok", ui.Send(On));
    }

    [Fact]
    public void The_poll_on_a_page_never_hooked_counts_nothing()
    {
        var ui = new FakeWebUi(HomeWithGameButtons);

        Assert.Equal("ok", ui.Send(SpeedWheel.PollScript));
    }

    [Theory]
    [InlineData("ok", true)]
    [InlineData("ok:-3", true)]
    [InlineData("error: TypeError", false)]
    [InlineData("no CoreUI1 frame", false)]
    [InlineData(null, false)]
    public void Only_a_reply_from_the_script_counts_as_the_page_taking_it(string? reply, bool expected)
    {
        Assert.Equal(expected, SpeedWheel.IsOk(reply));
    }

    [Theory]
    [InlineData("ok:-3", -3)]
    [InlineData("ok:4", 4)]
    [InlineData("ok", 0)]
    [InlineData("ok:many", 0)]
    [InlineData("error: ok:4", 0)]
    [InlineData(null, 0)]
    public void The_notches_are_read_from_the_reply(string? reply, int expected)
    {
        Assert.Equal(expected, SpeedWheel.Steps(reply));
    }
}
