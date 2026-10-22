using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Where the game shows the alerts after the queue running out. Each is read off the live world when the ticker asks,
/// and only for an alert that is switched on; one hook counts crops that ripen. A reading that fails is null, so it is
/// skipped rather than taken for a change, and the rest of the check goes on.
/// </summary>
internal static class MoreAlertSignals
{
    private const int MaxLoggedFailures = 3;

    /// <summary>The state DataCollectionManager.OnPlantStateChanged is told of when a crop becomes ready to harvest.</summary>
    private const int RipePlantState = 2;

    /// <summary>The buff type the game gives illnesses.</summary>
    private const int IllnessBuffType = 1;

    private static readonly Alert[] NeedAlerts = { Alert.LowSatiety, Alert.LowMorale, Alert.LowStamina, Alert.LowHealth, Alert.LowVitality };

    private static int _plantsRipened;
    private static int _failures;
    private static Action<string> _log = _ => { };
    private static bool _isPlantHooked;

    /// <summary>Called on the main thread at start and after each change. The crop hook goes in the first time its alert is turned on and stays in.</summary>
    public static void Use(Harmony harmony, AlertSet on, Action<string> log)
    {
        _log = log;

        if (!on.Has(Alert.PlantRipe) || _isPlantHooked) return;

        _isPlantHooked = true;

        try
        {
            harmony.Patch(
                AccessTools.Method(typeof(DataCollectionManager), "OnPlantStateChanged"),
                postfix: new HarmonyMethod(typeof(MoreAlertSignals), nameof(AfterPlantStateChanged)));
            _log("ripe crops watched");
        }
        catch (Exception ex)
        {
            _log($"could not hook DataCollectionManager.OnPlantStateChanged, PauseOnCropRipe will do nothing: {ex.Message}");
        }
    }

    /// <summary>What the game shows now for each alert in <paramref name="on"/>; the crops counted are taken either way, so none are saved up.</summary>
    public static AlertReadings Read(GameTimeManager time, AlertSet on)
    {
        var plantsRipened = TakePlantsRipened();
        var world = LiveWorld();

        if (world == null) return default;

        var isDayWatched = on.Has(Alert.Dusk) || on.Has(Alert.Dawn);
        var isPhoneWatched = on.Has(Alert.PhoneMessage) || on.Has(Alert.TradeMessage);
        var unread = isPhoneWatched ? Try("reading the phone's unread messages", () => UnreadCounts(world)) : null;

        return new AlertReadings(
            isDayWatched ? Try("reading the hour", () => (int?)time.GetHour24()) : null,
            IsAnyNeedWatched(on) ? Try("reading the character's needs", () => ReadNeeds(world)) ?? default : default,
            on.Has(Alert.Sick) ? Try("reading illnesses", () => Ailments(world)) : null,
            on.Has(Alert.HouseHit) ? Try("reading the wave's damage to the house", () => (int?)world._TowerWaveManager?.waveStructureDamage) : null,
            on.Has(Alert.TrapHit) ? Try("reading the wave's damage to traps", () => (int?)world._TowerWaveManager?.waveDeviceDamage) : null,
            on.Has(Alert.PowerOut) ? Try("reading the power", () => world._PowerManager?.IsPowerOff()) : null,
            on.Has(Alert.PhoneMessage) ? unread?.Messages : null,
            on.Has(Alert.TradeMessage) ? unread?.Trades : null,
            on.Has(Alert.PlantRipe) ? plantsRipened : 0);
    }

    private static int TakePlantsRipened()
    {
        var ripened = _plantsRipened;

        _plantsRipened = 0;

        return ripened;
    }

    private static bool IsAnyNeedWatched(AlertSet on)
    {
        foreach (var alert in NeedAlerts)
        {
            if (on.Has(alert)) return true;
        }

        return false;
    }

    /// <summary>
    /// Each need against its maximum, as the game's own bars show them: a need kept as a fraction is scaled by the game's
    /// ratio, and the bar divides its value by that ratio and not its maximum (AttributeComponent.SyncAttr2UI).
    /// </summary>
    private static NeedLevels? ReadNeeds(BattleLogicWorld world)
    {
        var role = world._AgentManager?.GetLeadingRole();
        var attributes = role == null ? null : AgentTools.GetAgentComponent<AttributeComponent>(role);

        if (attributes?.AttrDict == null) return null;

        var ratio = GameKey.Attr_ScalingRatio;

        return new NeedLevels(
            Fraction(attributes, AttrName.Satiety, AttrName.MaxSatiety, ratio),
            Fraction(attributes, AttrName.Morale, AttrName.MaxMorale, ratio),
            Fraction(attributes, AttrName.Stamina, AttrName.MaxStamina, ratio),
            Fraction(attributes, AttrName.Health, AttrName.MaxHealth, ratio),
            Fraction(attributes, AttrName.Vitality, AttrName.MaxVitality, ratio));
    }

    private static float? Fraction(AttributeComponent attributes, AttrName need, AttrName max, float ratio)
    {
        var value = Raw(attributes, need, ratio) / ratio;
        var limit = Raw(attributes, max, ratio);

        return value is { } now && limit is > 0f ? now / limit.Value : null;
    }

    /// <summary>The value as the game keeps it; the whole-number read of a fraction attribute only logs a complaint and returns 0.</summary>
    private static float? Raw(AttributeComponent attributes, AttrName name, float ratio)
    {
        if (!attributes.AttrDict.ContainsKey(name)) return null;

        return attributes.IsSingle(name) ? attributes.GetTotalValue_Float(name) * ratio : attributes.GetTotalValue_Int(name);
    }

    /// <summary>The character's illnesses: the bad buffs of the game's illness type in effect on them.</summary>
    private static int? Ailments(BattleLogicWorld world)
    {
        var role = world._AgentManager?.GetLeadingRole();
        var buffs = role == null ? null : AgentTools.GetAgentComponent<BuffComponent>(role)?.GetEffectiveBuffList();

        if (buffs == null) return null;

        var ailments = 0;

        for (var i = 0; i < buffs.Count; i++)
        {
            var config = buffs[i]?.config;

            if (config != null && config.BuffType == IllnessBuffType && !config.IsGood) ailments++;
        }

        return ailments;
    }

    private static (int Messages, int Trades)? UnreadCounts(BattleLogicWorld world)
    {
        var phone = world._PhoneSMSManager;

        if (phone == null) return null;

        phone.GetUnreadCounts(out var normal, out var evt, out var trade);

        return (normal + evt, trade);
    }

    /// <summary>Only the live world's crops count, so a leftover world still answering the game's events adds nothing. Only a plain field is written: this runs inside the game's own update.</summary>
    private static void AfterPlantStateChanged(DataCollectionManager __instance, int plantState)
    {
        try
        {
            if (plantState == RipePlantState && LiveWorld()?._DataCollectionManager?.Pointer == __instance.Pointer) _plantsRipened++;
        }
        catch (Exception ex)
        {
            Fail("counting a ripe crop", ex);
        }
    }

    /// <summary>The live world, or null before the game has made one; reading the singleton's Instance would build one.</summary>
    private static BattleLogicWorld? LiveWorld() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

    private static T? Try<T>(string doing, Func<T?> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            Fail(doing, ex);

            return null;
        }
    }

    private static void Fail(string doing, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"failed {doing} (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
