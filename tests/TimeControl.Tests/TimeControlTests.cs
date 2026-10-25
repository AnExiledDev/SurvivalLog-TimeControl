using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class SlowGearTests
{
    [Fact]
    public void Default_config_offers_half_then_quarter_speed()
    {
        Assert.Equal(new[] { 0.5f, 0.25f }, SlowGear.ParseSpeeds("0.5,0.25"));
    }

    [Fact]
    public void A_typo_drops_one_speed_not_the_whole_list()
    {
        Assert.Equal(new[] { 0.5f, 0.25f }, SlowGear.ParseSpeeds("0.5, half; 0.25"));
    }

    [Fact]
    public void Speeds_never_reach_a_stop_or_normal_speed()
    {
        Assert.Equal(new[] { SlowGear.Slowest, SlowGear.Fastest }, SlowGear.ParseSpeeds("0,1.5"));
    }

    [Fact]
    public void An_empty_setting_offers_no_speeds_so_the_game_keeps_its_own_slow_button()
    {
        Assert.Empty(SlowGear.ParseSpeeds(""));
    }

    [Fact]
    public void The_first_slow_press_from_another_speed_is_the_games_own_slow()
    {
        Assert.Equal(SlowGear.GamesOwn, SlowGear.NextIndex(SlowGear.GamesOwn, 2, SlowPress.FromOtherSpeed));
        Assert.Equal(SlowGear.GamesOwn, SlowGear.NextIndex(1, 2, SlowPress.FromOtherSpeed));
    }

    [Fact]
    public void Pressing_slow_again_goes_slower_then_wraps_back_to_the_games_own_slow()
    {
        var presses = new List<int>();
        var index = SlowGear.NextIndex(SlowGear.GamesOwn, 2, SlowPress.FromOtherSpeed);

        for (var i = 0; i < 3; i++)
        {
            index = SlowGear.NextIndex(index, 2, SlowPress.FromSlowSpeed);
            presses.Add(index);
        }

        Assert.Equal(new[] { 0, 1, SlowGear.GamesOwn }, presses);
    }

    [Fact]
    public void Resuming_from_pause_onto_slow_keeps_the_slow_speed_in_use_before()
    {
        Assert.Equal(1, SlowGear.NextIndex(1, 2, SlowPress.FromPause));
        Assert.Equal(SlowGear.GamesOwn, SlowGear.NextIndex(SlowGear.GamesOwn, 2, SlowPress.FromPause));
    }

    [Fact]
    public void A_remembered_speed_that_no_longer_exists_falls_back_to_the_games_own_slow()
    {
        Assert.Equal(SlowGear.GamesOwn, SlowGear.NextIndex(5, 2, SlowPress.FromPause));
        Assert.Equal(SlowGear.GamesOwn, SlowGear.NextIndex(0, 0, SlowPress.FromSlowSpeed));
    }

    [Fact]
    public void A_pause_wins_over_the_slow_flag_when_reading_where_a_press_came_from()
    {
        Assert.Equal(SlowPress.FromPause, SlowGear.PressFrom(isPaused: true, isSlowGearActive: true));
        Assert.Equal(SlowPress.FromSlowSpeed, SlowGear.PressFrom(isPaused: false, isSlowGearActive: true));
        Assert.Equal(SlowPress.FromOtherSpeed, SlowGear.PressFrom(isPaused: false, isSlowGearActive: false));
    }

    [Fact]
    public void Labels_read_as_the_speed_multiplier()
    {
        Assert.Equal("0.25x", SlowGear.Label(0.25f));
        Assert.Equal("0.5x", SlowGear.Label(0.5f));
    }
}

public class IdlePauseTests
{
    private const float Delay = 1f;

    private static IdleSnapshot Idle(float seconds, int runningLevel = PauseToggle.Normal) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: false, seconds, runningLevel, IsLeavingExpedition: false);

    private static IdleSnapshot IdleWhilePaused(float seconds) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: true, seconds, PauseToggle.Normal, IsLeavingExpedition: false);

    private static IdleSnapshot OrderGiven(bool isPaused) =>
        new(CanFreeze: true, IsBusy: true, IsPaused: isPaused, 0.1f, PauseToggle.Normal, IsLeavingExpedition: false);

    private static IdleSnapshot FinishingTheExpedition(bool isPaused, float seconds) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: isPaused, seconds, PauseToggle.Normal, IsLeavingExpedition: true);

    private static IdleStep Run(IdlePause state, params IdleSnapshot[] checks)
    {
        var step = new IdleStep(state, IdleCommand.Nothing);

        foreach (var seen in checks) step = IdlePause.Next(step.State, seen, Delay);

        return step;
    }

    [Fact]
    public void Standing_idle_past_the_delay_pauses_the_game_and_remembers_the_speed_it_ran_at()
    {
        var step = Run(IdlePause.Start, Idle(0.6f, runningLevel: 2), Idle(0.6f, runningLevel: 2));

        Assert.Equal(IdleCommand.PauseGame, step.Command);
        Assert.Equal(new IdlePause.Frozen(2), step.State);
    }

    [Fact]
    public void Idling_shorter_than_the_delay_leaves_the_game_running()
    {
        var step = Run(IdlePause.Start, Idle(0.5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.IsType<IdlePause.Watching>(step.State);
    }

    [Fact]
    public void An_order_between_two_short_idles_starts_the_count_over()
    {
        var step = Run(IdlePause.Start, Idle(0.6f), OrderGiven(isPaused: false), Idle(0.6f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
    }

    [Fact]
    public void An_order_while_paused_by_idling_resumes_at_the_remembered_speed()
    {
        var step = Run(new IdlePause.Frozen(2), OrderGiven(isPaused: true));

        Assert.Equal(new IdleCommand.Resume(2), step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void Standing_idle_while_paused_by_idling_keeps_the_game_paused()
    {
        var step = Run(new IdlePause.Frozen(2), IdleWhilePaused(0.1f), IdleWhilePaused(5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.Equal(new IdlePause.Frozen(2), step.State);
    }

    [Fact]
    public void A_remembered_speed_that_is_not_a_running_one_resumes_at_normal_speed()
    {
        var step = Run(IdlePause.Start, Idle(2f, runningLevel: PauseToggle.Paused));

        Assert.Equal(new IdlePause.Frozen(PauseToggle.Normal), step.State);
    }

    [Fact]
    public void The_players_own_pause_never_counts_as_idling_and_is_never_undone()
    {
        var step = Run(IdlePause.Start, IdleWhilePaused(5f), OrderGiven(isPaused: true), IdleWhilePaused(5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void Pressing_play_while_paused_by_idling_lets_time_run_until_the_next_order()
    {
        var released = Run(new IdlePause.Frozen(PauseToggle.Normal), Idle(0.1f), Idle(10f));

        Assert.Equal(IdleCommand.Nothing, released.Command);
        Assert.IsType<IdlePause.Released>(released.State);

        var afterOrder = Run(released.State, OrderGiven(isPaused: false), Idle(0.6f), Idle(0.6f));

        Assert.Equal(IdleCommand.PauseGame, afterOrder.Command);
    }

    [Fact]
    public void Time_let_run_while_idle_freezes_again_after_a_trip_away_from_home()
    {
        var awayFromHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: false, 5f, PauseToggle.Normal, false);

        var step = Run(new IdlePause.Released(), awayFromHome, Idle(0.6f), Idle(0.6f));

        Assert.Equal(IdleCommand.PauseGame, step.Command);
    }

    [Fact]
    public void Leaving_home_or_sleeping_while_paused_by_idling_resumes_the_game()
    {
        var leftHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: true, 0.1f, PauseToggle.Normal, false);

        var step = Run(new IdlePause.Frozen(3), leftHome);

        Assert.Equal(new IdleCommand.Resume(3), step.Command);
    }

    [Fact]
    public void Finishing_the_expedition_while_paused_by_idling_resumes_at_the_remembered_speed()
    {
        var step = Run(new IdlePause.Frozen(2), FinishingTheExpedition(isPaused: true, 0.1f));

        Assert.Equal(new IdleCommand.Resume(2), step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void Standing_idle_while_the_expedition_is_being_finished_never_pauses_the_game_again()
    {
        var step = Run(
            new IdlePause.Frozen(2),
            FinishingTheExpedition(isPaused: true, 0.1f),
            FinishingTheExpedition(isPaused: false, 5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
        Assert.Equal(IdlePause.Start, step.State);
    }

    [Fact]
    public void Idle_time_where_pausing_is_not_allowed_never_counts()
    {
        var awayFromHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: false, 5f, PauseToggle.Normal, false);

        var step = Run(IdlePause.Start, awayFromHome, Idle(0.5f));

        Assert.Equal(IdleCommand.Nothing, step.Command);
    }

    [Fact]
    public void A_zero_delay_still_needs_a_moment_of_idling()
    {
        var noTimePassed = IdlePause.Next(IdlePause.Start, Idle(0f), delaySeconds: 0f);
        var aMomentPassed = IdlePause.Next(IdlePause.Start, Idle(0.1f), delaySeconds: 0f);

        Assert.Equal(IdleCommand.Nothing, noTimePassed.Command);
        Assert.Equal(IdleCommand.PauseGame, aMomentPassed.Command);
    }

    [Fact]
    public void A_broken_delay_setting_falls_back_to_the_three_second_default()
    {
        Assert.Equal(3f, IdlePause.ClampDelay(float.NaN));
        Assert.Equal(3f, IdlePause.ClampDelay(float.PositiveInfinity));
        Assert.Equal(0f, IdlePause.ClampDelay(-3f));
        Assert.Equal(2.5f, IdlePause.ClampDelay(2.5f));
    }

    [Fact]
    public void By_default_a_short_stand_between_orders_does_not_pause_but_three_seconds_of_idling_does()
    {
        var shortStand = IdlePause.Next(IdlePause.Start, Idle(2.9f), IdlePause.DefaultDelaySeconds);
        var threeSeconds = IdlePause.Next(shortStand.State, Idle(0.2f), IdlePause.DefaultDelaySeconds);

        Assert.Equal(IdleCommand.Nothing, shortStand.Command);
        Assert.Equal(IdleCommand.PauseGame, threeSeconds.Command);
    }

    [Fact]
    public void Idle_freeze_pauses_the_countdown_to_the_disaster_as_well_as_the_day_clock()
    {
        Assert.True(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.PrologueCountdown, isFastForwarding: false));
        Assert.True(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.DayClock, isFastForwarding: false));
    }

    [Fact]
    public void Idle_freeze_pauses_an_expedition_so_the_exposure_meter_stops_while_the_player_idles()
    {
        Assert.True(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.Expedition, isFastForwarding: false));
    }

    [Fact]
    public void Idle_freeze_leaves_other_timers_and_fast_forwards_at_the_games_pace()
    {
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.Other, isFastForwarding: false));
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.PrologueCountdown, isFastForwarding: true));
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: true, GameTimer.Expedition, isFastForwarding: true));
    }

    [Fact]
    public void Idle_freeze_switched_off_never_pauses_anywhere()
    {
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: false, GameTimer.DayClock, isFastForwarding: false));
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: false, GameTimer.PrologueCountdown, isFastForwarding: false));
        Assert.False(IdlePause.CanFreeze(isIdleFreezeOn: false, GameTimer.Expedition, isFastForwarding: false));
    }
}

