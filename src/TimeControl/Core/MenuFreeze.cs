namespace TimeControl.Core;

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsFrozen">The game has frozen its world, the stop apart from the speed that also makes it ignore the speed keys.</param>
/// <param name="IsPlayerMenuOpen">A menu the player opened to use something is open: a workbench, the bag, a shop, a stove and the like.</param>
/// <param name="IsHeld">A screen the game must keep time stopped for is up: a story, an ending, a settlement, an event choice or the settings.</param>
/// <param name="Elapsed">Real seconds since the last check.</param>
public readonly record struct FreezeSnapshot(bool IsFrozen, bool IsPlayerMenuOpen, bool IsHeld, float Elapsed);

/// <param name="Lifts">Undo the game's freeze now.</param>
public readonly record struct FreezeStep(MenuFreeze State, bool Lifts);

/// <summary>
/// The game freezes its world when a menu opens (and at some moments inside one, such as a craft finishing), and while
/// frozen it ignores the speed keys and buttons. The speed belongs to the player in every menu, so a freeze that starts
/// while a player menu is open, or just before one shows, is lifted at once. A freeze that started with no player menu
/// coming, or while the game holds time for a story, ending or choice, belongs to the game and is left alone until it
/// ends on its own.
/// </summary>
public abstract record MenuFreeze
{
    /// <summary>Real seconds a new freeze waits for its menu to show, as the game freezes as the menu opens, before it is drawn.</summary>
    public const float LateSeconds = 1f;

    public static readonly MenuFreeze Start = new Running();

    private MenuFreeze()
    {
    }

    /// <summary>The world is not frozen.</summary>
    public sealed record Running : MenuFreeze;

    /// <summary>The world froze <paramref name="Age"/> real seconds ago and no player menu has shown yet.</summary>
    public sealed record New(float Age) : MenuFreeze;

    /// <summary>The game froze the world for its own reasons; it stays frozen until the game ends it.</summary>
    public sealed record Games : MenuFreeze;

    public static FreezeStep Next(MenuFreeze state, FreezeSnapshot seen)
    {
        if (!seen.IsFrozen) return new FreezeStep(Start, false);

        if (state is Games) return new FreezeStep(state, false);

        if (seen.IsPlayerMenuOpen && !seen.IsHeld) return new FreezeStep(Start, true);

        var age = state is New fresh ? fresh.Age + seen.Elapsed : 0f;

        return new FreezeStep(age > LateSeconds ? new Games() : new New(age), false);
    }
}
