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
    private const string Version = "0.4.0";

    public override void Load()
    {
        var slowSpeeds = Config.Bind("SlowButton", "SlowSpeeds", "0.5,0.25",
            "Speeds the slow button holds, comma separated, each between 0.05 and 0.95. Pressing slow again while slow moves to the next one. Empty leaves the game's own slow button alone.").Value;
        var fastMultiples = Config.Bind("FastButton", "FastSpeeds", "2",
            "Faster speeds above the game's high speed, as multiples of it, comma separated, each between 1.1 and 10. Pressing the fast button or key 3 while already at high speed moves to the next one, and after the last back to the game's high speed. The speed held never goes past 40x: the game moves its world one step per frame, and past 40x each step at 30 frames a second is longer than the game's own speeds allow. Empty leaves the game's own fast button alone.").Value;
        var clockRate = Config.Bind("Clock", "ClockRate", 1f,
            "How fast the day clock runs, from 0.05 to 4. 0.5 makes each day last twice as long; walking and actions keep their speed. 1 is the game's pace.").Value;
        var isIdleFreezeOn = Config.Bind("Clock", "IdleFreeze", true,
            "Pause the game while your character stands idle with nothing queued, at home, in the countdown before the disaster and on expeditions, and resume it on your next order. Press play or the pause key while it is paused to let time run until your next order.").Value;
        var idleDelay = Config.Bind("Clock", "IdleFreezeDelaySeconds", IdlePause.DefaultDelaySeconds,
            "Real seconds your character stands idle before the game pauses, so a moment between two orders does not pause it. 0 pauses as soon as nothing is queued.").Value;
        var isSleepFastForwardOn = Config.Bind("Sleep", "FastForwardSleep", true,
            "Run sleep and naps at the fastest FastSpeeds speed (the game's high speed if FastSpeeds is empty), in place of the game's own sleep speed-up. A speed you pick during the sleep is kept until you wake. Off leaves the game's own sleep speed-up as it is.").Value;
        var isPauseOnWakeOn = Config.Bind("Sleep", "PauseOnWake", true,
            "Pause the game when your character wakes from a sleep or nap, however it ends. Your next order, play or the pause key resumes the speed you had before the sleep.").Value;
        var pauseKeyText = Config.Bind("Keys", "PauseToggleKey", "Space",
            "A key that pauses, and on the next press resumes the speed you had, at home, in the countdown before the disaster and on expeditions. Ignored if the game already uses that key. None turns it off.").Value;
        var centerCameraKeyText = Config.Bind("Keys", "CenterCameraKey", "G",
            "The game's default key for centering the camera on your character, moved off Space so Space can pause. A key you set in the game's own key settings still wins. None leaves centering the camera with no key.").Value;
        var isExpeditionOn = Config.Bind("Expedition", "TimeControlOnExpeditions", true,
            "Let the pause key, the game's speed keys (1, 2, 3 and its pause and slow keys), the slow and fast buttons' speeds and the idle pause work on expeditions, where pausing also stops the exposure meter. Off leaves expeditions at the game's own pace, as before 0.4.0.").Value;
        var isIdleBadgeOn = Config.Bind("Display", "IdleBadge", true,
            "Show a small \"Paused: idle\" badge above the speed buttons while the idle pause holds the game. It goes away the moment time runs again.").Value;
        var isPauseKeyBadgeOn = Config.Bind("Display", "PauseKeyBadge", true,
            "Show a small \"Paused\" badge above the speed buttons while the pause key holds the game. It goes away the moment time runs again.").Value;

        var log = new TimeLog(Log, Path.Combine(Paths.BepInExRootPath, "time-control.log"));
        var speeds = new SpeedSettings(
            SlowGear.ParseSpeeds(slowSpeeds),
            FastGear.ParseMultiples(fastMultiples),
            ParseKey("PauseToggleKey", pauseKeyText, KeyCode.None, "pause toggle off", log.Write),
            ParseKey("CenterCameraKey", centerCameraKeyText, KeyCode.G, "using G", log.Write),
            isSleepFastForwardOn,
            isExpeditionOn);
        var settings = new ClockSettings(
            ClockPace.ClampRate(clockRate),
            isIdleFreezeOn,
            IdlePause.ClampDelay(idleDelay),
            isSleepFastForwardOn,
            isPauseOnWakeOn,
            isExpeditionOn,
            new BadgeSettings(isIdleBadgeOn, isPauseKeyBadgeOn));

        log.TrimIfLarge();
        log.Write($"===== Time Control {Version} started: clock rate {settings.ClockRate:0.##}, idle pause {(isIdleFreezeOn ? $"after {settings.IdleFreezeDelaySeconds:0.#}s" : "off")}, " +
            $"sleep fast-forward {OnOff(isSleepFastForwardOn)}, pause on waking {OnOff(isPauseOnWakeOn)}, on expeditions {OnOff(isExpeditionOn)}, " +
            $"idle badge {OnOff(isIdleBadgeOn)}, pause key badge {OnOff(isPauseKeyBadgeOn)}");
        SpeedPatches.Apply(new Harmony(Guid), speeds, log.Write);
        ClockTicker.Settings = settings;
        ClockTicker.Log = log.Write;
        ClockTicker.LogFileOnly = log.WriteFileOnly;
        ClockTicker.Page = new PageClockLink(log.Write);
        AddComponent<ClockTicker>();
    }

    private static string OnOff(bool isOn) => isOn ? "on" : "off";

    private static KeyCode ParseKey(string setting, string text, KeyCode fallback, string fallbackMeaning, Action<string> log)
    {
        if (Enum.TryParse<KeyCode>(text.Trim(), ignoreCase: true, out var key)) return key;

        log($"{setting} '{text}' is not a Unity key name, {fallbackMeaning}");

        return fallback;
    }
}