public class ExpeditionFinishTests
{
    private static FinishSnapshot Finishing(bool isPaused, bool wasPauseRefused = false) =>
        new(IsFinishing: true, isPaused, RunningLevel: PauseToggle.Double, wasPauseRefused);

    private static FinishSnapshot UnderWay(bool isPaused) => new(IsFinishing: false, isPaused, RunningLevel: PauseToggle.Double, WasPauseRefused: false);

    [Fact]
    public void A_pause_standing_when_the_player_finishes_the_expedition_is_resumed_at_the_last_running_speed()
    {
        var step = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: true));

        Assert.Equal(PauseToggle.Double, step.ResumeAt);
    }

    [Fact]
    public void The_trip_home_is_resumed_once_per_finish_not_on_every_check()
    {
        var resumed = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: true));
        var nextCheck = ExpeditionFinish.Next(resumed.State, Finishing(isPaused: true));

        Assert.Null(nextCheck.ResumeAt);
    }

    [Fact]
    public void A_finish_that_starts_while_time_runs_leaves_the_speed_alone()
    {
        var step = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: false));

        Assert.Null(step.ResumeAt);
    }

    [Fact]
    public void A_pause_on_an_expedition_that_is_not_being_finished_is_kept()
    {
        var step = ExpeditionFinish.Next(ExpeditionFinish.Start, UnderWay(isPaused: true));

        Assert.Null(step.ResumeAt);
    }

    [Fact]
    public void Once_home_the_next_finish_resumes_a_standing_pause_again()
    {
        var resumed = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: true));
        var home = ExpeditionFinish.Next(resumed.State, UnderWay(isPaused: false));
        var nextFinish = ExpeditionFinish.Next(home.State, Finishing(isPaused: true));

        Assert.Equal(ExpeditionFinish.Start, home.State);
        Assert.Equal(PauseToggle.Double, nextFinish.ResumeAt);
    }

    [Fact]
    public void The_pause_key_or_pause_button_pressed_while_the_expedition_is_being_finished_does_nothing()
    {
        var pauseKeyWhileRunning = PauseToggle.Target(isPaused: false, PauseToggle.Double);

        Assert.True(ExpeditionFinish.RefusesSpeed(pauseKeyWhileRunning, isFinishing: true));
    }

    [Fact]
    public void The_running_speed_buttons_still_work_while_the_expedition_is_being_finished()
    {
        var runningLevels = new[] { PauseToggle.Normal, PauseToggle.Double, PauseToggle.Triple, PauseToggle.Slow };

        Assert.All(runningLevels, level => Assert.False(ExpeditionFinish.RefusesSpeed(level, isFinishing: true)));
    }

    [Fact]
    public void A_refused_pause_is_told_of_once_per_finish()
    {
        var firstRefusal = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: false, wasPauseRefused: true));
        var secondRefusal = ExpeditionFinish.Next(firstRefusal.State, Finishing(isPaused: false, wasPauseRefused: true));
        var home = ExpeditionFinish.Next(secondRefusal.State, UnderWay(isPaused: false));
        var nextFinishRefusal = ExpeditionFinish.Next(home.State, Finishing(isPaused: false, wasPauseRefused: true));

        Assert.True(firstRefusal.TellsOfRefusal);
        Assert.False(secondRefusal.TellsOfRefusal);
        Assert.True(nextFinishRefusal.TellsOfRefusal);
    }

    [Fact]
    public void A_finish_with_no_pause_refused_tells_of_nothing()
    {
        var first = ExpeditionFinish.Next(ExpeditionFinish.Start, Finishing(isPaused: true));
        var next = ExpeditionFinish.Next(first.State, Finishing(isPaused: false));

        Assert.False(first.TellsOfRefusal);
        Assert.False(next.TellsOfRefusal);
    }

    [Fact]
    public void Pausing_on_an_expedition_still_works_until_it_is_being_finished_and_resuming_always_does()
    {
        var pauseKeyWhilePaused = PauseToggle.Target(isPaused: true, PauseToggle.Double);

        Assert.False(ExpeditionFinish.RefusesSpeed(PauseToggle.Paused, isFinishing: false));
        Assert.False(ExpeditionFinish.RefusesSpeed(pauseKeyWhilePaused, isFinishing: true));
    }
}

public class GameTimerTests
{
    [Fact]
    public void The_count_up_clock_is_the_day_clock_at_home()
    {
        Assert.Equal(GameTimer.DayClock, GameTimers.Classify(isCountingUp: true, isCountingDown: false, isFrozen: false, isBeforeDisaster: false));
    }

    [Fact]
    public void A_countdown_before_the_disaster_is_the_prologue()
    {
        Assert.Equal(GameTimer.PrologueCountdown, GameTimers.Classify(isCountingUp: false, isCountingDown: true, isFrozen: false, isBeforeDisaster: true));
    }

    [Fact]
    public void A_countdown_after_the_disaster_is_left_at_the_games_pace()
    {
        Assert.Equal(GameTimer.Other, GameTimers.Classify(isCountingUp: false, isCountingDown: true, isFrozen: false, isBeforeDisaster: false));
    }

