using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Wait until: the game runs at its fastest to a chosen moment, pauses there, and keeps the speed from before for the next resume.</summary>
public class WaitUntilTests
{
    private const int H = ClockHour.Hour;
    private const long Now = 1_000_000;

    private static readonly GameSpeed Normal = new(PauseToggle.Normal, FastGear.GamesOwn);
    private static readonly GameSpeed Double = new(PauseToggle.Double, FastGear.GamesOwn);
    private static readonly WaitGoal ToMorning = new WaitGoal.At(Now + 3 * H, WaitKind.Morning);

    [Fact]
    public void A_wait_runs_at_the_fastest_gear_and_remembers_the_speed_from_before()
    {
        var step = WaitUntil.Begin(WaitUntil.Start, ToMorning, Normal, GameTimer.DayClock, fastestGear: 3);

        Assert.Equal(new WaitCommand.FastForward(new GameSpeed(PauseToggle.Triple, 3)), step.Command);
        Assert.Equal(new WaitUntil.Waiting(ToMorning, Normal, GameTimer.DayClock), step.State);
    }

    [Fact]
    public void With_no_gear_of_the_mods_a_wait_runs_at_the_games_own_fastest_speed()
    {
        var step = WaitUntil.Begin(WaitUntil.Start, ToMorning, Normal, GameTimer.DayClock, fastestGear: -1);

        Assert.Equal(new WaitCommand.FastForward(new GameSpeed(PauseToggle.Triple, FastGear.GamesOwn)), step.Command);
    }

    [Fact]
    public void A_new_wait_over_a_running_one_still_puts_back_the_speed_from_before_the_first()
    {
        var first = WaitUntil.Begin(WaitUntil.Start, ToMorning, Normal, GameTimer.DayClock, 2).State;
        var fast = new GameSpeed(PauseToggle.Triple, 2);
        var second = WaitUntil.Begin(first, new WaitGoal.QueueDone(0f), fast, GameTimer.DayClock, 2).State;

        Assert.Equal(Normal, Assert.IsType<WaitUntil.Waiting>(second).Before);
    }

