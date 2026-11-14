using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>The game's time manager, and the three writes that make a game speed stick everywhere it is read.</summary>
internal static class GameClock
{
    internal static Action<string> Log = _ => { };

    private static bool _hasLoggedEndUnreadable;

    /// <summary>
    /// The live world's time manager, or null before the game has made one. Reading the singleton's Instance would
    /// build a world when none exists yet, before the game's configs load, and a world built that way registers the
    /// game's event handlers as well; so the world is only read once the game has created it.
    /// </summary>
    public static GameTimeManager? Find() =>
        GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager;

    public static bool IsLive(GameTimeManager time) => Find() is { } live && live.Pointer == time.Pointer;

    /// <summary>
    /// Which timer runs: the game counts the day clock up and the prologue before the disaster down, and gives an
    /// expedition's world a frozen timer (GameTimeManager.Initialize picks it for the exploration mode only). Completing
    /// preparations or the countdown running out both seal it (GameTimeManager.SealCountDownForDisaster sets IsCompleted).
    /// </summary>
    public static GameTimer Timer(GameTimeManager time)
    {
        var timer = time.Timer;
        var countdown = timer?.TryCast<CountDownTimer>();

        return GameTimers.Classify(
            isCountingUp: timer?.TryCast<CountUpTimer>() != null,
            isCountingDown: countdown != null,
            isFrozen: timer?.TryCast<FrozenTimer>() != null,
            isBeforeDisaster: time.WorldStateType == WorldStateType.PreDisaster,
            isCountdownOver: countdown?.IsCompleted == true);
    }

    /// <summary>Makes the game run at <paramref name="speed"/> everywhere the game reads its speed.</summary>
    public static void HoldSpeed(GameTimeManager time, float speed) => TimeScale.Hold(time, speed);

    /// <summary>
    /// Whether the player's character is sleeping or napping: the game marks the character asleep for the whole of
    /// both and clears it when they end, however they end. Read only once the game has created the world.
    /// </summary>
    public static bool IsLeadingRoleAsleep()
    {
        if (!GameSingleton.IsCreated<BattleLogicWorld>()) return false;

        var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (role == null) return false;

        return AgentTools.GetAgentComponent<AgentStateComponent>(role)?.IsStateExit(AgentState.Sleep) == true;
    }

    /// <summary>
    /// Whether the expedition is being finished: the game marks its explore manager the moment the player confirms
    /// leaving, or the exposure meter fills (ExploreManager.OnExploreEnd), and the mark goes with the world it was
    /// made in. The trip home then waits two seconds of game time before it loads home, so it never arrives while the
    /// game is paused. Only asked under an expedition's timer, and read only once the game has created the world. A
    /// read that fails counts as not finishing, so it cannot stop the rest of a check, and is logged once.
    /// </summary>
    public static bool IsExpeditionEnding(GameTimer timer)
    {
        if (timer != GameTimer.Expedition || !GameSingleton.IsCreated<BattleLogicWorld>()) return false;

        try
        {
            return GameSingleton.Existing<BattleLogicWorld>()?._ExploreManager?._hasEndProcessed == true;
        }
        catch (Exception ex)
        {
            if (!_hasLoggedEndUnreadable) Log($"could not read whether the expedition is being finished, so pauses are not lifted for the trip home: {ex.Message}");

            _hasLoggedEndUnreadable = true;

            return false;
        }
    }
}