    [Fact]
    public void Before_any_timer_starts_there_is_nothing_to_control()
    {
        Assert.Equal(GameTimer.Other, GameTimers.Classify(isCountingUp: false, isCountingDown: false, isFrozen: false, isBeforeDisaster: true));
    }

    [Fact]
    public void A_frozen_day_clock_is_an_expedition_whenever_it_runs()
    {
        Assert.Equal(GameTimer.Expedition, GameTimers.Classify(isCountingUp: false, isCountingDown: false, isFrozen: true, isBeforeDisaster: false));
        Assert.Equal(GameTimer.Expedition, GameTimers.Classify(isCountingUp: false, isCountingDown: false, isFrozen: true, isBeforeDisaster: true));
    }

    [Fact]
    public void Time_control_switched_off_on_expeditions_leaves_them_at_the_games_pace()
    {
        Assert.Equal(GameTimer.Other, GameTimers.Controlled(GameTimer.Expedition, isExpeditionControlOn: false));
        Assert.Equal(GameTimer.Expedition, GameTimers.Controlled(GameTimer.Expedition, isExpeditionControlOn: true));
    }

    [Fact]
    public void The_expedition_switch_never_touches_home_or_the_countdown()
    {
        Assert.Equal(GameTimer.DayClock, GameTimers.Controlled(GameTimer.DayClock, isExpeditionControlOn: false));
        Assert.Equal(GameTimer.PrologueCountdown, GameTimers.Controlled(GameTimer.PrologueCountdown, isExpeditionControlOn: false));
    }
}

public class SpeedKeysTests
{
    [Fact]
    public void The_pause_key_works_anywhere_after_the_disaster()
    {
        Assert.True(SpeedKeys.IsPauseKeyLive(isAfterDisaster: true, GameTimer.DayClock));
        Assert.True(SpeedKeys.IsPauseKeyLive(isAfterDisaster: true, GameTimer.Other));
    }

    [Fact]
    public void The_pause_key_works_in_the_countdown_to_the_disaster()
    {
        Assert.True(SpeedKeys.IsPauseKeyLive(isAfterDisaster: false, GameTimer.PrologueCountdown));
    }

    [Fact]
    public void The_pause_key_does_nothing_before_the_countdown_starts()
    {
        Assert.False(SpeedKeys.IsPauseKeyLive(isAfterDisaster: false, GameTimer.Other));
    }

    [Fact]
    public void The_mod_answers_the_games_speed_keys_only_where_the_game_does_not()
    {
        Assert.True(SpeedKeys.AreGameKeysOurs(isAfterDisaster: false, GameTimer.PrologueCountdown));
        Assert.False(SpeedKeys.AreGameKeysOurs(isAfterDisaster: true, GameTimer.DayClock));
        Assert.False(SpeedKeys.AreGameKeysOurs(isAfterDisaster: true, GameTimer.Other));
        Assert.False(SpeedKeys.AreGameKeysOurs(isAfterDisaster: false, GameTimer.Other));
    }

    [Fact]
    public void The_pause_key_works_on_an_expedition()
    {
        Assert.True(SpeedKeys.IsPauseKeyLive(isAfterDisaster: false, GameTimer.Expedition));
    }

    [Fact]
    public void The_mod_answers_the_games_speed_keys_on_an_expedition_where_the_game_ignores_them()
    {
        Assert.True(SpeedKeys.AreGameKeysOurs(isAfterDisaster: true, GameTimer.Expedition));
        Assert.True(SpeedKeys.AreGameKeysOurs(isAfterDisaster: false, GameTimer.Expedition));
    }

    [Fact]
    public void Keys_count_in_the_exploration_mode_only_on_an_expedition()
    {
        Assert.True(SpeedKeys.IsModeForKeys(isNormalMode: false, GameTimer.Expedition));
        Assert.False(SpeedKeys.IsModeForKeys(isNormalMode: false, GameTimer.DayClock));
        Assert.False(SpeedKeys.IsModeForKeys(isNormalMode: false, GameTimer.Other));
    }

    [Fact]
    public void Keys_count_in_the_normal_mode_wherever_the_player_is()
    {
        Assert.True(SpeedKeys.IsModeForKeys(isNormalMode: true, GameTimer.DayClock));
        Assert.True(SpeedKeys.IsModeForKeys(isNormalMode: true, GameTimer.PrologueCountdown));
    }
}

public class CenterCameraKeyTests
{
    private const int Space = 32;
    private const int G = 103;
    private const int P = 112;

    [Fact]
    public void Centering_the_camera_moves_off_space_so_space_can_pause()
    {
        Assert.Equal(G, CenterCameraKey.Move(gameDefault: Space, pauseKey: Space, wantedKey: G));
    }

    [Fact]
    public void Asking_for_no_center_camera_key_unbinds_it()
    {
        Assert.Equal(CenterCameraKey.None, CenterCameraKey.Move(gameDefault: Space, pauseKey: Space, wantedKey: CenterCameraKey.None));
    }

    [Fact]
    public void A_pause_key_the_camera_does_not_use_leaves_the_camera_key_alone()
    {
        Assert.Null(CenterCameraKey.Move(gameDefault: Space, pauseKey: P, wantedKey: G));
    }

    [Fact]
    public void Turning_the_pause_key_off_leaves_the_camera_on_space()
    {
        Assert.Null(CenterCameraKey.Move(gameDefault: Space, pauseKey: CenterCameraKey.None, wantedKey: G));
    }

    [Fact]
    public void Asking_for_the_pause_key_itself_leaves_the_camera_key_alone()
    {
        Assert.Null(CenterCameraKey.Move(gameDefault: Space, pauseKey: Space, wantedKey: Space));
    }
}

public class HoverClockTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void While_paused_hover_testing_runs_on_real_time()
    {
        var pausedScale = 1e-4f;

        Assert.Equal(Frame, HoverClock.TopUp(Frame * pausedScale, Frame), tolerance: 1e-5f);
    }

    [Fact]
    public void At_normal_speed_hover_testing_is_left_to_the_game()
    {
        Assert.Equal(0f, HoverClock.TopUp(Frame, Frame));
    }

    [Fact]
    public void Fast_speeds_never_slow_hover_testing_down()
    {
        Assert.Equal(0f, HoverClock.TopUp(Frame * 2f, Frame));
        Assert.Equal(0f, HoverClock.TopUp(Frame * 3f, Frame));
    }
}

public class PauseToggleTests
{
    [Fact]
    public void Pressing_pause_while_running_pauses()
    {
        Assert.Equal(PauseToggle.Paused, PauseToggle.Target(isPaused: false, lastRunningLevel: 3));
    }

    [Fact]
    public void Pressing_pause_while_paused_resumes_the_last_speed()
    {
        Assert.Equal(3, PauseToggle.Target(isPaused: true, lastRunningLevel: 3));
        Assert.Equal(PauseToggle.Slow, PauseToggle.Target(isPaused: true, lastRunningLevel: 4));
    }

    [Fact]
    public void Resuming_with_no_known_speed_goes_to_normal_speed()
    {
        Assert.Equal(PauseToggle.Normal, PauseToggle.Target(isPaused: true, lastRunningLevel: 0));
    }

    [Fact]
    public void A_speed_level_the_game_does_not_have_resumes_at_normal_speed()
    {
        Assert.Equal(PauseToggle.Normal, PauseToggle.RunningLevel(7));
        Assert.Equal(PauseToggle.Normal, PauseToggle.RunningLevel(-1));
    }
}

public class SpeedChangeFanOutTests
{
    [Fact]
    public void A_speed_change_reaching_two_time_managers_in_one_frame_counts_two()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 10, timeManager: 222);

        Assert.Equal(2, fanOut.TakeLargest());
    }

    [Fact]
    public void One_time_manager_changing_twice_in_a_frame_counts_once()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 10, timeManager: 111);

        Assert.Equal(1, fanOut.TakeLargest());
    }

    [Fact]
    public void Changes_in_separate_frames_are_not_added_together()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.Record(frame: 11, timeManager: 222);

        Assert.Equal(1, fanOut.TakeLargest());
    }

    [Fact]
    public void Taking_the_count_starts_it_over()
    {
        var fanOut = new SpeedChangeFanOut();

        fanOut.Record(frame: 10, timeManager: 111);
        fanOut.TakeLargest();

        Assert.Equal(0, fanOut.TakeLargest());
    }
}

