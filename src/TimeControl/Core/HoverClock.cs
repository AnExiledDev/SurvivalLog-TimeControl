using System;

namespace TimeControl.Core;

/// <summary>
/// The game tests what the mouse is over on a timer that runs on game time, so while paused one hover test takes
/// about a thousand real seconds and nothing highlights. Topping the timer up to real time keeps hover working while
/// paused or slowed, and adds nothing at normal speed or faster.
/// </summary>
public static class HoverClock
{
    /// <summary>The real seconds a frame's game time fell short of the real time that passed.</summary>
    public static float TopUp(float scaledSeconds, float unscaledSeconds) => MathF.Max(0f, unscaledSeconds - scaledSeconds);
}
