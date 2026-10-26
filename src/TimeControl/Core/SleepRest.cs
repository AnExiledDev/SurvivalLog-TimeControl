namespace TimeControl.Core;

/// <summary>
/// Sleep until ends a sleep the way the game ends a finished one, never as a cancel, so the character gets what a
/// finished sleep gives. The game grants Vim and Vigor at every finished sleep, but its own early wake only comes once
/// the character has slept its minimum hours, so a sleep the mod ends sooner goes without the buff, as one the player
/// cancels does.
/// </summary>
public static class SleepRest
{
    /// <summary>The fewest hours slept the game's own early wake asks for before the character may get up.</summary>
    public const float MinHours = 3f;

    /// <param name="slept">How long the character has slept, on the sleep's own timer.</param>
    /// <param name="needed">The minimum hours, on that same timer.</param>
    public static bool EarnsBuff(float slept, float needed) => slept >= needed;
}

/// <summary>The one ended sleep whose Vim and Vigor is held back, by the game's id for that sleep, used once.</summary>
public sealed class BuffHold
{
    private long? _held;

    public bool IsAny => _held != null;

    public void Hold(long sleep) => _held = sleep;

    public void Clear() => _held = null;

    /// <summary>Whether this sleep's buff is held back; true at most once per hold.</summary>
    public bool Take(long sleep)
    {
        if (_held != sleep) return false;

        _held = null;

        return true;
    }
}
