using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// Where the game says an alert happened. The wave state is read off the live world when the ticker asks; two hooks
/// count emergencies that start and trade drones that come home, and the ticker takes the counts on its next check.
/// Only the live world's managers count, so a leftover world still answering the game's events adds nothing. Only
/// plain fields are written in the hooks: they run inside the game's own update.
/// </summary>
internal static class AlertSignals
{
    private const int MaxLoggedFailures = 3;

    private static int _emergenciesFired;
    private static int _dronesLanded;
    private static bool _isCrisisNew;
    private static bool _isTripUnsettled;
    private static int _droneRestoreFrame = -1;
    private static int _failures;
    private static Action<string> _log = _ => { };

    public static void Apply(Harmony harmony, AlertSettings settings, Action<string> log)
    {
        _log = log;

        if (settings.IsEmergencyOn)
        {
            Patch(harmony, typeof(DynamicEventManager), nameof(DynamicEventManager.TriggerCrisisEvent), nameof(BeforeCrisis), nameof(AfterCrisis),
                done: "emergencies watched", consequence: "PauseOnEmergency will do nothing");
        }

        if (!settings.IsDroneArrivalOn) return;

        Patch(harmony, typeof(StrangerTradeActiveManager), nameof(StrangerTradeActiveManager.SettleDroneReturn), nameof(BeforeDroneSettle), nameof(AfterDroneSettle),
            done: "trade drone landings watched", consequence: "PauseOnDroneArrival will do nothing");
        Patch(harmony, typeof(StrangerTradeActiveManager), nameof(StrangerTradeActiveManager.RestoreDroneModelState), nameof(BeforeDroneRestore), postfix: null,
            done: "drone trips settled by a load are told apart from landings", consequence: "a drone that came home while the game was closed may pause it once on loading");
    }

    /// <summary>Whether a zombie wave is under way in the live world, by the game's own test: any wave state but idle.</summary>
    public static bool IsWaveActive() => LiveWorld()?._TowerWaveManager?.IsWaveActive() == true;

    /// <summary>Emergencies that started since the last call.</summary>
    public static int TakeEmergenciesFired()
    {
        var fired = _emergenciesFired;

        _emergenciesFired = 0;

        return fired;
    }

    /// <summary>Trade drone trips that came home since the last call.</summary>
    public static int TakeDronesLanded()
    {
        var landed = _dronesLanded;

        _dronesLanded = 0;

        return landed;
    }

    /// <summary>
    /// The game triggers a crisis that is already running again without firing it anew, so only one that was not
    /// running before and is after counts.
    /// </summary>
    private static void BeforeCrisis(DynamicEventManager __instance, int eventId)
    {
        _isCrisisNew = false;

        try
        {
            _isCrisisNew = IsLive(__instance) && !__instance.IsCrisisRunning(eventId);
        }
        catch (Exception ex)
        {
            Fail("reading an emergency before it fires", ex);
        }
    }

    private static void AfterCrisis(DynamicEventManager __instance, int eventId)
    {
        try
        {
            if (_isCrisisNew && __instance.IsCrisisRunning(eventId)) _emergenciesFired++;
        }
        catch (Exception ex)
        {
            Fail("counting an emergency", ex);
        }
        finally
        {
            _isCrisisNew = false;
        }
    }

    /// <summary>
    /// Each of the game's ways of landing a trip here marks it settled, and a settled trip is not settled again until it
    /// flies, so a trip going from unsettled to settled in here is one landing. A trip settled while the game puts its
    /// drones back after a load came home while the game was closed, which is not news.
    /// </summary>
    private static void BeforeDroneSettle(StrangerTradeActiveManager __instance, DroneTripComponent trip)
    {
        _isTripUnsettled = false;

        try
        {
            _isTripUnsettled = trip != null && Time.frameCount != _droneRestoreFrame && IsLive(__instance) && !trip.ReturnSettled;
        }
        catch (Exception ex)
        {
            Fail("reading a drone trip before it settles", ex);
        }
    }

    private static void AfterDroneSettle(DroneTripComponent trip)
    {
        try
        {
            if (_isTripUnsettled && trip != null && trip.ReturnSettled) _dronesLanded++;
        }
        catch (Exception ex)
        {
            Fail("counting a drone landing", ex);
        }
        finally
        {
            _isTripUnsettled = false;
        }
    }

    // Marks the frame rather than setting a flag, so a restore that throws cannot leave drone landings ignored for good.
    private static void BeforeDroneRestore() => _droneRestoreFrame = Time.frameCount;

    /// <summary>The live world, or null before the game has made one; reading the singleton's Instance would build one.</summary>
    private static BattleLogicWorld? LiveWorld() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

    private static bool IsLive(DynamicEventManager events) => LiveWorld()?._DynamicEventManager?.Pointer == events.Pointer;

    private static bool IsLive(StrangerTradeActiveManager trade) => LiveWorld()?._StrangerTradeActiveManager?.Pointer == trade.Pointer;

    private static void Patch(Harmony harmony, Type type, string method, string prefix, string? postfix, string done, string consequence)
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method),
                prefix: new HarmonyMethod(typeof(AlertSignals), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(AlertSignals), postfix));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, {consequence}: {ex.Message}");
        }
    }

    private static void Fail(string doing, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"failed {doing} (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