public class PageClockTests
{
    [Fact]
    public void Holding_a_slow_speed_puts_it_on_the_tooltip()
    {
        Assert.Equal(new PageClockUpdate("0.25x"), PageClock.Next("0.25x", wasNeutralLastSent: true));
    }

    [Fact]
    public void Nothing_is_sent_while_no_slow_speed_is_held()
    {
        Assert.Null(PageClock.Next("", wasNeutralLastSent: true));
    }

    [Fact]
    public void Leaving_the_slow_speed_strips_the_label_once()
    {
        Assert.Equal(new PageClockUpdate(""), PageClock.Next("", wasNeutralLastSent: false));
    }

    [Fact]
    public void The_page_clock_is_left_to_the_game()
    {
        Assert.DoesNotContain("setTimeScale", PageClock.Script(new PageClockUpdate("0.5x")));
    }

    [Fact]
    public void A_label_cannot_break_out_of_the_script_string()
    {
        var script = PageClock.Script(new PageClockUpdate("0.5x');alert(1);//"));

        Assert.DoesNotContain("alert", script);
        Assert.Contains("n=b+' 0.5x1'", script);
    }
}

public class PauseBadgeTests
{
    private static readonly BadgeSettings BothOn = new(IsIdleBadgeOn: true, IsPauseKeyBadgeOn: true, IsAlertBadgeOn: true);

    [Fact]
    public void The_idle_pause_says_it_holds_the_game()
    {
        Assert.Equal("Paused: idle", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: true, isPauseKeyHolding: false));
    }

    [Fact]
    public void In_a_chinese_game_the_badge_says_why_in_chinese()
    {
        Assert.Equal("已暂停：空闲", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: true, isPauseKeyHolding: false, isChinese: true));
        Assert.Equal("已暂停：尸潮", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.Wave, isChinese: true));
        Assert.Equal("已暂停", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: true, isChinese: true));
    }

    [Fact]
    public void A_turned_off_badge_stays_hidden_in_chinese_too()
    {
        var pauseKeyOnly = BothOn with { IsIdleBadgeOn = false };

        Assert.Equal("", PauseBadge.Text(pauseKeyOnly, isPaused: true, isIdleHolding: true, isPauseKeyHolding: false, isChinese: true));
    }

    [Fact]
    public void The_pause_key_says_it_holds_the_game()
    {
        Assert.Equal("Paused", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: true));
    }

    [Fact]
    public void The_badge_goes_the_moment_time_runs()
    {
        Assert.Equal("", PauseBadge.Text(BothOn, isPaused: false, isIdleHolding: true, isPauseKeyHolding: false));
        Assert.Equal("", PauseBadge.Text(BothOn, isPaused: false, isIdleHolding: false, isPauseKeyHolding: true));
    }

    [Fact]
    public void A_pause_the_game_or_its_own_buttons_made_gets_no_badge()
    {
        Assert.Equal("", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false));
    }

    [Fact]
    public void The_idle_badge_switched_off_shows_nothing_for_the_idle_pause()
    {
        var pauseKeyOnly = new BadgeSettings(IsIdleBadgeOn: false, IsPauseKeyBadgeOn: true, IsAlertBadgeOn: true);

        Assert.Equal("", PauseBadge.Text(pauseKeyOnly, isPaused: true, isIdleHolding: true, isPauseKeyHolding: false));
        Assert.Equal("Paused", PauseBadge.Text(pauseKeyOnly, isPaused: true, isIdleHolding: false, isPauseKeyHolding: true));
    }

    [Fact]
    public void The_pause_key_badge_switched_off_shows_nothing_for_the_pause_key()
    {
        var idleOnly = new BadgeSettings(IsIdleBadgeOn: true, IsPauseKeyBadgeOn: false, IsAlertBadgeOn: true);

        Assert.Equal("", PauseBadge.Text(idleOnly, isPaused: true, isIdleHolding: false, isPauseKeyHolding: true));
        Assert.Equal("Paused: idle", PauseBadge.Text(idleOnly, isPaused: true, isIdleHolding: true, isPauseKeyHolding: false));
    }

    [Fact]
    public void An_alert_pause_names_the_alert_it_holds_the_game_for()
    {
        Assert.Equal("Paused: wave", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.Wave));
        Assert.Equal("Paused: emergency", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.Emergency));
        Assert.Equal("Paused: drone back", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.DroneArrival));
        Assert.Equal("Paused: queue empty", PauseBadge.Text(BothOn, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.QueueEmpty));
    }

    [Fact]
    public void An_alert_badge_goes_the_moment_time_runs()
    {
        Assert.Equal("", PauseBadge.Text(BothOn, isPaused: false, isIdleHolding: false, isPauseKeyHolding: false, Alert.Wave));
    }

    [Fact]
    public void The_alert_badge_switched_off_shows_nothing_for_an_alert_pause()
    {
        var noAlerts = new BadgeSettings(IsIdleBadgeOn: true, IsPauseKeyBadgeOn: true, IsAlertBadgeOn: false);

        Assert.Equal("", PauseBadge.Text(noAlerts, isPaused: true, isIdleHolding: false, isPauseKeyHolding: false, Alert.Emergency));
        Assert.Equal("Paused", PauseBadge.Text(noAlerts, isPaused: true, isIdleHolding: false, isPauseKeyHolding: true));
    }

    [Fact]
    public void A_change_goes_out_at_once_without_waiting_for_the_refresh()
    {
        Assert.True(PauseBadge.ShouldSend("", lastSent: "Paused: idle", isLastSentConfirmed: true, isRefreshDue: false));
        Assert.True(PauseBadge.ShouldSend("Paused: idle", lastSent: "", isLastSentConfirmed: true, isRefreshDue: false));
    }

    [Fact]
    public void A_shown_badge_is_put_back_on_each_refresh_in_case_the_page_reloaded()
    {
        Assert.True(PauseBadge.ShouldSend("Paused", lastSent: "Paused", isLastSentConfirmed: true, isRefreshDue: true));
        Assert.False(PauseBadge.ShouldSend("Paused", lastSent: "Paused", isLastSentConfirmed: true, isRefreshDue: false));
    }

    [Fact]
    public void A_hidden_badge_sends_nothing_once_the_page_confirmed_its_removal()
    {
        Assert.False(PauseBadge.ShouldSend("", lastSent: "", isLastSentConfirmed: true, isRefreshDue: true));
    }

    [Fact]
    public void A_removal_the_page_never_confirmed_is_sent_again_on_the_next_refresh()
    {
        Assert.True(PauseBadge.ShouldSend("", lastSent: "", isLastSentConfirmed: false, isRefreshDue: true));
        Assert.False(PauseBadge.ShouldSend("", lastSent: "", isLastSentConfirmed: false, isRefreshDue: false));
    }

    [Fact]
    public void The_badge_script_names_the_games_own_chip_style_and_speed_buttons()
    {
        var script = PauseBadge.Script("Paused: idle");

        Assert.Contains("className='idle-slow-chip'", script);
        Assert.Contains("querySelector('.speed-controls')", script);
        Assert.Contains("t='Paused: idle';", script);
    }

    [Fact]
    public void An_empty_badge_script_carries_the_removal_branch()
    {
        Assert.Contains("t='';if(!t.length){if(b&&b.parentNode)b.parentNode.removeChild(b);return 'ok';}", PauseBadge.Script(""));
    }

    [Fact]
    public void Badge_text_cannot_break_out_of_the_script_string()
    {
        Assert.Contains(@"t='x\');window.close();//';", PauseBadge.Script("x');window.close();//"));
    }

    [Fact]
    public void Quoting_escapes_quotes_backslashes_and_anything_outside_printable_ascii()
    {
        Assert.Equal(@"'it\'s'", PauseBadge.Quote("it's"));
        Assert.Equal(@"'a\\b'", PauseBadge.Quote(@"a\b"));
        Assert.Equal(@"'a\u000ab'", PauseBadge.Quote("a\nb"));
        Assert.Equal(@"'\u00e9'", PauseBadge.Quote(((char)0xe9).ToString()));
    }

    [Fact]
    public void The_badge_goes_on_the_page_showing_the_speed_buttons_and_leaves_every_other()
    {
        var script = PauseBadge.Script("Paused", HudPage.Countdown);

        Assert.Contains("return 'no CoreUI0 frame';", script);
        Assert.Contains(@"if(/CoreUI0\.html/i.test(String(c.location)))continue;var o=c.document.getElementById('tc-pause-badge')", script);
        Assert.DoesNotContain("CoreUI1", script);
    }

    [Fact]
    public void Without_a_page_named_the_badge_stays_on_the_main_hud()
    {
        Assert.Contains("return 'no CoreUI1 frame';", PauseBadge.Script("Paused"));
    }
}

