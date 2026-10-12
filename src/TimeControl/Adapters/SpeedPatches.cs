using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TimeControl.Core;
using UnityEngine;
using CameraTouch = GameCore.Scripts.CameraTouch;

namespace TimeControl.Adapters;

/// <summary>The settings the hooks read; fixed for the session.</summary>
internal sealed record SpeedSettings(
    IReadOnlyList<float> SlowSpeeds,
    IReadOnlyList<float> FastMultiples,
    KeyCode PauseKey,
    KeyCode CenterCameraKey,
    bool IsSleepFastForwardOn,
    bool IsExpeditionOn);

/// <summary>
/// The Harmony hooks into the game's time manager, hotkeys and hover testing. Static because a patch has nowhere else
/// to look; the ticker owns everything that changes over time and writes <see cref="ClockFactor"/> and
/// <see cref="LastRunningLevel"/> here.
/// </summary>
internal static class SpeedPatches
{
    private const int MaxLoggedFailures = 3;

    /// <summary>
    /// Who asked for a speed change: the game (its buttons, keys and menus), the player through this mod's keys, the
    /// player through this mod's copy of the speed buttons, or this mod itself.
    /// </summary>
    private enum ChangeSource
    {
        Game,
        Player,
        Button,
        Mod,
    }

    private static readonly SpeedChangeFanOut FanOut = new();

    /// <summary>The game's own speed keys and the level its key handler sends for each.</summary>
    private static readonly (HotKeyAction Action, int Level)[] GameSpeedKeys =
    {
        (HotKeyAction.SpeedSlow, PauseToggle.Slow),
        (HotKeyAction.Speed1x, PauseToggle.Normal),
        (HotKeyAction.Speed2x, PauseToggle.Double),
        (HotKeyAction.Speed3x, PauseToggle.Triple),
        (HotKeyAction.SpeedPause, PauseToggle.Paused),
    };

    private static IReadOnlyList<float> _slowSpeeds = Array.Empty<float>();
    private static IReadOnlyList<float> _fastMultiples = Array.Empty<float>();
    private static KeyCode _pauseKey = KeyCode.None;
    private static KeyCode _centerCameraKey = KeyCode.None;
    private static IntPtr _listeningActionField;
    private static IntPtr _listeningActionClass;
    private static int _slowIndex = -1;
    private static bool _wasSlowBeforeChange;
    private static float? _lastLoggedHeld;
    private static int _fastGear = FastGear.GamesOwn;
    private static int? _forcedFastGear;
    private static FastPress _fastPressFrom = FastPress.FromOtherSpeed;
    private static string? _lastLoggedFast;
    private static ChangeSource _changeSource = ChangeSource.Game;
    private static bool _wasChangedByHand;
    private static bool _isPausedByKey;
    private static bool _isExpeditionOn;
    private static bool _isNormalMode;
    private static bool _areKeysSetUp;
    private static bool _hasLoggedPauseKeyIgnored;
    private static bool _hasLoggedPauseKeyTaken;
    private static int _failures;
    private static Action<string> _log = _ => { };

    /// <summary>Multiplies the day clock's frame delta: 1 runs it at game pace.</summary>
    internal static float ClockFactor = 1f;

    /// <summary>The speed level the pause key and idle freeze resume to.</summary>
    internal static int LastRunningLevel = PauseToggle.Normal;

    public static bool OwnsSlowGear => _slowSpeeds.Count > 0;

    public static bool OwnsFastGear => _fastMultiples.Count > 0;

    /// <summary>
    /// Whether the live world's last speed change was the player pausing through this mod's pause key, so the pause
    /// badge can say who holds the game. Any later change, by anyone, clears it.
    /// </summary>
    public static bool IsPausedByKey => _isPausedByKey;

    /// <summary>
    /// Whether the game was in its normal mode on the last frame it handled keys, where it would answer its own speed
    /// keys. False until the key hook has run, so nothing that depends on it shows before the game's mode is known.
    /// </summary>
    public static bool IsNormalMode => _isNormalMode;

    /// <summary>The fast gear sleep runs at: the one with the largest multiple, or the game's own high speed when there are none.</summary>
    public static int FastestGear => FastGear.FastestGear(_fastMultiples);

