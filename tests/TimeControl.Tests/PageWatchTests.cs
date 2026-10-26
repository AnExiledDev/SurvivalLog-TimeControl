using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// The page watch, run from the root page against the fake HUD pages the way the plugin sends it. The mod sends its
/// badge, chip, notes and buttons again only when the watch reads a change, so a change it misses leaves them off the
/// page, and a change it reads where there is none sends them all again.
/// </summary>
public class PageWatchPageTests
{
    private const string Pages =
        "var homeState = { i18n_speedSlow: 'Slow', i18n_speedFast: 'Fast', i18n_hotkey: 'Key' };" +
        "var countdown = openCountdown(); var home = openHome({ hasOwnButtons: true, state: homeState });" +
        "function frameOf(p) { return document.body.querySelectorAll('iframe').filter(function (f) { return f.contentWindow === p; })[0]; }";

    private static PageLook Look(FakeWebUi ui) => PageWatch.Read(ui.Send(PageWatch.Script)) ?? throw new Xunit.Sdk.XunitException("the page watch did not answer");

    [Fact]
    public void A_page_nothing_happened_to_reads_the_same_on_every_look()
    {
        var ui = new FakeWebUi(Pages);

        Assert.Equal(Look(ui).Hash, Look(ui).Hash);
    }

    [Fact]
    public void A_hud_page_that_reloaded_reads_as_changed_even_where_it_looks_the_same()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("document.body.removeChild(frameOf(home)); home = openHome({ hasOwnButtons: true, state: homeState });");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void A_reload_of_the_whole_web_view_reads_as_changed_though_it_counts_its_frames_from_the_start_again()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("document.body.querySelectorAll('iframe').forEach(function (f) { document.body.removeChild(f); });" +
               "delete document.__tcRoot; delete document.__tcTagN;" +
               "countdown = openCountdown(); home = openHome({ hasOwnButtons: true, state: homeState });");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void The_game_rebuilding_the_group_the_mods_things_hang_from_reads_as_changed()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("home.document.body.removeChild(home.middle);" +
               "home.middle = home.document.body.appendChild(new FakeElement('div', 'action-group-middle'));" +
               "home.queue = home.middle.appendChild(new FakeElement('div', 'queue-flow-wrapper')); showOwnButtons(home);");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void A_speed_button_that_moved_reads_as_changed()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("ownButtonsOn(home)[1].offsetLeft += 10;");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void The_mods_buttons_read_as_a_change_once_and_drawing_the_same_buttons_again_does_not()
    {
        var ui = new FakeWebUi(Pages);
        var draw = SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Normal, ""));
        var before = Look(ui);

        Assert.Equal("ok", ui.Send(draw));

        var drawn = Look(ui);

        Assert.NotEqual(before.Hash, drawn.Hash);
        Assert.Equal("ok", ui.Send(draw));
        Assert.Equal(drawn.Hash, Look(ui).Hash);
    }

    [Fact]
    public void The_bottom_left_buttons_hiding_reads_as_changed()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("countdown.bottomLeft.isHidden = true;");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void The_slow_text_comes_back_on_its_own_and_the_mod_writing_it_is_no_change()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        Assert.Equal("Slow", before.SlowText);

        ui.Run("homeState.i18n_speedSlow = 'Slow 0.25x';");

        var written = Look(ui);

        Assert.Equal("Slow 0.25x", written.SlowText);
        Assert.Equal(before.Hash, written.Hash);
    }

    [Fact]
    public void A_text_the_tooltips_read_changing_reads_as_changed()
    {
        var ui = new FakeWebUi(Pages);
        var before = Look(ui);

        ui.Run("homeState.i18n_speedFast = 'Quick';");

        Assert.NotEqual(before.Hash, Look(ui).Hash);
    }

    [Fact]
    public void With_no_home_page_open_the_slow_text_is_empty()
    {
        var ui = new FakeWebUi("var countdown = openCountdown();");

        Assert.Equal("", Look(ui).SlowText);
    }
}

/// <summary>What the plugin makes of the watch's answers, and when that counts as the page having changed.</summary>
public class PageWatchTests
{
    [Fact]
    public void An_answer_reads_as_its_fingerprint_and_the_slow_text_after_the_first_bar()
    {
        Assert.Equal(new PageLook(-42, "Slow|0.25x"), PageWatch.Read("ok:-42|Slow|0.25x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("error: TypeError")]
    [InlineData("ok:12")]
    [InlineData("ok:x|Slow")]
    public void An_answer_that_is_not_a_look_reads_as_none(string? reply)
    {
        Assert.Null(PageWatch.Read(reply));
    }

    [Fact]
    public void The_same_fingerprint_keeps_the_generation_and_takes_the_new_slow_text()
    {
        var seen = PageWatch.Next(PageGeneration.Start, new PageLook(7, "Slow"));
        var next = PageWatch.Next(seen, new PageLook(7, "Slow 0.5x"));

        Assert.Equal(seen.Number, next.Number);
        Assert.Equal("Slow 0.5x", next.Last?.SlowText);
    }

    [Fact]
    public void A_new_fingerprint_or_a_look_that_could_not_be_read_is_a_new_generation()
    {
        var seen = PageWatch.Next(PageGeneration.Start, new PageLook(7, ""));

        Assert.Equal(seen.Number + 1, PageWatch.Next(seen, new PageLook(8, "")).Number);
        Assert.Equal(seen.Number + 1, PageWatch.Next(seen, null).Number);
        Assert.NotEqual(PageGeneration.Start.Number, seen.Number);
    }

    [Fact]
    public void After_a_look_that_could_not_be_read_the_next_good_look_is_a_new_generation_too()
    {
        var lost = PageWatch.Next(PageWatch.Next(PageGeneration.Start, new PageLook(7, "")), null);

        Assert.Equal(lost.Number + 1, PageWatch.Next(lost, new PageLook(7, "")).Number);
    }
}

/// <summary>
/// Which answers settle what was sent. A page that is not open answers so, and the mod waits for the page watch to
/// see it open rather than sending again on every refresh; an error is not settled, so it is sent again.
/// </summary>
public class SettledReplyTests
{
    [Fact]
    public void A_badge_sent_while_its_hud_page_is_closed_is_settled()
    {
        var ui = new FakeWebUi("var speedUp = openSpeedUp();");

        var reply = ui.Send(PauseBadge.Script("Paused", HudPage.Home));

        Assert.True(HudPages.IsClosedReply(reply));
        Assert.True(HudPages.IsSettled(reply));
    }

    [Fact]
    public void Buttons_sent_while_the_countdown_page_is_closed_are_settled()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.True(HudPages.IsSettled(ui.Send(SpeedButtons.Script(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Normal, "")))));
    }

    [Fact]
    public void A_badge_the_page_took_is_settled()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.True(HudPages.IsSettled(ui.Send(PauseBadge.Script("Paused", HudPage.Home))));
    }

    [Theory]
    [InlineData("error: TypeError")]
    [InlineData("no SpeedUp frame")]
    [InlineData("ok:3")]
    [InlineData(null)]
    public void Errors_and_other_answers_are_not_settled(string? reply)
    {
        Assert.False(HudPages.IsSettled(reply));
        Assert.False(HudPages.IsClosedReply(reply));
    }
}
