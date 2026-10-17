using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The speed button scripts, run from the root page against the fake HUD pages the way the plugin sends them.</summary>
public class SpeedButtonsPageTests
{
    private const string BothPages = "var countdown = openCountdown(); var home = openHome();";

    private static readonly SpeedButtonsView Countdown = new(HudPage.Countdown, PauseToggle.Normal, "");
    private static readonly SpeedButtonsView Expedition = new(HudPage.Home, PauseToggle.Normal, "");

    private static readonly string Draw = SpeedButtons.Script(Countdown);
    private static readonly string Collect = SpeedButtons.CollectScript(HudPage.Countdown);

    [Fact]
    public void In_the_countdown_the_five_buttons_appear_in_the_games_order_on_the_countdown_page_only()
    {
        var ui = new FakeWebUi(BothPages);

        Assert.Equal("ok", ui.Send(Draw));
        Assert.Equal("4,1,2,3,0", ui.Text("levelsOn(countdown)"));
        Assert.True(ui.Is("countdown.document.getElementById('tc-speed-style')"));
        Assert.False(ui.Is("buttonsOn(home)"));
    }

    [Fact]
    public void Drawing_again_keeps_one_set_of_buttons_in_place()
    {
        var ui = new FakeWebUi("var home = openHome();");

        ui.Send(SpeedButtons.Script(Expedition));

        Assert.Equal("ok", ui.Send(SpeedButtons.Script(Expedition)));
        Assert.Equal("4,1,2,3,0", ui.Text("levelsOn(home)"));
        Assert.Equal(1.0, ui.Number("home.document.querySelectorAll('.speed-controls').length"));
    }

    [Fact]
    public void Clicks_come_back_in_the_order_made_and_only_once()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Run("buttonFor(countdown, 4).click(); buttonFor(countdown, 0).click();");

