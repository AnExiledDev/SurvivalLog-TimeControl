using System;
using GameCore.HotUpdate.Battle.Logic;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>The settings the ticker reads, swapped whole when one changes on the Mods screen.</summary>
internal sealed record ClockSettings(
    bool IsIdleFreezeOn,
    float IdleFreezeDelaySeconds,
    bool IsSleepFastForwardOn,
    bool IsPauseOnWakeOn,
    bool IsExpeditionOn,
    AlertSettings Alerts,
    BadgeSettings Badges,
    bool AreSpeedButtonsOn,
    string PauseOverlayCss);

/// <summary>
/// Ten times a second: pauses the game while the player idles at home, in the countdown to the disaster or on an
/// expedition and resumes it on their next order, fast-forwards sleep and pauses on waking, pauses once when an alert
/// starts, lets no pause hold up the trip home from an expedition, keeps the chosen slow or fast speed held, remembers
/// the speed to resume to, shows the pause badge, shows speed buttons where the game has none, makes the game's pause
/// strip see-through and puts the slow speed in use on the slow button's tooltip.
/// </summary>
public sealed partial class ClockTicker : MonoBehaviour
{
    private const float PageSeconds = 0.5f;

    /// <summary>How often the small scripts that take back clicks on the speed buttons and turns of the wheel go out.</summary>
    private const float PollSeconds = 0.2f;
    private const float RepeatLogSeconds = 60f;
    private const float SpeedTolerance = 0.001f;
    private const int MaxLoggedFailures = 3;

    internal static ClockSettings Settings = new(false, IdlePause.DefaultDelaySeconds, false, false, false,
        new AlertSettings(false, false, false, false), new BadgeSettings(false, false, false), false, "");
    internal static Action<string> Log = _ => { };
    internal static Action<string> LogFileOnly = _ => { };
    internal static PageClockLink? Page;
    internal static PageWatcher? Watch;
    internal static Func<bool> IsChinese = () => false;

    private static bool _isCheckWanted;

    private IdlePause _idle = IdlePause.Start;
    private SleepWatch _sleep = SleepWatch.Start;
    private AlertPause _alert = AlertPause.Start;
    private ExpeditionFinish _finish = ExpeditionFinish.Start;
    private IntPtr _timeSeen;
    private CheckPace _pace = CheckPace.Start;
    private float _nextPageAt;
    private float _nextPollAt;
    private float _nextLanguageAt;
    private bool _isChinese;
    private float _nextHealLogAt;
    private float _nextFanOutLogAt;
    private bool _isSingleWorldLogged;
    private int _failures;

    public ClockTicker(IntPtr pointer) : base(pointer)
    {
    }

    /// <summary>
    /// Asks for a check on this frame: a hold key or a custom speed key went down, the game's hold key changed, or a
    /// click or a turn of the wheel came back from the page. Only a flag is set, so the hooks that call it stay cheap.
    /// </summary>
    internal static void WantCheck() => _isCheckWanted = true;

    private void Update() => Tick();

    /// <summary>A key or a click seen after this frame's Update, in the game's own key handling or a page answer, still acts on this frame.</summary>
    private void LateUpdate()
    {
        if (_isCheckWanted) Tick();
    }

    private void Tick()
    {
        var now = Time.unscaledTime;

        if (CheckPace.Take(_pace, now, _isCheckWanted) is not { } due) return;

        _pace = due.Pace;
        _isCheckWanted = false;

        try
        {
            Check(now, due.ElapsedSeconds);
        }
        catch (Exception ex)
        {
            RecoverFromFailure(ex, now);
        }
    }

