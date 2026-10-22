using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>
/// The custom speeds on the keys 5 to 9, and the ladder of every speed the mouse wheel steps through: the mod's slow
/// speeds, the game's three levels, the fast gears above its high speed and the custom speeds, slowest first.
/// </summary>
public static class SpeedLadder
{
    /// <summary>The keys 5, 6, 7, 8 and 9.</summary>
    public const int MaxCustomSpeeds = 5;

    public const float SlowestCustom = SlowGear.Slowest;

    public const float FastestCustom = FastGear.FastestScale;

    /// <summary>Two speeds this close are one stop on the ladder.</summary>
    public const float SameSpeed = 0.001f;

    /// <summary>
    /// Each place in the list is a key, so an empty or unreadable entry leaves that key off rather than moving the
    /// speeds after it to other keys. Entries past the fifth are ignored.
    /// </summary>
    public static IReadOnlyList<float?> ParseCustom(string text)
    {
        var speeds = new List<float?>();

        if (string.IsNullOrWhiteSpace(text)) return speeds;

        foreach (var part in text.Split(','))
        {
            if (speeds.Count == MaxCustomSpeeds) break;

            speeds.Add(float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) && float.IsFinite(speed)
                ? Math.Clamp(speed, SlowestCustom, FastestCustom)
                : null);
        }

        return speeds;
    }

    /// <summary>The game level a scale reads as, the way the game's own speed level reading puts it.</summary>
    /// <param name="doubleScale">The game's speed on its double speed level.</param>
    /// <param name="fastScale">The game's high speed.</param>
    public static int LevelFor(float scale, float doubleScale, float fastScale)
    {
        if (scale < doubleScale) return PauseToggle.Normal;

        return scale < fastScale ? PauseToggle.Double : PauseToggle.Triple;
    }

    /// <summary>
    /// Every speed, slowest first. Where two land on the same scale only one is kept: a game level before a slow
    /// speed, a slow speed before a fast gear, a fast gear before a custom speed.
    /// </summary>
    public static IReadOnlyList<LadderStep> Build(IReadOnlyList<float> slowSpeeds, float doubleScale, float fastScale, IReadOnlyList<float> fastMultiples, IReadOnlyList<float?> customs)
    {
        var steps = new List<LadderStep>();

        AddIfNew(steps, 1f, Level(PauseToggle.Normal, FastGear.GamesOwn));
        AddIfNew(steps, doubleScale, Level(PauseToggle.Double, FastGear.GamesOwn));
        AddIfNew(steps, fastScale, Level(PauseToggle.Triple, FastGear.GamesOwn));

        for (var i = 0; i < slowSpeeds.Count; i++) AddIfNew(steps, slowSpeeds[i], new SpeedTarget.SlowSpeed(i));

        for (var i = 0; i < fastMultiples.Count; i++)
        {
            if (FastGear.HeldScale(fastScale, fastMultiples[i]) is { } scale) AddIfNew(steps, scale, Level(PauseToggle.Triple, i + 1));
        }

        foreach (var custom in customs)
        {
            if (custom is { } scale) AddIfNew(steps, scale, new SpeedTarget.Custom(scale));
        }

        steps.Sort((a, b) => a.Scale.CompareTo(b.Scale));

        return steps;
    }

    /// <summary>
    /// The stop <paramref name="steps"/> away from the current speed, up for positive steps and down for negative, held
    /// at the ends rather than wrapping. A speed between two stops counts the nearer one in the stepping direction as
    /// the first step. Null when nothing would change.
    /// </summary>
    public static LadderStep? Walk(IReadOnlyList<LadderStep> ladder, float currentScale, int steps)
    {
        if (steps == 0 || ladder.Count == 0) return null;

        var last = ladder.Count - 1;
        var at = IndexOf(ladder, currentScale);

        if (at >= 0)
        {
            var target = Math.Clamp(at + steps, 0, last);

            return target == at ? null : ladder[target];
        }

        if (steps > 0)
        {
            var above = FirstAbove(ladder, currentScale);

            return above < 0 ? null : ladder[Math.Min(above + steps - 1, last)];
        }

        var below = LastBelow(ladder, currentScale);

        return below < 0 ? null : ladder[Math.Max(below + steps + 1, 0)];
    }

    private static SpeedTarget Level(int level, int gear) => new SpeedTarget.Level(new GameSpeed(level, gear));

    private static void AddIfNew(List<LadderStep> steps, float scale, SpeedTarget target)
    {
        if (!float.IsFinite(scale) || scale <= 0f || IndexOf(steps, scale) >= 0) return;

        steps.Add(new LadderStep(scale, target));
    }

    private static int IndexOf(IReadOnlyList<LadderStep> ladder, float scale)
    {
        for (var i = 0; i < ladder.Count; i++)
        {
            if (Math.Abs(ladder[i].Scale - scale) <= SameSpeed) return i;
        }

        return -1;
    }

    private static int FirstAbove(IReadOnlyList<LadderStep> ladder, float scale)
    {
        for (var i = 0; i < ladder.Count; i++)
        {
            if (ladder[i].Scale > scale) return i;
        }

        return -1;
    }

    private static int LastBelow(IReadOnlyList<LadderStep> ladder, float scale)
    {
        for (var i = ladder.Count - 1; i >= 0; i--)
        {
            if (ladder[i].Scale < scale) return i;
        }

        return -1;
    }
}
