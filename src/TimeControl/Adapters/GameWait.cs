using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// What waits, alarms, sleep until and start paused read off the game, and the one thing they ask of it besides a
/// speed change: waking the character. One hook marks the game entering a save; another holds back the buff of a
/// sleep woken before its minimum hours. Every read is read only once the game
/// has created the world, and one that fails counts as nothing to read, logged at most a few times.
/// </summary>
internal static class GameWait
{
    private const int MaxLoggedFailures = 3;
    private const float BrewReadSeconds = 3f;

    /// <summary>The pieces of furniture one check reads for barrels.</summary>
    private const int BrewScanSlice = 32;

    /// <summary>Game seconds past a brew's last hour before the wait for it ends, so the game has marked it done.</summary>
    private const int BrewDoneMarginSeconds = 60;

    /// <summary>The game's event for the step it runs when a sleep finishes (BtEffectEventMsg.System_Completed).</summary>
    private const int FinishStepEvent = 3;

    /// <summary>The game's event for a behaviour tree's first timer (BtEffectEventMsg.Delay_Trigger0).</summary>
    private const int TimedRewardEvent = 10;

    private static Action<string> _log = _ => { };
    private static bool _isEnterHooked;
    private static bool _isEntered;
    private static IntPtr _worldAtEntry;
    private static long? _brewDoneAt;
    private static float _nextBrewReadAt;
    private static Il2CppSystem.Collections.Generic.List<Furniture>? _brewScan;
    private static int _brewScanAt;
    private static long? _brewScanEarliest;
    private static int _failures;
    private static bool _isBuffHookTried;
    private static bool _isBuffHooked;

