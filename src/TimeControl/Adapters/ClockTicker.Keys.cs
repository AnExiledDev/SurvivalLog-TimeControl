using System;
using GameCore.HotUpdate.Battle.Logic;
using Il2CppInterop.Runtime.Attributes;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// The hold keys, the custom speed keys and the mouse wheel over the speed buttons, the custom speed they keep in
/// place, and the notes under the clock at home: when the current order is done, and how long until night and the
/// next wave.
/// </summary>
public sealed partial class ClockTicker
{
    internal static KeysPage? SpeedKeysPage;

    private HoldKeys _hold = HoldKeys.Start;
    private CustomHold _custom = CustomHold.Start;
    private IdlePause _idleBeforeHold = IdlePause.Start;
    private float _nextKeysPageAt;
    private float _nextCustomHealLogAt;
    private bool _hasLoggedNoSlowSpeeds;

    /// <summary>
    /// Runs before anything else reads the speed, so a hold, a custom speed key or a turn of the wheel is the speed the
    /// rest of the check sees. Nothing is asked of the game unless a key is down or was pressed, or the wheel turned.
    /// </summary>
    [HideFromIl2Cpp]
    private void UpdateSpeedKeys(GameTimeManager time, GameTimer timer, bool isLeavingExpedition)
    {
        var keys = SpeedPatches.KeySettings;
        var now = Time.unscaledTime;
        var wasChangedByOthers = SpeedPatches.TakeChangedByOthers();
        var customPressed = SpeedPatches.TakeCustomSpeedPressed();
        var wheelSteps = SpeedKeysPage?.TakeWheelSteps() ?? 0;
        var isPaused = time.IsPausedScale();

        if (_custom is not CustomHold.None) _custom = CustomHold.Next(_custom, isPaused, time.GetSpeedLevel(), wasChangedByOthers);

        var isFastDown = SpeedPatches.IsHoldKeyDown(HoldKind.Fast, now);
        var isSlowDown = IsSlowHoldDown(now);
        var isGameKeyDown = SpeedPatches.IsGameKeyHeldFromPause;

        if (_hold is HoldKeys.Idle && !isFastDown && !isSlowDown && !isGameKeyDown && customPressed == null && wheelSteps == 0) return;

        var canChange = GameTimers.IsWaitedOut(timer) && !isLeavingExpedition && !IsFastForwarding(time) && !time.IsHoldBoostActive() &&
                        !GameClock.IsLeadingRoleAsleep();

        UpdateHold(time, new HoldSnapshot(isFastDown, isSlowDown, canChange, wasChangedByOthers,
            new HeldFrom(isPaused, SpeedPatches.IsPausedByKey, SpeedPatches.CurrentTarget(time, isPaused, _custom)), isGameKeyDown));

        if (_hold is HoldKeys.Holding)
        {
            _idle = new IdlePause.Released();

            return;
        }

        if (!canChange) return;

        if (customPressed is { } custom) PressCustomSpeed(time, custom);

        if (wheelSteps != 0 && keys.IsWheelOn && !time.IsPausedScale()) TurnWheel(time, keys, wheelSteps);
    }

    /// <summary>A slow hold needs the mod's slow speeds; asking for one without them is logged once.</summary>
    [HideFromIl2Cpp]
    private bool IsSlowHoldDown(float now)
    {
        if (!SpeedPatches.IsHoldKeyDown(HoldKind.Slow, now)) return false;

        if (SpeedPatches.SlowestTarget != null) return true;

        if (!_hasLoggedNoSlowSpeeds)
        {
            _hasLoggedNoSlowSpeeds = true;
            Log("hold keys: HoldSlowKey holds the slowest of SlowSpeeds, which is empty, so the key does nothing (logged once)");
        }

        return false;
    }

    [HideFromIl2Cpp]
    private void UpdateHold(GameTimeManager time, HoldSnapshot seen)
    {
        var wasIdle = _hold is HoldKeys.Idle;
        var step = HoldKeys.Next(_hold, seen);

        if (wasIdle && step.Command is HoldCommand.Start) _idleBeforeHold = _idle;

        _hold = step.State;
        CarryHold(time, step.Command);
    }

