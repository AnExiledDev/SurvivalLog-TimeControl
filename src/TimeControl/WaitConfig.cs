using System;
using BepInEx.Configuration;
using HarmonyLib;
using TimeControl.Adapters;
using TimeControl.Core;

namespace TimeControl;

/// <summary>
/// The settings for the alarm clock, start paused and sleep until, read again after each change while the Toolkit is
/// installed. A time that cannot be read is logged and counts as no time set.
/// </summary>
internal static class WaitConfig
{
    public static AlarmSettings Alarm { get; private set; } = new(null, true);

    public static bool IsStartPausedOn { get; private set; }

    public static bool IsWaitButtonOn { get; private set; }

    public static SleepUntilSettings SleepUntil { get; private set; } = new(null, false);

    public static void Bind(ConfigFile config, Harmony harmony, Action<string> log)
    {
        var alarmTime = config.Bind("Alarm", "AlarmTime", "",
            "A time of day on a 24-hour clock, such as 7 or 21:30, at which the game pauses once, at home and before the disaster. The pause key (Space by default) carries on at the speed you had. It stays quiet while your character sleeps and rings once they wake. Leave it empty for no alarm. With WaitButton on, the Wait until button beside the speed buttons can also set a one-time alarm a few hours ahead.\n" +
            "一天中的某个时间（24 小时制），例如 7 或 21:30，到这个时间游戏会暂停一次，在安全屋和灾前筹备时都有效。按暂停键（默认 Space）会以原来的速度继续。角色睡觉时不会响，醒来后才响。留空则不设闹钟。开启 WaitButton 后，速度按钮旁的「等到」按钮也可以设一个几小时后响一次的闹钟。");
        var isAlarmDaily = config.Bind("Alarm", "AlarmDaily", true,
            "Pause at the AlarmTime every day. Off pauses only once, and again only after AlarmTime changes or the game restarts.\n" +
            "每天都在 AlarmTime 的时间暂停。关闭则只暂停一次，修改 AlarmTime 或重新启动游戏后才会再次暂停。");
        var isStartPausedOn = config.Bind("Clock", "StartPaused", false,
            "Start paused when a save is loaded or a new game begins, at home and before the disaster. The pause key (Space by default) starts time at the speed the game would have run at. A guided start or a story scene is left running.\n" +
            "读取存档或开始新游戏时，以暂停状态开始，在安全屋和灾前筹备时都有效。按暂停键（默认 Space）会以游戏原本的速度开始。引导开局和剧情场景不受影响。");
        var wakeAtHour = config.Bind("Sleep", "WakeAtHour", "",
            "A time of day on a 24-hour clock, such as 6 or 6:30, at which your character wakes from sleep if the game has not woken them yet. It can only end a sleep early, never make it longer. Naps are never cut short. Leave it empty to let the game wake your character. If your character slept at least 3 hours, they still get Vim and Vigor when they wake.\n" +
            "一天中的某个时间（24 小时制），例如 6 或 6:30，如果角色到这个时间还在睡觉，就把角色叫醒。它只能让睡觉提前结束，不会让睡觉变长。打盹不会被打断。留空则由游戏决定何时醒来。如果角色已经睡了至少 3 小时，醒来时仍会获得「神清气爽」。");
        var isWakeOnAlertOn = config.Bind("Sleep", "WakeOnAlert", false,
            "Wake your character from sleep when an alert switched on under Alerts pauses the game, such as a zombie wave or an emergency, and keep the game paused once they are up. Off lets a sleeping character sleep through alerts. Naps are never cut short. If your character slept at least 3 hours, they still get Vim and Vigor when they wake.\n" +
            "当 Alerts 中开启的事件（例如尸潮或危机）让游戏暂停时，把正在睡觉的角色叫醒，并在角色起身后保持暂停。关闭则角色睡觉时会睡过这些事件。打盹不会被打断。如果角色已经睡了至少 3 小时，醒来时仍会获得「神清气爽」。");

        var isWaitButtonOn = config.Bind("Display", "WaitButton", false,
            "Show the Wait until button beside the speed buttons. It runs the game at top speed until morning, the alarm time, a brew or the queue being done, or a few hours on, then pauses.\n" +
            "在速度按钮旁显示「等到」按钮。它会以最快速度运行游戏，直到早上、闹钟时间、酿造或队列完成，或几小时后，然后暂停。");

        GameWait.HookEnter(harmony, log);
        GameWait.HookSleepBuff(harmony, log);
        ClockTicker.WaitPage = new WaitPageLink(log);

        OptionalLive.Watch(() =>
        {
            Alarm = new AlarmSettings(ReadTime("Alarm", "AlarmTime", alarmTime.Value, "no alarm", log), isAlarmDaily.Value);
            IsStartPausedOn = isStartPausedOn.Value;
            IsWaitButtonOn = isWaitButtonOn.Value;
            SleepUntil = new SleepUntilSettings(ReadTime("Sleep", "WakeAtHour", wakeAtHour.Value, "the game wakes the character", log), isWakeOnAlertOn.Value);

            log($"alarm {(Alarm.SecondOfDay is { } alarmAt ? $"at {ClockHour.Label(alarmAt)} {(Alarm.IsDaily ? "every day" : "once")}" : "off")}, " +
                $"start paused {OnOff(IsStartPausedOn)}, " +
                $"wait button {OnOff(IsWaitButtonOn)}, " +
                $"sleep wakes {(SleepUntil.WakeAtSecond is { } wakeAt ? $"at {ClockHour.Label(wakeAt)}" : "when the game wakes the character")}, " +
                $"wake on alert {OnOff(SleepUntil.IsWakeOnAlertOn)}");
        }, alarmTime, isAlarmDaily, isStartPausedOn, isWaitButtonOn, wakeAtHour, isWakeOnAlertOn);
    }

    private static int? ReadTime(string section, string key, string text, string meaningWhenUnread, Action<string> log)
    {
        if (ClockHour.TryParse(text, out var secondOfDay)) return secondOfDay;

        log($"[{section}] {key} '{text}' is not a time of day like 7 or 21:30, {meaningWhenUnread}");

        return null;
    }

    private static string OnOff(bool isOn) => isOn ? "on" : "off";
}
