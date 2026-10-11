using System;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>The settings the ticker reads; fixed for the session.</summary>
internal sealed record ClockSettings(float ClockRate, bool IsIdleFreezeOn, float IdleFreezeDelaySeconds);

/// <summary>
/// Ten times a second: works out whether the day clock is frozen and how fast it runs, keeps the chosen slow speed
/// held, remembers the speed to resume to, and keeps the page's drawn clock in step.
/// </summary>
public sealed class ClockTicker : MonoBehaviour
{
    private const float CheckSeconds = 0.1f;
    private const float PageSeconds = 0.5f;
    private const float HealLogSeconds = 60f;
    private const float SpeedTolerance = 0.001f;
    private const int MaxLoggedFailures = 3;

    internal static ClockSettings Settings = new(1f, false, 1f);
    internal static Action<string> Log = _ => { };
    internal static Action<string> LogFileOnly = _ => { };
    internal static PageClockLink? Page;

    private IdleClock _idle = IdleClock.Start;
    private bool _isFrozen;
    private float _nextCheckAt;
    private float _lastCheckAt = -1f;
    private float _nextPageAt;
    private float _nextHealLogAt;
    private int _failures;

    public ClockTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        var now = Time.unscaledTime;

        if (now < _nextCheckAt) return;

        var elapsed = _lastCheckAt < 0f ? 0f : now - _lastCheckAt;

        _nextCheckAt = now + CheckSeconds;
        _lastCheckAt = now;

        try
        {
            Check(now, elapsed);
        }
        catch (Exception ex)
        {
            // A clock left frozen by a failed check would stop the day for good, so a failure always lets it run.
            SpeedPatches.ClockFactor = 1f;
            _idle = IdleClock.Start;
            _failures++;

            if (_failures <= MaxLoggedFailures) Log($"clock check failed (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }

    private void Check(float now, float elapsed)
    {
        if (GameClock.Find() is not { IsGameStart: true } time)
        {
            SpeedPatches.ClockFactor = 1f;
            _idle = IdleClock.Start;

            return;
        }

        var isPaused = time.IsPausedScale();
        var isDayClock = GameClock.IsDayClock(time);

        if (!isPaused) SpeedPatches.LastRunningLevel = time.GetSpeedLevel();

        KeepSlowSpeedHeld(time, isPaused, now);
        SpeedPatches.ClockFactor = ClockPace.Factor(Settings.ClockRate, UpdateIdle(time, isPaused, isDayClock, elapsed), isDayClock);

        if (now < _nextPageAt) return;

        _nextPageAt = now + PageSeconds;
        Page?.Sync(SpeedPatches.ClockFactor, time.BattleTimeScale, SpeedPatches.HeldSlowSpeed(time) is { } held ? SlowGear.Label(held) : "");
    }

    private bool UpdateIdle(GameTimeManager time, bool isPaused, bool isDayClock, float elapsed)
    {
        var canFreeze = Settings.IsIdleFreezeOn && isDayClock && !isPaused && !IsFastForwarding(time);
        var isBusy = canFreeze && time.IsLeadingRoleBusy();

        _idle = _idle.Advance(canFreeze, isBusy, elapsed);

        var isFrozen = _idle.IsFrozen(Settings.IdleFreezeDelaySeconds);

        if (isFrozen != _isFrozen) LogFileOnly(isFrozen ? "idle: day clock frozen" : "day clock running");

        _isFrozen = isFrozen;

        return isFrozen;
    }

    /// <summary>
    /// Saves, menus and the game's other speed changes can put the scale back without going through the slow button,
    /// so a held slow speed is checked and restored here; a fast-forward such as sleeping is left alone.
    /// </summary>
    private void KeepSlowSpeedHeld(GameTimeManager time, bool isPaused, float now)
    {
        if (isPaused || IsFastForwarding(time) || SpeedPatches.HeldSlowSpeed(time) is not { } held) return;

        if (Math.Abs(time.BattleTimeScale - held) <= SpeedTolerance) return;

        if (now >= _nextHealLogAt)
        {
            _nextHealLogAt = now + HealLogSeconds;
            Log($"slow button: game speed was {time.BattleTimeScale:0.###}x, putting {SlowGear.Label(held)} back (logged at most once a minute)");
        }

        GameClock.HoldSpeed(time, held);
    }

    private static bool IsFastForwarding(GameTimeManager time) => time.TempSpeedUpBeforeScale >= 0f;
}
