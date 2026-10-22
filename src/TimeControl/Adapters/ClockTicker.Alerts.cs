using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// The pause while away from the game, in another window or one of its menus, the slow-down when the order queue runs
/// out, and the readings behind the alerts after the queue running out.
/// </summary>
public sealed partial class ClockTicker
{
    internal static AwaySettings Away = new(false, false, false, false, false);
    internal static QueueSlowSettings QueueSlowing = new(false, QueueSlowTarget.Normal);
    internal static int LowNeedPercent = AlertMemory.DefaultLowNeedPercent;

    private AwayPause _away = AwayPause.Start;
    private QueueSlow _queueSlow = QueueSlow.Start;
    private AlertMemory _readings = AlertMemory.Start;

    /// <summary>A new world, or none, starts every watch here afresh; a pause held on the old world went with it.</summary>
    private void ForgetExtraWatches()
    {
        _away = AwayPause.Start;
        _queueSlow = QueueSlow.Start;
        _readings = AlertMemory.Start;
    }

    /// <summary>The alerts after the queue running out that started since the last check, of those in <paramref name="on"/>.</summary>
    private AlertSet TakeMoreAlerts(GameTimeManager time, AlertSet on)
    {
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
    private void UpdateAwayPause(GameTimeManager time, GameTimer timer, bool isLeavingExpedition, bool wasChangedByHand)
    {
        var settings = Away;

        if (!settings.IsAnyOn && _away is AwayPause.Watching) return;

        var isPaused = time.IsPausedScale();
        var away = settings.Reason(!Application.isFocused, settings.IsAnyMenuOn && IsWatchedMenuOpen(settings));
        var canPause = AlertPause.CanPause(timer, IsFastForwarding(time), isLeavingExpedition);
        var seen = new AwaySnapshot(away, canPause, isPaused, SpeedPatches.CurrentSpeed(time, isPaused), wasChangedByHand);
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

    /// <summary>Whether a menu whose setting is on is open, by the game's own record of the menus it shows.</summary>
    private static bool IsWatchedMenuOpen(AwaySettings settings)
    {
        var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

        if (ui == null) return false;

        return (settings.IsBagOn && ui.IsUIActive(Il2CppType.Of<WebUI_BackpackUI>()))
            || (settings.IsWorkbenchOn && ui.IsUIActive(Il2CppType.Of<WebUI_ToolTable>()))
            || (settings.IsPhoneOn && ui.IsUIActive(Il2CppType.Of<WebUI_PhoneSMS>()))
            || (settings.IsTradeOn && ui.IsUIActive(Il2CppType.Of<WebUI_TradeUI>()));
    }

    private void LogAway(AwayPause from, AwayStep step)
    {
        var line = (from, step.State) switch
        {
            (_, AwayPause.Holding holding) when step.IsPausing => $"away: {Describe(holding.Reason)}, paused the game; coming back resumes {Describe(holding.ResumeAt)}",
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
    private void UpdateQueueSlow(GameTimeManager time, GameTimer timer, bool isAsleep, bool isLeavingExpedition, float elapsed, bool wasChangedByHand)
    {
        var settings = QueueSlowing;

        if (!settings.IsOn)
        {
            _queueSlow = QueueSlow.Start;

            return;
        }

        var isPaused = time.IsPausedScale();
        var canSlow = AlertPause.CanPause(timer, IsFastForwarding(time) || isAsleep, isLeavingExpedition);
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
    private void RecoverExtraWatches()
    {
        if (_away is AwayPause.Holding holding) TryResume(time => SpeedPatches.ChangeSpeed(time, holding.ResumeAt));

        ForgetExtraWatches();
    }
}
