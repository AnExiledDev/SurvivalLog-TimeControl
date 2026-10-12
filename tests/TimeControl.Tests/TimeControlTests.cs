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
    public void Pressing_slow_while_slow_cycles_to_the_next_speed_and_wraps()
    {
        Assert.Equal(1, SlowGear.NextIndex(0, 2, wasAlreadySlow: true));
        Assert.Equal(0, SlowGear.NextIndex(1, 2, wasAlreadySlow: true));
    }

    [Fact]
    public void Coming_back_to_slow_from_another_speed_keeps_the_speed_last_used()
    {
        Assert.Equal(1, SlowGear.NextIndex(1, 2, wasAlreadySlow: false));
    }

    [Fact]
    public void A_remembered_speed_that_no_longer_exists_falls_back_to_the_first()
    {
        Assert.Equal(0, SlowGear.NextIndex(5, 2, wasAlreadySlow: false));
        Assert.Equal(-1, SlowGear.NextIndex(0, 0, wasAlreadySlow: true));
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
        new(CanFreeze: true, IsBusy: false, IsPaused: false, seconds, runningLevel);

    private static IdleSnapshot IdleWhilePaused(float seconds) =>
        new(CanFreeze: true, IsBusy: false, IsPaused: true, seconds, PauseToggle.Normal);

    private static IdleSnapshot OrderGiven(bool isPaused) =>
        new(CanFreeze: true, IsBusy: true, IsPaused: isPaused, 0.1f, PauseToggle.Normal);

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
        var awayFromHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: false, 5f, PauseToggle.Normal);

        var step = Run(new IdlePause.Released(), awayFromHome, Idle(0.6f), Idle(0.6f));

        Assert.Equal(IdleCommand.PauseGame, step.Command);
    }

    [Fact]
    public void Leaving_home_or_sleeping_while_paused_by_idling_resumes_the_game()
    {
        var leftHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: true, 0.1f, PauseToggle.Normal);

        var step = Run(new IdlePause.Frozen(3), leftHome);

        Assert.Equal(new IdleCommand.Resume(3), step.Command);
    }

    [Fact]
    public void Idle_time_where_pausing_is_not_allowed_never_counts()
    {
        var awayFromHome = new IdleSnapshot(CanFreeze: false, IsBusy: false, IsPaused: false, 5f, PauseToggle.Normal);

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

public class ClockPaceTests
{
    [Fact]
    public void The_day_clock_follows_the_configured_rate()
    {
        Assert.Equal(0.5f, ClockPace.Factor(0.5f, isDayClock: true));
    }

    [Fact]
    public void Exploration_timers_keep_the_games_pace()
    {
        Assert.Equal(1f, ClockPace.Factor(0.5f, isDayClock: false));
    }

    [Fact]
    public void A_broken_rate_setting_cannot_stop_or_race_the_clock()
    {
        Assert.Equal(ClockPace.SlowestRate, ClockPace.ClampRate(0f));
        Assert.Equal(ClockPace.FastestRate, ClockPace.ClampRate(100f));
        Assert.Equal(1f, ClockPace.ClampRate(float.NaN));
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
    public void A_slowed_clock_is_drawn_at_the_slowed_pace()
    {
        var update = PageClock.Next(0.5f, 2f, "", wasNeutralLastSent: true);

        Assert.Equal(new PageClockUpdate(1f, ""), update);
    }

    [Fact]
    public void A_frozen_clock_is_drawn_stopped()
    {
        Assert.Equal(0f, PageClock.Next(0f, 1f, "", wasNeutralLastSent: true)?.ClockScale);
    }

    [Fact]
    public void Holding_a_slow_speed_only_relabels_and_leaves_the_clock_to_the_game()
    {
        Assert.Equal(new PageClockUpdate(null, "0.25x"), PageClock.Next(1f, 0.25f, "0.25x", wasNeutralLastSent: true));
    }

    [Fact]
    public void Nothing_is_sent_while_the_mod_changes_nothing()
    {
        Assert.Null(PageClock.Next(1f, 1f, "", wasNeutralLastSent: true));
    }

    [Fact]
    public void Turning_everything_off_puts_the_clock_back_at_game_speed_once()
    {
        Assert.Equal(new PageClockUpdate(2f, ""), PageClock.Next(1f, 2f, "", wasNeutralLastSent: false));
    }

    [Fact]
    public void Scripts_use_a_dot_decimal_whatever_the_system_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            Assert.Contains("var k=0.25;", PageClock.Script(new PageClockUpdate(0.25f, "")));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_label_cannot_break_out_of_the_script_string()
    {
        var script = PageClock.Script(new PageClockUpdate(null, "0.5x');alert(1);//"));

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
        new(IsAsleep: true, IsGameFastForwarding: false, IsBusy: true, isPaused, running, isChangedByHand);

    private static SleepSnapshot Woken(bool isBusy = false, bool isPaused = false, bool isGameFastForwarding = false) =>
        new(IsAsleep: false, isGameFastForwarding, isBusy, isPaused, Super, WasSpeedChangedByHand: false);

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
    private static readonly AlertSnapshot Quiet = new(CanPause: true, IsPaused: false, IsWaveActive: false, EmergenciesFired: 0, DronesLanded: 0, IsBusy: false);
    private static readonly AlertSnapshot WaveUnderWay = Quiet with { IsWaveActive = true };
    private static readonly AlertSnapshot EmergencyFired = Quiet with { EmergenciesFired = 1 };
    private static readonly AlertSnapshot DroneLanded = Quiet with { DronesLanded = 1 };
    private static readonly AlertSnapshot Busy = Quiet with { IsBusy = true };

    private static AlertStep Run(AlertSettings settings, params AlertSnapshot[] checks)
    {
        var step = new AlertStep(AlertPause.Start, null);

        foreach (var seen in checks) step = AlertPause.Next(step.State, seen, settings);

        return step;
    }

    private static AlertSnapshot Paused(AlertSnapshot seen) => seen with { IsPaused = true };

    [Fact]
    public void A_wave_starting_pauses_the_game_and_holds_it_for_the_wave()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay);

        Assert.Equal(Alert.Wave, step.PauseFor);
        Assert.Equal(new AlertPause.Holding(Alert.Wave, WasWaveActive: true, WasBusy: false), step.State);
    }

    [Fact]
    public void A_wave_under_way_when_the_world_loads_does_not_pause()
    {
        var step = Run(Defaults, WaveUnderWay, WaveUnderWay);

        Assert.Null(step.PauseFor);
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
        Assert.Null(Run(Defaults, Quiet, Busy, Quiet).PauseFor);
        Assert.Equal(Alert.QueueEmpty, Run(AllOn, Quiet, Busy, Quiet).PauseFor);
    }

    [Fact]
    public void A_queue_that_stays_empty_pauses_only_when_it_runs_out()
    {
        Assert.Null(Run(AllOn, Quiet, Quiet, Quiet).PauseFor);
        Assert.Null(Run(AllOn, Quiet, Busy, Quiet, Quiet).PauseFor);
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
    public void A_new_alert_after_the_player_resumes_pauses_again()
    {
        var step = Run(Defaults, Quiet, WaveUnderWay, Paused(WaveUnderWay), WaveUnderWay with { EmergenciesFired = 1 });

        Assert.Equal(Alert.Emergency, step.PauseFor);
    }

    [Fact]
    public void Alerts_pause_only_under_the_timers_the_player_waits_out_and_never_in_a_fast_forward()
    {
        Assert.True(AlertPause.CanPause(GameTimer.DayClock, isFastForwarding: false));
        Assert.True(AlertPause.CanPause(GameTimer.PrologueCountdown, isFastForwarding: false));
        Assert.True(AlertPause.CanPause(GameTimer.Expedition, isFastForwarding: false));
        Assert.False(AlertPause.CanPause(GameTimer.Other, isFastForwarding: false));
        Assert.False(AlertPause.CanPause(GameTimer.DayClock, isFastForwarding: true));
    }
}
