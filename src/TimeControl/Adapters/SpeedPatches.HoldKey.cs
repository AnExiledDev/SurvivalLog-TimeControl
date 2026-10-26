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
    private static void RaiseGameHoldTarget(GameTimeManager __instance, bool held, out int __state)
    {
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
