using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>Where the game's speed was when a slow press came in, which decides where the slow button lands.</summary>
public enum SlowPress
{
    /// <summary>Running at any speed but slow.</summary>
    FromOtherSpeed,

    /// <summary>Paused, by anyone.</summary>
    FromPause,

    /// <summary>Already running on the slow button, the game's own slow or one of ours.</summary>
    FromSlowSpeed,
}

/// <summary>
/// The extra speeds below the game's own slow. The first press is the game's own slow, which runs at full speed while
/// the character has orders and slows down when they idle; pressing slow again moves on through the list and wraps
/// back to the game's own, so with "0.5,0.25" the presses go game's slow, half, quarter, game's slow.
/// </summary>
public static class SlowGear
{
    /// <summary>The game's own slow, with none of our speeds held.</summary>
    public const int GamesOwn = -1;

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

    /// <summary>A paused game is checked first, as the game drops its slow flag on a pause.</summary>
    public static SlowPress PressFrom(bool isPaused, bool isSlowGearActive)
    {
        if (isPaused) return SlowPress.FromPause;

        return isSlowGearActive ? SlowPress.FromSlowSpeed : SlowPress.FromOtherSpeed;
    }

    /// <summary>
    /// Which speed a slow press lands on: an index into the list, or <see cref="GamesOwn"/>. A press while already slow
    /// moves on one and wraps back to the game's own; a press that resumes from pause comes back to the speed in use
    /// before it; a press from any other speed starts at the game's own.
    /// </summary>
    public static int NextIndex(int current, int count, SlowPress from)
    {
        if (count <= 0) return GamesOwn;

        var remembered = current >= GamesOwn && current < count ? current : GamesOwn;

        return from switch
        {
            SlowPress.FromSlowSpeed => remembered == count - 1 ? GamesOwn : remembered + 1,
            SlowPress.FromPause => remembered,
            SlowPress.FromOtherSpeed => GamesOwn,
            _ => throw new ArgumentOutOfRangeException(nameof(from), from, "a slow press NextIndex does not handle"),
        };
    }

    public static string Label(float speed) => speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
}
