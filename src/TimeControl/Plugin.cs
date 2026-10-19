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
    private const string Version = "0.7.0";

    public override void Load()
    {
        var slowSpeeds = Config.Bind("SlowButton", "SlowSpeeds", "0.5,0.25",
            "Speeds for the Slow button and the ` (~) key, separated by commas, each from 0.05 to 0.95. Pressing slow again while slow moves to the next speed, and after the last back to the first. Leave it empty to keep the game's own slow button.\n" +
            "慢速按钮和 ` (~) 键使用的速度，用逗号分隔，每个在 0.05 到 0.95 之间。已经在慢速时再按一次慢速，会换到下一个速度，最后一个之后回到第一个。留空则保留游戏原本的慢速按钮。").Value;
        var fastMultiples = Config.Bind("FastButton", "FastSpeeds", "2",
            "Extra speeds above the game's Fastest speed, as multiples of it, separated by commas, each from 1.1 to 10. Pressing the Fastest button or the 3 key while already at Fastest moves to the next one, and after the last back to Fastest. Speeds are capped at 40x. Leave it empty to keep the game's own Fastest button.\n" +
            "比游戏极速更快的额外速度，按极速的倍数填写，用逗号分隔，每个在 1.1 到 10 之间。已经在极速时再按极速按钮或 3 键，会换到下一档，最后一档之后回到极速。速度上限为 40 倍。留空则保留游戏原本的极速按钮。").Value;
        var isIdleFreezeOn = Config.Bind("Clock", "IdleFreeze", true,
            "Pause the game while your character is idle with nothing queued, and resume it on your next order. Works at home, before the disaster and on expeditions. Pick a speed or press the pause key (Space by default) while it is paused to let time run until your next order.\n" +
            "角色空闲、没有待执行的指令时暂停游戏，你下达下一个指令时自动恢复。在安全屋、灾前筹备和外出探索时都有效。暂停时选一个速度或按暂停键（默认 Space），时间会一直走到你下达下一个指令。").Value;
        var idleDelay = Config.Bind("Clock", "IdleFreezeDelaySeconds", IdlePause.DefaultDelaySeconds,
            "How many real seconds your character stands idle before the game pauses, so a short gap between two orders does not pause it. 0 pauses as soon as nothing is queued.\n" +
            "角色空闲多少秒（现实时间）后才暂停游戏，这样两个指令之间的短暂空档不会触发暂停。设为 0 则一没有待执行的指令就立刻暂停。").Value;
        var isSleepFastForwardOn = Config.Bind("Sleep", "FastForwardSleep", true,
            "Run sleep and naps at the fastest FastSpeeds speed (or at Fastest if FastSpeeds is empty), instead of the game's own sleep speed-up. A speed you pick while asleep is kept until you wake. Off keeps the game's own sleep speed-up.\n" +
            "睡觉和打盹时以 FastSpeeds 里最快的速度运行（FastSpeeds 留空时用极速），代替游戏自带的睡眠加速。睡着时你选的速度会一直保持到醒来。关闭则保留游戏自带的睡眠加速。").Value;
        var isPauseOnWakeOn = Config.Bind("Sleep", "PauseOnWake", true,
            "Pause the game when your character wakes from sleep or a nap, however it ends. Your next order or the pause key (Space by default) brings back the speed you had before the sleep.\n" +
            "角色从睡觉或打盹中醒来时暂停游戏，不管是怎么醒的。下达下一个指令或按暂停键（默认 Space）会恢复睡前的速度。").Value;
        var pauseKeyText = Config.Bind("Keys", "PauseToggleKey", "Space",
            "The key that pauses the game and, on the next press, brings back the speed you had. Works at home, before the disaster and on expeditions. The game uses Space for camera follow (centering the camera on your character), so while this is Space the mod moves camera follow to CenterCameraKey. A key the game uses for anything else does not pause. None turns it off.\n" +
            "暂停游戏的按键，再按一次恢复之前的速度。在安全屋、灾前筹备和外出探索时都有效。游戏原本用 Space（空格键）做镜头跟随，所以这里设为 Space 时，本模组会把镜头跟随改到 CenterCameraKey 设置的键。如果这个键已被游戏用于其他功能，按下不会暂停。设为 None 则关闭。").Value;
        var centerCameraKeyText = Config.Bind("Keys", "CenterCameraKey", "G",
            "The key for camera follow (centering the camera on your character) when PauseToggleKey is Space, which is the game's own key for camera follow. By default the game does not use G for anything else. A key you set for camera follow in the game's key settings still wins. None leaves camera follow with no key.\n" +
            "当 PauseToggleKey 为 Space 时，镜头跟随（把镜头对准你的角色）改用的按键；Space 原本是游戏的镜头跟随键。游戏默认没有把 G 用在别处。你在游戏「按键」设置里给镜头跟随设的键仍然优先。设为 None 则镜头跟随没有按键。").Value;
        var isExpeditionOn = Config.Bind("Expedition", "TimeControlOnExpeditions", true,
            "Let the pause key, the game's speed keys (1, 2, 3, 4 for pause and ` (~) for slow, by default), the SlowSpeeds and FastSpeeds speeds, the idle pause and the alert pauses work on expeditions. Pausing there also stops the exposure meter. Off leaves expeditions at the game's own pace and hides the mod's speed buttons there.\n" +
            "外出探索时启用暂停键、游戏的速度键（默认 1、2、3，4 为暂停，` (~) 为慢速）、SlowSpeeds 和 FastSpeeds 的速度、空闲暂停和事件暂停。外出探索中暂停时，暴露值也会停止上涨。关闭则外出探索保持游戏原本的速度，也不显示本模组的速度按钮。").Value;
        var isPauseOnWaveOn = Config.Bind("Alerts", "PauseOnWave", true,
            "Pause the game once when a zombie wave starts. Pick a speed or press the pause key (Space by default) to carry on; the same wave does not pause the game again.\n" +
            "尸潮开始时暂停游戏一次。选一个速度或按暂停键（默认 Space）继续；同一波尸潮不会再暂停。").Value;
        var isPauseOnEmergencyOn = Config.Bind("Alerts", "PauseOnEmergency", true,
            "Pause the game once when an emergency starts. Pick a speed or press the pause key (Space by default) to carry on.\n" +
            "危机事件开始时暂停游戏一次。选一个速度或按暂停键（默认 Space）继续。").Value;
        var isPauseOnDroneArrivalOn = Config.Bind("Alerts", "PauseOnDroneArrival", false,
            "Pause the game once when a trade drone comes home from a trip. Pick a speed or press the pause key (Space by default) to carry on.\n" +
            "交易无人机出行归来时暂停游戏一次。选一个速度或按暂停键（默认 Space）继续。").Value;
        var isPauseOnQueueEmptyOn = Config.Bind("Alerts", "PauseOnQueueEmpty", false,
            "Pause the game once when your character finishes the last order in their queue. Unlike IdleFreeze, it pauses right away and stays paused until you pick a speed or press the pause key (Space by default).\n" +
            "角色做完队列里最后一个指令时暂停游戏一次。与 IdleFreeze 不同，它会立刻暂停，并一直停着，直到你选一个速度或按暂停键（默认 Space）。").Value;
        var isIdleBadgeOn = Config.Bind("Display", "IdleBadge", true,
            "Show a small \"Paused: idle\" badge next to the speed buttons while the idle pause holds the game. It goes away as soon as time runs again.\n" +
            "空闲暂停期间，在速度按钮旁显示一个小标记「已暂停：空闲」。时间重新走动后立刻消失。").Value;
        var isPauseKeyBadgeOn = Config.Bind("Display", "PauseKeyBadge", true,
            "Show a small \"Paused\" badge next to the speed buttons while a pause you made with a key holds the game: the pause key anywhere, or the game's own pause key (4 by default) before the disaster and on expeditions. It goes away as soon as time runs again.\n" +
            "你用按键暂停游戏时，在速度按钮旁显示一个小标记「已暂停」：任何地方按暂停键，或在灾前筹备和外出探索时按游戏自带的暂停键（默认 4）。时间重新走动后立刻消失。").Value;
        var isAlertBadgeOn = Config.Bind("Display", "AlertBadge", true,
            "Show a small badge naming the alert, such as \"Paused: wave\", next to the speed buttons while an alert pause holds the game. It goes away as soon as time runs again.\n" +
            "因尸潮、危机等事件暂停时，在速度按钮旁显示一个写明原因的小标记，例如「已暂停：尸潮」。时间重新走动后立刻消失。").Value;
        var areSpeedButtonsOn = Config.Bind("Display", "SpeedButtons", true,
            "Show speed buttons (slow, normal, fast, fastest and pause) before the disaster and on expeditions, where the game has none. They work like the game's own buttons at home, using your SlowSpeeds and FastSpeeds. On expeditions they only show when TimeControlOnExpeditions is on.\n" +
            "在灾前筹备和外出探索时显示速度按钮（慢速、正常速度、快速、极速、暂停），游戏在这些时候没有这些按钮。用法和安全屋里游戏自带的按钮一样，并使用你的 SlowSpeeds 和 FastSpeeds。外出探索时只有开启 TimeControlOnExpeditions 才会显示。").Value;
        var pauseOverlayOpacity = Config.Bind("Display", "PauseOverlayOpacity", PauseOverlay.DefaultOpacity,
            "How visible the game's own paused banner across the lower middle of the screen is, from 0 (hidden) to 1 (as the game draws it). Lower values let you read the phone's reply choices under it. Clicks always pass through the banner.\n" +
            "游戏自带的暂停横条（横跨屏幕中下方）有多明显，0 为隐藏，1 为游戏原样。数值越低，越容易看清下面手机里的回复选项。点击总是会穿过横条。").Value;

        var log = new TimeLog(Log, Path.Combine(Paths.BepInExRootPath, "time-control.log"));
        var speeds = new SpeedSettings(
            SlowGear.ParseSpeeds(slowSpeeds),
            FastGear.ParseMultiples(fastMultiples),
            ParseKey("PauseToggleKey", pauseKeyText, KeyCode.None, "pause toggle off", log.Write),
            ParseKey("CenterCameraKey", centerCameraKeyText, KeyCode.G, "using G", log.Write),
            isSleepFastForwardOn,
            isExpeditionOn);
        var alerts = new AlertSettings(isPauseOnWaveOn, isPauseOnEmergencyOn, isPauseOnDroneArrivalOn, isPauseOnQueueEmptyOn);
        var settings = new ClockSettings(
            isIdleFreezeOn,
            IdlePause.ClampDelay(idleDelay),
            isSleepFastForwardOn,
            isPauseOnWakeOn,
            isExpeditionOn,
            alerts,
            new BadgeSettings(isIdleBadgeOn, isPauseKeyBadgeOn, isAlertBadgeOn),
            areSpeedButtonsOn,
            PauseOverlay.Css(pauseOverlayOpacity));
        var harmony = new Harmony(Guid);

        log.TrimIfLarge();
        log.Write($"===== Time Control {Version} started: idle pause {(isIdleFreezeOn ? $"after {settings.IdleFreezeDelaySeconds:0.#}s" : "off")}, " +
            $"sleep fast-forward {OnOff(isSleepFastForwardOn)}, pause on waking {OnOff(isPauseOnWakeOn)}, on expeditions {OnOff(isExpeditionOn)}, " +
            $"pause on wave {OnOff(alerts.IsWaveOn)}, on emergency {OnOff(alerts.IsEmergencyOn)}, on drone arrival {OnOff(alerts.IsDroneArrivalOn)}, on queue empty {OnOff(alerts.IsQueueEmptyOn)}, " +
            $"idle badge {OnOff(isIdleBadgeOn)}, pause key badge {OnOff(isPauseKeyBadgeOn)}, alert badge {OnOff(isAlertBadgeOn)}, " +
            $"speed buttons before the disaster and on expeditions {OnOff(areSpeedButtonsOn)}, " +
            $"game's paused banner opacity {PauseOverlay.ClampOpacity(pauseOverlayOpacity):0.##}");
        SpeedPatches.Apply(harmony, speeds, log.Write);
        AlertSignals.Apply(harmony, alerts, log.Write);
        ClockTicker.Settings = settings;
        ClockTicker.Log = log.Write;
        GameClock.Log = log.Write;
        ClockTicker.LogFileOnly = log.WriteFileOnly;
        ClockTicker.Page = new PageClockLink(log.Write);
        ClockTicker.IsChinese = GameLanguage.IsChinese;
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
