using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// The pause while away from the game, in another window or one of its menus, the slow-down when the order queue runs
/// out, and the readings behind the alerts after the queue running out.
/// </summary>
public sealed partial class ClockTicker
{
    internal static AwaySettings Away = new(false, false, false, false, false, false);
    internal static QueueSlowSettings QueueSlowing = new(false, QueueSlowTarget.Normal);
    internal static int LowNeedPercent = AlertMemory.DefaultLowNeedPercent;

    private AwayPause _away = AwayPause.Start;
    private QueueSlow _queueSlow = QueueSlow.Start;
    private AlertMemory _readings = AlertMemory.Start;
    private DangerWatch _danger = DangerWatch.Start;

    /// <summary>The work under way at the last check while a menu was open; null outside a menu or after a failed read.</summary>
    private MenuWork? _menuWork;

    /// <summary>The count of machine jobs started, as of the last check.</summary>
    private int _machineJobsSeen;

    /// <summary>The game's types for the menus watched, each looked up the first time it is asked about.</summary>
    private static Il2CppSystem.Type? _backpackUiType;
    private static Il2CppSystem.Type? _phoneUiType;
    private static Il2CppSystem.Type? _tradeUiType;
    private static Il2CppSystem.Type[]? _otherMenuTypes;

    /// <summary>A new world, or none, starts every watch here afresh; a pause held on the old world went with it.</summary>
    [HideFromIl2Cpp]
    private void ForgetExtraWatches()
    {
        _away = AwayPause.Start;
        _queueSlow = QueueSlow.Start;
        _readings = AlertMemory.Start;
        _danger = DangerWatch.Start;
        _menuWork = null;
        _machineJobsSeen = MachineJobs.Started;
    }

    /// <summary>
    /// The alerts after the queue running out that started since the last read, of those in <paramref name="on"/>. With
    /// none on, nothing is read off the game and the memory is where a first read would start from.
    /// </summary>
    [HideFromIl2Cpp]
    private AlertSet TakeMoreAlerts(GameTimeManager time, AlertSet on)
    {
        if (on.IsEmpty)
        {
            MoreAlertSignals.Forget();
            _readings = AlertMemory.Start;

            return AlertSet.None;
        }

        var step = AlertMemory.Next(_readings, MoreAlertSignals.Read(time, on), LowNeedPercent);

        _readings = step.Memory;

        return step.Raised;
    }

    /// <summary>
    /// Runs after the alert pause, so an alert starting as the player leaves is the pause that holds the game. It pauses
    /// where the alerts do, and also during sleep, so a sleep fast-forward is not run through while the player is away;
    /// the game's own sleep speed-up is left alone, as pausing it would have the game undo the pause. Like idle freeze's,
    /// the pause is kept before it is made and the resume after.
    /// </summary>
    [HideFromIl2Cpp]
    private void UpdateAwayPause(GameTimeManager time, GameTimer timer, bool isLeavingExpedition, bool wasChangedByHand, float elapsed)
    {
        var settings = Away;

        if (!settings.IsAnyOn && _away is AwayPause.Watching)
        {
            _machineJobsSeen = MachineJobs.Started;

            return;
        }

        var isPaused = time.IsPausedScale();
        var away = settings.Reason(!Application.isFocused, settings.IsAnyMenuOn && IsWatchedMenuOpen(settings));
        var canPause = AlertPause.CanPause(timer, IsFastForwarding(time), isLeavingExpedition);
        var isBusy = away == AwayReason.Menu && IsWorking(time);
        var hasNewOrder = TakeNewMenuWork(away == AwayReason.Menu);
        var hasNewJob = TakeNewMachineJob(away == AwayReason.Menu);
        var seen = new AwaySnapshot(away, canPause, isPaused, SpeedPatches.CurrentSpeed(time, isPaused), wasChangedByHand, isBusy, hasNewOrder, elapsed, hasNewJob);
        var before = _away;
        var step = AwayPause.Next(before, seen);

        if (step.IsPausing)
        {
            _away = step.State;
            SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
        }

        if (step.ResumeAt is { } resumeAt && time.IsPausedScale()) SpeedPatches.ChangeSpeed(time, resumeAt);

        _away = step.State;
        LogAway(before, step);
    }

    /// <summary>The character has an order queued, or a workbench is crafting, which runs on its own timer and is no order.</summary>
    private static bool IsWorking(GameTimeManager time) => time.IsLeadingRoleBusy() || Workbench.IsAnyCrafting();

    /// <summary>
    /// Whether the player gave an order or started a craft since the last check in the same open menu: an order or a
    /// crafting bench not seen before, so a craft queued behind an order already running counts too.
    /// </summary>
    [HideFromIl2Cpp]
    private bool TakeNewMenuWork(bool isInMenu)
    {
        var before = _menuWork;

        _menuWork = isInMenu ? ReadMenuWork() : null;

        return before is { } was && _menuWork is { } now && MenuWork.HasNew(was, now);
    }

    /// <summary>Whether a machine started a job since the last check while a menu is open, such as a stove starting to cook.</summary>
    [HideFromIl2Cpp]
    private bool TakeNewMachineJob(bool isInMenu)
    {
        var started = MachineJobs.Started;
        var isNew = isInMenu && started != _machineJobsSeen;

        _machineJobsSeen = started;

        return isNew;
    }

    private static MenuWork? ReadMenuWork() =>
        ReadPlayerOrders() is { } orders && Workbench.CraftingBenches() is { } benches ? new MenuWork(orders, benches) : null;

