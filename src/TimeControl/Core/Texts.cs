using System.Collections.Generic;

namespace TimeControl.Core;

/// <summary>One piece of on-screen text in the two languages the game ships.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;
}

/// <summary>
/// Every word the mod puts on screen. Chinese uses the game's own terms; the speed names are the game's own, used only
/// when the page has not loaded its text yet.
/// </summary>
public static class Texts
{
    public static readonly Phrase PausedIdle = new("Paused: idle", "已暂停：空闲");
    public static readonly Phrase Paused = new("Paused", "已暂停");
    public static readonly Phrase PausedWave = new("Paused: wave", "已暂停：尸潮");
    public static readonly Phrase PausedEmergency = new("Paused: emergency", "已暂停：危机事件");
    public static readonly Phrase PausedDroneBack = new("Paused: drone back", "已暂停：无人机归来");
    public static readonly Phrase PausedQueueEmpty = new("Paused: queue empty", "已暂停：队列已空");
    public static readonly Phrase PausedHouseHit = new("Paused: house attacked", "已暂停：丧尸攻门");
    public static readonly Phrase PausedTrapHit = new("Paused: trap attacked", "已暂停：陷阱遭攻击");
    public static readonly Phrase PausedPowerOut = new("Paused: power out", "已暂停：停电");
    public static readonly Phrase PausedLowSatiety = new("Paused: low satiety", "已暂停：饱腹偏低");
    public static readonly Phrase PausedLowMorale = new("Paused: low morale", "已暂停：心态偏低");
    public static readonly Phrase PausedLowStamina = new("Paused: low stamina", "已暂停：精力偏低");
    public static readonly Phrase PausedLowHealth = new("Paused: low fitness", "已暂停：健康偏低");
    public static readonly Phrase PausedLowVitality = new("Paused: low life", "已暂停：生命偏低");
    public static readonly Phrase PausedSick = new("Paused: sick", "已暂停：生病");
    public static readonly Phrase PausedPhoneMessage = new("Paused: new message", "已暂停：新短信");
    public static readonly Phrase PausedTradeMessage = new("Paused: trade message", "已暂停：交易短信");
    public static readonly Phrase PausedPlantRipe = new("Paused: crop ripe", "已暂停：作物成熟");
    public static readonly Phrase PausedDusk = new("Paused: evening", "已暂停：晚上");
    public static readonly Phrase PausedDawn = new("Paused: morning", "已暂停：清晨");
    public static readonly Phrase PausedInBackground = new("Paused: in background", "已暂停：游戏在后台");
    public static readonly Phrase PausedMenuOpen = new("Paused: menu open", "已暂停：界面已打开");

    public static readonly Phrase SpeedSlow = new("Slow", "慢速");
    public static readonly Phrase SpeedNormal = new("Normal", "正常速度");
    public static readonly Phrase SpeedFast = new("Fast", "快速");
    public static readonly Phrase SpeedFastest = new("Fastest", "极速");
    public static readonly Phrase SpeedPause = new("Pause", "暂停");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        PausedIdle, Paused, PausedWave, PausedEmergency, PausedDroneBack, PausedQueueEmpty,
        PausedHouseHit, PausedTrapHit, PausedPowerOut, PausedLowSatiety, PausedLowMorale, PausedLowStamina, PausedLowHealth,
        PausedLowVitality, PausedSick, PausedPhoneMessage, PausedTradeMessage, PausedPlantRipe, PausedDusk, PausedDawn,
        PausedInBackground, PausedMenuOpen,
        SpeedSlow, SpeedNormal, SpeedFast, SpeedFastest, SpeedPause,
    };
}
