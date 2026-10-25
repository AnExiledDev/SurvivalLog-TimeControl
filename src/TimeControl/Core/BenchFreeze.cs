namespace TimeControl.Core;

/// <summary>
/// The game freezes its world when the workbench window opens and when a craft finishes, a freeze apart from the
/// speed that also makes it ignore its speed keys, and lifts it only when a craft starts or the window closes. With
/// speed control at the workbench on, a freeze the workbench made while its window is open is lifted at once, so the
/// speed is the player's there as everywhere else. A freeze that was already on is left alone, as someone else made it.
/// </summary>
public static class BenchFreeze
{
    /// <summary>
    /// Real seconds after the window opens in which a freeze is still taken as the workbench's, in case the game
    /// makes it after the opening rather than during it.
    /// </summary>
    public const float LateSeconds = 1f;

    /// <param name="isOn">Speed control at the workbench is on.</param>
    /// <param name="isBenchOpen">The workbench window is open, or opening.</param>
    /// <param name="wasFrozen">The world was frozen before the workbench acted.</param>
    /// <param name="isFrozen">The world is frozen now.</param>
    public static bool Lifts(bool isOn, bool isBenchOpen, bool wasFrozen, bool isFrozen) =>
        isOn && isBenchOpen && isFrozen && !wasFrozen;
}