        Assert.Equal("ok:4,0", ui.Send(Collect));
        Assert.Equal("ok", ui.Send(Collect));
    }

    [Fact]
    public void A_full_draw_hands_back_the_clicks_too_and_only_once()
    {
        var ui = new FakeWebUi("var home = openHome();");
        var draw = SpeedButtons.Script(Expedition);

        ui.Send(draw);
        ui.Run("buttonFor(home, 2).click();");

        Assert.Equal("ok:2", ui.Send(draw));
        Assert.Equal("ok", ui.Send(draw));
    }

    [Fact]
    public void A_click_is_never_lost_to_a_draw_that_fails()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Run("buttonFor(countdown, 3).click(); countdown.getComputedStyle = function () { throw new Error('layout failed'); };");

        Assert.StartsWith("error:", ui.Send(Draw));
        Assert.Equal("ok:3", ui.Send(Collect));
    }

    [Fact]
    public void Clicks_on_buttons_the_page_lost_are_dropped_when_they_are_drawn_again()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Run("buttonFor(countdown, 2).click(); loseButtons(countdown);");

        Assert.Equal("ok", ui.Send(Draw));
        Assert.Equal("ok", ui.Send(Collect));
    }

    [Fact]
    public void Collecting_from_a_page_that_lost_the_buttons_asks_for_a_redraw_and_drops_their_clicks()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Run("buttonFor(countdown, 1).click(); loseButtons(countdown);");

        Assert.Equal(SpeedButtons.NotOnPage, ui.Send(Collect));

        ui.Send(Draw);

        Assert.Equal("ok", ui.Send(Collect));
    }

    [Fact]
    public void Where_the_game_shows_its_own_buttons_the_copy_is_taken_away()
    {
        var ui = new FakeWebUi("var home = openHome();");

        ui.Send(SpeedButtons.Script(Expedition));
        ui.Run("showOwnButtons(home);");

        Assert.Equal("ok", ui.Send(SpeedButtons.Script(Expedition)));
        Assert.False(ui.Is("buttonsOn(home)"));
        Assert.Equal("ok", ui.Send(SpeedButtons.CollectScript(HudPage.Home)));
    }

    [Fact]
    public void Never_drawn_beside_the_games_own_buttons()
    {
        var ui = new FakeWebUi("var home = openHome({ hasOwnButtons: true });");

        Assert.Equal("ok", ui.Send(SpeedButtons.Script(Expedition)));
        Assert.False(ui.Is("buttonsOn(home)"));
    }

    [Fact]
    public void Only_the_button_for_the_views_level_is_lit()
    {
        var ui = new FakeWebUi("var home = openHome();");

        ui.Send(SpeedButtons.Script(Expedition with { ActiveLevel = PauseToggle.Paused }));

        Assert.Equal("0", ui.Text("litOn(home)"));

        ui.Send(SpeedButtons.Script(Expedition with { ActiveLevel = PauseToggle.Double }));

        Assert.Equal("2", ui.Text("litOn(home)"));
    }

    [Fact]
    public void On_an_expedition_the_buttons_sit_in_the_order_queue_where_the_games_own_would()
    {
        var ui = new FakeWebUi("var home = openHome();");

        ui.Send(SpeedButtons.Script(Expedition));

        Assert.True(ui.Is("buttonsOn(home).parentNode === home.queue"));
        Assert.Equal("", ui.Text("buttonsOn(home).style.cssText"));
    }

    [Fact]
    public void Without_the_order_queue_the_buttons_sit_in_the_middle_action_group()
    {
        var ui = new FakeWebUi("var home = openHome({ hasNoQueue: true });");

        ui.Send(SpeedButtons.Script(Expedition));

        Assert.True(ui.Is("buttonsOn(home).parentNode === home.middle"));
    }

    [Fact]
    public void In_the_countdown_the_buttons_sit_just_right_of_the_bottom_left_buttons()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);

        var css = ui.Text("buttonsOn(countdown).style.cssText");

        Assert.Contains("position:fixed;bottom:30px;left:244px;", css);
        Assert.DoesNotContain("display:none", css);
    }

    [Fact]
    public void In_the_countdown_the_buttons_hide_with_the_bottom_left_buttons_in_the_planning_view()
    {
        var ui = new FakeWebUi("var countdown = openCountdown({ isPlanning: true });");

        ui.Send(Draw);

        Assert.Contains("display:none", ui.Text("buttonsOn(countdown).style.cssText"));
    }

    [Fact]
    public void Tooltips_use_the_pages_own_texts_and_key_names()
    {
        var ui = new FakeWebUi("var home = openHome({ state: { i18n_speedPause: 'Pausa', i18n_hotkey: 'Tecla', i18n_hotkeySpeedPause: 'Espacio' } });");

        ui.Send(SpeedButtons.Script(Expedition));

        Assert.Equal("Pausa (Tecla:Espacio)", ui.Text("tooltipFor(home, 0)"));
        Assert.Equal("Normal", ui.Text("tooltipFor(home, 1)"));
    }

    [Fact]
    public void Without_page_texts_the_tooltips_are_english_and_the_slow_one_names_the_speed_held()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(SpeedButtons.Script(Countdown with { SlowLabel = "0.25x" }));

        Assert.Equal("Slow 0.25x", ui.Text("tooltipFor(countdown, 4)"));
        Assert.Equal("Fastest", ui.Text("tooltipFor(countdown, 3)"));
    }

    [Fact]
    public void Without_page_texts_a_chinese_game_gets_the_games_own_chinese_speed_names()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(SpeedButtons.Script(Countdown with { SlowLabel = "0.25x", IsChinese = true }));

        Assert.Equal("慢速 0.25x", ui.Text("tooltipFor(countdown, 4)"));
        Assert.Equal("极速", ui.Text("tooltipFor(countdown, 3)"));
    }

    [Fact]
    public void Switching_the_game_to_chinese_renames_the_buttons_on_the_next_draw()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Send(SpeedButtons.Script(Countdown with { IsChinese = true }));

        Assert.Equal("暂停", ui.Text("tooltipFor(countdown, 0)"));
    }

    [Fact]
    public void A_slow_label_cannot_run_as_script()
    {
        var ui = new FakeWebUi(BothPages);

        Assert.Equal("ok", ui.Send(SpeedButtons.Script(Countdown with { SlowLabel = "0.5x');broken=1;//" })));
        Assert.False(ui.Is("typeof broken !== 'undefined'"));
    }

    [Fact]
    public void Moving_to_the_other_page_takes_the_buttons_off_the_first()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Send(SpeedButtons.Script(Expedition));

        Assert.False(ui.Is("buttonsOn(countdown)"));
        Assert.True(ui.Is("buttonsOn(home)"));
    }

    [Fact]
    public void The_removal_takes_the_buttons_off_every_page()
    {
        var ui = new FakeWebUi(BothPages);

        ui.Send(Draw);
        ui.Run("var stray = home.document.createElement('div'); stray.id = 'tc-speed-buttons'; home.document.body.appendChild(stray);");

        Assert.Equal("ok", ui.Send(SpeedButtons.RemovalScript()));
        Assert.False(ui.Is("buttonsOn(countdown)"));
        Assert.False(ui.Is("buttonsOn(home)"));
    }

    [Fact]
    public void With_the_page_not_open_the_scripts_say_so_and_draw_nothing()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.Equal("no CoreUI0 frame", ui.Send(Draw));
        Assert.Equal("no CoreUI0 frame", ui.Send(Collect));
        Assert.False(ui.Is("buttonsOn(home)"));
    }
}
