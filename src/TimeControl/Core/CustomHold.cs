using System;

namespace TimeControl.Core;

/// <summary>
/// A custom speed from the keys 5 to 9 or the mouse wheel, kept in place. The game knows only its own speeds and puts
/// one of them back after saves, menus and resumes; the custom speed sits on the game level it reads as, so a pause
/// and a resume to that same level bring it back, and any other speed change lets it go.
/// </summary>
public abstract record CustomHold
{
    public static readonly CustomHold Start = new None();

    private CustomHold()
    {
    }

    public sealed record None : CustomHold;

    /// <summary>Running at <paramref name="Scale"/>, on the game level <paramref name="Level"/>.</summary>
    public sealed record Held(float Scale, int Level) : CustomHold;

    /// <summary>Paused; resuming to <paramref name="Level"/> brings <paramref name="Scale"/> back.</summary>
    public sealed record Suspended(float Scale, int Level) : CustomHold;

    /// <summary>The scale to keep the game at now, or null when the custom speed is not running.</summary>
    public static float? HeldScale(CustomHold state) => state is Held held ? held.Scale : null;

    /// <param name="isPaused">The game is paused, by anyone.</param>
    /// <param name="level">The game's speed level now; only read while running.</param>
    /// <param name="wasChangedByOthers">Anything but the speed keys changed the game's speed since the last check.</param>
    public static CustomHold Next(CustomHold state, bool isPaused, int level, bool wasChangedByOthers) => state switch
    {
        None => state,
        Held held when isPaused => new Suspended(held.Scale, held.Level),
        Held => wasChangedByOthers ? Start : state,
        Suspended when isPaused => state,
        Suspended suspended => level == suspended.Level ? new Held(suspended.Scale, suspended.Level) : Start,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a custom hold state Next does not handle"),
    };
}
