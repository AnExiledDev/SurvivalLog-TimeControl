using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// The pause badge on the HUD pages, run from the root page the way the plugin sends it. It sits beside the speed
/// buttons, not above them where it covered the row of suggested actions, and it never takes a click.
/// </summary>
public class PauseBadgePageTests
{
    private static readonly SpeedButtonsView Countdown = new(HudPage.Countdown, PauseToggle.Paused, "");

    [Fact]
    public void At_home_the_badge_sits_right_of_the_games_own_speed_buttons_not_above_them()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        Assert.Equal("ok", ui.Send(PauseBadge.Script("Paused", HudPage.Home)));
        Assert.True(ui.Is("badgeOn(home).parentNode.classList.contains('speed-controls')"));
        Assert.Equal("calc(100% + 6px)", ui.Text("inlineStyle(badgeOn(home), 'left')"));
        Assert.Equal("50%", ui.Text("inlineStyle(badgeOn(home), 'top')"));
        Assert.Equal("auto", ui.Text("inlineStyle(badgeOn(home), 'bottom')"));
    }

    [Fact]
    public void In_the_countdown_the_badge_sits_right_of_the_mods_own_speed_buttons()
    {
        var ui = new FakeWebUi("var countdown = openCountdown(); var home = openHome();");

        ui.Send(SpeedButtons.Script(Countdown));

        Assert.Equal("ok", ui.Send(PauseBadge.Script("Paused: idle", HudPage.Countdown)));
        Assert.True(ui.Is("badgeOn(countdown).parentNode === buttonsOn(countdown)"));
        Assert.Equal("calc(100% + 6px)", ui.Text("inlineStyle(badgeOn(countdown), 'left')"));
        Assert.Equal("auto", ui.Text("inlineStyle(badgeOn(countdown), 'bottom')"));
        Assert.False(ui.Is("badgeOn(home)"));
    }

    [Theory]
    [InlineData("openHome({ hasOwnButtons: true })")]
    [InlineData("openHome()")]
    public void The_badge_is_see_through_and_never_takes_a_click_wherever_it_sits(string openPage)
    {
        var ui = new FakeWebUi($"var home = {openPage};");

        ui.Send(PauseBadge.Script("Paused: wave", HudPage.Home));

        Assert.Equal("none", ui.Text("inlineStyle(badgeOn(home), 'pointer-events')"));
        Assert.InRange(ui.Number("inlineStyle(badgeOn(home), 'opacity')"), 0.3, 0.9);
        Assert.False(ui.Is("badgeOn(home).getAttribute('data-interactive') !== null"));
        Assert.False(ui.Is("badgeOn(home).classList.contains('unity-interactive')"));
    }

    [Fact]
    public void Where_the_page_has_no_speed_buttons_the_badge_sits_at_the_top_centre()
    {
        var ui = new FakeWebUi("var home = openHome();");

        ui.Send(PauseBadge.Script("Paused", HudPage.Home));

        Assert.True(ui.Is("badgeOn(home).parentNode === home.document.body"));
        Assert.Equal("12px", ui.Text("inlineStyle(badgeOn(home), 'top')"));
    }

    [Fact]
    public void Time_running_again_takes_the_badge_off()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        ui.Send(PauseBadge.Script("Paused", HudPage.Home));

        Assert.Equal("ok", ui.Send(PauseBadge.Script("", HudPage.Home)));
        Assert.False(ui.Is("badgeOn(home)"));
    }
}

/// <summary>
/// The style that makes the game's own "paused" strip see-through, so the phone's reply choices under it can be read,
/// and keeps it out of the way of clicks.
/// </summary>
public class PauseOverlayPageTests
{
    private const string AllPages = "var speedUp = openSpeedUp(); var countdown = openCountdown(); var home = openHome();";
    private const string PauseRule = ".speedup-strip.is-pause";

    private static readonly string Half = PauseOverlay.Script(PauseOverlay.Css(0.5f));

