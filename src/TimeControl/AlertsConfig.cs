using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TimeControl.Adapters;
using TimeControl.Core;

namespace TimeControl;

/// <summary>
/// The settings for the pause while away from the game, what happens when the order queue runs out, and the alerts
/// after it. They are applied on their own, on top of the clock settings, and read live from their entries.
/// </summary>
internal static class AlertsConfig
{
    private const string ClockSection = "Clock";
    private const string AlertsSection = "Alerts";
    private const string RetiredQueueKey = "PauseOnQueueEmpty";

    private const string CarryOn = " Pick a speed or press the pause key (Space by default) to carry on.\n";
    private const string CarryOnChinese = "选一个速度或按暂停键（默认 Space）继续。";
    private const string MenuKept = ", and bring back your speed when you close it. Giving an order while it is open, such as a craft, runs the game until the order is done, then it pauses again. If you change the speed yourself while it is open, your choice is kept.\n";
    private const string MenuKeptChinese = "时暂停游戏，关闭时恢复原来的速度。打开期间下达指令（例如制作）会让游戏运行到指令完成，然后再次暂停。如果打开期间你自己改了速度，则保留你的选择。";

    private static readonly List<(ConfigEntry<bool> Entry, Alert[] Alerts)> AlertToggles = new();

    private static ConfigEntry<QueueEmptyAction>? _onQueueEmpty;

    /// <summary>The queue running out pauses the game, as the setting this one replaced did.</summary>
    public static bool IsQueuePauseOn => _onQueueEmpty?.Value == QueueEmptyAction.Pause;

    /// <summary>The alerts after the queue running out that are switched on.</summary>
    public static AlertSet MoreOn
    {
        get
        {
            var on = AlertSet.None;

            foreach (var (entry, alerts) in AlertToggles)
            {
                foreach (var alert in alerts) on = on.With(alert, entry.Value);
            }

            return on;
        }
    }

