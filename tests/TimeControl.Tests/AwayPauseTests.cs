using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class AwayPauseTests
{
    private static readonly GameSpeed Fast = new(PauseToggle.Double, FastGear.GamesOwn);
    private static readonly GameSpeed SecondFastGear = new(PauseToggle.Triple, 2);
    private static readonly AwaySnapshot AtTheGame = new(Away: null, CanPause: true, IsPaused: false, Running: Fast, WasSpeedChangedByHand: false);
    private static readonly AwaySnapshot InBackground = AtTheGame with { Away = AwayReason.Background };
    private static readonly AwaySettings AllOn = new(IsFocusLossOn: true, IsBagOn: true, IsWorkbenchOn: true, IsPhoneOn: true, IsTradeOn: true);
    private static readonly BadgeSettings Badges = new(IsIdleBadgeOn: true, IsPauseKeyBadgeOn: true, IsAlertBadgeOn: true);

    private static List<AwayStep> Steps(params AwaySnapshot[] checks)
    {
        var state = AwayPause.Start;
        var steps = new List<AwayStep>();

        foreach (var seen in checks)
        {
            var step = AwayPause.Next(state, seen);

            steps.Add(step);
            state = step.State;
        }

        return steps;
    }

    private static AwaySnapshot Paused(AwaySnapshot seen) => seen with { IsPaused = true };

    [Fact]
    public void Switching_away_from_a_running_game_pauses_it_and_remembers_its_speed()
    {
        var step = Steps(InBackground)[0];

        Assert.True(step.IsPausing);
        Assert.Equal(new AwayPause.Holding(AwayReason.Background, Fast), step.State);
    }

    [Fact]
    public void Coming_back_resumes_the_speed_the_game_ran_at_once()
    {
        var steps = Steps(InBackground, Paused(InBackground), Paused(AtTheGame), AtTheGame);

        Assert.Null(steps[1].ResumeAt);
        Assert.Equal(Fast, steps[2].ResumeAt);
        Assert.Null(steps[3].ResumeAt);
        Assert.Equal(AwayPause.Start, steps[3].State);
    }

    [Fact]
    public void The_fast_gear_in_use_is_what_comes_back()
    {
        var running = InBackground with { Running = SecondFastGear };
        var steps = Steps(running, Paused(AtTheGame));

        Assert.Equal(SecondFastGear, steps[1].ResumeAt);
    }

    [Fact]
    public void A_speed_picked_while_away_is_kept_on_coming_back()
    {
        var steps = Steps(InBackground, Paused(InBackground) with { WasSpeedChangedByHand = true }, InBackground, AtTheGame);

        Assert.All(steps.Skip(1), step => Assert.Null(step.ResumeAt));
        Assert.All(steps.Skip(1), step => Assert.False(step.IsPausing));
    }

    [Fact]
    public void Time_resumed_by_anyone_while_away_runs_on_and_nothing_is_resumed_later()
    {
        var steps = Steps(InBackground, InBackground, InBackground, AtTheGame);

        Assert.False(steps[1].IsPausing);
        Assert.False(steps[2].IsPausing);
        Assert.Null(steps[3].ResumeAt);
    }

    [Fact]
    public void Leaving_a_game_already_paused_leaves_the_pause_to_its_owner()
    {
        var steps = Steps(Paused(InBackground), Paused(InBackground), Paused(AtTheGame));

        Assert.All(steps, step => Assert.False(step.IsPausing));
        Assert.All(steps, step => Assert.Null(step.ResumeAt));
    }

    [Fact]
    public void Being_away_where_the_mod_does_not_pause_never_pauses_even_when_that_changes()
    {
        var steps = Steps(InBackground with { CanPause = false }, InBackground, AtTheGame);

        Assert.All(steps, step => Assert.False(step.IsPausing));
    }

    [Fact]
    public void Each_time_away_pauses_once()
    {
        var steps = Steps(InBackground, Paused(AtTheGame), AtTheGame, InBackground);

        Assert.True(steps[0].IsPausing);
        Assert.True(steps[3].IsPausing);
    }

    [Fact]
    public void Going_from_the_background_into_a_menu_keeps_the_game_paused_until_fully_back()
    {
        var inMenu = Paused(AtTheGame) with { Away = AwayReason.Menu };
        var steps = Steps(InBackground, inMenu, Paused(AtTheGame));

        Assert.Equal(new AwayPause.Holding(AwayReason.Menu, Fast), steps[1].State);
        Assert.Null(steps[1].ResumeAt);
        Assert.Equal(Fast, steps[2].ResumeAt);
    }

    [Fact]
    public void The_background_wins_over_an_open_menu()
    {
        Assert.Equal(AwayReason.Background, AllOn.Reason(isInBackground: true, isWatchedMenuOpen: true));
        Assert.Equal(AwayReason.Menu, AllOn.Reason(isInBackground: false, isWatchedMenuOpen: true));
        Assert.Null(AllOn.Reason(isInBackground: false, isWatchedMenuOpen: false));
    }

    [Fact]
    public void Focus_loss_switched_off_never_counts_as_away()
    {
        var menusOnly = AllOn with { IsFocusLossOn = false };

        Assert.Null(menusOnly.Reason(isInBackground: true, isWatchedMenuOpen: false));
        Assert.Equal(AwayReason.Menu, menusOnly.Reason(isInBackground: true, isWatchedMenuOpen: true));
    }

    [Fact]
    public void Any_menu_setting_turns_the_watch_on_and_focus_loss_is_not_a_menu()
    {
        var none = new AwaySettings(false, false, false, false, false);

        Assert.False(none.IsAnyOn);
        Assert.True((none with { IsTradeOn = true }).IsAnyMenuOn);
        Assert.True((none with { IsFocusLossOn = true }).IsAnyOn);
        Assert.False((none with { IsFocusLossOn = true }).IsAnyMenuOn);
    }

    [Fact]
    public void The_badge_names_why_the_game_is_held()
    {
        var background = new AwayPause.Holding(AwayReason.Background, Fast);
        var menu = new AwayPause.Holding(AwayReason.Menu, Fast);

        Assert.Equal(Texts.PausedInBackground.En, AwayPause.Badge(background, Badges, isPaused: true, isChinese: false));
        Assert.Equal(Texts.PausedMenuOpen.Zh, AwayPause.Badge(menu, Badges, isPaused: true, isChinese: true));
    }

    [Fact]
    public void The_alert_badge_setting_hides_the_away_badge_and_no_other_badge_takes_its_place()
    {
        var held = new AwayPause.Holding(AwayReason.Background, Fast);

        Assert.Equal("", AwayPause.Badge(held, Badges with { IsAlertBadgeOn = false }, isPaused: true, isChinese: false));
    }

    [Fact]
    public void Without_a_held_pause_the_other_badges_decide()
    {
        var held = new AwayPause.Holding(AwayReason.Background, Fast);

        Assert.Null(AwayPause.Badge(AwayPause.Start, Badges, isPaused: true, isChinese: false));
        Assert.Null(AwayPause.Badge(new AwayPause.Released(), Badges, isPaused: true, isChinese: false));
        Assert.Null(AwayPause.Badge(held, Badges, isPaused: false, isChinese: false));
    }
}