    [Fact]
    public void The_style_goes_on_the_games_pause_strip_page_and_no_other()
    {
        var ui = new FakeWebUi(AllPages);

        Assert.Equal("ok", ui.Send(Half));
        Assert.True(ui.Is("overlayStyleOn(speedUp)"));
        Assert.False(ui.Is("overlayStyleOn(home)"));
        Assert.False(ui.Is("overlayStyleOn(countdown)"));
    }

    [Fact]
    public void The_paused_strip_turns_see_through_and_lets_clicks_through()
    {
        var ui = new FakeWebUi(AllPages);

        ui.Send(Half);

        Assert.Equal("0.5!important", ui.Text($"sheetStyle(overlayStyleOn(speedUp), '{PauseRule}', 'opacity')"));
        Assert.Equal("none!important", ui.Text($"sheetStyle(overlayStyleOn(speedUp), '{PauseRule}', 'pointer-events')"));
    }

    [Fact]
    public void Only_the_paused_strip_fades_not_the_games_sleep_or_slowdown_strip()
    {
        var css = PauseOverlay.Css(0.5f);
        var selectors = css.Split('}', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(rule => rule.Substring(0, rule.IndexOf('{')).Split(','))
            .Select(selector => selector.Trim())
            .ToList();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, selector => Assert.Equal(PauseRule, selector));
    }

    [Fact]
    public void A_page_whose_name_only_ends_in_SpeedUp_is_not_taken_for_the_strip_page()
    {
        var ui = new FakeWebUi("var other = openFrame('CompostSpeedUp', null); var speedUp = openSpeedUp();");

        Assert.Equal("ok", ui.Send(Half));
        Assert.False(ui.Is("overlayStyleOn(other)"));
        Assert.True(ui.Is("overlayStyleOn(speedUp)"));
    }

    [Fact]
    public void Sending_on_every_refresh_keeps_one_style_and_follows_a_new_opacity()
    {
        var ui = new FakeWebUi(AllPages);

        ui.Send(Half);
        ui.Send(Half);

        Assert.Equal("ok", ui.Send(PauseOverlay.Script(PauseOverlay.Css(0.25f))));
        Assert.Equal(1.0, ui.Number("speedUp.document.querySelectorAll('style').length"));
        Assert.Equal("0.25!important", ui.Text($"sheetStyle(overlayStyleOn(speedUp), '{PauseRule}', 'opacity')"));
    }

    [Fact]
    public void Taking_the_style_off_leaves_the_strip_as_the_game_draws_it()
    {
        var ui = new FakeWebUi(AllPages);

        ui.Send(Half);

        Assert.Equal("ok", ui.Send(PauseOverlay.Script("")));
        Assert.False(ui.Is("overlayStyleOn(speedUp)"));
        Assert.Equal(0.0, ui.Number("speedUp.document.querySelectorAll('style').length"));
    }

    [Fact]
    public void With_the_strip_page_closed_there_is_nothing_to_change_and_that_is_no_error()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.Equal("ok", ui.Send(Half));
        Assert.Equal("ok", ui.Send(PauseOverlay.Script("")));
        Assert.False(ui.Is("overlayStyleOn(home)"));
    }

    [Fact]
    public void Fully_opaque_leaves_the_games_strip_alone()
    {
        Assert.Equal("", PauseOverlay.Css(1f));
        Assert.Equal("", PauseOverlay.Css(3f));
    }

    [Fact]
    public void Opacity_is_kept_between_hidden_and_as_drawn_and_a_bad_number_keeps_the_default()
    {
        Assert.Equal(0f, PauseOverlay.ClampOpacity(-1f));
        Assert.Equal(1f, PauseOverlay.ClampOpacity(2f));
        Assert.Equal(PauseOverlay.DefaultOpacity, PauseOverlay.ClampOpacity(float.NaN));
        Assert.Contains("opacity:0!important", PauseOverlay.Css(0f));
    }
}
