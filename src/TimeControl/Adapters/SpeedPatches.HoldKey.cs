using System;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using TimeControl.Core;

namespace TimeControl.Adapters;

internal static partial class SpeedPatches
{
    /// <summary>GameHoldKeySpeed, as a multiple of the game's high speed. Set by KeysConfig, live.</summary>
    public static float HoldKeyMultiple = FastGear.GamesOwnHold;

    private const float HoldKeyTolerance = 0.001f;

    public static void ApplyHoldKey(Harmony harmony) =>
        Patch(harmony, typeof(GameTimeManager), nameof(GameTimeManager.OnHoldSpeedBoost), prefix: nameof(RaiseGameHoldTarget), postfix: nameof(HoldGameHoldSpeed),
            done: "the game's hold-to-speed-up key runs at GameHoldKeySpeed", consequence: "the game's hold-to-speed-up key keeps the game's own speed");

    /// <summary>
    /// The game only starts its hold speed-up when the speed now is below its high speed, and then runs at that high
    /// speed, a whole number. Raising that number for the length of the call lets the hold start from any speed below
    /// ours, with the game's own checks (not paused, no sleep speed-up, not on a frozen timer) still deciding; the
    /// postfix puts the number back and sets the exact speed.
    /// </summary>
    /// <summary>
    /// Set while the game's hold-to-speed-up key is held after being pressed with the game paused. The game ignores
    /// that press, so the hold keys run time at the key's speed instead. The game sends one event on press and one on
    /// release, so this follows the key exactly.
    /// </summary>
    public static bool IsGameKeyHeldFromPause { get; private set; }

    /// <summary>What the game's hold-to-speed-up key runs at: its multiple of the game's high speed, or that high speed.</summary>
    public static SpeedTarget GameKeyTarget(GameTimeManager time) =>
        FastGear.HoldKeyScale(time.ChapterSpeedUp2, HoldKeyMultiple) is { } scale
            ? new SpeedTarget.Custom(scale)
            : new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, FastGear.GamesOwn));

    /// <summary>The game's hold-to-speed-up key is running time now, by the game's own boost or by the hold keys from a pause.</summary>
    public static bool IsHoldingGameKey(GameTimeManager time) => time.IsHoldBoostActive() || IsGameKeyHeldFromPause;

    private static void RaiseGameHoldTarget(GameTimeManager __instance, bool held, out int __state)
    {
        try
        {
            if (!held) IsGameKeyHeldFromPause = false;
            else if (GameClock.IsLive(__instance) && __instance.IsPausedScale()) IsGameKeyHeldFromPause = true;
        }
        catch (Exception ex)
        {
            IsGameKeyHeldFromPause = false;
            Fail("reading the game's hold-to-speed-up key", ex);
        }

        __state = -1;

        if (!held || FastGear.HoldKeyScale(__instance.ChapterSpeedUp2, HoldKeyMultiple) is not { } target) return;

        __state = __instance.ChapterSpeedUp2;
        __instance.ChapterSpeedUp2 = Math.Max(__state, (int)Math.Ceiling(target));
    }

    private static void HoldGameHoldSpeed(GameTimeManager __instance, int __state)
    {
        if (__state < 0) return;

        __instance.ChapterSpeedUp2 = __state;

        if (!__instance.IsHoldBoostActive() || FastGear.HoldKeyScale(__state, HoldKeyMultiple) is not { } target) return;

        if (Math.Abs(__instance.BattleTimeScale - target) > HoldKeyTolerance) GameClock.HoldSpeed(__instance, target);
    }
}