    /// <summary>The ids of the orders the player gave the character that are queued or running; null when unreadable.</summary>
    private static HashSet<long>? ReadPlayerOrders()
    {
        try
        {
            var world = BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;
            var role = world?._AgentManager?.GetLeadingRole();
            var orders = role == null ? null : world!._ActionManager?.GetActionSource(role.InstanceId)?.ParentList;

            if (orders == null) return null;

            var ids = new HashSet<long>();

            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];

                if (order != null && order.SourceType == ActionSourceType.ManualCreate) ids.Add(order.ListId);
            }

            return ids;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Whether a menu whose setting is on is open, by the game's own record of the menus it shows.</summary>
    private static bool IsWatchedMenuOpen(AwaySettings settings)
    {
        var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

        if (ui == null) return false;

        return (settings.IsBagOn && ui.IsUIActive(_backpackUiType ??= Il2CppType.Of<WebUI_BackpackUI>()))
            || (settings.IsWorkbenchOn && Workbench.IsOpen())
            || (settings.IsPhoneOn && ui.IsUIActive(_phoneUiType ??= Il2CppType.Of<WebUI_PhoneSMS>()))
            || (settings.IsTradeOn && ui.IsUIActive(_tradeUiType ??= Il2CppType.Of<WebUI_TradeUI>()))
            || (settings.IsOtherMenusOn && GameFreeze.IsAnyActive(ui, _otherMenuTypes ??= GameFreeze.OtherMenus()));
    }

    [HideFromIl2Cpp]
    private void LogAway(AwayPause from, AwayStep step)
    {
        var line = (from, step.State) switch
        {
            (AwayPause.RunningOrder, AwayPause.Holding { Reason: AwayReason.Menu }) when step.IsPausing => "away: the work started in the menu is done, paused the game again",
            (_, AwayPause.Holding holding) when step.IsPausing => $"away: {Describe(holding.Reason)}, paused the game; coming back resumes {Describe(holding.ResumeAt)}",
            (AwayPause.Holding holding, AwayPause.RunningOrder) when step.ResumeAt != null => $"away: order or craft started in a menu, resumed {Describe(holding.ResumeAt)} until it is done",
            (AwayPause.Holding holding, _) when step.ResumeAt != null => $"away: back, resumed {Describe(holding.ResumeAt)}",
            (AwayPause.Holding, AwayPause.Released or AwayPause.Watching) => "away: speed changed while away, it is kept",
            _ => null,
        };

        if (line != null) LogFileOnly(line);
    }

    private static string Describe(AwayReason reason) => reason switch
    {
        AwayReason.Background => "the game window went to the background",
        AwayReason.Menu => "a menu opened",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "an away reason with no description"),
    };

    /// <summary>
    /// Runs after idle freeze, so a resume it makes on the next order comes first and the old speed is put back on top
    /// of it. It slows the game where the alerts pause it, and never during sleep.
    /// </summary>
    [HideFromIl2Cpp]
    private void UpdateQueueSlow(GameTimeManager time, GameTimer timer, bool isAsleep, bool isLeavingExpedition, float elapsed, bool wasChangedByHand)
    {
        var settings = QueueSlowing;

        if (!settings.IsOn)
        {
            _queueSlow = QueueSlow.Start;

            return;
        }

        var isPaused = time.IsPausedScale();
        var canSlow = AlertPause.CanPause(timer, IsFastForwarding(time) || isAsleep, isLeavingExpedition, SpeedPatches.IsHoldingGameKey(time));
        var seen = new QueueSlowSnapshot(canSlow, isPaused, time.IsLeadingRoleBusy(), SpeedPatches.CurrentSpeed(time, isPaused), elapsed, wasChangedByHand);
        var step = QueueSlow.Next(_queueSlow, seen, settings.TargetSpeed);

        _queueSlow = step.State;

        if (step.SlowTo is { } slowTo)
        {
            SpeedPatches.ChangeSpeed(time, slowTo);
            LogFileOnly($"queue: ran out, slowed from {Describe(seen.Running)} to {Describe(slowTo)}; the next order puts it back");
        }

        if (step.RestoreTo is not { } restoreTo) return;

        if (time.IsPausedScale()) SpeedPatches.SetResumeSpeed(restoreTo);
        else SpeedPatches.ChangeSpeed(time, restoreTo);

        LogFileOnly($"queue: order given, put back {Describe(restoreTo)}");
    }

    private static string DescribeMore(Alert alert) => alert switch
    {
        Alert.HouseHit => "zombies damaged the house in this wave",
        Alert.TrapHit => "zombies damaged a trap in this wave",
        Alert.PowerOut => "the power went out",
        Alert.LowSatiety => "satiety fell low",
        Alert.LowMorale => "morale fell low",
        Alert.LowStamina => "stamina fell low",
        Alert.LowHealth => "fitness fell low",
        Alert.LowVitality => "life fell low",
        Alert.Sick => "the character fell ill",
        Alert.PhoneMessage => "a phone message came in",
        Alert.TradeMessage => "a trade message came in",
        Alert.PlantRipe => "a crop ripened",
        Alert.Dusk => "evening came",
        Alert.Dawn => "morning came",
        _ => throw new ArgumentOutOfRangeException(nameof(alert), alert, "an alert with no description"),
    };

    /// <summary>A failed check must never leave the game paused by this mod, so a pause held while away is undone.</summary>
    [HideFromIl2Cpp]
    private void RecoverExtraWatches()
    {
        if (_away is AwayPause.Holding holding) TryResume(time => SpeedPatches.ChangeSpeed(time, holding.ResumeAt));

        ForgetExtraWatches();
    }
}