    private void Check(float now, float elapsed)
    {
        ReportFanOut(now);

        if (GameClock.Find() is not { } time)
        {
            _idle = IdlePause.Start;
            _sleep = SleepWatch.Start;
            _alert = AlertPause.Start;
            _finish = ExpeditionFinish.Start;
            _timeSeen = IntPtr.Zero;
            ForgetExtraWatches();
            HidePage(now);

            return;
        }

        ForgetPauseOnNewWorld(time);

        if (!time.IsGameStart)
        {
            _alert = AlertPause.Start;
            ForgetExtraWatches();
            HidePage(now);

            return;
        }

        var timer = GameTimers.Controlled(GameClock.Timer(time), Settings.IsExpeditionOn);
        var isLeavingExpedition = GameClock.IsExpeditionEnding(timer);

        ReadLanguage(now);
        GameFreeze.Update(elapsed);
        PressSpeedButtons(time, timer);
        UpdateSpeedKeys(time, timer, isLeavingExpedition);

        var isPaused = time.IsPausedScale();

        if (!isPaused) SpeedPatches.LastRunningLevel = time.GetSpeedLevel();

        KeepGearSpeedHeld(time, isPaused, now);
        KeepCustomSpeedHeld(time, isPaused);

        var wasChangedByHand = SpeedPatches.TakeSpeedChangedByHand();
        var isAsleep = UpdateSleep(time, isPaused, wasChangedByHand, isLeavingExpedition);

        UpdateAlertPause(time, timer, isAsleep, isLeavingExpedition, elapsed, wasChangedByHand, now);
        UpdateAwayPause(time, timer, isLeavingExpedition, wasChangedByHand, elapsed);
        UpdateWaits(time, timer, isAsleep, isLeavingExpedition, wasChangedByHand, elapsed, now);
        UpdateIdlePause(time, time.IsPausedScale(), timer, elapsed, isAsleep, isLeavingExpedition);
        UpdateQueueSlow(time, timer, isAsleep, isLeavingExpedition, elapsed, wasChangedByHand);
        UpdateExpeditionFinish(time, isLeavingExpedition);

        var isPollDue = TakePoll(now);
        var isRefreshDue = TakePageRefresh(now);

        SyncPage(time, timer, isRefreshDue, isPollDue);
        SyncKeysPage(time, timer, now, isPollDue);

        if (isRefreshDue) Watch?.Look();
    }

    /// <summary>The game's language is asked for at the page pace: it only changes on the settings screen.</summary>
    private void ReadLanguage(float now)
    {
        if (now < _nextLanguageAt) return;

        _nextLanguageAt = now + PageSeconds;
        _isChinese = IsChinese();
    }

    /// <summary>
    /// Clicks on the mod's speed buttons go through first, as a click on the game's own button lands before the rest
    /// of the check sees the speed. A click is only applied while the buttons are still meant to show.
    /// </summary>
    private static void PressSpeedButtons(GameTimeManager time, GameTimer timer)
    {
        if (Page?.TakeSpeedClicks() is not { Count: > 0 } clicks || !AreSpeedButtonsShown(timer)) return;

        foreach (var level in clicks) SpeedPatches.PressSpeedButton(time, level);
    }

    private static bool AreSpeedButtonsShown(GameTimer timer) =>
        SpeedButtons.ShouldShow(Settings.AreSpeedButtonsOn, SpeedPatches.IsNormalMode, timer);

    /// <summary>A load replaces the world, and a pause or a sleep this mod was watching on the old one went with it.</summary>
    private void ForgetPauseOnNewWorld(GameTimeManager time)
    {
        if (time.Pointer == _timeSeen) return;

        _timeSeen = time.Pointer;
        _idle = IdlePause.Start;
        _sleep = SleepWatch.Start;
        _alert = AlertPause.Start;
        _finish = ExpeditionFinish.Start;
        ForgetExtraWatches();
        SpeedPatches.ForgetWorld();
        ForgetSpeedKeysOnNewWorld();
    }

