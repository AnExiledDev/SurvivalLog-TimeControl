using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// The workbench as time sees it: whether its window is open, which benches are crafting, and the game's freeze of
/// its world when the window opens and when a craft finishes, which this lifts while speed control at the workbench is
/// on (<see cref="BenchFreeze"/>). The freeze comes from Reducer_Web_ToolTable.RA_Open, through the game's pause
/// event, and from ToolTableManager.TryCreateItem, which calls BattleLogicWorld.OnPause; each is watched from just
/// before it to just after. Every read fails soft, logged at most a few times.
/// </summary>
internal static class Workbench
{
    private const int MaxLoggedFailures = 3;

    /// <summary>The game's HandMadeState.Making: the bench's craft timer is running.</summary>
    private const int MakingState = 1;

    /// <summary>Speed control at the workbench is on; read on every freeze, so a change applies at once.</summary>
    internal static bool IsSpeedControlOn;

    private static Action<string> _log = _ => { };
    private static bool _isHooked;
    private static float _lateUntil = -1f;
    private static bool _wasFrozenAtOpen;
    private static int _failures;

    public static void Hook(Harmony harmony, Action<string> log)
    {
        _log = log;

        if (_isHooked) return;

        _isHooked = true;
        Patch(harmony, typeof(Reducer_Web_ToolTable), nameof(Reducer_Web_ToolTable.RA_Open), nameof(AfterOpen), "opening the workbench");
        Patch(harmony, typeof(ToolTableManager), nameof(ToolTableManager.TryCreateItem), nameof(AfterCraftDone), "a craft finishing");
    }

    /// <summary>Whether the workbench window is open, by the game's own record of the menus it shows.</summary>
    public static bool IsOpen()
    {
        var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

        return ui != null && ui.IsUIActive(Il2CppType.Of<WebUI_ToolTable>());
    }

    /// <summary>Whether any workbench is crafting; false when unreadable.</summary>
    public static bool IsAnyCrafting() => CraftingBenches() is { Count: > 0 };

    /// <summary>The furniture ids of the workbenches whose craft timer is running; null when unreadable.</summary>
    public static HashSet<long>? CraftingBenches()
    {
        try
        {
            var benches = LiveWorld()?._ToolTableManager?.ToolTableMap;

            if (benches == null) return null;

            var crafting = new HashSet<long>();

            foreach (var bench in benches)
            {
                if (bench.Value != null && (int)bench.Value.State == MakingState) crafting.Add(bench.Key);
            }

            return crafting;
        }
        catch (Exception ex)
        {
            Fail("reading the workbenches", ex);

            return null;
        }
    }

    /// <summary>
    /// Lifts a freeze the game makes just after the window opens rather than while it opens. Called on every check;
    /// does nothing outside the short time after an opening.
    /// </summary>
    public static void LiftLateFreeze(float now)
    {
        if (now >= _lateUntil) return;

        try
        {
            var world = LiveWorld();

            if (world == null || !IsOpen()) return;

            if (BenchFreeze.Lifts(IsSpeedControlOn, isBenchOpen: true, _wasFrozenAtOpen, world.IsPause))
            {
                _lateUntil = -1f;
                Lift(world, "the workbench opened");
            }
        }
        catch (Exception ex)
        {
            _lateUntil = -1f;
            Fail("lifting the workbench's freeze", ex);
        }
    }

    private static void BeforeFreeze(out bool __state) => __state = IsFrozen();

    private static void AfterOpen(bool __state)
    {
        _wasFrozenAtOpen = __state;
        _lateUntil = Time.unscaledTime + BenchFreeze.LateSeconds;
        LiftIfNew(__state, isBenchOpen: true, "the workbench opened");
    }

    /// <summary>Only while the window is open: a craft finishing with it closed keeps the game's own freeze and result.</summary>
    private static void AfterCraftDone(bool __state) => LiftIfNew(__state, IsOpen(), "a craft finished");

    private static void LiftIfNew(bool wasFrozen, bool isBenchOpen, string why)
    {
        try
        {
            var world = LiveWorld();

            if (world == null || !BenchFreeze.Lifts(IsSpeedControlOn, isBenchOpen, wasFrozen, world.IsPause)) return;

            if (isBenchOpen) _lateUntil = -1f;

            Lift(world, why);
        }
        catch (Exception ex)
        {
            Fail("lifting the workbench's freeze", ex);
        }
    }

    private static void Lift(BattleLogicWorld world, string why)
    {
        world.OnResume();
        ClockTicker.LogFileOnly($"workbench: {why} and the game froze time, lifted it so the speed stays yours");
    }

    private static bool IsFrozen()
    {
        try
        {
            return LiveWorld()?.IsPause == true;
        }
        catch (Exception ex)
        {
            Fail("reading whether the game froze time", ex);

            return true;
        }
    }

    private static void Patch(Harmony harmony, Type type, string method, string postfix, string what)
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method) ?? throw new MissingMethodException($"{type.Name}.{method}"),
                prefix: new HarmonyMethod(typeof(Workbench), nameof(BeforeFreeze)),
                postfix: new HarmonyMethod(typeof(Workbench), postfix));
            _log($"the game's freeze on {what} is lifted while speed control at the workbench is on");
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, the game keeps freezing time on {what}: {ex.Message}");
        }
    }

    /// <summary>The live world, or null before the game has made one; reading the singleton's Instance would build one.</summary>
    private static BattleLogicWorld? LiveWorld() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

    private static void Fail(string doing, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"failed {doing} (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