public class SpeedButtonsTests
{
    [Fact]
    public void Shown_in_the_countdown_to_the_disaster_where_the_game_has_none()
    {
        Assert.True(SpeedButtons.ShouldShow(isOn: true, isNormalMode: true, GameTimer.PrologueCountdown));
    }

    [Fact]
    public void Shown_on_expeditions_which_the_game_runs_outside_its_normal_mode()
    {
        Assert.True(SpeedButtons.ShouldShow(isOn: true, isNormalMode: false, GameTimer.Expedition));
    }

    [Fact]
    public void Never_shown_at_home_where_the_games_own_buttons_are()
    {
        Assert.False(SpeedButtons.ShouldShow(isOn: true, isNormalMode: true, GameTimer.DayClock));
        Assert.False(SpeedButtons.ShouldShow(isOn: true, isNormalMode: true, GameTimer.Other));
    }

    [Fact]
    public void Not_shown_on_an_expedition_left_at_the_games_pace()
    {
        var timer = GameTimers.Controlled(GameTimer.Expedition, isExpeditionControlOn: false);

        Assert.False(SpeedButtons.ShouldShow(isOn: true, isNormalMode: false, timer));
    }

    [Fact]
    public void Switched_off_shows_nothing_anywhere()
    {
        Assert.False(SpeedButtons.ShouldShow(isOn: false, isNormalMode: true, GameTimer.PrologueCountdown));
        Assert.False(SpeedButtons.ShouldShow(isOn: false, isNormalMode: false, GameTimer.Expedition));
    }

    [Fact]
    public void Not_shown_in_the_countdown_outside_the_games_normal_mode_where_its_speed_keys_do_not_work()
    {
        Assert.False(SpeedButtons.ShouldShow(isOn: true, isNormalMode: false, GameTimer.PrologueCountdown));
    }

    [Fact]
    public void The_countdown_has_its_own_hud_page_and_an_expedition_uses_the_main_one()
    {
        Assert.Equal(HudPage.Countdown, SpeedButtons.Page(GameTimer.PrologueCountdown));
        Assert.Equal(HudPage.Home, SpeedButtons.Page(GameTimer.Expedition));
        Assert.Equal("CoreUI0", HudPages.FileName(HudPage.Countdown));
        Assert.Equal("CoreUI1", HudPages.FileName(HudPage.Home));
    }

    [Fact]
    public void The_pause_button_is_lit_while_paused_whatever_speed_resumes()
    {
        Assert.Equal(PauseToggle.Paused, SpeedButtons.View(GameTimer.Expedition, isPaused: true, PauseToggle.Triple, "").ActiveLevel);
    }

    [Fact]
    public void While_running_the_button_for_the_speed_level_is_lit()
    {
        var view = SpeedButtons.View(GameTimer.PrologueCountdown, isPaused: false, PauseToggle.Slow, "0.5x");

        Assert.Equal(new SpeedButtonsView(HudPage.Countdown, PauseToggle.Slow, "0.5x"), view);
    }

    [Fact]
    public void Clicks_come_back_oldest_first()
    {
        Assert.Equal(new[] { 4, 1, 0 }, SpeedButtons.Clicks("ok:4,1,0"));
        Assert.Empty(SpeedButtons.Clicks("ok"));
    }

    [Fact]
    public void Anything_but_the_five_buttons_levels_is_never_applied()
    {
        Assert.Equal(new[] { 3 }, SpeedButtons.Clicks("ok:9,-1,x,,3"));
        Assert.Empty(SpeedButtons.Clicks("error: ReferenceError"));
        Assert.Empty(SpeedButtons.Clicks(null));
    }

    [Fact]
    public void Only_ok_with_or_without_clicks_counts_as_the_page_answering()
    {
        Assert.True(SpeedButtons.IsOk("ok"));
        Assert.True(SpeedButtons.IsOk("ok:2"));
        Assert.False(SpeedButtons.IsOk("okay"));
        Assert.False(SpeedButtons.IsOk("no CoreUI0 frame"));
        Assert.False(SpeedButtons.IsOk(null));
    }
}

public class ButtonsSyncTests
{
    private static readonly SpeedButtonsView Running = new(HudPage.Countdown, PauseToggle.Normal, "");
    private static readonly SpeedButtonsView Paused = Running with { ActiveLevel = PauseToggle.Paused };
    private static readonly ButtonsOnPage DrawnRunning = new ButtonsOnPage.Drawn(Running, NeedsRedraw: false);
    private static readonly ButtonsOnPage Unconfirmed = new ButtonsOnPage.Removed(IsConfirmed: false);

    [Fact]
    public void Buttons_shown_for_the_first_time_are_drawn_in_full()
    {
        Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Running, ButtonsOnPage.Start, isRefreshDue: false));
    }

    [Fact]
    public void Between_refreshes_an_unchanged_view_only_collects_the_clicks()
    {
        Assert.Equal(ButtonsScript.Collect, ButtonsSync.Next(Running, DrawnRunning, isRefreshDue: false));
    }

    [Fact]
    public void A_changed_view_is_drawn_at_once_without_waiting_for_a_refresh()
    {
        Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Paused, DrawnRunning, isRefreshDue: false));
        Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Running with { Page = HudPage.Home }, DrawnRunning, isRefreshDue: false));
    }

    [Fact]
    public void Each_refresh_draws_in_full_to_follow_the_pages_layout_and_texts()
    {
        Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Running, DrawnRunning, isRefreshDue: true));
    }

    [Fact]
    public void Buttons_the_page_may_have_lost_are_drawn_again_on_the_next_check()
    {
        Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Running, new ButtonsOnPage.Drawn(Running, NeedsRedraw: true), isRefreshDue: false));
    }

    [Fact]
    public void Buttons_that_just_hid_are_taken_off_at_once()
    {
        Assert.Equal(ButtonsScript.Remove, ButtonsSync.Next(null, DrawnRunning, isRefreshDue: false));
    }

    [Fact]
    public void Hidden_buttons_send_nothing_once_the_page_confirmed_their_removal()
    {
        Assert.Equal(ButtonsScript.None, ButtonsSync.Next(null, ButtonsOnPage.Start, isRefreshDue: true));
        Assert.Equal(ButtonsScript.None, ButtonsSync.Next(null, new ButtonsOnPage.Removed(IsConfirmed: true), isRefreshDue: true));
    }

    [Fact]
    public void A_removal_the_page_never_confirmed_is_sent_again_on_the_next_refresh()
    {
        Assert.Equal(ButtonsScript.Remove, ButtonsSync.Next(null, Unconfirmed, isRefreshDue: true));
        Assert.Equal(ButtonsScript.None, ButtonsSync.Next(null, Unconfirmed, isRefreshDue: false));
    }

    [Fact]
    public void A_draw_sent_records_the_view_drawn_and_a_removal_sent_waits_for_its_answer()
    {
        Assert.Equal(new ButtonsOnPage.Drawn(Paused, NeedsRedraw: false), ButtonsSync.AfterSending(ButtonsScript.Draw, Paused, new ButtonsOnPage.Drawn(Running, NeedsRedraw: true)));
        Assert.Equal(Unconfirmed, ButtonsSync.AfterSending(ButtonsScript.Remove, null, DrawnRunning));
    }

    [Fact]
    public void Collecting_or_sending_nothing_leaves_what_the_page_has_as_it_was()
    {
        Assert.Equal(DrawnRunning, ButtonsSync.AfterSending(ButtonsScript.Collect, Running, DrawnRunning));
        Assert.Equal(Unconfirmed, ButtonsSync.AfterSending(ButtonsScript.None, null, Unconfirmed));
    }

    [Fact]
    public void Clicks_in_an_answer_are_applied_while_the_buttons_are_still_drawn()
    {
        var taken = ButtonsSync.TakeReply(DrawnRunning, wasShownWhenSent: true, "ok:4,1");

        Assert.Equal(new[] { 4, 1 }, taken.Clicks);
        Assert.Equal(DrawnRunning, taken.OnPage);
    }

    [Fact]
    public void A_click_made_just_before_the_buttons_hid_is_never_applied()
    {
        var taken = ButtonsSync.TakeReply(Unconfirmed, wasShownWhenSent: true, "ok:2");

        Assert.Empty(taken.Clicks);
        Assert.Equal(Unconfirmed, taken.OnPage);
    }

    [Fact]
    public void Any_answer_but_ok_to_a_draw_or_collect_has_the_buttons_drawn_in_full_next()
    {
        foreach (var reply in new string?[] { SpeedButtons.NotOnPage, "no CoreUI0 frame", "error: TypeError", null })
        {
            var taken = ButtonsSync.TakeReply(DrawnRunning, wasShownWhenSent: true, reply);

            Assert.Empty(taken.Clicks);
            Assert.Equal(ButtonsScript.Draw, ButtonsSync.Next(Running, taken.OnPage, isRefreshDue: false));
        }
    }

    [Fact]
    public void Only_ok_to_a_removal_confirms_it()
    {
        Assert.Equal(new ButtonsOnPage.Removed(IsConfirmed: true), ButtonsSync.TakeReply(Unconfirmed, wasShownWhenSent: false, "ok").OnPage);
        Assert.Equal(Unconfirmed, ButtonsSync.TakeReply(Unconfirmed, wasShownWhenSent: false, "error: TypeError").OnPage);
    }

    [Fact]
    public void An_answer_to_a_removal_arriving_after_the_buttons_came_back_changes_nothing()
    {
        var taken = ButtonsSync.TakeReply(DrawnRunning, wasShownWhenSent: false, "ok");

        Assert.Equal(DrawnRunning, taken.OnPage);
        Assert.Empty(taken.Clicks);
    }
}

