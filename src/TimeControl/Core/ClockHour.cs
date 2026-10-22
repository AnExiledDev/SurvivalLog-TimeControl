using System;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>A moment of game time: game seconds counted up by the running clock, and the second of the day it falls on.</summary>
/// <param name="Total">Game seconds on the running clock; only compared with totals read off the same clock.</param>
/// <param name="SecondOfDay">The time of day the game shows, in seconds after midnight.</param>
public readonly record struct ClockReading(long Total, int SecondOfDay)
{
    /// <summary>The total at the next time the clock shows this time of day, never now.</summary>
    public long Next(int secondOfDay) => Total + ClockHour.SecondsUntil(SecondOfDay, secondOfDay);

    /// <summary>The time of day a total on the same clock falls on.</summary>
    public int SecondOfDayAt(long total) => ClockHour.Wrap(SecondOfDay + (total - Total));
}

/// <summary>Times of day as the settings write them and the menu shows them: hours and minutes on a 24-hour clock.</summary>
public static class ClockHour
{
    public const int Day = 24 * Hour;
    public const int Hour = 3600;

    /// <summary>The game's day starts at seven: from then until seven in the evening it counts as day.</summary>
    public const int Morning = 7 * Hour;

    /// <summary>The colon a Chinese keyboard types.</summary>
    private const char FullWidthColon = (char)0xFF1A;

    /// <summary>
    /// Reads "7", "07", "7:30" or "07:30", with a full-width colon too. Empty means no time set, and anything else is
    /// unreadable.
    /// </summary>
    public static bool TryParse(string? text, out int? secondOfDay)
    {
        secondOfDay = null;

        var trimmed = (text ?? "").Trim().Replace(FullWidthColon, ':');

        if (trimmed.Length == 0) return true;

        var parts = trimmed.Split(':');

        if (parts.Length > 2 || !TryPart(parts[0], 23, out var hour)) return false;

        var minute = 0;

        if (parts.Length == 2 && (parts[1].Length != 2 || !TryPart(parts[1], 59, out minute))) return false;

        secondOfDay = hour * Hour + minute * 60;

        return true;
    }

    /// <summary>Game seconds from one time of day to the next time the clock shows another; a whole day when they are the same.</summary>
    public static int SecondsUntil(int fromSecondOfDay, int toSecondOfDay)
    {
        var until = Wrap(toSecondOfDay - (long)fromSecondOfDay);

        return until == 0 ? Day : until;
    }

    public static string Label(int secondOfDay)
    {
        var wrapped = Wrap(secondOfDay);

        return (wrapped / Hour).ToString("00", CultureInfo.InvariantCulture) + ":" + (wrapped % Hour / 60).ToString("00", CultureInfo.InvariantCulture);
    }

    internal static int Wrap(long seconds) => (int)((seconds % Day + Day) % Day);

    /// <summary>One or two digits, from 0 to the most allowed.</summary>
    private static bool TryPart(string text, int most, out int value)
    {
        if (text.Length is >= 1 and <= 2 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= most) return true;

        value = 0;

        return false;
    }
}