    public static void Apply(Harmony harmony, SpeedSettings settings, Action<string> log)
    {
        _slowSpeeds = settings.SlowSpeeds;
        _fastMultiples = settings.FastMultiples;
        _pauseKey = settings.PauseKey;
        _centerCameraKey = settings.CenterCameraKey;
        _isExpeditionOn = settings.IsExpeditionOn;
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

        if (OwnsFastGear)
        {
            Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.ChangeGameSpeed), postfix: nameof(HoldChosenFastSpeed),
                done: $"fast button goes on to {string.Join(" then ", FastLabels())} the game's high speed", consequence: "the fast button keeps the game's behaviour");
        }

        if (settings.IsSleepFastForwardOn)
        {
            Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.StartTempSpeedUp), prefix: nameof(SkipGameSleepSpeedUp),
                done: "the game's own sleep speed-up is replaced by the sleep fast-forward",
                consequence: "the game's own sleep speed-up takes over from the sleep fast-forward partway through a sleep");
        }

        Patch(harmony, typeof(CameraTouch), nameof(CameraTouch.Update), prefix: nameof(KeepHoverTestOnRealTime),
            done: "hover highlight works while paused", consequence: "objects will not highlight on hover while paused");

        FindListeningActionField();
        Patch(harmony, typeof(HotKeyManager), nameof(HotKeyManager.Update), prefix: nameof(HandleSpeedKeys), isLast: true,
            done: KeysHooked(settings.PauseKey), consequence: "the pause key and the game's speed keys before the disaster and on expeditions will not work");
    }

    private static string KeysHooked(KeyCode pauseKey)
    {
        var where = _isExpeditionOn ? "in the countdown to the disaster and on expeditions" : "in the countdown to the disaster";

        return pauseKey == KeyCode.None
            ? $"the game's speed keys work {where}"
            : $"{pauseKey} toggles pause, and the game's speed keys work {where}";
    }

    /// <summary>The slow speed being held, or null when the slow button is not in use or not ours.</summary>
    public static float? HeldSlowSpeed(GameTimeManager time)
    {
        if (!OwnsSlowGear || !time.IsSlowGearActive) return null;

        return _slowSpeeds[Math.Clamp(_slowIndex, 0, _slowSpeeds.Count - 1)];
    }

    /// <summary>
    /// The fast speed being held, or null on the game's own high speed, at any other speed, while paused, or before
    /// the game's high speed is known.
    /// </summary>
    public static float? HeldFastSpeed(GameTimeManager time)
    {
        if (!OwnsFastGear || _fastGear == FastGear.GamesOwn || time.IsPausedScale() || time.GetSpeedLevel() != PauseToggle.Triple) return null;

        return FastGear.HeldScale(time.ChapterSpeedUp2, FastMultiple());
    }

    /// <summary>The speed the game runs at, or resumes to while paused, with the fast gear in use.</summary>
    public static GameSpeed CurrentSpeed(GameTimeManager time, bool isPaused) =>
        GameSpeed.Of(isPaused ? LastRunningLevel : time.GetSpeedLevel(), _fastGear);

    /// <summary>Makes the next resume, from the pause key, the play button or idle freeze, go back to this speed.</summary>
    public static void SetResumeSpeed(GameSpeed speed)
    {
        LastRunningLevel = speed.Level;
        _fastGear = FastGear.Valid(speed.Gear, _fastMultiples.Count);
    }

    /// <summary>A new world starts at the game's own speeds, and unpaused by the pause key.</summary>
    public static void ForgetWorld()
    {
        _fastGear = FastGear.GamesOwn;
        _isPausedByKey = false;
    }

    /// <summary>Whether the player or the game's own speed buttons changed the live world's speed since the last call.</summary>
    public static bool TakeSpeedChangedByHand()
    {
        var wasChanged = _wasChangedByHand;

        _wasChangedByHand = false;

        return wasChanged;
    }

    /// <summary>The most time managers one frame's speed change reached since the last call; 0 when none changed speed.</summary>
    public static int TakeLargestFanOut() => FanOut.TakeLargest();

    /// <summary>A speed change this mod makes on its own, for idle freeze or sleep.</summary>
    public static void ChangeSpeed(GameTimeManager time, int level) => ChangeSpeedAs(time, level, ChangeSource.Mod);

    /// <summary>A speed change this mod makes on its own, landing on the given fast gear when the level is the high speed.</summary>
    public static void ChangeSpeed(GameTimeManager time, GameSpeed speed)
    {
        _forcedFastGear = speed.Gear;

        try
        {
            ChangeSpeed(time, speed.Level);
        }
        finally
        {
            _forcedFastGear = null;
        }
    }

    /// <summary>
    /// A click on this mod's copy of the game's speed buttons. It counts as a change by hand and never as a pause key
    /// press, as a click on the game's own button does.
    /// </summary>
    public static void PressSpeedButton(GameTimeManager time, int level) => ChangeSpeedAs(time, level, ChangeSource.Button);

    /// <summary>A speed change the player asked for through this mod's keys.</summary>
    private static void ChangeSpeedForPlayer(GameTimeManager time, int level) => ChangeSpeedAs(time, level, ChangeSource.Player);

    /// <summary>
    /// A speed change this mod makes goes straight to the live time manager, so it would always count one and say
    /// nothing about leftover worlds; only the game's own changes, which reach every manager listening, are counted.
    /// A pause the player asks for while an expedition is being finished does nothing, as the trip home waits on game
    /// time; the mod's own pauses refuse it themselves.
    /// </summary>
    private static void ChangeSpeedAs(GameTimeManager time, int level, ChangeSource source)
    {
        if (source != ChangeSource.Mod && IsRefusedForTripHome(time, level)) return;

        _changeSource = source;

        try
        {
            time.ChangeGameSpeed(level);
        }
        finally
        {
            _changeSource = ChangeSource.Game;
        }
    }

    private static bool IsRefusedForTripHome(GameTimeManager time, int level) =>
        ExpeditionFinish.RefusesSpeed(level, GameClock.IsExpeditionEnding(GameTimers.Controlled(GameClock.Timer(time), _isExpeditionOn)));

    private static float FastMultiple() => _fastMultiples[Math.Clamp(_fastGear, 1, _fastMultiples.Count) - 1];

    private static IEnumerable<string> Labels()
    {
        foreach (var speed in _slowSpeeds) yield return SlowGear.Label(speed);
    }

    private static IEnumerable<string> FastLabels()
    {
        foreach (var multiple in _fastMultiples) yield return FastGear.Label(multiple);
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
    /// The game's sleep speed-up would run at its high speed partway through the sleep and put its own speed back
    /// when the sleep ends; sleep runs at the fast gear the ticker picks instead, so the game's is never started.
    /// Its other speed-ups are left alone.
    /// </summary>
    private static bool SkipGameSleepSpeedUp(SpeedUpType speedUpType) => speedUpType != SpeedUpType.Sleep;

    /// <summary>
    /// Every speed change from the game is counted per frame, to catch leftover worlds answering its events. Only
    /// the live world's change counts for the slow and fast buttons, and a press while paused resumes rather than
    /// cycles, so only a running slow or fast speed counts as already slow or fast. Only plain fields are written here:
    /// this runs inside the game's speed change.
    /// </summary>
    private static void BeforeSpeedChange(GameTimeManager __instance, int speed)
    {
        try
        {
            if (_changeSource == ChangeSource.Game) FanOut.Record(Time.frameCount, __instance.Pointer.ToInt64());

            if (!GameClock.IsLive(__instance)) return;

            if (_changeSource != ChangeSource.Mod) _wasChangedByHand = true;

            _isPausedByKey = speed == PauseToggle.Paused && _changeSource == ChangeSource.Player;

            var isPaused = __instance.IsPausedScale();

            _wasSlowBeforeChange = __instance.IsSlowGearActive && !isPaused;
            _fastPressFrom = FastGear.PressFrom(isPaused, __instance.GetSpeedLevel());
        }
        catch (Exception ex)
        {
            _wasSlowBeforeChange = false;
            _fastPressFrom = FastPress.FromOtherSpeed;
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

    /// <summary>
    /// The game's high speed button stays lit on every fast gear, since the game reads any speed at or above its high
    /// speed as that level. A change this mod makes for sleep or a resume lands on the gear it asks for; any other
    /// goes by where the speed was.
    /// </summary>
    private static void HoldChosenFastSpeed(GameTimeManager __instance, int speed)
    {
        try
        {
            if (!GameClock.IsLive(__instance)) return;

            _fastGear = speed == PauseToggle.Triple && _forcedFastGear is { } forced
                ? FastGear.Valid(forced, _fastMultiples.Count)
                : FastGear.GearAfter(_fastGear, _fastMultiples.Count, speed, _fastPressFrom);

            if (speed != PauseToggle.Triple) return;

            if (HeldFastSpeed(__instance) is { } held) GameClock.HoldSpeed(__instance, held);

            LogFastSpeed(__instance);
        }
        catch (Exception ex)
        {
            Fail("holding the fast speed", ex);
        }
    }

    private static void LogFastSpeed(GameTimeManager time)
    {
        var gameFastSpeed = time.ChapterSpeedUp2;
        var line = HeldFastSpeed(time) is { } held
            ? $"fast button: holding {held:0.##}x ({FastGear.Label(FastMultiple())} the game's high speed of {gameFastSpeed}x)" +
              (held < gameFastSpeed * FastMultiple() ? $", capped at {FastGear.FastestScale:0}x" : "")
            : $"fast button: the game's high speed of {gameFastSpeed}x";

        if (line == _lastLoggedFast) return;

        _lastLoggedFast = line;
        _log(line);
    }

    /// <summary>
    /// The game tests what the mouse is over once per tenth of a second of game time, which while paused is about a
    /// quarter of an hour of real time; topping its timer up to real time first keeps hover highlights working while
    /// paused or slowed.
    /// </summary>
    private static void KeepHoverTestOnRealTime(CameraTouch __instance)
    {
        try
        {
            var topUp = HoverClock.TopUp(Time.deltaTime, Time.unscaledDeltaTime);

            if (topUp > 0f) __instance.testActionTimer += topUp;
        }
        catch (Exception ex)
        {
            Fail("keeping hover testing on real time", ex);
        }
    }

    /// <summary>
    /// Runs before the game's own key handling. The game ignores its speed keys before the disaster and on expeditions,
    /// so there this answers them, along with the pause key wherever it works. A pause key press is never also read as
    /// one of the game's speed keys, unless the game itself uses that key.
    /// </summary>
    private static void HandleSpeedKeys(HotKeyManager __instance, bool __runOriginal)
    {
        SetUpKeysOnce();
        RememberGameMode(__instance);

        if (!__runOriginal || !Input.anyKeyDown) return;

        try
        {
            if (GameClock.Find() is not { IsGameStart: true } time) return;

            var isAfterDisaster = __instance._worldStateType == WorldStateType.PostDisaster;
            var timer = GameTimers.Controlled(GameClock.Timer(time), _isExpeditionOn);

            if (IsPauseKeyDown() && TogglePause(__instance, time, isAfterDisaster, timer)) return;

            if (AreKeysForTheGame(__instance, timer) && SpeedKeys.AreGameKeysOurs(isAfterDisaster, timer)) AnswerGameSpeedKeys(time);
        }
        catch (Exception ex)
        {
            Fail("handling the speed keys", ex);
        }
    }

    /// <summary>Read every frame, before any key is checked, so the speed buttons know the mode even when no key is pressed.</summary>
    private static void RememberGameMode(HotKeyManager keys)
    {
        try
        {
            _isNormalMode = keys._gameMode == GameMode.Default;
        }
        catch (Exception ex)
        {
            _isNormalMode = false;
            Fail("reading the game's mode", ex);
        }
    }

    private static bool IsPauseKeyDown() => _pauseKey != KeyCode.None && Input.GetKeyDown(_pauseKey);

    /// <returns>Whether the press was taken as a pause key press.</returns>
    private static bool TogglePause(HotKeyManager keys, GameTimeManager time, bool isAfterDisaster, GameTimer timer)
    {
        if (!AreKeysForTheGame(keys, timer) || !SpeedKeys.IsPauseKeyLive(isAfterDisaster, timer))
        {
            LogOnce(ref _hasLoggedPauseKeyIgnored,
                $"{_pauseKey} pressed where it does not pause (mode {keys._gameMode}, world {keys._worldStateType}, timer {timer}, " +
                $"menu holding time {keys._isTimePaused}, input off {keys._isInputDisabled}, typing {HotKeyManager.IsUGUIInputFocused()}); logged once");

            return false;
        }

        if (HotKeyManager.IsKeyTakenByOtherAction(_pauseKey))
        {
            LogOnce(ref _hasLoggedPauseKeyTaken,
                $"{_pauseKey} is bound to one of the game's own actions, so it does not pause: rebind that action in the game's key settings or pick another PauseToggleKey; logged once");

            return false;
        }

        ChangeSpeedForPlayer(time, PauseToggle.Target(time.IsPausedScale(), LastRunningLevel));

        return true;
    }

    /// <summary>The game's own speed keys, read the way its key handler reads them and sending the levels it sends.</summary>
    private static void AnswerGameSpeedKeys(GameTimeManager time)
    {
        foreach (var (action, level) in GameSpeedKeys)
        {
            if (!Input.GetKeyDown(HotKeyManager.Bind(action))) continue;

            ChangeSpeedForPlayer(time, level);

            return;
        }
    }

    /// <summary>
    /// Done on the first frame the game handles keys rather than at plugin load: the game's key settings are read
    /// through its SettingManager, and reading them at load would build it before the game does.
    /// </summary>
    private static void SetUpKeysOnce()
    {
        if (_areKeysSetUp) return;

        _areKeysSetUp = true;
        MoveCenterCameraOffPauseKey();
        LogGamePauseKey();
    }

    /// <summary>
    /// Only the game's default moves, so a key the player set in the game's own key settings still wins, and the
    /// settings page and its reset show the new default.
    /// </summary>
    private static void MoveCenterCameraOffPauseKey()
    {
        try
        {
            var defaults = HotKeyBinding.Defaults;

            if (defaults == null || !defaults.TryGetValue(HotKeyAction.CameraLock, out var gameDefault)) return;

            if (CenterCameraKey.Move((int)gameDefault, (int)_pauseKey, (int)_centerCameraKey) is not { } moved) return;

            defaults[HotKeyAction.CameraLock] = (KeyCode)moved;

            var bound = HotKeyManager.Bind(HotKeyAction.CameraLock);

            _log(bound == _pauseKey
                ? $"centering the camera moved off {gameDefault} by default, but the game's key settings keep it on {_pauseKey}, so {_pauseKey} does not pause until it is rebound there"
                : $"centering the camera on your character moved from {gameDefault} to {bound}, so {_pauseKey} pauses");
        }
        catch (Exception ex)
        {
            _log($"could not move centering the camera off {_pauseKey}, so {_pauseKey} may not pause: {ex.Message}");
        }
    }

    private static void LogGamePauseKey()
    {
        try
        {
            _log($"the game's own pause key is {HotKeyManager.Bind(HotKeyAction.SpeedPause)}");
        }
        catch (Exception ex)
        {
            _log("could not read the game's pause key: " + ex.Message);
        }
    }

    private static void LogOnce(ref bool hasLogged, string line)
    {
        if (hasLogged) return;

        hasLogged = true;
        _log(line);
    }

    /// <summary>
    /// The conditions the game checks before its own speed keys apart from where the player is (normal mode, no menu
    /// holding time, input on), plus no text box being typed in and no key being rebound. An expedition runs in the
    /// game's exploration mode, which the mod accepts too.
    /// </summary>
    private static bool AreKeysForTheGame(HotKeyManager keys, GameTimer timer) =>
        SpeedKeys.IsModeForKeys(keys._gameMode == GameMode.Default, timer) &&
        !keys._isTimePaused &&
        !keys._isInputDisabled &&
        !IsRebindingAKey(keys) &&
        !HotKeyManager.IsUGUIInputFocused();

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
