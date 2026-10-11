using System;

namespace TimeControl.Core;

/// <summary>
/// How fast the day clock runs against the rest of the game. Only the clock is scaled, so walking and actions keep
/// their speed while the day lasts longer.
/// </summary>
public static class ClockPace
{
    public const float SlowestRate = 0.05f;
    public const float FastestRate = 4f;

    public static float ClampRate(float rate) => float.IsFinite(rate) ? Math.Clamp(rate, SlowestRate, FastestRate) : 1f;

    /// <summary>The multiplier for the clock's frame delta; outside the day clock the game's own pace is kept.</summary>
    public static float Factor(float rate, bool isFrozen, bool isDayClock)
    {
        if (!isDayClock) return 1f;

        return isFrozen ? 0f : ClampRate(rate);
    }
}
