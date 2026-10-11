using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>
/// The speeds the game's slow button holds. Pressing slow while already slow moves to the next speed in the list,
/// so "0.5,0.25" gives half speed on the first press and quarter speed on the second.
/// </summary>
public static class SlowGear
{
    public const float Slowest = 0.05f;
    public const float Fastest = 0.95f;

    /// <summary>Unreadable entries are dropped and the rest clamped, so a typo costs one speed rather than the mod.</summary>
    public static IReadOnlyList<float> ParseSpeeds(string text)
    {
        var speeds = new List<float>();

        foreach (var part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || !float.IsFinite(speed)) continue;

            var clamped = Math.Clamp(speed, Slowest, Fastest);

            if (!speeds.Contains(clamped)) speeds.Add(clamped);
        }

        return speeds;
    }

    /// <summary>
    /// Which speed a slow press lands on. A press while already slow cycles; a press from another speed, or one that
    /// resumes from pause, comes back to the speed last used.
    /// </summary>
    public static int NextIndex(int current, int count, bool wasAlreadySlow)
    {
        if (count <= 0) return -1;

        var remembered = current >= 0 && current < count ? current : 0;

        return wasAlreadySlow ? (remembered + 1) % count : remembered;
    }

    public static string Label(float speed) => speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
}
