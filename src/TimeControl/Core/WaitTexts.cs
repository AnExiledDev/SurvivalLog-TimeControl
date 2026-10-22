using System.Collections.Generic;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>Every word the wait menu puts on screen. Chinese uses the game's own terms where it has them.</summary>
public static class WaitTexts
{
    public static readonly Phrase WaitButton = new("Wait until", "等到");
    public static readonly Phrase AlarmIn = new("Pause in", "定时暂停");
    public static readonly Phrase Morning = new("Morning", "早晨");
    public static readonly Phrase BrewDone = new("Brew done", "酿造完成");
    public static readonly Phrase QueueDone = new("Queue done", "队列完成");
    public static readonly Phrase Waiting = new("Waiting: ", "等待中：");
    public static readonly Phrase AlarmAt = new("Pause at ", "暂停于 ");
    public static readonly Phrase Alarm = new("Alarm ", "闹钟 ");
    public static readonly Phrase Cancel = new("Cancel", "取消");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        WaitButton, AlarmIn, Morning, BrewDone, QueueDone, Waiting, AlarmAt, Alarm, Cancel,
    };

    /// <summary>A number of hours, as the menu's alarm buttons show it.</summary>
    public static Phrase Hours(int hours) => new(Number(hours) + "h", Number(hours) + "小时");

    /// <summary>A number of hours on from now, as the menu's fast-forward buttons show it.</summary>
    public static Phrase HoursOn(int hours) => new("+" + Number(hours) + "h", "+" + Number(hours) + "小时");

    private static string Number(int hours) => hours.ToString(CultureInfo.InvariantCulture);
}