    [HideFromIl2Cpp]
    private void CarryHold(GameTimeManager time, HoldCommand command)
    {
        switch (command)
        {
            case HoldCommand.Start start:
                StartHold(time, start.Kind);
                break;
            case HoldCommand.Release release when release.From.WasPaused:
                SpeedPatches.PauseForKeys(time, release.From.WasPausedByKey);
                SpeedPatches.SetResumeTarget(time, release.From.Running);
                _custom = CustomAfterHold(time, release.From.Running, isPaused: true);
                _idle = IdleAfterHold();
                LogFileOnly($"hold keys: let go, paused the game again; play or the pause key resumes {SpeedTargets.Describe(release.From.Running)}");
                break;
            case HoldCommand.Release release:
                SpeedPatches.ApplyTarget(time, release.From.Running);
                _custom = CustomAfterHold(time, release.From.Running, isPaused: false);
                _idle = IdleAfterHold();
                LogFileOnly($"hold keys: let go, put back {SpeedTargets.Describe(release.From.Running)}");
                break;
            case HoldCommand.Cancel cancel:
                CancelHold(time, cancel.From);
                break;
        }
    }

    [HideFromIl2Cpp]
    private void StartHold(GameTimeManager time, HoldKind kind)
    {
        var wanted = kind switch
        {
            HoldKind.Fast => SpeedPatches.FastestTarget,
            HoldKind.GameKey => SpeedPatches.GameKeyTarget(time),
            _ => SpeedPatches.SlowestTarget,
        };

        if (wanted is not { } target) return;

        SpeedPatches.ApplyTarget(time, target);
        _custom = CustomHold.Start;
        LogFileOnly($"hold keys: holding {SpeedTargets.Describe(target)}");
    }

    /// <summary>
    /// Something else changed the speed during the hold, or it stopped being the player's to change: the speed is left
    /// as it is, but a pause made now resumes to where the game was before the hold rather than the held speed.
    /// </summary>
    [HideFromIl2Cpp]
    private void CancelHold(GameTimeManager time, HeldFrom from)
    {
        var isPaused = time.IsPausedScale();

        if (isPaused)
        {
            SpeedPatches.SetResumeTarget(time, from.Running);
            _custom = CustomAfterHold(time, from.Running, isPaused: true);
        }

        _idle = IdlePause.Start;
        LogFileOnly(isPaused
            ? $"hold keys: the game was paused during the hold; play or the pause key resumes {SpeedTargets.Describe(from.Running)}"
            : "hold keys: the speed was changed during the hold, it is kept until every hold key is let go");
    }

    /// <summary>A custom speed the hold took over comes back with it, held while running and waiting on a resume while paused.</summary>
    private static CustomHold CustomAfterHold(GameTimeManager time, SpeedTarget running, bool isPaused)
    {
        if (running is not SpeedTarget.Custom custom) return CustomHold.Start;

        var level = SpeedPatches.CustomLevel(time, custom.Scale);

        return isPaused ? new CustomHold.Suspended(custom.Scale, level) : new CustomHold.Held(custom.Scale, level);
    }

    /// <summary>
    /// Idle freeze picks up where it was before the hold: a pause it made is its own again, so the next order resumes
    /// it, and time the player let run while idle keeps running. Otherwise it starts counting idle time afresh.
    /// </summary>
    [HideFromIl2Cpp]
    private IdlePause IdleAfterHold() => _idleBeforeHold is IdlePause.Frozen or IdlePause.Released ? _idleBeforeHold : IdlePause.Start;

    /// <summary>A custom speed key also resumes a pause, as the game's own speed keys do.</summary>
    [HideFromIl2Cpp]
    private void PressCustomSpeed(GameTimeManager time, float scale)
    {
        SpeedPatches.ApplyTarget(time, new SpeedTarget.Custom(scale));
        _custom = new CustomHold.Held(scale, SpeedPatches.CustomLevel(time, scale));
        LogFileOnly($"speed keys: {SpeedTargets.Describe(new SpeedTarget.Custom(scale))}");
    }