    /// <summary>
    /// Like the idle pause, the wake pause is kept before it is made, so the recovery undoes one that fails halfway.
    /// Whether the player is busy only matters once the character is asleep or the game is paused on waking.
    /// </summary>
    /// <returns>Whether the character is asleep, which idle freeze leaves alone.</returns>
    private bool UpdateSleep(GameTimeManager time, bool isPaused, bool wasChangedByHand, bool isLeavingExpedition)
    {
        var isAsleep = GameClock.IsLeadingRoleAsleep();
        var settings = new SleepSettings(Settings.IsSleepFastForwardOn, Settings.IsPauseOnWakeOn, SpeedPatches.FastestGear);
        var isBusy = _sleep is not SleepWatch.Awake && time.IsLeadingRoleBusy();
        var running = SpeedPatches.CurrentSpeed(time, isPaused);
        var seen = new SleepSnapshot(isAsleep, IsFastForwarding(time), isBusy, isPaused, running, wasChangedByHand, isLeavingExpedition);
        var before = _sleep;
        var step = SleepWatch.Next(before, seen, settings);

        if (step.Command is SleepCommand.PauseOnWake) _sleep = step.State;

        CarrySleep(time, step.Command);
        _sleep = step.State;
        LogSleep(time, before, step);

        return isAsleep;
    }

