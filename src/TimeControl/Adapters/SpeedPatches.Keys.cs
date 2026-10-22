using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>The hold keys, the custom speeds and the notes under the clock, swapped whole when one changes on the Mods screen.</summary>
internal sealed record KeysSettings(
    KeyCode HoldFastKey,
    KeyCode HoldSlowKey,
    IReadOnlyList<float?> CustomSpeeds,
    bool IsWheelOn,
    bool IsOrderFinishOn,
    CountdownSettings Countdowns)
{
    public static readonly KeysSettings Off = new(KeyCode.None, KeyCode.None, Array.Empty<float?>(), false, false, new CountdownSettings(false, false));
}

/// <summary>
/// The hold keys, the custom speed keys and the speed changes they make. The key hook only notes what was pressed;
/// the ticker decides what it does on its next check.
/// </summary>
internal static partial class SpeedPatches
{
    /// <summary>A hold key seen down this recently still counts as held: the ticker checks ten times a second.</summary>
    private const float HeldKeyGraceSeconds = 0.25f;

    private static float _fastKeySeenAt = float.NegativeInfinity;
    private static float _slowKeySeenAt = float.NegativeInfinity;
    private static bool _isFastKeyTaken;
    private static bool _isSlowKeyTaken;
    private static bool _hasLoggedFastKeyTaken;
    private static bool _hasLoggedSlowKeyTaken;
    private static float? _customSpeedPressed;
    private static bool _isKeysChanging;
    private static bool _wasChangedByOthers;

    /// <summary>Read by the key hook and the ticker; written on the main thread at start and after each change.</summary>
    internal static KeysSettings KeySettings = KeysSettings.Off;

