using System;
using System.Collections.Generic;

namespace TimeControl.Core;

/// <summary>Which countdowns show under the clock at home.</summary>
public readonly record struct CountdownSettings(bool IsNightOn, bool IsWaveOn);

/// <summary>
/// How long until night and until the next zombie wave, by the game clock, shown under the clock at home. The
/// disaster's own countdown is left to the game, which already shows one.
/// </summary>
public static class Countdowns
{
    public const string ElementId = "tc-countdown";

    private const int SecondsPerHour = 3600;
    private const int SecondsPerDay = 24 * SecondsPerHour;

    /// <summary>The game's day runs from 7:00 until 19:00.</summary>
    private const int DayStarts = 7 * SecondsPerHour;

    private const int NightStarts = 19 * SecondsPerHour;

    /// <summary>Game seconds until night falls; null at night, when there is nothing to count down to.</summary>
    /// <param name="clockSeconds">The game's clock in seconds; whole days are dropped.</param>
    public static int? NightIn(long clockSeconds)
    {
        var clock = (int)(clockSeconds % SecondsPerDay);

        return clock is >= DayStarts and < NightStarts ? NightStarts - clock : null;
    }

    /// <summary>Game seconds until the soonest wave still to come; null when none is known.</summary>
    /// <param name="clockSeconds">The game's clock in seconds; whole days are dropped.</param>
    /// <param name="waveHours">The hours of the day the game has waves set for today.</param>
    /// <param name="lureSecondsLeft">Game seconds until a lure rig's wave; 0 or less when none is coming.</param>
    public static int? WaveIn(long clockSeconds, IEnumerable<int> waveHours, int lureSecondsLeft)
    {
        var clock = (int)(clockSeconds % SecondsPerDay);
        int? soonest = lureSecondsLeft > 0 ? lureSecondsLeft : null;

        foreach (var hour in waveHours)
        {
            var left = hour * SecondsPerHour - clock;

            if (left > 0 && (soonest == null || left < soonest)) soonest = left;
        }

        return soonest;
    }

    /// <returns>One line per countdown that is on and running, such as "Night in 3h 20m"; empty when there are none.</returns>
    public static string Text(int? nightIn, int? waveIn, CountdownSettings settings, bool isChinese)
    {
        var lines = new List<string>();

        if (settings.IsNightOn && nightIn is { } night && night > 0) lines.Add(Line(KeysTexts.NightIn, night, isChinese));

        if (settings.IsWaveOn && waveIn is { } wave && wave > 0) lines.Add(Line(KeysTexts.WaveIn, wave, isChinese));

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Hours and minutes, with the minutes rounded up so the countdown never reads 0m while time is left: "3h 20m",
    /// "45m", "3h", or "3小时20分钟" in Chinese.
    /// </summary>
    public static string Duration(int seconds, bool isChinese)
    {
        var minutes = (int)Math.Ceiling(Math.Max(seconds, 0) / 60.0);
        var hours = minutes / 60;
        var rest = minutes % 60;
        var hoursText = hours + KeysTexts.Hours.Pick(isChinese);
        var minutesText = rest + KeysTexts.Minutes.Pick(isChinese);

        if (hours == 0) return minutesText;

        if (rest == 0) return hoursText;

        return isChinese ? hoursText + minutesText : hoursText + " " + minutesText;
    }

    /// <summary>Puts the countdowns under the clock, below the order's finish time; empty text takes them off.</summary>
    public static string Script(string text) => ClockNotes.Script(ElementId, text);

    private static string Line(Phrase label, int seconds, bool isChinese) => label.Pick(isChinese) + " " + Duration(seconds, isChinese);
}
