using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameScale = GameCore.Scripts.TimeManager;

namespace TimeControl.Adapters;

/// <summary>The game's time manager, and the three writes that make a game speed stick everywhere it is read.</summary>
internal static class GameClock
{
    /// <summary>
    /// The live world's time manager, or null before the game has made one. Reading the singleton's Instance would
    /// build a world when none exists yet, before the game's configs load, and a world built that way registers the
    /// game's event handlers as well; so the world is only read once the game has created it.
    /// </summary>
    public static GameTimeManager? Find() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance?._GameTimeManager : null;

    public static bool IsLive(GameTimeManager time) => Find() is { } live && live.Pointer == time.Pointer;

    public static bool IsDayClock(GameTimeManager time) => time.Timer?.TryCast<CountUpTimer>() != null;

    /// <summary>
    /// The same writes the game's own speed change makes: the scale it reads back, the scale Unity runs at, and the
    /// message that tells the page's timers and progress bars the new pace.
    /// </summary>
    public static void HoldSpeed(GameTimeManager time, float speed)
    {
        time.BattleTimeScale = speed;
        GameScale.SetBattleTimeScale(speed);
        time.NotifyCountDownTimerScaleSync();
    }
}