    private void CarrySleep(GameTimeManager time, SleepCommand command)
    {
        switch (command)
        {
            case SleepCommand.FastForward fastForward:
                SpeedPatches.ChangeSpeed(time, new GameSpeed(PauseToggle.Triple, fastForward.Gear));
                break;
            case SleepCommand.PauseOnWake wake:
                if (!time.IsPausedScale()) SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);

                SpeedPatches.SetResumeSpeed(wake.ResumeAt);
                break;
            case SleepCommand.Resume resume when time.IsPausedScale():
                SpeedPatches.ChangeSpeed(time, resume.At);
                break;
            case SleepCommand.Restore restore when time.IsPausedScale():
                SpeedPatches.SetResumeSpeed(restore.To);
                break;
            case SleepCommand.Restore restore:
                SpeedPatches.ChangeSpeed(time, restore.To);
                break;
            case SleepCommand.LetRun:
                _idle = new IdlePause.Released();
                break;
        }
    }

    private static void LogSleep(GameTimeManager time, SleepWatch from, SleepStep step)
    {
        var line = (from, step.State, step.Command) switch
        {
            (_, SleepWatch.Asleep asleep, SleepCommand.FastForward) =>
                $"sleep: started, fast-forwarding at {time.BattleTimeScale:0.##}x in place of the game's own sleep speed-up of {time.ChapterSpeedUp2}x; {Describe(asleep.Before)} before",
            (SleepWatch.Awake, SleepWatch.Asleep asleep, _) =>
                $"sleep: started, at the game's own sleep speed (its speed-up runs at {time.ChapterSpeedUp2}x); {Describe(asleep.Before)} before",
            (SleepWatch.Asleep { IsHeld: true }, SleepWatch.Asleep { IsHeld: false }, _) => "sleep: speed changed by hand, kept until waking",
            (_, _, SleepCommand.PauseOnWake wake) => $"sleep: woke, paused the game; the next order, play or the pause key resumes {Describe(wake.ResumeAt)}",
            (_, _, SleepCommand.Resume resume) => $"sleep: order given after waking, resumed {Describe(resume.At)}",
            (_, _, SleepCommand.Restore restore) => $"sleep: woke, put back {Describe(restore.To)}",
            (_, _, SleepCommand.LetRun) => "sleep: time was resumed after waking, it runs until the next order",
            (SleepWatch.Asleep, SleepWatch.Awake, _) => "sleep: woke, the speed chosen by hand is kept",
            _ => null,
        };

        if (line != null) LogFileOnly(line);
    }

    private static string Describe(GameSpeed speed) => speed.Gear > FastGear.GamesOwn
        ? $"speed level {speed.Level} on fast gear {speed.Gear}"
        : $"speed level {speed.Level}";

    /// <summary>
    /// Runs before idle freeze, so an alert starting as the player falls idle is the pause that holds the game rather
    /// than one their next order would resume. The counts are taken on every check, so ones from before a world has
    /// settled, or from while alerts cannot pause, are let go rather than saved up. The wave and the alerts after the
    /// queue running out are read at the <see cref="DangerWatch"/> pace, and the last wave reading stands between reads.
    /// Every pause but this mod's own goes through the game's speed change, which the hand-change flag sees, so a pause
    /// seen after one is not the alert's.
    /// </summary>
    private void UpdateAlertPause(GameTimeManager time, GameTimer timer, bool isAsleep, bool isLeavingExpedition, float elapsed, bool wasChangedByHand, float now)
    {
        var alerts = Settings.Alerts;
        var emergenciesFired = AlertSignals.TakeEmergenciesFired();
        var dronesLanded = AlertSignals.TakeDronesLanded();
        var moreRaised = AlertSet.None;

        if (_danger.IsDue(now))
        {
            moreRaised = TakeMoreAlerts(time, alerts.MoreOn);
            _danger = _danger.Read(now, alerts.IsWaveOn && AlertSignals.IsWaveActive());
        }

        if (!alerts.IsAnyOn) return;

        var canPause = AlertPause.CanPause(timer, (IsFastForwarding(time) || isAsleep) && !CanAlertWakeSleep(isAsleep), isLeavingExpedition);
        var isWaveActive = alerts.IsWaveOn && _danger.IsWaveActive;
        var isBusy = alerts.IsQueueEmptyOn && time.IsLeadingRoleBusy();
        var seen = new AlertSnapshot(canPause, time.IsPausedScale(), isWaveActive, emergenciesFired, dronesLanded, isBusy, elapsed, wasChangedByHand, moreRaised, SpeedPatches.IsHoldingGameKey(time));
        var step = AlertPause.Next(_alert, seen, alerts);

        _alert = step.State;

        if (step.PauseFor is not { } alert) return;

        SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
        LogFileOnly($"alert: {Describe(alert)}, paused the game; play or the pause key resumes speed level {SpeedPatches.LastRunningLevel}");
    }

    private static string Describe(Alert alert) => alert switch
    {
        Alert.Wave => "a zombie wave started",
        Alert.Emergency => "an emergency fired",
        Alert.DroneArrival => "a trade drone came home",
        Alert.QueueEmpty => "the order queue ran out",
        _ => DescribeMore(alert),
    };

    /// <summary>
    /// A pause is kept as Frozen before it is made, so one that fails halfway is still undone by the recovery; a resume
    /// is kept after, so one that fails is tried again by it. Whether the player is busy, with an order queued or a
    /// workbench crafting, only matters while time runs where idle freeze applies or the game is held by this mod.
    /// </summary>
    private void UpdateIdlePause(GameTimeManager time, bool isPaused, GameTimer timer, float elapsed, bool isAsleep, bool isLeavingExpedition)
    {
        var canFreeze = IdlePause.CanFreeze(Settings.IsIdleFreezeOn, timer, IsFastForwarding(time) || isAsleep, SpeedPatches.IsHoldingGameKey(time));
        var isBusy = ((canFreeze && !isPaused) || _idle is not IdlePause.Watching) && IsWorking(time);
        var seen = new IdleSnapshot(canFreeze, isBusy, isPaused, elapsed, SpeedPatches.LastRunningLevel, isLeavingExpedition);
        var before = _idle;
        var step = IdlePause.Next(before, seen, Settings.IdleFreezeDelaySeconds);

        if (step.Command is IdleCommand.Pause) _idle = step.State;

        Carry(time, step.Command);
        _idle = step.State;
        LogTransition(before, step, seen);
    }

    private static void Carry(GameTimeManager time, IdleCommand command)
    {
        switch (command)
        {
            case IdleCommand.Pause:
                SpeedPatches.ChangeSpeed(time, PauseToggle.Paused);
                break;
            case IdleCommand.Resume resume when time.IsPausedScale():
                SpeedPatches.ChangeSpeed(time, resume.Level);
                break;
        }
    }

    /// <summary>
    /// Runs after idle freeze, which lets go of its own pause with its own log line, so the pause met here is one made
    /// through the pause key, a speed button or an alert. Like idle freeze's, the resume is kept after it is made, so
    /// one that fails is tried again. The pauses refused during the finish are taken on every check, so none is told
    /// of in a later finish.
    /// </summary>
    private void UpdateExpeditionFinish(GameTimeManager time, bool isLeavingExpedition)
    {
        var seen = new FinishSnapshot(isLeavingExpedition, time.IsPausedScale(), SpeedPatches.LastRunningLevel, SpeedPatches.TakePauseRefused());
        var step = ExpeditionFinish.Next(_finish, seen);

        if (step.ResumeAt is { } level)
        {
            SpeedPatches.ChangeSpeed(time, level);
            LogFileOnly($"expedition: being finished while paused, resumed at speed level {level} so the trip home can run");
        }

        if (step.TellsOfRefusal) LogFileOnly("expedition: pause ignored until the trip home finishes");

        _finish = step.State;
    }

    private static void LogTransition(IdlePause from, IdleStep step, IdleSnapshot seen)
    {
        var line = (from, step.State, step.Command) switch
        {
            (_, _, IdleCommand.Pause) => "idle: paused the game",
            (_, _, IdleCommand.Resume resume) when seen.IsLeavingExpedition =>
                $"idle: the expedition is being finished, resumed at speed level {resume.Level} so the trip home can run",
            (_, _, IdleCommand.Resume resume) => $"idle: order given, resumed at speed level {resume.Level}",
            (IdlePause.Frozen, IdlePause.Released, _) => "idle: time was resumed while idle, it runs until the next order",
            _ => null,
        };

        if (line != null) LogFileOnly(line);
    }

    /// <summary>
    /// Saves, menus and the game's other speed changes can put the scale back without going through the slow or fast
    /// button, so a held slow or fast speed is checked and restored here; the game's own fast-forwards and its own speed
    /// boost are left alone.
    /// </summary>
    private void KeepGearSpeedHeld(GameTimeManager time, bool isPaused, float now)
    {
        if (isPaused || IsFastForwarding(time) || time.IsHoldBoostActive()) return;

        var slow = SpeedPatches.HeldSlowSpeed(time);

        if ((slow ?? SpeedPatches.HeldFastSpeed(time)) is not { } held) return;

        if (Math.Abs(time.BattleTimeScale - held) <= SpeedTolerance) return;

        if (now >= _nextHealLogAt)
        {
            _nextHealLogAt = now + RepeatLogSeconds;
            Log($"{(slow != null ? "slow" : "fast")} button: game speed was {time.BattleTimeScale:0.###}x, putting {SlowGear.Label(held)} back (logged at most once a minute)");
        }

        GameClock.HoldSpeed(time, held);
    }

    /// <summary>
    /// Every check works out what the page should show, and each part goes out only when it changed, when the page
    /// changed under it or until the page confirms it, so the badge leaves the moment time runs; the clicks on the
    /// buttons are taken at the poll pace. The buttons go first so the badge finds them to sit beside.
    /// </summary>
    private void SyncPage(GameTimeManager time, GameTimer timer, bool isRefreshDue, bool isPollDue)
    {
        var generation = Watch?.Generation ?? 0;
        var isPaused = time.IsPausedScale();
        var isChinese = _isChinese;
        var heldSlow = SpeedPatches.HeldSlowSpeed(time);
        var slowLabel = heldSlow is { } held ? SlowGear.Label(held) : "";
        SpeedButtonsView? buttons = AreSpeedButtonsShown(timer) ? SpeedButtons.View(timer, isPaused, time.GetSpeedLevel(), slowLabel, isChinese) : null;
        Alert? alertHolding = _alert is AlertPause.Holding holding ? holding.Reason : null;
        var badge = AwayPause.Badge(_away, Settings.Badges, isPaused, isChinese) ?? PauseBadge.Text(Settings.Badges, isPaused, _idle is IdlePause.Frozen, SpeedPatches.IsPausedByKey, alertHolding, isChinese);

        Page?.SyncSpeedButtons(buttons, isRefreshDue, isPollDue, generation);
        Page?.SyncBadge(badge, buttons?.Page ?? HudPage.Home, isRefreshDue, generation);
        var heldFast = SpeedChip.HoldKeyFast(SpeedPatches.IsHoldingGameKey(time), time.ChapterSpeedUp2, SpeedPatches.HoldKeyMultiple) ?? SpeedPatches.HeldFastSpeed(time);
        Page?.SyncSpeedChip(SpeedChip.View(isPaused, heldSlow, heldFast), buttons?.Page ?? HudPage.Home, isRefreshDue, generation);
        Page?.SyncPauseOverlay(Settings.PauseOverlayCss, isRefreshDue, generation);
        Page?.Sync(slowLabel, isRefreshDue, generation, Watch?.SlowText);
    }

    /// <summary>
    /// Takes the speed buttons, the badge and the pause strip's style off the page, sending a removal the page never
    /// confirmed again on each refresh.
    /// </summary>
    private void HidePage(float now)
    {
        var isRefreshDue = TakePageRefresh(now);
        var generation = Watch?.Generation ?? 0;

        Page?.SyncSpeedButtons(null, isRefreshDue, isPollDue: false, generation);
        Page?.SyncBadge("", HudPage.Home, isRefreshDue, generation);
        Page?.SyncSpeedChip(null, HudPage.Home, isRefreshDue, generation);
        Page?.SyncPauseOverlay("", isRefreshDue, generation);
        HideKeysPage(now, isRefreshDue);
        HideWaitPage(isRefreshDue);
    }

    /// <summary>Whether the slower page pace is due a refresh; when it is, the next one is counted from now.</summary>
    private bool TakePageRefresh(float now)
    {
        if (now < _nextPageAt) return false;

        _nextPageAt = now + PageSeconds;

        return true;
    }

    /// <summary>Whether the clicks and the wheel's turns are due to be taken; when they are, the next take is counted from now.</summary>
    private bool TakePoll(float now)
    {
        if (now < _nextPollAt) return false;

        _nextPollAt = now + PollSeconds;

        return true;
    }

    /// <summary>
    /// One speed change reaching several time managers means worlds the game replaced are still answering its events,
    /// and each of them acts on a trade or an order as well. Logged so a session's health can be read off the log.
    /// </summary>
    private void ReportFanOut(float now)
    {
        var reached = SpeedPatches.TakeLargestFanOut();

        if (reached > 1 && now >= _nextFanOutLogAt)
        {
            _nextFanOutLogAt = now + RepeatLogSeconds;
            Log($"speed change reached {reached} time managers in one frame (expected 1): leftover game worlds are answering events (logged at most once a minute)");
        }
        else if (reached == 1 && !_isSingleWorldLogged)
        {
            _isSingleWorldLogged = true;
            Log("speed change reached 1 time manager, as it should");
        }
    }

    /// <summary>
    /// A failed check must never leave the game paused by this mod. An alert pause is left
    /// as it is: it holds the game until the player resumes it in any case, and only its badge goes. The speed buttons
    /// go too, so none stay up that no check would answer.
    /// </summary>
    private void RecoverFromFailure(Exception ex, float now)
    {
        if (_idle is IdlePause.Frozen frozen) TryResume(time => SpeedPatches.ChangeSpeed(time, frozen.ResumeLevel));

        if (_sleep is SleepWatch.WakePaused wake) TryResume(time => SpeedPatches.ChangeSpeed(time, wake.ResumeAt));

        ForgetWaits();
        _idle = IdlePause.Start;
        _sleep = SleepWatch.Start;
        _alert = AlertPause.Start;
        RecoverExtraWatches();
        _failures++;

        TryHidePage(now);

        if (_failures <= MaxLoggedFailures) Log($"clock check failed (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }

    private void TryHidePage(float now)
    {
        try
        {
            HidePage(now);
        }
        catch (Exception)
        {
            // Best effort inside a failure already being logged; the next check or recovery tries again.
        }
    }

    private static void TryResume(Action<GameTimeManager> resume)
    {
        try
        {
            if (GameClock.Find() is { } time && time.IsPausedScale()) resume(time);
        }
        catch (Exception)
        {
            // Best effort inside a failure already being logged; the player can still press play.
        }
    }

    private static bool IsFastForwarding(GameTimeManager time) => time.TempSpeedUpBeforeScale >= 0f;
}
