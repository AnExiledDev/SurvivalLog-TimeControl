using System;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Wait until, the alarm clock, start paused and sleep until: each check reads the clock, carries out the clicks on
/// the wait menu and steps each of them, after the sleep and the alert pause have had their turn and before idle
/// freeze, which a running wait keeps out of the way.
/// </summary>
public sealed partial class ClockTicker
{
    private const float WaitPageSeconds = 0.5f;

    internal static WaitPageLink? WaitPage;

    private WaitUntil _wait = WaitUntil.Start;
    private AlarmClock _alarm = AlarmClock.Start;
    private SleepUntil _sleepUntil = SleepUntil.Start;
    private StartPause _startPause = StartPause.Done;
    private IntPtr _waitWorld;
    private bool _wasAlertHolding;
    private float _nextWaitPageAt;

    private void UpdateWaits(GameTimeManager time, GameTimer timer, bool isAsleep, bool isLeavingExpedition, bool wasChangedByHand, float elapsed, float now)
    {
        ForgetWaitsOnNewWorld(time);
        UpdateStartPause(time, timer);

        var reading = GameWait.Read(time, timer);
        var brewDoneAt = GameWait.BrewDoneAt(timer, now);
        var canStartWait = !isAsleep && !IsFastForwarding(time) && !isLeavingExpedition;

        UpdateWait(time, timer, reading, brewDoneAt, isAsleep, wasChangedByHand, elapsed);
        PressWaitMenu(time, timer, reading, brewDoneAt, canStartWait);
        UpdateAlarm(time, timer, reading, canStartWait);
        UpdateSleepUntil(time, isAsleep, reading);
        SyncWaitPage(time, timer, reading, brewDoneAt, canStartWait, now);
    }

    /// <summary>A wait, an alarm from the menu and a sleep being cut short all count on the old world's clock, so a new world drops them.</summary>
    private void ForgetWaitsOnNewWorld(GameTimeManager time)
    {
        if (time.Pointer == _waitWorld) return;

        _waitWorld = time.Pointer;
        _wait = WaitUntil.Start;
        _alarm = AlarmClock.ForNewWorld(_alarm);
        _sleepUntil = SleepUntil.Start;
        _wasAlertHolding = false;
        _startPause = GameWait.TakeEntered(time.Pointer) ? StartPause.Entered : StartPause.Done;
    }

    /// <summary>The speed the world starts at is kept for play and the pause key, as if the player had paused it.</summary>
    private void UpdateStartPause(GameTimeManager time, GameTimer timer)
    {
        var step = StartPause.Next(_startPause, new StartSnapshot(WaitConfig.IsStartPausedOn, timer, SpeedPatches.IsNormalMode));

        _startPause = step.State;

        if (!step.ShouldPause || time.IsPausedScale()) return;

        var resumeAt = SpeedPatches.CurrentSpeed(time, isPaused: false);

        SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
        SpeedPatches.SetResumeSpeed(resumeAt);
        LogFileOnly($"start paused: the save started paused; play or the pause key resumes {Describe(resumeAt)}");
    }

    /// <summary>
    /// Stepped before the menu's clicks, so a wait started on this check is first looked at on the next, once the speed
    /// it set has been seen. A running wait keeps idle freeze released, so being idle never pauses it.
    /// </summary>
    private void UpdateWait(GameTimeManager time, GameTimer timer, ClockReading? reading, long? brewDoneAt, bool isAsleep, bool wasChangedByHand, float elapsed)
    {
        if (reading is { } read) _wait = WaitUntil.FollowBrew(_wait, brewDoneAt, read);

        var isBusy = _wait is WaitUntil.Waiting { Goal: WaitGoal.QueueDone } && time.IsLeadingRoleBusy();
        var seen = new WaitSnapshot(timer, reading, time.IsPausedScale(), wasChangedByHand, isBusy, isAsleep, _sleep is SleepWatch.Asleep, IsFastForwarding(time), elapsed, _away is AwayPause.Holding);
        var before = _wait;
        var step = WaitUntil.Next(before, seen);

        _wait = step.State;
        CarryWait(time, step.Command);
        LogWait(before, step);

        if (WaitUntil.IsWaiting(_wait)) _idle = new IdlePause.Released();
    }

