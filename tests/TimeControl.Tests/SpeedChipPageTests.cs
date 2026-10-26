using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// The chip on the slow or fastest speed button naming the speed the mod holds there, so a second press of the same
/// key shows which speed is in use. Run from the root page the way the plugin sends it.
/// </summary>
public class SpeedChipPageTests
{
    private static readonly SpeedChipView Fast = new(PauseToggle.Triple, "6x");
    private static readonly SpeedChipView Slow = new(PauseToggle.Slow, "0.25x");

    [Fact]
    public void A_held_fast_speed_shows_on_the_top_right_corner_of_the_games_fastest_button()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        Assert.Equal("ok", ui.Send(SpeedChip.Script(Fast)));
        Assert.Equal("6x", ui.Text("chipOn(home).textContent"));
        Assert.True(ui.Is("chipOn(home).parentNode.classList.contains('speed-controls')"));
        Assert.Equal("131px", ui.Text("inlineStyle(chipOn(home), 'left')"));
        Assert.Equal("5px", ui.Text("inlineStyle(chipOn(home), 'top')"));
    }

    [Fact]
    public void A_held_slow_speed_shows_on_the_games_slow_button()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        ui.Send(SpeedChip.Script(Slow));

        Assert.Equal("0.25x", ui.Text("chipOn(home).textContent"));
        Assert.Equal("29px", ui.Text("inlineStyle(chipOn(home), 'left')"));
    }

    [Fact]
    public void Without_the_games_slow_button_the_fast_chip_still_finds_the_fastest_button_and_a_slow_chip_shows_nowhere()
    {
        var ui = new FakeWebUi("var home = openHome(); showOwnButtons(home, true);");

        ui.Send(SpeedChip.Script(Fast));

        Assert.Equal("97px", ui.Text("inlineStyle(chipOn(home), 'left')"));

        ui.Send(SpeedChip.Script(Slow));

        Assert.False(ui.Is("chipOn(home)"));
    }

    [Fact]
    public void In_the_countdown_the_chip_goes_on_the_mods_own_fastest_button()
    {
        var ui = new FakeWebUi("var countdown = openCountdown(); var home = openHome({ hasOwnButtons: true });");

        ui.Send(SpeedChip.Script(Fast, HudPage.Home));
        ui.Send(SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Triple, "")));
        ui.Run("buttonFor(countdown, 3).offsetLeft = 70; buttonFor(countdown, 3).offsetWidth = 30;");

        Assert.Equal("ok", ui.Send(SpeedChip.Script(Fast, HudPage.Countdown)));
        Assert.True(ui.Is("chipOn(countdown).parentNode === buttonsOn(countdown)"));
        Assert.Equal("97px", ui.Text("inlineStyle(chipOn(countdown), 'left')"));
        Assert.False(ui.Is("chipOn(home)"));
    }

    [Fact]
    public void The_chip_never_takes_a_click()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        ui.Send(SpeedChip.Script(Fast));

        Assert.Equal("none", ui.Text("inlineStyle(chipOn(home), 'pointer-events')"));
        Assert.False(ui.Is("chipOn(home).getAttribute('data-interactive') !== null"));
    }

    [Fact]
    public void Leaving_the_held_speed_takes_the_chip_off()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        ui.Send(SpeedChip.Script(Fast));

        Assert.Equal("ok", ui.Send(SpeedChip.Script(null)));
        Assert.False(ui.Is("chipOn(home)"));
    }

    [Fact]
    public void A_page_with_no_speed_buttons_gets_no_chip()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.Equal("ok", ui.Send(SpeedChip.Script(Fast)));
        Assert.False(ui.Is("chipOn(home)"));
    }
}

/// <summary>Which speed gets the chip: only one the mod holds, and never while paused.</summary>
public class SpeedChipViewTests
{
    [Fact]
    public void A_fast_gear_above_the_games_high_speed_shows_the_speed_it_really_runs_at()
    {
        Assert.Equal(new SpeedChipView(PauseToggle.Triple, "6x"), SpeedChip.View(isPaused: false, heldSlow: null, heldFast: 6f));
    }

    [Fact]
    public void Holding_the_games_speed_up_key_above_its_own_speed_shows_the_speed_on_the_fastest_button()
    {
        var fast = SpeedChip.HoldKeyFast(isHoldKeyRunning: true, gameFastSpeed: 3, multiple: 2f);

        Assert.Equal(6f, fast);
        Assert.Equal(new SpeedChipView(PauseToggle.Triple, "6x"), SpeedChip.View(isPaused: false, heldSlow: null, heldFast: fast));
    }

    [Fact]
    public void The_games_speed_up_key_at_its_own_speed_or_let_go_gets_no_chip()
    {
        Assert.Null(SpeedChip.HoldKeyFast(isHoldKeyRunning: true, gameFastSpeed: 3, multiple: 1f));
        Assert.Null(SpeedChip.HoldKeyFast(isHoldKeyRunning: false, gameFastSpeed: 3, multiple: 2f));
    }

    [Fact]
    public void A_held_slow_speed_shows_on_the_slow_button()
    {
        Assert.Equal(new SpeedChipView(PauseToggle.Slow, "0.25x"), SpeedChip.View(isPaused: false, heldSlow: 0.25f, heldFast: null));
    }

    [Fact]
    public void The_games_own_speeds_and_a_pause_get_no_chip()
    {
        Assert.Null(SpeedChip.View(isPaused: false, heldSlow: null, heldFast: null));
        Assert.Null(SpeedChip.View(isPaused: true, heldSlow: null, heldFast: 6f));
        Assert.Null(SpeedChip.View(isPaused: true, heldSlow: 0.5f, heldFast: null));
    }

    [Fact]
    public void A_shown_chip_is_resent_once_the_page_changed_and_an_unconfirmed_removal_until_confirmed()
    {
        var fast = new SpeedChipView(PauseToggle.Triple, "6x");

        Assert.True(SpeedChip.ShouldSend(fast, null, isLastSentConfirmed: true, isRefreshDue: false, hasPageChanged: false));
        Assert.False(SpeedChip.ShouldSend(fast, fast, isLastSentConfirmed: true, isRefreshDue: false, hasPageChanged: false));
        Assert.False(SpeedChip.ShouldSend(fast, fast, isLastSentConfirmed: true, isRefreshDue: true, hasPageChanged: false));
        Assert.True(SpeedChip.ShouldSend(fast, fast, isLastSentConfirmed: true, isRefreshDue: false, hasPageChanged: true));
        Assert.True(SpeedChip.ShouldSend(null, null, isLastSentConfirmed: false, isRefreshDue: true, hasPageChanged: false));
        Assert.False(SpeedChip.ShouldSend(null, null, isLastSentConfirmed: true, isRefreshDue: true, hasPageChanged: true));
    }

    [Fact]
    public void A_chip_taken_off_with_no_hud_page_open_is_confirmed_rather_than_sent_again()
    {
        var ui = new FakeWebUi("var speedUp = openSpeedUp();");

        Assert.Equal("ok", ui.Send(SpeedChip.Script(null, HudPage.Countdown)));
    }
}
