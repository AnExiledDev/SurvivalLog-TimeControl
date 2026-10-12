using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// The Harmony hooks into the game's time manager and hotkeys. Static because a patch has nowhere else to look; the
/// ticker owns everything that changes over time and writes <see cref="ClockFactor"/> and
/// <see cref="LastRunningLevel"/> here.
/// </summary>
internal static class SpeedPatches
{
    private const int MaxLoggedFailures = 3;

    private static readonly SpeedChangeFanOut FanOut = new();

    private static IReadOnlyList<float> _slowSpeeds = Array.Empty<float>();
    private static KeyCode _pauseKey = KeyCode.None;
    private static IntPtr _listeningActionField;
    private static IntPtr _listeningActionClass;
    private static int _slowIndex = -1;
    private static bool _wasSlowBeforeChange;
    private static float? _lastLoggedHeld;
    private static bool _isOwnChange;
    private static bool _hasLoggedGamePauseKey;
    private static int _failures;
    private static Action<string> _log = _ => { };

    /// <summary>Multiplies the day clock's frame delta: 1 runs it at game pace.</summary>
    internal static float ClockFactor = 1f;

    /// <summary>The speed level the pause key and idle freeze resume to.</summary>
    internal static int LastRunningLevel = PauseToggle.Normal;

    public static bool OwnsSlowGear => _slowSpeeds.Count > 0;