    [Fact]
    public void The_wait_goes_on_until_the_moment_then_pauses_and_hands_back_the_speed_from_before()
    {
        var waiting = Waiting(ToMorning);

        Assert.Equal(new WaitStep(waiting, WaitCommand.Nothing), WaitUntil.Next(waiting, Seen(Now + 3 * H - 1)));
        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.Arrive(Normal)), WaitUntil.Next(waiting, Seen(Now + 3 * H)));
    }

    [Fact]
    public void A_wait_that_overshoots_its_moment_in_one_check_still_ends()
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now + 5 * H));

        Assert.Equal(new WaitCommand.Arrive(Normal), step.Command);
    }

    [Fact]
    public void A_speed_the_player_picks_during_the_wait_is_theirs_and_ends_it_without_touching_the_speed()
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { WasSpeedChangedByHand = true });

        Assert.Equal(new WaitStep(WaitUntil.Start, WaitCommand.Nothing), step);
    }

    [Fact]
    public void Anything_else_that_pauses_the_game_ends_the_wait_and_leaves_the_pause_with_the_old_speed_to_resume()
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { IsPaused = true });

        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.ResumeTo(Normal)), step);
    }

    [Theory]
    [InlineData(GameTimer.Expedition)]
    [InlineData(GameTimer.PrologueCountdown)]
    [InlineData(GameTimer.Other)]
    public void A_change_of_clock_ends_the_wait_and_puts_back_the_speed(GameTimer timer)
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { Timer = timer });

        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.Restore(Normal)), step);
    }

    [Fact]
    public void A_clock_that_cannot_be_read_ends_the_wait_and_puts_back_the_speed()
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { Reading = null });

        Assert.Equal(new WaitCommand.Restore(Normal), step.Command);
    }

    [Fact]
    public void Falling_asleep_hands_the_speed_from_before_to_the_mods_sleep_handling()
    {
        var step = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { IsAsleep = true, IsSleepWatched = true, WasSpeedChangedByHand = true });

        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.HandToSleep(Normal)), step);
    }

    [Fact]
    public void Without_the_mods_sleep_handling_the_speed_from_before_comes_back_once_the_character_is_up_and_the_speed_up_is_done()
    {
        var asleep = WaitUntil.Next(Waiting(ToMorning), Seen(Now) with { IsAsleep = true, WasSpeedChangedByHand = true });

        Assert.Equal(new WaitStep(new WaitUntil.AfterSleep(Normal), WaitCommand.Nothing), asleep);

        var stillAsleep = WaitUntil.Next(asleep.State, Seen(Now + H) with { IsAsleep = true, WasSpeedChangedByHand = true });
        var speedingUp = WaitUntil.Next(stillAsleep.State, Seen(Now + 2 * H) with { IsGameFastForwarding = true, WasSpeedChangedByHand = true });
        var up = WaitUntil.Next(speedingUp.State, Seen(Now + 3 * H));

        Assert.Equal(WaitCommand.Nothing, stillAsleep.Command);
        Assert.Equal(WaitCommand.Nothing, speedingUp.Command);
        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.Restore(Normal)), up);
    }

    [Fact]
    public void A_speed_the_player_picks_after_waking_is_kept()
    {
        var afterSleep = new WaitUntil.AfterSleep(Normal);
        var step = WaitUntil.Next(afterSleep, Seen(Now) with { WasSpeedChangedByHand = true });

        Assert.Equal(new WaitStep(WaitUntil.Start, WaitCommand.Nothing), step);
    }

    [Fact]
    public void A_wait_started_while_a_speed_is_still_owed_from_a_sleep_puts_back_that_speed()
    {
        var step = WaitUntil.Begin(new WaitUntil.AfterSleep(Normal), ToMorning, Double, GameTimer.DayClock, 0);

        Assert.Equal(Normal, Assert.IsType<WaitUntil.Waiting>(step.State).Before);
    }

    [Fact]
    public void The_queue_is_done_only_once_it_stays_empty_for_a_moment()
    {
        var waiting = Waiting(new WaitGoal.QueueDone(0f));

        var empty = WaitUntil.Next(waiting, Seen(Now) with { ElapsedSeconds = 0.2f });

        Assert.Equal(WaitCommand.Nothing, empty.Command);

        var nextOrder = WaitUntil.Next(empty.State, Seen(Now) with { IsBusy = true, ElapsedSeconds = 0.2f });
        var emptyAgain = WaitUntil.Next(nextOrder.State, Seen(Now) with { ElapsedSeconds = 0.2f });

        Assert.Equal(WaitCommand.Nothing, nextOrder.Command);
        Assert.Equal(WaitCommand.Nothing, emptyAgain.Command);

        var done = WaitUntil.Next(emptyAgain.State, Seen(Now) with { ElapsedSeconds = 0.2f });

        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.Arrive(Normal)), done);
    }

    [Fact]
    public void Cancelling_a_wait_puts_back_the_speed_from_before()
    {
        Assert.Equal(new WaitStep(WaitUntil.Start, new WaitCommand.Restore(Normal)), WaitUntil.Cancel(Waiting(ToMorning)));
    }

    [Fact]
    public void Cancelling_with_no_wait_running_changes_nothing()
    {
        var afterSleep = new WaitUntil.AfterSleep(Normal);

        Assert.Equal(new WaitStep(WaitUntil.Start, WaitCommand.Nothing), WaitUntil.Cancel(WaitUntil.Start));
        Assert.Equal(new WaitStep(afterSleep, WaitCommand.Nothing), WaitUntil.Cancel(afterSleep));
    }

    [Fact]
    public void A_brew_moved_along_by_another_mod_ends_the_wait_earlier()
    {
        var waiting = Waiting(new WaitGoal.At(Now + 5 * H, WaitKind.Brew));

        var followed = WaitUntil.FollowBrew(waiting, Now + 2 * H, new ClockReading(Now, 0));

        Assert.Equal(new WaitGoal.At(Now + 2 * H, WaitKind.Brew), Assert.IsType<WaitUntil.Waiting>(followed).Goal);
    }

    [Fact]
    public void A_wait_for_a_brew_never_moves_later()
    {
        var waiting = Waiting(new WaitGoal.At(Now + 5 * H, WaitKind.Brew));

        Assert.Same(waiting, WaitUntil.FollowBrew(waiting, Now + 8 * H, new ClockReading(Now, 0)));
    }

    [Fact]
    public void With_no_brew_left_brewing_the_wait_for_a_brew_ends_on_the_next_check()
    {
        var waiting = Waiting(new WaitGoal.At(Now + 5 * H, WaitKind.Brew));

        var followed = WaitUntil.FollowBrew(waiting, null, new ClockReading(Now, 0));

        Assert.Equal(new WaitCommand.Arrive(Normal), WaitUntil.Next(followed, Seen(Now)).Command);
    }

    [Fact]
    public void Only_a_wait_for_a_brew_follows_the_brew()
    {
        var waiting = Waiting(ToMorning);

        Assert.Same(waiting, WaitUntil.FollowBrew(waiting, Now, new ClockReading(Now, 0)));
    }

    [Theory]
    [InlineData(GameTimer.DayClock, true)]
    [InlineData(GameTimer.PrologueCountdown, true)]
    [InlineData(GameTimer.Expedition, false)]
    [InlineData(GameTimer.Other, false)]
    public void Waits_run_only_where_the_game_shows_the_time_of_day(GameTimer timer, bool expected)
    {
        Assert.Equal(expected, WaitUntil.CanWait(timer));
    }

    private static WaitUntil.Waiting Waiting(WaitGoal goal) => new(goal, Normal, GameTimer.DayClock);

    private static WaitSnapshot Seen(long total) => new(
        GameTimer.DayClock,
        new ClockReading(total, ClockHour.Wrap(total)),
        IsPaused: false,
        WasSpeedChangedByHand: false,
        IsBusy: false,
        IsAsleep: false,
        IsSleepWatched: false,
        IsGameFastForwarding: false,
        ElapsedSeconds: 0.1f);
}
