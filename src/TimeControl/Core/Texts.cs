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

    public static readonly Phrase SpeedSlow = new("Slow", "慢速");
    public static readonly Phrase SpeedNormal = new("Normal", "正常速度");
    public static readonly Phrase SpeedFast = new("Fast", "快速");
    public static readonly Phrase SpeedFastest = new("Fastest", "极速");
    public static readonly Phrase SpeedPause = new("Pause", "暂停");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        PausedIdle, Paused, PausedWave, PausedEmergency, PausedDroneBack, PausedQueueEmpty,
        SpeedSlow, SpeedNormal, SpeedFast, SpeedFastest, SpeedPause,
    };
}