public class FastGearTests
{
    private const int OneGear = 1;

    [Fact]
    public void Pressing_fast_while_fast_goes_from_high_to_super_and_back_to_high()
    {
        var super = FastGear.GearAfter(FastGear.GamesOwn, OneGear, PauseToggle.Triple, FastPress.FromFastSpeed);
        var backToHigh = FastGear.GearAfter(super, OneGear, PauseToggle.Triple, FastPress.FromFastSpeed);

        Assert.Equal(1, super);
        Assert.Equal(FastGear.GamesOwn, backToHigh);
    }

    [Fact]
    public void A_fast_press_from_another_speed_lands_on_the_games_high_speed_first()
    {
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(1, OneGear, PauseToggle.Triple, FastPress.FromOtherSpeed));
    }

    [Fact]
    public void Pausing_at_super_speed_and_resuming_comes_back_to_super_speed()
    {
        var paused = FastGear.GearAfter(1, OneGear, PauseToggle.Paused, FastPress.FromFastSpeed);
        var resumed = FastGear.GearAfter(paused, OneGear, PauseToggle.Triple, FastPress.FromPause);

        Assert.Equal(1, resumed);
    }

    [Fact]
    public void Normal_double_or_slow_speed_leaves_super_speed_behind()
    {
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(1, OneGear, PauseToggle.Normal, FastPress.FromFastSpeed));
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(1, OneGear, PauseToggle.Double, FastPress.FromFastSpeed));
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(1, OneGear, PauseToggle.Slow, FastPress.FromFastSpeed));
    }

    [Fact]
    public void With_no_fast_gears_the_fast_button_stays_the_games_own()
    {
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(FastGear.GamesOwn, 0, PauseToggle.Triple, FastPress.FromFastSpeed));
    }

    [Fact]
    public void A_remembered_gear_that_no_longer_exists_falls_back_to_the_games_high_speed()
    {
        Assert.Equal(FastGear.GamesOwn, FastGear.GearAfter(3, OneGear, PauseToggle.Triple, FastPress.FromPause));
    }

    [Fact]
    public void A_paused_game_counts_as_paused_although_the_game_reads_it_as_normal_speed()
    {
        Assert.Equal(FastPress.FromPause, FastGear.PressFrom(isPaused: true, PauseToggle.Normal));
        Assert.Equal(FastPress.FromFastSpeed, FastGear.PressFrom(isPaused: false, PauseToggle.Triple));
        Assert.Equal(FastPress.FromOtherSpeed, FastGear.PressFrom(isPaused: false, PauseToggle.Double));
    }

    [Fact]
    public void Default_config_offers_one_gear_at_twice_the_games_high_speed()
    {
        Assert.Equal(new[] { 2f }, FastGear.ParseMultiples("2"));
    }

    [Fact]
    public void Gears_are_clamped_and_deduplicated_and_a_typo_drops_only_one_gear()
    {
        Assert.Equal(new[] { FastGear.LeastMultiple, 2f, FastGear.MostMultiple }, FastGear.ParseMultiples("1, 2, fast; 50, 2"));
    }

    [Fact]
    public void An_empty_setting_offers_no_gears_so_the_game_keeps_its_own_fast_button()
    {
        Assert.Empty(FastGear.ParseMultiples(""));
    }

    [Fact]
    public void Super_speed_is_its_multiple_of_the_games_high_speed()
    {
        Assert.Equal(10f, FastGear.HeldScale(gameFastSpeed: 5f, multiple: 2f));
    }

    [Fact]
    public void Super_speed_never_passes_the_ceiling_and_never_drops_below_the_games_high_speed()
    {
        Assert.Equal(FastGear.FastestScale, FastGear.HeldScale(gameFastSpeed: 5f, multiple: 10f));
        Assert.Equal(50f, FastGear.HeldScale(gameFastSpeed: 50f, multiple: 2f));
    }

    [Fact]
    public void Nothing_is_held_while_the_games_high_speed_is_unknown()
    {
        Assert.Null(FastGear.HeldScale(gameFastSpeed: 0f, multiple: 2f));
        Assert.Null(FastGear.HeldScale(gameFastSpeed: float.NaN, multiple: 2f));
    }

    [Fact]
    public void The_fastest_gear_is_the_one_with_the_largest_multiple_and_none_means_the_games_own()
    {
        Assert.Equal(2, FastGear.FastestGear(new[] { 2f, 4f, 3f }));
        Assert.Equal(FastGear.GamesOwn, FastGear.FastestGear(Array.Empty<float>()));
    }
}

public class SleepWatchTests
{
    private static readonly SleepSettings Both = new(IsFastForwardOn: true, IsPauseOnWakeOn: true, FastestGear: 1);
    private static readonly SleepSettings FastForwardOnly = Both with { IsPauseOnWakeOn = false };
    private static readonly SleepSettings PauseOnWakeOnly = Both with { IsFastForwardOn = false };
    private static readonly SleepSettings Neither = new(IsFastForwardOn: false, IsPauseOnWakeOn: false, FastestGear: 1);
    private static readonly GameSpeed Double = GameSpeed.Of(PauseToggle.Double, FastGear.GamesOwn);
    private static readonly GameSpeed Super = GameSpeed.Of(PauseToggle.Triple, 1);

    private static SleepSnapshot Sleeping(GameSpeed running, bool isPaused = false, bool isChangedByHand = false) =>
        new(IsAsleep: true, IsGameFastForwarding: false, IsBusy: true, isPaused, running, isChangedByHand, IsLeavingExpedition: false);

    private static SleepSnapshot Woken(bool isBusy = false, bool isPaused = false, bool isGameFastForwarding = false, bool isLeavingExpedition = false) =>
        new(IsAsleep: false, isGameFastForwarding, isBusy, isPaused, Super, WasSpeedChangedByHand: false, isLeavingExpedition);

    private static SleepStep Run(SleepWatch state, SleepSettings settings, params SleepSnapshot[] checks)
    {
        var step = new SleepStep(state, SleepCommand.Nothing);

        foreach (var seen in checks) step = SleepWatch.Next(step.State, seen, settings);

        return step;
    }

