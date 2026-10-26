using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>Where the game's speed was when a speed change came in, which decides what the fast button does.</summary>
public enum FastPress
{
    /// <summary>Running at normal, double or slow speed.</summary>
    FromOtherSpeed,

    /// <summary>Paused, by anyone.</summary>
    FromPause,

    /// <summary>Already running at the game's high speed or one of the faster gears.</summary>
    FromFastSpeed,
}

/// <summary>
/// The faster gears above the game's high speed. Gear 0 is the game's own high speed; gear N holds the Nth multiple
/// of it. Pressing the fast button while already fast moves up a gear and wraps back to the game's high speed, so
/// with the default "2" the presses go high, super, high.
/// </summary>
public static class FastGear
{
    /// <summary>The game's own high speed, with no gear of ours on top.</summary>
    public const int GamesOwn = 0;

    /// <summary>Anything closer to 1 than this would look like the button did nothing.</summary>
    public const float LeastMultiple = 1.1f;

    public const float MostMultiple = 10f;

    /// <summary>
    /// The most game seconds a real second may hold. The game steps its world once per frame with no catch-up, and
    /// its own custom speed lets one frame step up to 2 game seconds with 1.5 times headroom, so 40x is the most that
    /// keeps each frame inside that step at 30 frames a second. Faster than that, every frame takes the full 2 second
    /// step and the extra speed is lost anyway.
    /// </summary>
    public const float FastestScale = 40f;

    /// <summary>Unreadable entries are dropped and the rest clamped, so a typo costs one gear rather than the mod.</summary>
    public static IReadOnlyList<float> ParseMultiples(string text)
    {
        var multiples = new List<float>();

        foreach (var part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiple) || !float.IsFinite(multiple)) continue;

            var clamped = Math.Clamp(multiple, LeastMultiple, MostMultiple);

            if (!multiples.Contains(clamped)) multiples.Add(clamped);
        }

        return multiples;
    }

    /// <summary>A paused game reads as normal speed to the game, so the pause is checked first.</summary>
    public static FastPress PressFrom(bool isPaused, int level)
    {
        if (isPaused) return FastPress.FromPause;

        return level == PauseToggle.Triple ? FastPress.FromFastSpeed : FastPress.FromOtherSpeed;
    }

    /// <summary>
    /// The gear after a speed change to <paramref name="level"/>. A pause keeps the gear so resuming comes back to
    /// it; any other speed drops it. The fast button cycles when already fast, resumes the kept gear from a pause,
    /// and starts at the game's own high speed from any other speed.
    /// </summary>
    public static int GearAfter(int gear, int count, int level, FastPress from)
    {
        var kept = Valid(gear, count);

        if (level == PauseToggle.Paused) return kept;

        if (level != PauseToggle.Triple) return GamesOwn;

        return from switch
        {
            FastPress.FromFastSpeed => (kept + 1) % (count + 1),
            FastPress.FromPause => kept,
            FastPress.FromOtherSpeed => GamesOwn,
            _ => throw new ArgumentOutOfRangeException(nameof(from), from, "a fast press GearAfter does not handle"),
        };
    }

    /// <summary>A gear that no longer exists, after the list shrank, is the game's own high speed.</summary>
    public static int Valid(int gear, int count) => gear >= GamesOwn && gear <= Math.Max(count, 0) ? gear : GamesOwn;

    /// <summary>The gear with the largest multiple, or the game's own high speed when there are no gears.</summary>
    public static int FastestGear(IReadOnlyList<float> multiples)
    {
        var fastest = GamesOwn;

        for (var i = 0; i < multiples.Count; i++)
        {
            if (fastest == GamesOwn || multiples[i] > multiples[fastest - 1]) fastest = i + 1;
        }

        return fastest;
    }

    /// <summary>
    /// The scale a gear holds: its multiple of the game's high speed, capped at <see cref="FastestScale"/> but never
    /// below the game's high speed itself. Null when the game's high speed is not known yet, so nothing is held.
    /// </summary>
    public static float? HeldScale(float gameFastSpeed, float multiple)
    {
        if (!float.IsFinite(gameFastSpeed) || gameFastSpeed < 1f || !float.IsFinite(multiple)) return null;

        return Math.Min(gameFastSpeed * multiple, Math.Max(gameFastSpeed, FastestScale));
    }

    /// <summary>The game's hold-to-speed-up key at its own speed: the game's high speed, with nothing on top.</summary>
    public const float GamesOwnHold = 1f;

    /// <summary>An unreadable setting keeps the game's own hold speed; the rest is kept between it and the largest multiple.</summary>
    public static float ClampHoldMultiple(float multiple) =>
        float.IsFinite(multiple) ? Math.Clamp(multiple, GamesOwnHold, MostMultiple) : GamesOwnHold;

    /// <summary>
    /// The scale the game's hold-to-speed-up key runs at: its multiple of the game's high speed, capped like a fast
    /// gear. Null leaves the game's own speed-up alone, at a multiple of 1 or before the game's high speed is known.
    /// </summary>
    public static float? HoldKeyScale(float gameFastSpeed, float multiple)
    {
        var clamped = ClampHoldMultiple(multiple);

        return clamped <= GamesOwnHold ? null : HeldScale(gameFastSpeed, clamped);
    }

    public static string Label(float multiple) => SlowGear.Label(multiple);
}