    /// <summary>Binds the settings, moves the retired queue setting over, and applies them now and after each change.</summary>
    public static void Bind(ConfigFile config, Harmony harmony, Action<string> log)
    {
        var retiredQueueValue = TakeRetired(config, RetiredQueueKey, log);
        var isFocusLossOn = config.Bind(ClockSection, "PauseOnFocusLoss", true,
            "Pause the game when its window goes to the background, for example after Alt+Tab, and bring back your speed when you switch back. If you change the speed yourself in the meantime, your choice is kept. Works at home, before the disaster, on expeditions (with TimeControlOnExpeditions on) and during sleep fast-forward.\n" +
            "游戏窗口切到后台时（例如按 Alt+Tab 后）暂停游戏，切回来时恢复原来的速度。如果期间你自己改了速度，则保留你的选择。在安全屋、灾前筹备、外出探索（需开启 TimeControlOnExpeditions）和睡眠快进时都有效。");
        var isBagOn = config.Bind(ClockSection, "PauseInBag", false,
            "Pause the game while your bag is open" + MenuKept + "打开背包" + MenuKeptChinese);
        var isWorkbenchOn = config.Bind(ClockSection, "PauseAtWorkbench", true,
            "Pause the game while the workbench window is open" + MenuKept + "打开工作台界面" + MenuKeptChinese);
        var isPhoneOn = config.Bind(ClockSection, "PauseInPhone", false,
            "Pause the game while the phone is open" + MenuKept + "打开手机" + MenuKeptChinese);
        var isTradeOn = config.Bind(ClockSection, "PauseInTrade", false,
            "Pause the game while the trade window is open" + MenuKept + "打开交易界面" + MenuKeptChinese);

        var onQueueEmpty = config.Bind(AlertsSection, "OnQueueEmpty", QueueEmptyAction.Off,
            "What happens when your character finishes the last order in their queue. Off does nothing. Slow drops the game to QueueEmptySlowSpeed if it runs faster, and brings your speed back on your next order unless you change the speed yourself in between. Pause pauses the game once; unlike IdleFreeze, it pauses right away and stays paused until you pick a speed or press the pause key (Space by default).\n" +
            "角色做完队列里最后一个指令时怎么做。Off 不做任何事。Slow 在游戏比 QueueEmptySlowSpeed 快时降到这个速度，你下达下一个指令时恢复原来的速度，除非期间你自己改了速度。Pause 暂停游戏一次；与 IdleFreeze 不同，它会立刻暂停，并一直停着，直到你选一个速度或按暂停键（默认 Space）。");

        var slowSpeed = config.Bind(AlertsSection, "QueueEmptySlowSpeed", QueueSlowTarget.Normal,
            new ConfigDescription(
                "The speed OnQueueEmpty = Slow drops the game to: Slow (the slow speed you last used), Normal or Fast. It only slows the game down, never speeds it up.\n" +
                "OnQueueEmpty 为 Slow 时降到的速度：Slow 为慢速（你上次用的慢速），Normal 为正常速度，Fast 为快速。只会让游戏变慢，不会变快。",
                null, "ModManager.ShowWhen=OnQueueEmpty=Slow"));

        _onQueueEmpty = onQueueEmpty;
        MigrateQueueSetting(config, onQueueEmpty, retiredQueueValue, log);
        AlertToggles.Clear();
        Toggle(config, "PauseOnHouseHit",
            "Pause the game once when zombies first damage the house, a door or a window during a wave." + CarryOn + "尸潮中丧尸第一次破坏房屋、门或窗户时暂停游戏一次。" + CarryOnChinese,
            Alert.HouseHit);
        Toggle(config, "PauseOnTrapHit",
            "Pause the game once when zombies first damage a trap during a wave." + CarryOn + "尸潮中丧尸第一次破坏陷阱时暂停游戏一次。" + CarryOnChinese,
            Alert.TrapHit);
        Toggle(config, "PauseOnPowerOut",
            "Pause the game once when the power goes out." + CarryOn + "停电时暂停游戏一次。" + CarryOnChinese,
            Alert.PowerOut);

        var isLowNeedOn = Toggle(config, "PauseOnLowNeed",
            "Pause the game once when your character's satiety, morale, stamina, fitness or life falls to LowNeedPercent of its maximum or below. It pauses again for the same need only after it has risen a little above that." + CarryOn +
            "角色的饱腹、心态、精力、健康或生命降到最大值的 LowNeedPercent 或以下时暂停游戏一次。同一项要先回升到稍高于这个值，才会再次暂停。" + CarryOnChinese,
            Alert.LowSatiety, Alert.LowMorale, Alert.LowStamina, Alert.LowHealth, Alert.LowVitality);
        var lowNeedPercent = config.Bind(AlertsSection, "LowNeedPercent", AlertMemory.DefaultLowNeedPercent,
            new ConfigDescription(
                "How low a need has to fall, in percent of its maximum, for PauseOnLowNeed to pause the game. From 1 to 90.\n" +
                "需求降到最大值的百分之几时，PauseOnLowNeed 会暂停游戏。范围 1 到 90。",
                new AcceptableValueRange<int>(1, 90), "ModManager.ShowWhen=PauseOnLowNeed"));

        Toggle(config, "PauseOnSick",
            "Pause the game once when your character falls ill." + CarryOn + "角色生病时暂停游戏一次。" + CarryOnChinese,
            Alert.Sick);
        Toggle(config, "PauseOnPhoneMessage",
            "Pause the game once when a new text message arrives on the phone. Trade messages have their own setting, PauseOnTradeMessage." + CarryOn +
            "手机收到新短信时暂停游戏一次。交易短信由 PauseOnTradeMessage 单独设置。" + CarryOnChinese,
            Alert.PhoneMessage);
        Toggle(config, "PauseOnTradeMessage",
            "Pause the game once when a new trade message arrives on the phone." + CarryOn + "手机收到新的交易短信时暂停游戏一次。" + CarryOnChinese,
            Alert.TradeMessage);
        Toggle(config, "PauseOnCropRipe",
            "Pause the game once when a crop is ready to harvest. Several ripening at once pause it once." + CarryOn +
            "作物成熟可以收获时暂停游戏一次。几株同时成熟只暂停一次。" + CarryOnChinese,
            Alert.PlantRipe);
        Toggle(config, "PauseOnDusk",
            "Pause the game once when evening comes, at 19:00 game time." + CarryOn + "游戏时间到晚上 19:00 时暂停游戏一次。" + CarryOnChinese,
            Alert.Dusk);
        Toggle(config, "PauseOnDawn",
            "Pause the game once when morning comes, at 7:00 game time. Sleeping through it does not pause the game." + CarryOn +
            "游戏时间到清晨 7:00 时暂停游戏一次。睡觉时经过这个时间不会暂停。" + CarryOnChinese,
            Alert.Dawn);

        var watched = new List<ConfigEntryBase> { isFocusLossOn, isBagOn, isWorkbenchOn, isPhoneOn, isTradeOn, onQueueEmpty, slowSpeed, lowNeedPercent };

        foreach (var (entry, _) in AlertToggles) watched.Add(entry);

        OptionalLive.Watch(() =>
        {
            var moreOn = MoreOn;
            var away = new AwaySettings(isFocusLossOn.Value, isBagOn.Value, isWorkbenchOn.Value, isPhoneOn.Value, isTradeOn.Value);
            var queueSlowing = new QueueSlowSettings(onQueueEmpty.Value == QueueEmptyAction.Slow, slowSpeed.Value);

            ClockTicker.Settings = ClockTicker.Settings with { Alerts = ClockTicker.Settings.Alerts with { IsQueueEmptyOn = IsQueuePauseOn, MoreOn = moreOn } };
            ClockTicker.Away = away;
            ClockTicker.QueueSlowing = queueSlowing;
            ClockTicker.LowNeedPercent = AlertMemory.ClampPercent(lowNeedPercent.Value);
            log($"pause on focus loss {OnOff(away.IsFocusLossOn)}, in bag {OnOff(away.IsBagOn)}, at workbench {OnOff(away.IsWorkbenchOn)}, in phone {OnOff(away.IsPhoneOn)}, in trade {OnOff(away.IsTradeOn)}, " +
                $"on queue empty {onQueueEmpty.Value}{(queueSlowing.IsOn ? $" to {slowSpeed.Value}" : "")}, " +
                $"more alerts on: {(moreOn.IsEmpty ? "none" : string.Join(", ", Names(moreOn)))}{(isLowNeedOn.Value ? $", low need at {ClockTicker.LowNeedPercent}%" : "")}");
            MoreAlertSignals.Use(harmony, moreOn, log);
        }, watched.ToArray());
    }

