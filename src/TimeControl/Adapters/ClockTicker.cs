using System;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>The settings the ticker reads; fixed for the session.</summary>
internal sealed record ClockSettings(float ClockRate, bool IsIdleFreezeOn, float IdleFreezeDelaySeconds);

/// <summary>
/// Ten times a second: pauses the game while the player idles at home and resumes it on their next order, keeps the
/// chosen slow speed held, remembers the speed to resume to, sets the day clock's pace and keeps the page's drawn
/// clock in step.
/// </summary>
public sealed class ClockTicker : MonoBehaviour
{
    private const float CheckSeconds = 0.1f;
    private const float PageSeconds = 0.5f;
    private const float RepeatLogSeconds = 60f;
    private const float SpeedTolerance = 0.001f;
    private const int MaxLoggedFailures = 3;

    internal static ClockSettings Settings = new(1f, false, 1f);
    internal static Action<string> Log = _ => { };
    internal static Action<string> LogFileOnly = _ => { };
    internal static PageClockLink? Page;

    private IdlePause _idle = IdlePause.Start;
    private IntPtr _timeSeen;
    private float _nextCheckAt;
    private float _lastCheckAt = -1f;
    private float _nextPageAt;
    private float _nextHealLogAt;
    private float _nextFanOutLogAt;
    private bool _isSingleWorldLogged;
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
            RecoverFromFailure(ex);
        }
    }

    private void Check(float now, float elapsed)
    {
        ReportFanOut(now);

        if (GameClock.Find() is not { } time)
        {
            SpeedPatches.ClockFactor = 1f;
            _idle = IdlePause.Start;
            _timeSeen = IntPtr.Zero;

            return;
        }

        ForgetPauseOnNewWorld(time);

        if (!time.IsGameStart)
        {
            SpeedPatches.ClockFactor = 1f;

            return;
        }

        var isPaused = time.IsPausedScale();
        var isDayClock = GameClock.IsDayClock(time);

        if (!isPaused) SpeedPatches.LastRunningLevel = time.GetSpeedLevel();

        KeepSlowSpeedHeld(time, isPaused, now);
        UpdateIdlePause(time, isPaused, isDayClock, elapsed);
        SpeedPatches.ClockFactor = ClockPace.Factor(Settings.ClockRate, isDayClock);
        SyncPage(time, now);
    }

    /// <summary>A load replaces the world, and a pause this mod made on the old one went with it.</summary>
    private void ForgetPauseOnNewWorld(GameTimeManager time)
    {
        if (time.Pointer == _timeSeen) return;

        _timeSeen = time.Pointer;
        _idle = IdlePause.Start;
    }

    /// <summary>
    /// A pause is kept as Frozen before it is made, so one that fails halfway is still undone by the recovery; a resume
    /// is kept after, so one that fails is tried again by it. Whether the player is busy only matters while time runs
    /// at home or the game is held by this mod.
    /// </summary>
    private void UpdateIdlePause(GameTimeManager time, bool isPaused, bool isDayClock, float elapsed)
    {
        var canFreeze = Settings.IsIdleFreezeOn && isDayClock && !IsFastForwarding(time);
        var isBusy = ((canFreeze && !isPaused) || _idle is not IdlePause.Watching) && time.IsLeadingRoleBusy();
        var seen = new IdleSnapshot(canFreeze, isBusy, isPaused, elapsed, SpeedPatches.LastRunningLevel);
        var before = _idle;
        var step = IdlePause.Next(before, seen, Settings.IdleFreezeDelaySeconds);

        if (step.Command is IdleCommand.Pause) _idle = step.State;

        Carry(time, step.Command);
        _idle = step.State;
        LogTransition(before, step);
    }

    private static void Carry(GameTimeManager time, IdleCommand command)
    {
        switch (command)
        {
            case IdleCommand.Pause:
                SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
                break;
            case IdleCommand.Resume resume when time.IsPausedScale():
                SpeedPatches.ChangeSpeed(time, resume.Level);
                break;
        }
    }

    private static void LogTransition(IdlePause from, IdleStep step)
    {
        var line = (from, step.State, step.Command) switch
        {
            (_, _, IdleCommand.Pause) => "idle: paused the game",
            (_, _, IdleCommand.Resume resume) => $"idle: order given, resumed at speed level {resume.Level}",
            (IdlePause.Frozen, IdlePause.Released, _) => "idle: time was resumed while idle, it runs until the next order",
            _ => null,
        };

        if (line != null) LogFileOnly(line);
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
            _nextHealLogAt = now + RepeatLogSeconds;
            Log($"slow button: game speed was {time.BattleTimeScale:0.###}x, putting {SlowGear.Label(held)} back (logged at most once a minute)");
        }

        GameClock.HoldSpeed(time, held);
    }

    private void SyncPage(GameTimeManager time, float now)
    {
        if (now < _nextPageAt) return;

        _nextPageAt = now + PageSeconds;
        Page?.Sync(SpeedPatches.ClockFactor, time.BattleTimeScale, SpeedPatches.HeldSlowSpeed(time) is { } held ? SlowGear.Label(held) : "");
    }

    /// <summary>
    /// One speed change reaching several time managers means worlds the game replaced are still answering its events,
    /// and each of them acts on a trade or an order as well. Logged so a session's health can be read off the log.
    /// </summary>
    private void ReportFanOut(float now)
    {
        var reached = SpeedPatches.TakeLargestFanOut();

        if (reached > 1 && now >= _nextFanOutLogAt)
        {
            _nextFanOutLogAt = now + RepeatLogSeconds;
            Log($"speed change reached {reached} time managers in one frame (expected 1): leftover game worlds are answering events (logged at most once a minute)");
        }
        else if (reached == 1 && !_isSingleWorldLogged)
        {
            _isSingleWorldLogged = true;
            Log("speed change reached 1 time manager, as it should");
        }
    }

    /// <summary>A failed check must never leave the game paused by this mod or the clock off its pace.</summary>
    private void RecoverFromFailure(Exception ex)
    {
        SpeedPatches.ClockFactor = 1f;

        if (_idle is IdlePause.Frozen frozen) TryResume(frozen.ResumeLevel);

        _idle = IdlePause.Start;
        _failures++;

        if (_failures <= MaxLoggedFailures) Log($"clock check failed (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }

    private static void TryResume(int level)
    {
        try
        {
            if (GameClock.Find() is { } time && time.IsPausedScale()) SpeedPatches.ChangeSpeed(time, level);
        }
        catch (Exception)
        {
            // Best effort inside a failure already being logged; the player can still press play.
        }
    }

    private static bool IsFastForwarding(GameTimeManager time) => time.TempSpeedUpBeforeScale >= 0f;
}