    [Fact]
    public void Falling_asleep_fast_forwards_at_the_fastest_gear_and_remembers_the_speed_before()
    {
        var step = Run(SleepWatch.Start, Both, Sleeping(Double));

        Assert.Equal(new SleepCommand.FastForward(1), step.Command);
        Assert.Equal(new SleepWatch.Asleep(Double, IsHeld: true), step.State);
    }

    [Fact]
    public void With_no_fast_gears_sleep_runs_at_the_games_high_speed()
    {
        var step = Run(SleepWatch.Start, Both with { FastestGear = FastGear.GamesOwn }, Sleeping(Double));

        Assert.Equal(new SleepCommand.FastForward(FastGear.GamesOwn), step.Command);
    }

    [Fact]
    public void A_sleep_that_starts_while_paused_waits_until_time_runs()
    {
        var whilePaused = Run(SleepWatch.Start, Both, Sleeping(Double, isPaused: true));
        var onceRunning = Run(whilePaused.State, Both, Sleeping(Double));

        Assert.Equal(SleepCommand.Nothing, whilePaused.Command);
        Assert.Equal(SleepWatch.Start, whilePaused.State);
        Assert.Equal(new SleepCommand.FastForward(1), onceRunning.Command);
    }

    [Fact]
    public void Waking_pauses_the_game_and_resumes_the_speed_from_before_the_sleep_not_the_sleeps_own()
    {
        var step = Run(SleepWatch.Start, Both, Sleeping(Double), Sleeping(Super), Woken());

        Assert.Equal(new SleepCommand.PauseOnWake(Double), step.Command);
        Assert.Equal(new SleepWatch.WakePaused(Double, WasBusy: false), step.State);
    }

    [Fact]
    public void Sleeping_from_super_speed_wakes_to_resume_super_speed()
    {
        var step = Run(SleepWatch.Start, Both, Sleeping(Super), Woken());

        Assert.Equal(new SleepCommand.PauseOnWake(Super), step.Command);
    }

    [Fact]
    public void The_wake_pause_waits_until_the_games_own_speed_up_has_finished_ending()
    {
        var step = Run(SleepWatch.Start, PauseOnWakeOnly, Sleeping(Double), Woken(isGameFastForwarding: true));

        Assert.Equal(SleepCommand.Nothing, step.Command);
        Assert.IsType<SleepWatch.Asleep>(step.State);
    }

    [Fact]
    public void The_wake_pause_happens_however_the_sleep_ended_even_after_a_speed_picked_by_hand()
    {
        var step = Run(SleepWatch.Start, Both, Sleeping(Double), Sleeping(Super, isChangedByHand: true), Woken());

        Assert.Equal(new SleepCommand.PauseOnWake(Double), step.Command);
    }

