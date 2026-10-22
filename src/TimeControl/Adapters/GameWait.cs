using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// What waits, alarms, sleep until and start paused read off the game, and the one thing they ask of it besides a
/// speed change: waking the character. One hook marks the game entering a save. Every read is read only once the game
/// has created the world, and one that fails counts as nothing to read, logged at most a few times.
/// </summary>
internal static class GameWait
{
    private const int MaxLoggedFailures = 3;
    private const float BrewReadSeconds = 3f;

    /// <summary>Game seconds past a brew's last hour before the wait for it ends, so the game has marked it done.</summary>
    private const int BrewDoneMarginSeconds = 60;

    private static Action<string> _log = _ => { };
    private static bool _isEnterHooked;
    private static bool _isEntered;
    private static IntPtr _worldAtEntry;
    private static long? _brewDoneAt;
    private static float _nextBrewReadAt;
    private static int _failures;

    /// <summary>
    /// Watches the game entering a save: loading one, starting a new game, and the game's own moves to the next
    /// chapter, which all go through GameEntrance.EnterBattle. Hooked once; whether it pauses is the setting's call.
    /// </summary>
    public static void HookEnter(Harmony harmony, Action<string> log)
    {
        _log = log;

        if (_isEnterHooked) return;

        _isEnterHooked = true;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(GameEntrance), nameof(GameEntrance.EnterBattle)), prefix: new HarmonyMethod(typeof(GameWait), nameof(BeforeEnterBattle)));
            log("entering a save watched");
        }
        catch (Exception ex)
        {
            log($"could not hook GameEntrance.EnterBattle, StartPaused will do nothing: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether the game entered a save since the world this ticker last saw: the world now running is not the one
    /// that was live when the save was entered. Taken once.
    /// </summary>
    public static bool TakeEntered(IntPtr world)
    {
        if (!_isEntered || world == _worldAtEntry) return false;

        _isEntered = false;

        return true;
    }

    /// <summary>
    /// The running clock: on the day clock the game's total seconds and its time of day; on the countdown to the
    /// disaster the game seconds it has run and the time of day it shows. Null under any other timer.
    /// </summary>
    public static ClockReading? Read(GameTimeManager time, GameTimer timer)
    {
        try
        {
            return timer switch
            {
                GameTimer.DayClock => new ClockReading(time.GetTotalSeconds(), ClockHour.Wrap(time.GetSecond())),
                GameTimer.PrologueCountdown => ReadCountdown(time),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            Fail("reading the clock", ex);

            return null;
        }
    }

    /// <summary>
    /// When the first brew in a barrel is done, on the day clock; null when none is brewing or under another timer.
    /// The barrels are read every few seconds, since the game builds a fresh list of every piece of furniture on each read.
    /// </summary>
    public static long? BrewDoneAt(GameTimer timer, float now)
    {
        if (timer != GameTimer.DayClock)
        {
            _brewDoneAt = null;
            _nextBrewReadAt = 0f;

            return null;
        }

        if (now < _nextBrewReadAt) return _brewDoneAt;

        _nextBrewReadAt = now + BrewReadSeconds;
        _brewDoneAt = ReadBrewDoneAt();

        return _brewDoneAt;
    }

    /// <summary>
    /// Whether the character is in a real sleep, the kind the game speeds up: the action they are on is one the game
    /// marks fast. A nap is not, and is never cut short.
    /// </summary>
    public static bool IsWakeableSleep()
    {
        try
        {
            var world = LiveWorld();
            var role = world?._AgentManager?.GetLeadingRole();

            if (world == null || role == null || !BaseSingleton<ConfigManager>.IsInstanceCreated) return false;

            var actionId = world._ActionManager?.GetActionSource(role.InstanceId)?.CurrentAction?.ActionId;

            return actionId is { } id && BaseSingleton<ConfigManager>.Instance?.Get_Config_Action(id)?.IsFast == true;
        }
        catch (Exception ex)
        {
            Fail("reading whether the sleep can be cut short", ex);

            return false;
        }
    }

    /// <summary>Wakes the character the way the player cancelling the sleep does.</summary>
    /// <returns>Whether the wake was asked for.</returns>
    public static bool WakeLeadingRole()
    {
        try
        {
            var world = LiveWorld();
            var actions = world?._ActionManager;
            var role = world?._AgentManager?.GetLeadingRole();

            if (actions == null || role == null) return false;

            actions.BreakCurrentParent(role.InstanceId, true);

            return true;
        }
        catch (Exception ex)
        {
            Fail("waking the character", ex);

            return false;
        }
    }

    /// <summary>The countdown's own clock: game seconds run since it started, and the time of day it started at.</summary>
    private static ClockReading? ReadCountdown(GameTimeManager time)
    {
        if (time.Timer?.TryCast<CountDownTimer>() is not { } countdown) return null;

        var elapsed = countdown.GetTime();

        return new ClockReading(elapsed, ClockHour.Wrap((long)countdown.StartTime + elapsed));
    }

    /// <summary>A brew is done once the hours it takes have passed since the hour it started, counted in the game's total hours.</summary>
    private static long? ReadBrewDoneAt()
    {
        try
        {
            var furnitures = LiveWorld()?._AgentManager?.GetAllFurnitures();
            long? earliest = null;

            for (var i = 0; furnitures != null && i < furnitures.Count; i++)
            {
                var furniture = furnitures[i];
                var brew = furniture == null || furniture.IsDead ? null : AgentTools.GetAgentComponent<BrewComponent>(furniture);

                if (brew == null || !brew.IsBrewing()) continue;

                var doneAt = ((long)brew.StartHour + brew.TotalHours) * ClockHour.Hour + BrewDoneMarginSeconds;

                if (earliest == null || doneAt < earliest) earliest = doneAt;
            }

            return earliest;
        }
        catch (Exception ex)
        {
            Fail("reading the brewing barrels", ex);

            return null;
        }
    }

    /// <summary>Marks the entry and the world live as it starts, so the world the save loads into can be told from it.</summary>
    private static void BeforeEnterBattle()
    {
        try
        {
            _isEntered = true;
            _worldAtEntry = GameClock.Find()?.Pointer ?? IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _worldAtEntry = IntPtr.Zero;
            Fail("marking a save being entered", ex);
        }
    }

    /// <summary>The live world, or null before the game has made one; reading the singleton's Instance would build one.</summary>
    private static BattleLogicWorld? LiveWorld() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

    private static void Fail(string doing, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"failed {doing} (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
