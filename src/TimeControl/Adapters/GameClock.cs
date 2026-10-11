using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameScale = GameCore.Scripts.TimeManager;

namespace TimeControl.Adapters;

/// <summary>The game's time manager, and the three writes that make a game speed stick everywhere it is read.</summary>
internal static class GameClock
{
    public static GameTimeManager? Find() => BaseSingleton<BattleLogicWorld>.Instance?._GameTimeManager;

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