    [Fact]
    public void Waking_while_the_expedition_is_being_finished_does_not_pause_the_trip_home()
    {
        var step = Run(SleepWatch.Start, Both, Sleeping(Double), Woken(isLeavingExpedition: true));

        Assert.Equal(new SleepCommand.Restore(Double), step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void Without_the_wake_pause_the_speed_from_before_the_sleep_comes_back_on_waking()
    {
        var step = Run(SleepWatch.Start, FastForwardOnly, Sleeping(Double), Woken());

        Assert.Equal(new SleepCommand.Restore(Double), step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void A_speed_picked_by_hand_during_the_sleep_is_kept_after_waking()
    {
        var step = Run(SleepWatch.Start, FastForwardOnly, Sleeping(Double), Sleeping(Super, isChangedByHand: true), Woken());

        Assert.Equal(SleepCommand.Nothing, step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void An_order_given_after_waking_resumes_the_speed_from_before_the_sleep()
    {
        var step = Run(new SleepWatch.WakePaused(Double, WasBusy: false), Both, Woken(isBusy: true, isPaused: true));

        Assert.Equal(new SleepCommand.Resume(Double), step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void An_order_already_queued_on_waking_does_not_skip_the_wake_pause()
    {
        var onWaking = Run(SleepWatch.Start, Both, Sleeping(Double), Woken(isBusy: true));
        var stillQueued = Run(onWaking.State, Both, Woken(isBusy: true, isPaused: true));
        var newOrder = Run(stillQueued.State, Both, Woken(isPaused: true), Woken(isBusy: true, isPaused: true));

        Assert.Equal(new SleepCommand.PauseOnWake(Double), onWaking.Command);
        Assert.Equal(SleepCommand.Nothing, stillQueued.Command);
        Assert.IsType<SleepWatch.WakePaused>(stillQueued.State);
        Assert.Equal(new SleepCommand.Resume(Double), newOrder.Command);
    }

    [Fact]
    public void Pressing_play_after_waking_lets_time_run_until_the_next_order()
    {
        var step = Run(new SleepWatch.WakePaused(Double, WasBusy: false), Both, Woken());

        Assert.Equal(SleepCommand.LetTimeRun, step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void With_only_the_wake_pause_on_sleep_keeps_the_games_speed_and_still_pauses_on_waking()
    {
        var asleep = Run(SleepWatch.Start, PauseOnWakeOnly, Sleeping(Double));
        var woken = Run(asleep.State, PauseOnWakeOnly, Woken());

        Assert.Equal(SleepCommand.Nothing, asleep.Command);
        Assert.Equal(new SleepCommand.PauseOnWake(Double), woken.Command);
    }

    [Fact]
    public void With_both_settings_off_sleep_is_left_entirely_to_the_game()
    {
        var step = Run(SleepWatch.Start, Neither, Sleeping(Double), Woken());

        Assert.Equal(SleepCommand.Nothing, step.Command);
        Assert.Equal(SleepWatch.Start, step.State);
    }

    [Fact]
    public void A_fast_gear_only_counts_at_the_high_speed_level()
    {
        Assert.Equal(new GameSpeed(PauseToggle.Double, FastGear.GamesOwn), GameSpeed.Of(PauseToggle.Double, 1));
        Assert.Equal(new GameSpeed(PauseToggle.Normal, FastGear.GamesOwn), GameSpeed.Of(PauseToggle.Paused, 1));
        Assert.Equal(new GameSpeed(PauseToggle.Triple, 1), GameSpeed.Of(PauseToggle.Triple, 1));
    }
}

public class AlertPauseTests
{
    private static readonly AlertSettings Defaults = new(IsWaveOn: true, IsEmergencyOn: true, IsDroneArrivalOn: false, IsQueueEmptyOn: false);
    private static readonly AlertSettings AllOn = new(IsWaveOn: true, IsEmergencyOn: true, IsDroneArrivalOn: true, IsQueueEmptyOn: true);
    private static readonly AlertSnapshot Quiet = new(CanPause: true, IsPaused: false, IsWaveActive: false, EmergenciesFired: 0, DronesLanded: 0, IsBusy: false,
        ElapsedSeconds: 0.1f, WasSpeedChangedByHand: false);
    private static readonly AlertSnapshot WaveUnderWay = Quiet with { IsWaveActive = true };
    private static readonly AlertSnapshot EmergencyFired = Quiet with { EmergenciesFired = 1 };
    private static readonly AlertSnapshot DroneLanded = Quiet with { DronesLanded = 1 };
    private static readonly AlertSnapshot Busy = Quiet with { IsBusy = true };

    // Shorter than the queue's settle time, but two in a row are longer.
    private static readonly AlertSnapshot BetweenOrders = Quiet with { ElapsedSeconds = 0.2f };
    private static readonly AlertSnapshot StillEmpty = Quiet with { ElapsedSeconds = AlertPause.QueueSettleSeconds };

    /// <summary>The last step of the checks on a world that settled seeing the first of them.</summary>
    private static AlertStep Run(AlertSettings settings, params AlertSnapshot[] checks) =>
        Steps(settings, Enumerable.Repeat(checks[0], AlertPause.SettleChecks - 1).Concat(checks)).Last();

    /// <summary>The alerts paused for, from a world's very first check on.</summary>
    private static Alert[] PausesFromLoad(AlertSettings settings, IEnumerable<AlertSnapshot> checks) =>
        Steps(settings, checks).Select(step => step.PauseFor).OfType<Alert>().ToArray();

    private static List<AlertStep> Steps(AlertSettings settings, IEnumerable<AlertSnapshot> checks)
    {
        var state = AlertPause.Start;
        var steps = new List<AlertStep>();

        foreach (var seen in checks)
        {
            var step = AlertPause.Next(state, seen, settings);

            steps.Add(step);
            state = step.State;
        }

        return steps;
    }

    private static AlertSnapshot Paused(AlertSnapshot seen) => seen with { IsPaused = true };

    [Fact]
    public void A_wave_starting_pauses_the_game_and_holds_it_for_the_wave()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay);

        Assert.Equal(Alert.Wave, step.PauseFor);
        Assert.Equal(new AlertPause.Holding(Alert.Wave, WasWaveActive: true, QueueEmptySeconds: null), step.State);
    }

    [Fact]
    public void A_wave_under_way_when_the_world_loads_does_not_pause()
    {
        var step = Run(Defaults, WaveUnderWay, WaveUnderWay);

        Assert.Null(step.PauseFor);
    }

    [Fact]
    public void A_wave_the_load_puts_back_after_the_worlds_first_check_does_not_pause()
    {
        var load = new[] { Quiet }.Concat(Enumerable.Repeat(WaveUnderWay, AlertPause.SettleChecks + 2));

        Assert.Empty(PausesFromLoad(Defaults, load));
    }

    [Fact]
    public void Drone_landings_and_emergencies_while_a_load_settles_are_not_news()
    {
        var settling = new[] { Quiet, DroneLanded with { EmergenciesFired = 1 } }.Concat(Enumerable.Repeat(Quiet, AlertPause.SettleChecks - 2)).ToArray();

        Assert.Empty(PausesFromLoad(AllOn, settling));
        Assert.Equal(new[] { Alert.DroneArrival }, PausesFromLoad(AllOn, settling.Append(DroneLanded)));
    }

    [Fact]
    public void Resuming_during_a_wave_keeps_time_running_for_the_rest_of_it()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay, Paused(WaveUnderWay), WaveUnderWay, WaveUnderWay);

        Assert.Null(step.PauseFor);
        Assert.IsType<AlertPause.Watching>(step.State);
    }

    [Fact]
    public void A_wave_starting_while_already_paused_is_let_go_not_saved_for_later()
    {
        var whilePaused = Run(Defaults, Quiet, Paused(WaveUnderWay));
        var afterResume = Run(Defaults, Quiet, Paused(WaveUnderWay), WaveUnderWay);

        Assert.Null(whilePaused.PauseFor);
        Assert.IsType<AlertPause.Watching>(whilePaused.State);
        Assert.Null(afterResume.PauseFor);
    }

    [Fact]
    public void Each_new_wave_pauses_once_more()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay, WaveUnderWay, Quiet, WaveUnderWay);

        Assert.Equal(Alert.Wave, step.PauseFor);
    }

    [Fact]
    public void An_emergency_firing_pauses_the_game()
    {
        var step = Run(Defaults, Quiet, EmergencyFired);

        Assert.Equal(Alert.Emergency, step.PauseFor);
    }

    [Fact]
    public void An_emergency_counted_before_the_worlds_first_check_is_not_news()
    {
        var step = Run(Defaults, EmergencyFired);

        Assert.Null(step.PauseFor);
    }

    [Fact]
    public void A_drone_coming_home_pauses_only_when_switched_on()
    {
        Assert.Null(Run(Defaults, Quiet, DroneLanded).PauseFor);
        Assert.Equal(Alert.DroneArrival, Run(AllOn, Quiet, DroneLanded).PauseFor);
    }

    [Fact]
    public void The_order_queue_running_out_pauses_only_when_switched_on()
    {
        Assert.Null(Run(Defaults, Quiet, Busy, StillEmpty).PauseFor);
        Assert.Equal(Alert.QueueEmpty, Run(AllOn, Quiet, Busy, StillEmpty).PauseFor);
        Assert.Equal(Alert.QueueEmpty, Run(AllOn, Quiet, Busy, BetweenOrders, BetweenOrders).PauseFor);
    }

    [Fact]
    public void A_queue_that_stays_empty_pauses_only_when_it_runs_out()
    {
        Assert.Null(Run(AllOn, Quiet, Quiet, Quiet).PauseFor);
        Assert.Null(Run(AllOn, Quiet, Busy, StillEmpty, StillEmpty).PauseFor);
    }

    [Fact]
    public void The_queue_reading_empty_between_two_orders_does_not_pause()
    {
        Assert.Null(Run(AllOn, Quiet, Busy, BetweenOrders).PauseFor);
    }

    [Fact]
    public void An_order_inside_the_settle_time_means_the_queue_never_ran_out()
    {
        Assert.Null(Run(AllOn, Quiet, Busy, BetweenOrders, Busy, BetweenOrders).PauseFor);
    }

    [Fact]
    public void A_switched_off_alert_never_pauses()
    {
        var waveAndEmergencyOff = Defaults with { IsWaveOn = false, IsEmergencyOn = false };

        Assert.Null(Run(waveAndEmergencyOff, Quiet, WaveUnderWay).PauseFor);
        Assert.Null(Run(waveAndEmergencyOff, Quiet, EmergencyFired).PauseFor);
    }

    [Fact]
    public void An_alert_while_the_game_is_already_paused_is_let_go_not_saved_for_later()
    {
        var step = Run(Defaults, Quiet, Paused(EmergencyFired), Quiet);

        Assert.Null(step.PauseFor);
        Assert.IsType<AlertPause.Watching>(step.State);
    }

    [Fact]
    public void A_wave_starting_where_the_mod_does_not_pause_is_let_go()
    {
        var asleep = WaveUnderWay with { CanPause = false };
        var step = Run(Defaults, Quiet, asleep, WaveUnderWay);

        Assert.Null(step.PauseFor);
    }

    [Fact]
    public void Alerts_that_start_together_pause_once_for_the_most_pressing()
    {
        var step = Run(AllOn, Busy, WaveUnderWay with { EmergenciesFired = 1, DronesLanded = 1 });

        Assert.Equal(Alert.Wave, step.PauseFor);
    }

    [Fact]
    public void While_the_game_stays_paused_for_an_alert_it_keeps_naming_that_alert()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay, Paused(WaveUnderWay with { EmergenciesFired = 1 }));

        Assert.Null(step.PauseFor);
        Assert.Equal(Alert.Wave, Assert.IsType<AlertPause.Holding>(step.State).Reason);
    }

    [Fact]
    public void A_pause_made_by_hand_after_the_alert_pause_is_not_named_for_the_alert()
    {
        var resumedAndPausedAgain = Paused(WaveUnderWay) with { WasSpeedChangedByHand = true };
        var step = Run(Defaults, Quiet, WaveUnderWay, resumedAndPausedAgain);

        Assert.Null(step.PauseFor);
        Assert.IsType<AlertPause.Watching>(step.State);
    }

    [Fact]
    public void A_new_alert_after_the_player_resumes_pauses_again()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay, Paused(WaveUnderWay), WaveUnderWay with { EmergenciesFired = 1 });

        Assert.Equal(Alert.Emergency, step.PauseFor);
    }

    [Fact]
    public void Alerts_pause_only_under_the_timers_the_player_waits_out_and_never_in_a_fast_forward()
    {
        Assert.True(AlertPause.CanPause(GameTimer.DayClock, isFastForwarding: false, isLeavingExpedition: false));
        Assert.True(AlertPause.CanPause(GameTimer.PrologueCountdown, isFastForwarding: false, isLeavingExpedition: false));
        Assert.True(AlertPause.CanPause(GameTimer.Expedition, isFastForwarding: false, isLeavingExpedition: false));
        Assert.False(AlertPause.CanPause(GameTimer.Other, isFastForwarding: false, isLeavingExpedition: false));
        Assert.False(AlertPause.CanPause(GameTimer.DayClock, isFastForwarding: true, isLeavingExpedition: false));
    }

    [Fact]
    public void An_alert_while_the_expedition_is_being_finished_does_not_pause_the_trip_home()
    {
        var canPause = AlertPause.CanPause(GameTimer.Expedition, isFastForwarding: false, isLeavingExpedition: true);
        var step = Run(AllOn, Quiet, WaveUnderWay with { CanPause = canPause, EmergenciesFired = 1, DronesLanded = 1 });

        Assert.Null(step.PauseFor);
    }
}
