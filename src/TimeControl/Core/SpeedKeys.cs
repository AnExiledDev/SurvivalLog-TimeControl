namespace TimeControl.Core;

/// <summary>
/// Where the pause key and the game's own speed keys work. The game answers its speed keys only after the disaster,
/// so before it the countdown to the disaster ran with no way to pause or speed it up.
/// </summary>
public static class SpeedKeys
{
    /// <summary>The pause key works wherever the game's speed keys do, and in the countdown to the disaster.</summary>
    public static bool IsPauseKeyLive(bool isAfterDisaster, GameTimer timer) =>
        isAfterDisaster || timer == GameTimer.PrologueCountdown;

    /// <summary>
    /// The mod answers the game's speed keys only in the countdown to the disaster; anywhere the game answers them
    /// itself, answering too would change the speed twice for one press.
    /// </summary>
    public static bool AreGameKeysOurs(bool isAfterDisaster, GameTimer timer) =>
        !isAfterDisaster && timer == GameTimer.PrologueCountdown;
}