    private static ConfigEntry<bool> Toggle(ConfigFile config, string key, string description, params Alert[] alerts)
    {
        var entry = config.Bind(AlertsSection, key, false, description);

        AlertToggles.Add((entry, alerts));

        return entry;
    }

    /// <summary>A player who had the retired on/off setting on keeps the pause they chose.</summary>
    private static void MigrateQueueSetting(ConfigFile config, ConfigEntry<QueueEmptyAction> onQueueEmpty, string? retiredValue, Action<string> log)
    {
        var migrated = QueueEmptyActions.FromRetired(retiredValue, onQueueEmpty.Value);

        if (migrated != onQueueEmpty.Value)
        {
            onQueueEmpty.Value = migrated;
            log($"{RetiredQueueKey} = {retiredValue} moved to OnQueueEmpty = {migrated}");
        }

        if (retiredValue != null) Save(config, log);
    }

    /// <summary>
    /// BepInEx keeps keys no plugin binds and writes them back on every save, and only offers them through a private
    /// property, so the retired key is read and removed through it; if a BepInEx update renames it, the key just stays
    /// in the file and the new setting keeps its default.
    /// </summary>
    /// <returns>The retired key's value as saved, or null when the file has none.</returns>
    private static string? TakeRetired(ConfigFile config, string key, Action<string> log)
    {
        try
        {
            var orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(config) as IDictionary;
            var definition = new ConfigDefinition(AlertsSection, key);

            if (orphans == null || !orphans.Contains(definition)) return null;

            var value = orphans[definition] as string;

            orphans.Remove(definition);

            return value;
        }
        catch (Exception ex)
        {
            log($"could not read the old {key} setting: {ex.Message}");

            return null;
        }
    }

    private static void Save(ConfigFile config, Action<string> log)
    {
        try
        {
            config.Save();
        }
        catch (Exception ex)
        {
            log($"could not save the settings file after moving {RetiredQueueKey}: {ex.Message}");
        }
    }

    private static IEnumerable<string> Names(AlertSet set)
    {
        foreach (Alert alert in Enum.GetValues(typeof(Alert)))
        {
            if (set.Has(alert)) yield return alert.ToString();
        }
    }

    private static string OnOff(bool isOn) => isOn ? "on" : "off";
}