    /// <summary>The wheel steps through every speed the mod has, slowest to fastest, from the speed the game runs at now.</summary>
    [HideFromIl2Cpp]
    private void TurnWheel(GameTimeManager time, KeysSettings keys, int steps)
    {
        var ladder = SpeedPatches.Ladder(time, keys.CustomSpeeds);

        if (SpeedLadder.Walk(ladder, time.BattleTimeScale, steps) is not { } step) return;

        SpeedPatches.ApplyTarget(time, step.Target);
        _custom = CustomAfterHold(time, step.Target, isPaused: false);
        LogFileOnly($"speed wheel: {SpeedTargets.Describe(step.Target)}");
    }

    /// <summary>
    /// The game puts one of its own speeds back after saves, menus and resumes, so a custom speed is checked and
    /// restored here; the game's own fast-forwards and its own speed boost are left alone.
    /// </summary>
    [HideFromIl2Cpp]
    private void KeepCustomSpeedHeld(GameTimeManager time, bool isPaused)
    {
        if (isPaused || CustomHold.HeldScale(_custom) is not { } held) return;

        if (Math.Abs(time.BattleTimeScale - held) <= SpeedTolerance || IsFastForwarding(time) || time.IsHoldBoostActive()) return;

        var now = Time.unscaledTime;

        if (now >= _nextCustomHealLogAt)
        {
            _nextCustomHealLogAt = now + RepeatLogSeconds;
            Log($"custom speed: game speed was {time.BattleTimeScale:0.###}x, putting {SlowGear.Label(held)} back (logged at most once a minute)");
        }

        GameClock.HoldSpeed(time, held);
    }

    /// <summary>
    /// The wheel switches on or off within a check and has its turns taken at the poll pace; the notes under the clock
    /// are worked out at the page pace, and only at home.
    /// </summary>
    [HideFromIl2Cpp]
    private void SyncKeysPage(GameTimeManager time, GameTimer timer, float now, bool isPollDue)
    {
        var keys = SpeedPatches.KeySettings;
        var generation = Watch?.Generation ?? 0;

        SpeedKeysPage?.SyncWheel(keys.IsWheelOn, isPollDue, generation);

        if (!TakeKeysPageRefresh(now)) return;

        var isChinese = _isChinese;
        var orderFinish = "";
        var countdowns = "";

        if (timer == GameTimer.DayClock && CountdownSignals.TotalSeconds(time) is { } clock)
        {
            if (keys.IsOrderFinishOn) orderFinish = OrderFinish.Label(clock, CountdownSignals.OrderSecondsLeft(), isChinese);

            countdowns = CountdownText(keys.Countdowns, clock, isChinese);
        }

        SpeedKeysPage?.SyncOrderFinish(orderFinish, isRefreshDue: true, generation);
        SpeedKeysPage?.SyncCountdown(countdowns, isRefreshDue: true, generation);
    }

    private static string CountdownText(CountdownSettings settings, long clock, bool isChinese)
    {
        if (!settings.IsNightOn && !settings.IsWaveOn) return "";

        var nightIn = settings.IsNightOn ? Countdowns.NightIn(clock) : null;
        var waveIn = settings.IsWaveOn ? Countdowns.WaveIn(clock, CountdownSignals.WaveHours(), CountdownSignals.LureSecondsLeft(clock)) : null;

        return Countdowns.Text(nightIn, waveIn, settings, isChinese);
    }

    /// <summary>Takes the wheel and the notes off the page, sending a removal the page never confirmed again at the page pace.</summary>
    [HideFromIl2Cpp]
    private void HideKeysPage(float now, bool isRefreshDue)
    {
        var generation = Watch?.Generation ?? 0;

        SpeedKeysPage?.SyncWheel(false, isPollDue: isRefreshDue, generation);

        if (!TakeKeysPageRefresh(now)) return;

        SpeedKeysPage?.SyncOrderFinish("", isRefreshDue: true, generation);
        SpeedKeysPage?.SyncCountdown("", isRefreshDue: true, generation);
    }

    [HideFromIl2Cpp]
    private bool TakeKeysPageRefresh(float now)
    {
        if (now < _nextKeysPageAt) return false;

        _nextKeysPageAt = now + PageSeconds;

        return true;
    }

    /// <summary>A hold or a custom speed on the old world went with it.</summary>
    [HideFromIl2Cpp]
    private void ForgetSpeedKeysOnNewWorld()
    {
        _hold = HoldKeys.Start;
        _custom = CustomHold.Start;
        _idleBeforeHold = IdlePause.Start;
    }
}