    /// <summary>Called once at start, after <see cref="Apply"/>, whose log it uses. The hooks stay in whatever the settings.</summary>
    public static void ApplyKeys(Harmony harmony)
    {
        Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.ChangeGameSpeed), prefix: nameof(NoteChangeByOthers),
            done: "speed changes watched for the hold keys", consequence: "a hold key may put back a speed after something else changed it");
        Patch(harmony, typeof(HotKeyManager), nameof(HotKeyManager.Update), prefix: nameof(TrackSpeedKeys), isLast: true,
            done: "hold keys and custom speed keys read", consequence: "HoldFastKey, HoldSlowKey and SpeedKeys5To9 will not work");
    }

    /// <summary>The fastest speed the mod has: its fastest fast gear, or the game's own high speed when it has none.</summary>
    public static SpeedTarget FastestTarget => new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, FastestGear));

    /// <summary>The slowest of the slow button's speeds, or null when the slow button is the game's own.</summary>
    public static SpeedTarget? SlowestTarget
    {
        get
        {
            if (!OwnsSlowGear) return null;

            var slowest = 0;

            for (var i = 1; i < _slowSpeeds.Count; i++)
            {
                if (_slowSpeeds[i] < _slowSpeeds[slowest]) slowest = i;
            }

            return new SpeedTarget.SlowSpeed(slowest);
        }
    }

    /// <summary>Every speed the mouse wheel steps through, with the game's speeds as they are now.</summary>
    public static IReadOnlyList<LadderStep> Ladder(GameTimeManager time, IReadOnlyList<float?> customSpeeds) =>
        SpeedLadder.Build(_slowSpeeds, time.ChapterSpeedUp1, time.ChapterSpeedUp2, _fastMultiples, customSpeeds);

    /// <summary>The game level a custom speed sits on, so the game's buttons and its resume read it right.</summary>
    public static int CustomLevel(GameTimeManager time, float scale) => SpeedLadder.LevelFor(scale, time.ChapterSpeedUp1, time.ChapterSpeedUp2);

    /// <summary>The speed the game runs at now, or resumes to while paused, as the speed keys see it.</summary>
    public static SpeedTarget CurrentTarget(GameTimeManager time, bool isPaused, CustomHold custom)
    {
        switch (custom)
        {
            case CustomHold.Held held:
                return new SpeedTarget.Custom(held.Scale);
            case CustomHold.Suspended suspended when isPaused:
                return new SpeedTarget.Custom(suspended.Scale);
        }

        var speed = CurrentSpeed(time, isPaused);

        return speed.Level == PauseToggle.Slow && OwnsSlowGear ? new SpeedTarget.SlowSpeed(SlowIndexInUse()) : new SpeedTarget.Level(speed);
    }

    /// <summary>
    /// Puts the game at the speed, as the player's own change. It never counts as a change by others, so a hold or a
    /// custom speed the keys set is not let go because of it.
    /// </summary>
    public static void ApplyTarget(GameTimeManager time, SpeedTarget target)
    {
        _isKeysChanging = true;

        try
        {
            switch (target)
            {
                case SpeedTarget.Level level:
                    _forcedFastGear = level.Speed.Gear;
                    ChangeSpeedAs(time, level.Speed.Level, ChangeSource.Player);
                    break;
                case SpeedTarget.SlowSpeed slow:
                    ChangeSpeedAs(time, PauseToggle.Slow, ChangeSource.Player);
                    PinSlowSpeed(time, slow.Index);
                    break;
                case SpeedTarget.Custom custom:
                    _forcedFastGear = FastGear.GamesOwn;
                    ChangeSpeedAs(time, CustomLevel(time, custom.Scale), ChangeSource.Player);

                    if (!time.IsPausedScale()) GameClock.HoldSpeed(time, custom.Scale);

                    break;
            }
        }
        finally
        {
            _forcedFastGear = null;
            _isKeysChanging = false;
        }
    }

    /// <summary>Pauses the game for the hold keys, as the pause key would when the pause they took over was the pause key's.</summary>
    public static void PauseForKeys(GameTimeManager time, bool isPauseKeysOwn)
    {
        _isKeysChanging = true;

        try
        {
            ChangeSpeedAs(time, PauseToggle.Paused, isPauseKeysOwn ? ChangeSource.Player : ChangeSource.Mod);
        }
        finally
        {
            _isKeysChanging = false;
        }
    }

    /// <summary>Makes the next resume go back to this speed. A custom speed resumes to its game level, which the ticker then raises.</summary>
    public static void SetResumeTarget(GameTimeManager time, SpeedTarget target)
    {
        switch (target)
        {
            case SpeedTarget.Level level:
                SetResumeSpeed(level.Speed);
                break;
            case SpeedTarget.SlowSpeed slow:
                SetResumeSpeed(new GameSpeed(PauseToggle.Slow, FastGear.GamesOwn));
                _slowIndex = Math.Clamp(slow.Index, 0, Math.Max(_slowSpeeds.Count - 1, 0));
                break;
            case SpeedTarget.Custom custom:
                SetResumeSpeed(new GameSpeed(CustomLevel(time, custom.Scale), FastGear.GamesOwn));
                break;
        }
    }

    /// <summary>Whether anything but the speed keys changed the live world's speed since the last call.</summary>
    public static bool TakeChangedByOthers()
    {
        var wasChanged = _wasChangedByOthers;

        _wasChangedByOthers = false;

        return wasChanged;
    }

    /// <summary>The custom speed whose key was pressed since the last call, or null.</summary>
    public static float? TakeCustomSpeedPressed()
    {
        var pressed = _customSpeedPressed;

        _customSpeedPressed = null;

        return pressed;
    }

    /// <summary>Whether the hold key of this kind is down where the game's speed keys would be answered.</summary>
    public static bool IsHoldKeyDown(HoldKind kind, float now) =>
        now - (kind == HoldKind.Fast ? _fastKeySeenAt : _slowKeySeenAt) <= HeldKeyGraceSeconds;

    private static int SlowIndexInUse() => Math.Clamp(_slowIndex, 0, Math.Max(_slowSpeeds.Count - 1, 0));

    /// <summary>The slow button's press picks a speed by its own turn; the keys want a given one, so it is set after.</summary>
    private static void PinSlowSpeed(GameTimeManager time, int index)
    {
        if (!OwnsSlowGear || !time.IsSlowGearActive) return;

        _slowIndex = Math.Clamp(index, 0, _slowSpeeds.Count - 1);
        GameClock.HoldSpeed(time, _slowSpeeds[_slowIndex]);
    }

    /// <summary>Only a plain field is written here: this runs inside the game's speed change.</summary>
    private static void NoteChangeByOthers(GameTimeManager __instance)
    {
        try
        {
            if (!_isKeysChanging && GameClock.IsLive(__instance)) _wasChangedByOthers = true;
        }
        catch (Exception ex)
        {
            Fail("watching speed changes for the hold keys", ex);
        }
    }

    /// <summary>
    /// Runs before the game's own key handling, with the pause key's hook. A key only counts where the game would
    /// answer its own speed keys, so typing in a text box or rebinding a key never changes the speed. Nothing is read
    /// unless a hold key is down or a key was pressed this frame.
    /// </summary>
    private static void TrackSpeedKeys(HotKeyManager __instance, bool __runOriginal)
    {
        if (!__runOriginal) return;

        try
        {
            var keys = KeySettings;
            var isFastHeld = IsHeld(keys.HoldFastKey);
            var isSlowHeld = IsHeld(keys.HoldSlowKey);

            if (!isFastHeld && !isSlowHeld && !Input.anyKeyDown) return;

            if (GameClock.Find() is not { IsGameStart: true } time) return;

            if (!AreKeysForTheGame(__instance, GameTimers.Controlled(GameClock.Timer(time), _isExpeditionOn))) return;

            var now = Time.unscaledTime;

            if (isFastHeld && IsHoldKeyFree(keys.HoldFastKey, "HoldFastKey", ref _isFastKeyTaken, ref _hasLoggedFastKeyTaken)) _fastKeySeenAt = now;

            if (isSlowHeld && IsHoldKeyFree(keys.HoldSlowKey, "HoldSlowKey", ref _isSlowKeyTaken, ref _hasLoggedSlowKeyTaken)) _slowKeySeenAt = now;

            if (Input.anyKeyDown) NoteCustomSpeedKey(keys.CustomSpeeds);
        }
        catch (Exception ex)
        {
            Fail("reading the hold keys and custom speed keys", ex);
        }
    }

    private static bool IsHeld(KeyCode key) => key != KeyCode.None && Input.GetKey(key);

    /// <summary>
    /// A key the game uses for one of its own actions does not hold a speed, as with the pause key. Asked once per
    /// press rather than on every frame the key is held.
    /// </summary>
    private static bool IsHoldKeyFree(KeyCode key, string setting, ref bool isTaken, ref bool hasLogged)
    {
        if (!Input.GetKeyDown(key)) return !isTaken;

        isTaken = HotKeyManager.IsKeyTakenByOtherAction(key);

        if (isTaken)
        {
            LogOnce(ref hasLogged,
                $"{key} is bound to one of the game's own actions, so it does not hold a speed: rebind that action in the game's key settings or pick another {setting}; logged once");
        }

        return !isTaken;
    }

    /// <summary>
    /// The keys 5 to 9 are the game's quick action keys, which it has switched off; they are not checked against the
    /// game's bindings, as they always are bound there.
    /// </summary>
    private static void NoteCustomSpeedKey(IReadOnlyList<float?> customSpeeds)
    {
        for (var i = 0; i < customSpeeds.Count; i++)
        {
            if (customSpeeds[i] is not { } scale || !Input.GetKeyDown(KeyCode.Alpha5 + i)) continue;

            _customSpeedPressed = scale;

            return;
        }
    }
}
