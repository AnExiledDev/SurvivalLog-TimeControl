using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TimeControl.Adapters;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl;

[BepInPlugin(Guid, "Time Control", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.timecontrol";
    private const string Version = "0.2.0";

    public override void Load()
    {
        var slowSpeeds = Config.Bind("SlowButton", "SlowSpeeds", "0.5,0.25",
            "Speeds the slow button holds, comma separated, each between 0.05 and 0.95. Pressing slow again while slow moves to the next one. Empty leaves the game's own slow button alone.").Value;
        var clockRate = Config.Bind("Clock", "ClockRate", 1f,
            "How fast the day clock runs, from 0.05 to 4. 0.5 makes each day last twice as long; walking and actions keep their speed. 1 is the game's pace.").Value;
        var isIdleFreezeOn = Config.Bind("Clock", "IdleFreeze", true,
            "Pause the game while your character stands idle at home with nothing queued, and resume it on your next order. Press play or the pause key while it is paused to let time run until your next order.").Value;
        var idleDelay = Config.Bind("Clock", "IdleFreezeDelaySeconds", 1f,
            "Real seconds of idling before the game pauses, so the short gap between two orders does not count.").Value;
        var pauseKeyText = Config.Bind("Keys", "PauseToggleKey", "Space",
            "A key that pauses, and on the next press resumes the speed you had. Ignored if the game already uses that key. None turns it off.").Value;

        var log = new TimeLog(Log, Path.Combine(Paths.BepInExRootPath, "time-control.log"));
        var pauseKey = ParseKey(pauseKeyText, log.Write);
        var speeds = SlowGear.ParseSpeeds(slowSpeeds);
        var settings = new ClockSettings(ClockPace.ClampRate(clockRate), isIdleFreezeOn, IdlePause.ClampDelay(idleDelay));

        log.TrimIfLarge();
        log.Write($"===== Time Control {Version} started: clock rate {settings.ClockRate:0.##}, idle pause {(isIdleFreezeOn ? $"after {settings.IdleFreezeDelaySeconds:0.#}s" : "off")}");
        SpeedPatches.Apply(new Harmony(Guid), speeds, pauseKey, log.Write);
        ClockTicker.Settings = settings;
        ClockTicker.Log = log.Write;
        ClockTicker.LogFileOnly = log.WriteFileOnly;
        ClockTicker.Page = new PageClockLink(log.Write);
        AddComponent<ClockTicker>();
    }

    private static KeyCode ParseKey(string text, Action<string> log)
    {
        if (Enum.TryParse<KeyCode>(text.Trim(), ignoreCase: true, out var key)) return key;

        log($"PauseToggleKey '{text}' is not a Unity key name, pause toggle off");

        return KeyCode.None;
    }
}