    private void CarryWait(GameTimeManager time, WaitCommand command)
    {
        switch (command)
        {
            case WaitCommand.FastForward fastForward:
                SpeedPatches.ChangeSpeed(time, fastForward.Speed);
                break;
            case WaitCommand.Arrive arrive:
                if (!time.IsPausedScale()) SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);

                SpeedPatches.SetResumeSpeed(arrive.Before);
                break;
            case WaitCommand.ResumeTo resumeTo:
                SpeedPatches.SetResumeSpeed(resumeTo.Before);
                break;
            case WaitCommand.Restore restore when time.IsPausedScale():
                SpeedPatches.SetResumeSpeed(restore.Before);
                break;
            case WaitCommand.Restore restore:
                SpeedPatches.ChangeSpeed(time, restore.Before);
                break;
            case WaitCommand.HandToSleep handOver when _sleep is SleepWatch.Asleep asleep:
                _sleep = asleep with { Before = handOver.Before };
                break;
        }
    }

    private static void LogWait(WaitUntil from, WaitStep step)
    {
        var line = (from, step.State, step.Command) switch
        {
            (_, _, WaitCommand.Arrive arrive) => $"wait: reached, paused the game; play or the pause key resumes {Describe(arrive.Before)}",
            (_, _, WaitCommand.ResumeTo resumeTo) => $"wait: the game was paused before it was reached, ended; play or the pause key resumes {Describe(resumeTo.Before)}",
            (_, _, WaitCommand.Restore restore) => $"wait: ended, put back {Describe(restore.Before)}",
            (_, _, WaitCommand.HandToSleep) => "wait: the character fell asleep, ended; waking puts back the speed from before it",
            (WaitUntil.Waiting, WaitUntil.AfterSleep, _) => "wait: the character fell asleep, ended; the speed from before it comes back once they are up",
            (WaitUntil.Waiting, WaitUntil.Idle, _) => "wait: speed changed by hand, ended",
            (WaitUntil.AfterSleep, WaitUntil.Idle, _) => "wait: speed changed by hand after waking, kept",
            _ => null,
        };

        if (line != null) LogFileOnly(line);
    }

    /// <summary>Clicks go through only while the menu is meant to show: on a clock a wait can run on, read this check.</summary>
    private void PressWaitMenu(GameTimeManager time, GameTimer timer, ClockReading? reading, long? brewDoneAt, bool canStartWait)
    {
        if (WaitPage?.TakeClicks() is not { Count: > 0 } clicks || !WaitUntil.CanWait(timer) || reading is not { } read) return;

        foreach (var id in clicks)
        {
            switch (WaitMenu.ChoiceFor(id))
            {
                case WaitChoice.WaitFor waitFor:
                    StartWait(time, timer, read, waitFor, brewDoneAt, canStartWait);
                    break;
                case WaitChoice.CancelWait:
                    var cancel = WaitUntil.Cancel(_wait);

                    LogWait(_wait, cancel);
                    _wait = cancel.State;
                    CarryWait(time, cancel.Command);
                    break;
                case WaitChoice.AlarmIn alarmIn:
                    _alarm = AlarmClock.SetQuick(_alarm, read, timer, alarmIn.Hours);
                    LogFileOnly($"alarm: set {alarmIn.Hours}h on, for {ClockHour.Label(read.SecondOfDayAt(_alarm.QuickAlarmAt ?? read.Total))}");
                    break;
                case WaitChoice.CancelAlarm:
                    _alarm = AlarmClock.CancelQuick(_alarm);
                    LogFileOnly("alarm: the one set from the menu was cancelled");
                    break;
            }
        }
    }

    private void StartWait(GameTimeManager time, GameTimer timer, ClockReading reading, WaitChoice.WaitFor choice, long? brewDoneAt, bool canStartWait)
    {
        var isBusy = choice.Kind == WaitKind.Queue && time.IsLeadingRoleBusy();
        var goal = canStartWait ? WaitMenu.GoalFor(choice, reading, WaitConfig.Alarm.SecondOfDay, brewDoneAt, isBusy) : null;

        if (goal == null)
        {
            LogFileOnly($"wait: {choice.Kind} not started, nothing to wait for or the character is asleep");

            return;
        }

        var step = WaitUntil.Begin(_wait, goal, SpeedPatches.CurrentSpeed(time, time.IsPausedScale()), timer, SpeedPatches.FastestGear);

        _wait = step.State;
        CarryWait(time, step.Command);

        if (_wait is WaitUntil.Waiting waiting) LogFileOnly($"wait: started for {choice.Kind}, fast-forwarding at {time.BattleTimeScale:0.##}x; {Describe(waiting.Before)} before");
    }

    /// <summary>The alarm stays quiet while the character sleeps, while the game fast-forwards on its own and while an expedition is being finished.</summary>
    private void UpdateAlarm(GameTimeManager time, GameTimer timer, ClockReading? reading, bool canRing)
    {
        var step = AlarmClock.Next(_alarm, new AlarmSnapshot(timer, reading, canRing), WaitConfig.Alarm);

        _alarm = step.State;

        if (step.Rang is not { } rang) return;

        if (time.IsPausedScale())
        {
            LogFileOnly($"alarm: {Describe(rang)} came due while paused, left as it is");

            return;
        }

        SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
        LogFileOnly($"alarm: {Describe(rang)} rang, paused the game; play or the pause key resumes speed level {SpeedPatches.LastRunningLevel}");
    }

    private static string Describe(AlarmRing ring) => ring switch
    {
        AlarmRing.Set => "the alarm from the settings",
        AlarmRing.Quick => "the alarm set from the menu",
        _ => throw new ArgumentOutOfRangeException(nameof(ring), ring, "an alarm with no description"),
    };

    /// <summary>Whether the sleep is a real one is only read while the character is asleep and a setting could cut it short.</summary>
    private void UpdateSleepUntil(GameTimeManager time, bool isAsleep, ClockReading? reading)
    {
        var settings = WaitConfig.SleepUntil;
        var isAlertHolding = _alert is AlertPause.Holding;
        var isNewAlert = isAlertHolding && !_wasAlertHolding;

        _wasAlertHolding = isAlertHolding;

        if (!settings.IsAnyOn && _sleepUntil is SleepUntil.Awake) return;

        var isWakeable = isAsleep && settings.IsAnyOn && _sleepUntil is SleepUntil.Awake && GameWait.IsWakeableSleep();
        var seen = new SleepUntilSnapshot(isAsleep, isWakeable, reading, isNewAlert, IsFastForwarding(time), time.IsPausedScale());
        var step = SleepUntil.Next(_sleepUntil, seen, settings);

        _sleepUntil = step.State;

        switch (step.Command)
        {
            case SleepUntilCommand.Wake:
                var isWoken = GameWait.WakeLeadingRole();

                LogFileOnly(isWoken
                    ? $"sleep until: {(_sleepUntil is SleepUntil.Woken { ByAlert: true } ? "an alert paused the game" : "the wake time came")}, woke the character"
                    : "sleep until: could not wake the character");
                break;
            case SleepUntilCommand.Pause:
                SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
                LogFileOnly("sleep until: up after an alert, paused the game");
                break;
        }
    }

    /// <summary>
    /// An alert may pause the game during a real sleep only when it is to wake the character; otherwise a sleeping
    /// character is left to sleep through it, as without this setting.
    /// </summary>
    private static bool CanAlertWakeSleep(bool isAsleep) =>
        isAsleep && WaitConfig.SleepUntil.IsWakeOnAlertOn && GameWait.IsWakeableSleep();

    /// <summary>
    /// Sent at the page pace, which also takes the menu's clicks, so a click is carried out within half a second. The
    /// button shows, when its setting is on, wherever a wait can run and the page has speed buttons to sit beside.
    /// </summary>
    private void SyncWaitPage(GameTimeManager time, GameTimer timer, ClockReading? reading, long? brewDoneAt, bool canStartWait, float now)
    {
        if (WaitPage == null || now < _nextWaitPageAt) return;

        _nextWaitPageAt = now + WaitPageSeconds;

        if (!WaitConfig.IsWaitButtonOn || !WaitUntil.CanWait(timer) || reading is not { } read)
        {
            WaitPage.Sync(null, isRefreshDue: true);

            return;
        }

        var input = new WaitMenuInput(SpeedButtons.Page(timer), _wait, read, _alarm.QuickAlarmAt, WaitConfig.Alarm.SecondOfDay,
            brewDoneAt != null, time.IsLeadingRoleBusy(), canStartWait, IsChinese());

        WaitPage.Sync(WaitMenu.View(input), isRefreshDue: true);
    }

    private static void HideWaitPage(bool isRefreshDue) => WaitPage?.Sync(null, isRefreshDue);

    /// <summary>A failed check must never leave a wait's fast speed running: the speed from before it is put back.</summary>
    private void ForgetWaits()
    {
        GameSpeed? before = _wait switch
        {
            WaitUntil.Waiting waiting => waiting.Before,
            WaitUntil.AfterSleep afterSleep => afterSleep.Before,
            _ => null,
        };

        _wait = WaitUntil.Start;
        _sleepUntil = SleepUntil.Start;
        _startPause = StartPause.Done;

        if (before is { } speed) TryRestoreSpeed(speed);
    }

    private static void TryRestoreSpeed(GameSpeed speed)
    {
        try
        {
            if (GameClock.Find() is not { } time) return;

            if (time.IsPausedScale()) SpeedPatches.SetResumeSpeed(speed);
            else SpeedPatches.ChangeSpeed(time, speed);
        }
        catch (Exception)
        {
            // Best effort inside a failure already being logged; the player can still pick a speed.
        }
    }
}