    /// <summary>The sleep WakeLeadingRole ended before the minimum hours, whose Vim and Vigor is held back.</summary>
    internal static readonly BuffHold BuffHeld = new();

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
    /// When the first brew in a barrel is done, on the day clock; null when none is brewing, under another timer, or
    /// while nothing needs it. The game builds a fresh list of every piece of furniture for each read, and each piece
    /// is asked for its barrel one at a time, so a read is spread over checks, a few pieces each, and a new one starts
    /// a few seconds after the last finished. The last finished read stands meanwhile; there is none before the first.
    /// </summary>
    /// <param name="isWanted">The wait button shows or a wait for a brew runs; otherwise nothing is read.</param>
    public static long? BrewDoneAt(GameTimer timer, float now, bool isWanted)
    {
        if (timer != GameTimer.DayClock || !isWanted)
        {
            _brewDoneAt = null;
            _brewScan = null;
            _nextBrewReadAt = 0f;

            return null;
        }

        if (_brewScan == null && now >= _nextBrewReadAt) StartBrewScan();

        if (_brewScan != null && ScanBrews()) _nextBrewReadAt = now + BrewReadSeconds;

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

    /// <summary>
    /// Wakes the character the way the game ends a finished sleep, never as a cancel: the game's own
    /// ActionManager.CompleteCurrentAction, which closes the sleep with no break, so the game gives what a finished sleep
    /// gives. A sleep shorter than the minimum hours of the game's own early wake has its Vim and Vigor held back.
    /// With nothing to end the game's way, nothing is done; the mod never falls back to a cancel.
    /// </summary>
    /// <returns>What happened, for the log; null when the character could not be woken.</returns>
    public static string? WakeLeadingRole()
    {
        try
        {
            var world = LiveWorld();
            var actions = world?._ActionManager;
            var trees = world?._BTEffectManager;
            var role = world?._AgentManager?.GetLeadingRole();

            if (actions == null || trees == null || role == null) return null;

            var run = actions.GetActionSource(role.InstanceId)?.CurrentTaskBt ?? 0;
            var sleep = run > 0 ? trees.GetBtArgsById(run) : null;

            // The game is already ending this sleep; a second end could run its finish step twice.
            if (sleep == null || trees.IsBtEnding(run)) return null;

            var slept = sleep.MaxLifeDuration - sleep.CurLifeDuration;
            var needed = Toolset.GetGameTime_Float(SleepRest.MinHours * ClockHour.Hour);
            var hours = needed > 0f ? slept / needed * SleepRest.MinHours : 0f;
            var plan = SleepRest.Plan(SleepRest.EarnsBuff(slept, needed), HasFinishStep(trees, sleep), IsRewardPending(sleep));

            BuffHeld.Clear();

            if (plan == WakeReward.BuffHeldBack) BuffHeld.Hold(sleep.InstanceId);
            // Queued ahead of the end the wake queues next, so it runs first, as it would at the 6 hour mark.
            if (plan == WakeReward.TimedRewardFired) trees.AddCacheEvent(sleep, TimedRewardEvent);

            actions.CompleteCurrentAction(role.InstanceId);

            return plan switch
            {
                WakeReward.Finished => $"ended the sleep after {hours:0.0} h as a finished sleep",
                WakeReward.TimedRewardFired => $"ended the sleep after {hours:0.0} h and gave this bed's 6 h reward",
                WakeReward.NothingMore => $"ended the sleep after {hours:0.0} h, this bed gives nothing more",
                _ when _isBuffHooked => $"ended the sleep after {hours:0.0} h, under {SleepRest.MinHours:0} h so Vim and Vigor held back",
                _ => $"ended the sleep after {hours:0.0} h, under {SleepRest.MinHours:0} h, but Vim and Vigor could not be held back",
            };
        }
        catch (Exception ex)
        {
            Fail("waking the character", ex);

            return null;
        }
    }

    /// <summary>
    /// Watches the game granting a buff from a sleep or another action (AE_AddAttackerBuff.Run), so the one sleep
    /// WakeLeadingRole ended short goes without Vim and Vigor. Hooked once.
    /// </summary>
    public static void HookSleepBuff(Harmony harmony, Action<string> log)
    {
        if (_isBuffHookTried) return;

        _isBuffHookTried = true;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(AE_AddAttackerBuff), nameof(AE_AddAttackerBuff.Run)), prefix: new HarmonyMethod(typeof(GameWait), nameof(BeforeAddBuff)));
            _isBuffHooked = true;
            log("sleep buff watched");
        }
        catch (Exception ex)
        {
            log($"could not hook AE_AddAttackerBuff.Run, a sleep woken early keeps Vim and Vigor: {ex.Message}");
        }
    }

    /// <summary>The countdown's own clock: game seconds run since it started, and the time of day it started at.</summary>
    private static ClockReading? ReadCountdown(GameTimeManager time)
    {
        if (time.Timer?.TryCast<CountDownTimer>() is not { } countdown) return null;

        var elapsed = countdown.GetTime();

        return new ClockReading(elapsed, ClockHour.Wrap((long)countdown.StartTime + elapsed));
    }

    /// <summary>Takes the furniture list the read goes through; with no world there is nothing brewing.</summary>
    private static void StartBrewScan()
    {
        _brewScanAt = 0;
        _brewScanEarliest = null;

        try
        {
            _brewScan = LiveWorld()?._AgentManager?.GetAllFurnitures();
        }
        catch (Exception ex)
        {
            _brewScan = null;
            Fail("reading the brewing barrels", ex);
        }

        if (_brewScan == null) _brewDoneAt = null;
    }

    /// <summary>
    /// Reads the next few pieces of furniture. A brew is done once the hours it takes have passed since the hour it
    /// started, counted in the game's total hours.
    /// </summary>
    /// <returns>Whether the read finished, or failed, on this call.</returns>
    private static bool ScanBrews()
    {
        try
        {
            var furnitures = _brewScan!;
            var end = Math.Min(furnitures.Count, _brewScanAt + BrewScanSlice);

            for (var i = _brewScanAt; i < end; i++)
            {
                var furniture = furnitures[i];
                var brew = furniture == null || furniture.IsDead ? null : AgentTools.GetAgentComponent<BrewComponent>(furniture);

                if (brew == null || !brew.IsBrewing()) continue;

                var doneAt = ((long)brew.StartHour + brew.TotalHours) * ClockHour.Hour + BrewDoneMarginSeconds;

                if (_brewScanEarliest == null || doneAt < _brewScanEarliest) _brewScanEarliest = doneAt;
            }

            _brewScanAt = end;

            if (end < furnitures.Count) return false;

            _brewDoneAt = _brewScanEarliest;
        }
        catch (Exception ex)
        {
            _brewDoneAt = null;
            Fail("reading the brewing barrels", ex);
        }

        _brewScan = null;

        return true;
    }

    /// <summary>Marks the entry and the world live as it starts, so the world the save loads into can be told from it.</summary>
    private static void BeforeEnterBattle()
    {
        try
        {
            _isEntered = true;
            BuffHeld.Clear();
            _worldAtEntry = GameClock.Find()?.Pointer ?? IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _worldAtEntry = IntPtr.Zero;
            Fail("marking a save being entered", ex);
        }
    }

    /// <summary>Whether the sleep has the step the game runs when a sleep finishes; unknown counts as having it.</summary>
    private static bool HasFinishStep(BTEffectManager trees, EffectBtArgs sleep)
    {
        var name = sleep.ConfigName;

        if (name == null || trees.ConfigDict == null || !trees.ConfigDict.TryGetValue(name, out var config)) return true;

        return config?.SearchEventMap?.ContainsKey(FinishStepEvent) != false;
    }

    /// <summary>Whether the sleep's first timer, the one that gives the 6 hour reward, is still waiting to fire.</summary>
    private static bool IsRewardPending(EffectBtArgs sleep)
    {
        var timers = sleep.DelayArgsList;

        if (timers == null) return false;

        for (var i = 0; i < timers.Count; i++)
            if (timers[i]?.EventMsg == TimedRewardEvent) return true;

        return false;
    }

    /// <summary>Skips the buff of the one sleep held back; every other buff, and every other sleep, goes as usual.</summary>
    private static bool BeforeAddBuff(EffectBtArgs args, ref bool __result)
    {
        if (!BuffHeld.IsAny) return true;

        try
        {
            if (args == null || !BuffHeld.Take(args.InstanceId)) return true;

            __result = true;

            return false;
        }
        catch (Exception ex)
        {
            BuffHeld.Clear();
            Fail("holding back the sleep buff", ex);

            return true;
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