    public static void Apply(Harmony harmony, IReadOnlyList<float> slowSpeeds, KeyCode pauseKey, Action<string> log)
    {
        _slowSpeeds = slowSpeeds;
        _pauseKey = pauseKey;
        _log = log;

        Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.Update), prefix: nameof(ScaleDayClock),
            done: "day clock pace hooked", consequence: "ClockRate will do nothing");
        Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.ChangeGameSpeed), prefix: nameof(BeforeSpeedChange),
            done: "speed changes watched", consequence: "leftover game worlds cannot be detected and the slow button keeps the game's behaviour");

        if (OwnsSlowGear)
        {
            Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.ChangeGameSpeed), postfix: nameof(HoldChosenSlowSpeed),
                done: $"slow button holds {string.Join(" then ", Labels())}", consequence: "the slow button keeps the game's behaviour");
            Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.UpdateIdleSlowdown), prefix: nameof(SkipGameIdleSlowdown),
                done: "the game's own idle slowdown is off while the slow button is ours", consequence: "the game may drop the slow speed to 0.3x when idle");
        }

        if (pauseKey == KeyCode.None) return;

        FindListeningActionField();
        Patch(harmony, typeof(HotKeyManager), nameof(HotKeyManager.Update), prefix: nameof(TogglePauseOnKey), isLast: true,
            done: $"{pauseKey} toggles pause", consequence: $"{pauseKey} will not toggle pause");
    }

    /// <summary>The slow speed being held, or null when the slow button is not in use or not ours.</summary>
    public static float? HeldSlowSpeed(GameTimeManager time)
    {
        if (!OwnsSlowGear || !time.IsSlowGearActive) return null;

        return _slowSpeeds[Math.Clamp(_slowIndex, 0, _slowSpeeds.Count - 1)];
    }

    /// <summary>The most time managers one frame's speed change reached since the last call; 0 when none changed speed.</summary>
    public static int TakeLargestFanOut() => FanOut.TakeLargest();

    /// <summary>
    /// A speed change this mod makes goes straight to the live time manager, so it would always count one and say
    /// nothing about leftover worlds; only the game's own changes, which reach every manager listening, are counted.
    /// </summary>
    public static void ChangeSpeed(GameTimeManager time, int level)
    {
        _isOwnChange = true;

        try
        {
            time.ChangeGameSpeed(level);
        }
        finally
        {
            _isOwnChange = false;
        }
    }

    private static IEnumerable<string> Labels()
    {
        foreach (var speed in _slowSpeeds) yield return SlowGear.Label(speed);
    }

    private static void Patch(Harmony harmony, Type type, string method, string? prefix = null, string? postfix = null, bool isLast = false, string done = "", string consequence = "")
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method),
                prefix: prefix == null ? null : Hook(prefix, isLast),
                postfix: postfix == null ? null : Hook(postfix, isLast));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, {consequence}: {ex.Message}");
        }
    }

    private static HarmonyMethod Hook(string name, bool isLast) =>
        new(typeof(SpeedPatches), name) { priority = isLast ? Priority.Last : Priority.Normal };

    /// <summary>
    /// The game's getter for HotKeyManager._listeningAction boxes the nullable, and il2cpp boxes an empty nullable to
    /// null, so the getter throws whenever no key is being rebound. The field and the nullable's class are looked up
    /// once here so <see cref="IsRebindingAKey"/> can box it itself and check for null.
    /// </summary>
    private static void FindListeningActionField()
    {
        try
        {
            _listeningActionField = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<HotKeyManager>.NativeClassPtr, "_listeningAction");
            _listeningActionClass = Il2CppClassPointerStore<Il2CppSystem.Nullable<HotKeyAction>>.NativeClassPtr;
        }
        catch (Exception ex)
        {
            _listeningActionField = IntPtr.Zero;
            _log("could not look up the key rebinding state: " + ex.Message);
        }

        if (_listeningActionField == IntPtr.Zero || _listeningActionClass == IntPtr.Zero)
        {
            _listeningActionField = IntPtr.Zero;
            _log("the key rebinding state was not found, so the pause key also works while a key is being rebound");
        }
    }

    // Runs every frame, so it only reads the factor the ticker already worked out.
    private static void ScaleDayClock(ref float deltaTime) => deltaTime *= ClockFactor;

    private static bool SkipGameIdleSlowdown() => false;

    /// <summary>
    /// Every speed change is counted per frame, to catch leftover worlds answering the game's events. Only the live
    /// world's change counts for the slow button, and a press while paused resumes rather than cycles, so only a
    /// running slow speed counts as already slow.
    /// </summary>
    private static void BeforeSpeedChange(GameTimeManager __instance)
    {
        try
        {
            if (!_isOwnChange) FanOut.Record(Time.frameCount, __instance.Pointer.ToInt64());

            if (!OwnsSlowGear || !GameClock.IsLive(__instance)) return;

            _wasSlowBeforeChange = __instance.IsSlowGearActive && !__instance.IsPausedScale();
        }
        catch (Exception ex)
        {
            _wasSlowBeforeChange = false;
            Fail("reading the speed before a change", ex);
        }
    }

    /// <summary>The game's slow gear runs at full speed until the player idles; this makes it the chosen slow speed at once.</summary>
    private static void HoldChosenSlowSpeed(GameTimeManager __instance, int speed)
    {
        if (speed != PauseToggle.Slow) return;

        try
        {
            if (!GameClock.IsLive(__instance)) return;

            _slowIndex = SlowGear.NextIndex(_slowIndex, _slowSpeeds.Count, _wasSlowBeforeChange);

            var held = _slowSpeeds[_slowIndex];

            GameClock.HoldSpeed(__instance, held);

            if (held == _lastLoggedHeld) return;

            _lastLoggedHeld = held;
            _log($"slow button: holding {SlowGear.Label(held)}");
        }
        catch (Exception ex)
        {
            Fail("holding the slow speed", ex);
        }
    }

    private static void TogglePauseOnKey(HotKeyManager __instance, bool __runOriginal)
    {
        LogGamePauseKeyOnce();

        if (!__runOriginal || !Input.GetKeyDown(_pauseKey)) return;

        try
        {
            if (!AreSpeedKeysLive(__instance) || GameClock.Find() is not { IsGameStart: true } time) return;

            ChangeSpeed(time, PauseToggle.Target(time.IsPausedScale(), LastRunningLevel));
        }
        catch (Exception ex)
        {
            Fail("toggling pause", ex);
        }
    }

    /// <summary>
    /// Read on the first key frame rather than at plugin load: the read goes through the game's SettingManager, and
    /// reading it at load would build it before the game does.
    /// </summary>
    private static void LogGamePauseKeyOnce()
    {
        if (_hasLoggedGamePauseKey) return;

        _hasLoggedGamePauseKey = true;

        try
        {
            _log($"the game's own pause key is {HotKeyManager.Bind(HotKeyAction.SpeedPause)}");
        }
        catch (Exception ex)
        {
            _log("could not read the game's pause key: " + ex.Message);
        }
    }

    /// <summary>
    /// The same conditions the game checks before its own speed keys (home, normal mode, no menu holding time), plus
    /// no text box, no key being rebound, and the key not bound to anything else.
    /// </summary>
    private static bool AreSpeedKeysLive(HotKeyManager keys) =>
        keys._gameMode == GameMode.Default &&
        keys._worldStateType == WorldStateType.PostDisaster &&
        !keys._isTimePaused &&
        !keys._isInputDisabled &&
        !IsRebindingAKey(keys) &&
        !HotKeyManager.IsUGUIInputFocused() &&
        !HotKeyManager.IsKeyTakenByOtherAction(_pauseKey);

    /// <summary>Boxing an empty nullable gives null, so a non-null box means a key is being rebound.</summary>
    private static bool IsRebindingAKey(HotKeyManager keys)
    {
        if (_listeningActionField == IntPtr.Zero) return false;

        var field = IL2CPP.Il2CppObjectBaseToPtrNotNull(keys) + (int)IL2CPP.il2cpp_field_get_offset(_listeningActionField);

        return IL2CPP.il2cpp_value_box(_listeningActionClass, field) != IntPtr.Zero;
    }

    private static void Fail(string doing, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"failed {doing} (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
