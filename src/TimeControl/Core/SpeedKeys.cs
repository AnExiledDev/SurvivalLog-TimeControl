namespace TimeControl.Core;

/// <summary>
/// Where the pause key and the game's own speed keys work. The game answers its speed keys only at home after the
/// disaster, so the countdown to the disaster and every expedition ran with no way to pause or speed them up.
/// </summary>
public static class SpeedKeys
{
    /// <summary>
    /// The game reads its speed keys only in its normal mode; an expedition runs in the game's exploration mode, where
    /// the mod still takes its keys.
    /// </summary>
    public static bool IsModeForKeys(bool isNormalMode, GameTimer timer) => isNormalMode || timer == GameTimer.Expedition;

    /// <summary>The pause key works wherever the game's speed keys do, in the countdown to the disaster and on expeditions.</summary>
    public static bool IsPauseKeyLive(bool isAfterDisaster, GameTimer timer) =>
        isAfterDisaster || timer is GameTimer.PrologueCountdown or GameTimer.Expedition;

    /// <summary>
    /// The mod answers the game's speed keys only in the countdown to the disaster and on expeditions; anywhere the
    /// game answers them itself, answering too would change the speed twice for one press.
    /// </summary>
    public static bool AreGameKeysOurs(bool isAfterDisaster, GameTimer timer) =>
        timer == GameTimer.Expedition || (!isAfterDisaster && timer == GameTimer.PrologueCountdown);
}
