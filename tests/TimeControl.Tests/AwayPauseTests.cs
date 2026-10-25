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
    public void A_pause_made_by_hand_while_away_still_holds_on_coming_back()
    {
        var steps = Steps(InBackground, Paused(InBackground) with { WasSpeedChangedByHand = true }, Paused(AtTheGame));

        Assert.Null(steps[2].ResumeAt);
        Assert.Equal(AwayPause.Start, steps[2].State);
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

    private static readonly AwaySnapshot InMenu = AtTheGame with { Away = AwayReason.Menu };
    private static readonly AwaySnapshot BusyInMenu = InMenu with { IsBusy = true };
    private static readonly AwaySnapshot OrderGivenInMenu = BusyInMenu with { HasNewOrder = true };
    private static readonly AwaySnapshot DoneInMenu = InMenu with { Elapsed = AwayPause.SettleSeconds };

    private static AwaySnapshot IdleInMenuFor(float seconds) => InMenu with { Elapsed = seconds };

    [Fact]
    public void An_order_given_in_a_menu_resumes_the_game_so_a_craft_can_run()
    {
        var steps = Steps(InMenu, Paused(InMenu), Paused(OrderGivenInMenu));

        Assert.True(steps[0].IsPausing);
        Assert.Null(steps[1].ResumeAt);
        Assert.Equal(Fast, steps[2].ResumeAt);
        Assert.IsType<AwayPause.RunningOrder>(steps[2].State);
    }

    [Fact]
    public void The_menu_pauses_again_once_the_order_is_done()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), BusyInMenu, BusyInMenu, DoneInMenu);

        Assert.False(steps[2].IsPausing);
        Assert.False(steps[3].IsPausing);
        Assert.True(steps[4].IsPausing);
        Assert.Equal(new AwayPause.Holding(AwayReason.Menu, Fast), steps[4].State);
    }

    [Fact]
    public void Each_order_in_the_menu_runs_the_game_again_and_each_finish_pauses_it()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), DoneInMenu, Paused(OrderGivenInMenu), DoneInMenu);

        Assert.Equal(Fast, steps[1].ResumeAt);
        Assert.True(steps[2].IsPausing);
        Assert.Equal(Fast, steps[3].ResumeAt);
        Assert.True(steps[4].IsPausing);
    }

    [Fact]
    public void The_next_craft_of_a_batch_starting_within_the_settle_keeps_the_game_running()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), IdleInMenuFor(0.6f), BusyInMenu, IdleInMenuFor(0.6f), IdleInMenuFor(0.6f));

        Assert.False(steps[2].IsPausing);
        Assert.False(steps[3].IsPausing);
        Assert.False(steps[4].IsPausing);
        Assert.True(steps[5].IsPausing);
        Assert.Equal(new AwayPause.Holding(AwayReason.Menu, Fast), steps[5].State);
    }

    [Fact]
    public void Work_starting_in_the_same_check_it_went_idle_is_still_running()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), DoneInMenu with { HasNewOrder = true }, IdleInMenuFor(0.6f));

        Assert.False(steps[2].IsPausing);
        Assert.False(steps[3].IsPausing);
        Assert.IsType<AwayPause.RunningOrder>(steps[3].State);
    }

    [Fact]
    public void A_new_order_lets_the_pause_go_even_when_an_older_one_was_running_when_the_menu_opened()
    {
        var steps = Steps(BusyInMenu, Paused(BusyInMenu), Paused(OrderGivenInMenu));

        Assert.True(steps[0].IsPausing);
        Assert.Null(steps[1].ResumeAt);
        Assert.IsType<AwayPause.Holding>(steps[1].State);
        Assert.Equal(Fast, steps[2].ResumeAt);
    }

    [Fact]
    public void Leaving_the_menu_while_the_order_runs_ends_it_with_nothing_to_resume()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), AtTheGame, InMenu);

        Assert.Equal(AwayPause.Start, steps[2].State);
        Assert.False(steps[2].IsPausing);
        Assert.Null(steps[2].ResumeAt);
        Assert.True(steps[3].IsPausing);
    }

    [Fact]
    public void A_speed_change_by_hand_while_the_order_runs_keeps_the_player_speed_when_the_order_ends()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), BusyInMenu with { WasSpeedChangedByHand = true }, InMenu);

        Assert.IsType<AwayPause.Released>(steps[2].State);
        Assert.False(steps[3].IsPausing);
    }

    [Fact]
    public void Going_to_the_background_while_the_order_runs_pauses_the_game()
    {
        var steps = Steps(InMenu, Paused(OrderGivenInMenu), InBackground with { IsBusy = true });

        Assert.True(steps[2].IsPausing);
        Assert.Equal(new AwayPause.Holding(AwayReason.Background, Fast), steps[2].State);
    }

    [Fact]
    public void Being_busy_in_the_background_does_not_resume_the_game()
    {
        var steps = Steps(InBackground, Paused(InBackground with { IsBusy = true, HasNewOrder = true }));

        Assert.Null(steps[1].ResumeAt);
        Assert.IsType<AwayPause.Holding>(steps[1].State);
    }

    [Fact]
    public void An_order_queued_behind_a_running_one_is_new()
    {
        Assert.True(OrderIds.HasNew(new long[] { 4 }, new long[] { 4, 5 }));
    }

    [Fact]
    public void An_order_finishing_as_another_is_given_still_shows_the_new_one()
    {
        Assert.True(OrderIds.HasNew(new long[] { 4 }, new long[] { 5 }));
    }

    [Fact]
    public void Orders_finishing_or_staying_put_are_not_new()
    {
        Assert.False(OrderIds.HasNew(new long[] { 4, 5 }, new long[] { 5 }));
        Assert.False(OrderIds.HasNew(new long[] { 4 }, new long[] { 4 }));
        Assert.False(OrderIds.HasNew(new long[] { 4 }, new long[0]));
    }

    private static MenuWork Work(long[] orders, long[] benches) => new(orders, benches);

    [Fact]
    public void A_craft_starting_at_a_workbench_is_new_work_with_no_new_order()
    {
        Assert.True(MenuWork.HasNew(Work(new long[] { 4 }, new long[0]), Work(new long[] { 4 }, new long[] { 900 })));
    }

    [Fact]
    public void A_bench_whose_id_matches_an_old_order_still_counts_as_a_new_craft()
    {
        Assert.True(MenuWork.HasNew(Work(new long[] { 900 }, new long[0]), Work(new long[0], new long[] { 900 })));
    }

    [Fact]
    public void A_craft_still_running_or_finishing_is_not_new_work()
    {
        Assert.False(MenuWork.HasNew(Work(new long[0], new long[] { 900 }), Work(new long[0], new long[] { 900 })));
        Assert.False(MenuWork.HasNew(Work(new long[0], new long[] { 900 }), Work(new long[0], new long[0])));
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
