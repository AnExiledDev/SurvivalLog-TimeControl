namespace TimeControl.Core;

/// <summary>
/// The game's default key for centering the camera on the character is Space, the default pause key, and the pause key
/// stands aside for any key the game uses, so Space never paused. The game's default for centering the camera moves to
/// a key of its own instead. Only the default moves: the game reads a key the player chose in its own key settings
/// first, so that choice still wins.
/// </summary>
public static class CenterCameraKey
{
    /// <summary>Unity's code for no key; centering the camera on None turns its key off.</summary>
    public const int None = 0;

    /// <returns>
    /// The key the game's default for centering the camera becomes, or null to leave it alone: there is no pause key,
    /// the default is not the pause key, or the key asked for is the pause key itself.
    /// </returns>
    public static int? Move(int gameDefault, int pauseKey, int wantedKey) =>
        pauseKey != None && gameDefault == pauseKey && wantedKey != pauseKey ? wantedKey : null;
}
