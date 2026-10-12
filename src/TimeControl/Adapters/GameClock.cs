using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;
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

    /// <summary>
    /// Which timer runs: the game counts the day clock up and the prologue before the disaster down, and gives an
    /// expedition's world a frozen timer (GameTimeManager.Initialize picks it for the exploration mode only).
    /// </summary>
    public static GameTimer Timer(GameTimeManager time)
    {
        var timer = time.Timer;

        return GameTimers.Classify(
            isCountingUp: timer?.TryCast<CountUpTimer>() != null,
            isCountingDown: timer?.TryCast<CountDownTimer>() != null,
            isFrozen: timer?.TryCast<FrozenTimer>() != null,
            isBeforeDisaster: time.WorldStateType == WorldStateType.PreDisaster);
    }

    /// <summary>
    /// The same writes the game's own speed change makes: the scale it reads back, the scale Unity runs at, and the
    /// message that tells the page's timers and progress bars the new pace. The longest frame step is set the way the
    /// game's own custom speed sets it, so a fast speed is not cut short by the step the game allows at its high
    /// speed; for the game's own speeds and slower that is the step it already uses.
    /// </summary>
    public static void HoldSpeed(GameTimeManager time, float speed)
    {
        time.BattleTimeScale = speed;
        GameScale.SetBattleTimeScale(speed);
        time.ApplyGmMaxDeltaTime(speed);
        time.NotifyCountDownTimerScaleSync();
    }

    /// <summary>
    /// Whether the player's character is sleeping or napping: the game marks the character asleep for the whole of
    /// both and clears it when they end, however they end. Read only once the game has created the world.
    /// </summary>
    public static bool IsLeadingRoleAsleep()
    {
        if (!BaseSingleton<BattleLogicWorld>.IsInstanceCreated) return false;

        var role = BaseSingleton<BattleLogicWorld>.Instance?._AgentManager?.GetLeadingRole();

        if (role == null) return false;

        return AgentTools.GetAgentComponent<AgentStateComponent>(role)?.IsStateExit(AgentState.Sleep) == true;
    }
}
