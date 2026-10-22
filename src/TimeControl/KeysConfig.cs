using System;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using TimeControl.Adapters;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl;

/// <summary>The settings for the hold keys, the custom speed keys, the speed wheel and the notes under the clock.</summary>
internal static class KeysConfig
{
    public static void Bind(ConfigFile config, Harmony harmony, Action<string> log)
    {
        var holdFastKeyText = config.Bind("Keys", "HoldFastKey", "X",
            "Hold this key to run the game at the fastest speed the mod has (the fastest FastSpeeds speed, or Fastest if FastSpeeds is empty). Letting go brings back the speed you had, and if the game was paused it pauses again. Works at home, before the disaster and on expeditions when TimeControlOnExpeditions is on, but not while your character sleeps. A key the game uses for anything else does nothing. None turns it off.\n" +
            "按住这个键时，游戏以本模组最快的速度运行（FastSpeeds 里最快的速度，FastSpeeds 留空时用极速）。松开后恢复之前的速度；如果之前是暂停的，会重新暂停。在安全屋、灾前筹备和外出探索（需开启 TimeControlOnExpeditions）时有效，角色睡觉时无效。如果这个键已被游戏用于其他功能，按住不会有作用。设为 None 则关闭。");
        var holdSlowKeyText = config.Bind("Keys", "HoldSlowKey", "K",
            "Hold this key to run the game at the slowest SlowSpeeds speed. Letting go brings back the speed you had, and if the game was paused it pauses again. Does nothing while SlowSpeeds is empty. While both hold keys are down, HoldFastKey wins. Works where HoldFastKey does. A key the game uses for anything else does nothing. None turns it off.\n" +
            "按住这个键时，游戏以 SlowSpeeds 里最慢的速度运行。松开后恢复之前的速度；如果之前是暂停的，会重新暂停。SlowSpeeds 留空时无效。两个按住键同时按下时以 HoldFastKey 为准。生效范围与 HoldFastKey 相同。如果这个键已被游戏用于其他功能，按住不会有作用。设为 None 则关闭。");
        var customSpeedsText = config.Bind("Keys", "SpeedKeys5To9", "",
            "Custom speeds for the keys 5, 6, 7, 8 and 9, separated by commas in that order, each from 0.05 to 40 (for example 0.75,1.5,6). Leave a place empty to skip that key. Pressing the key runs the game at that speed, and also resumes a pause. The speed is kept until you pick another, and pausing and resuming brings it back; any other speed change, sleep included, lets it go. Works where HoldFastKey does. The game keeps 5 to 9 for its quick actions, which it has switched off for now; if a game update turns them back on, a key set here does both. Empty turns them all off.\n" +
            "数字键 5、6、7、8、9 的自定义速度，按这个顺序用逗号分隔，每个在 0.05 到 40 之间（例如 0.75,1.5,6）。某个位置留空则跳过对应的键。按下该键时游戏以这个速度运行，暂停中按下也会恢复运行。这个速度会一直保持到你选别的速度，暂停后再恢复也会回到这个速度；任何其他速度变化（包括睡觉）都会取消它。生效范围与 HoldFastKey 相同。游戏把 5 到 9 留给「常用行为」，目前游戏已关闭这个功能；如果游戏更新后重新启用，这里设置的键会同时触发两者。留空则全部关闭。");
        var isWheelOn = config.Bind("Keys", "SpeedWheel", true,
            "Turn the mouse wheel over the speed buttons to step through every speed, slowest to fastest: the SlowSpeeds speeds, the game's own speeds, the FastSpeeds speeds and the SpeedKeys5To9 speeds. Wheel up is faster. Turning it over the buttons does not zoom the camera. Does nothing while paused.\n" +
            "在速度按钮上滚动鼠标滚轮，可以按从慢到快的顺序逐档切换所有速度：SlowSpeeds 的速度、游戏自带的速度、FastSpeeds 的速度和 SpeedKeys5To9 的速度。向上滚动加快。在按钮上滚动时镜头不会缩放。暂停时无效。");
        var isOrderFinishOn = config.Bind("Display", "OrderFinishTime", true,
            "Show, under the clock at home, the game time your character's current order will be done, such as \"Current order done at 14:30\". Only the order being carried out counts, not the ones queued after it.\n" +
            "在安全屋的时钟下方显示角色当前行为完成的游戏时间，例如「当前行为完成于 14:30」。只计算正在执行的行为，不包括排在后面的。");
        var isNightCountdownOn = config.Bind("Display", "NightCountdown", true,
            "Show, under the clock at home, how much game time is left until night falls, such as \"Night in 3h 20m\". It shows during the day only.\n" +
            "在安全屋的时钟下方显示距离入夜还剩多少游戏时间，例如「距入夜 3小时20分钟」。只在白天显示。");
        var isWaveCountdownOn = config.Bind("Display", "WaveCountdown", true,
            "Show, under the clock at home, how much game time is left until the next zombie wave, such as \"Wave in 1h 5m\". It counts the waves the game has planned for today and the wave a lure rig calls in.\n" +
            "在安全屋的时钟下方显示距离下一波尸潮还剩多少游戏时间，例如「距尸潮 1小时5分钟」。包括游戏为今天安排的尸潮和「引诱装置」引来的尸潮。");

        CountdownSignals.Log = log;
        SpeedPatches.ApplyKeys(harmony);
        ClockTicker.SpeedKeysPage = new KeysPage(log);

        OptionalLive.Watch(() =>
        {
            var settings = new KeysSettings(
                ParseKey("HoldFastKey", holdFastKeyText.Value, log),
                ParseKey("HoldSlowKey", holdSlowKeyText.Value, log),
                SpeedLadder.ParseCustom(customSpeedsText.Value),
                isWheelOn.Value,
                isOrderFinishOn.Value,
                new CountdownSettings(isNightCountdownOn.Value, isWaveCountdownOn.Value));

            log($"hold fast key {settings.HoldFastKey}, hold slow key {settings.HoldSlowKey}, speed keys 5 to 9 {DescribeCustom(settings)}, " +
                $"speed wheel {OnOff(settings.IsWheelOn)}, order finish time {OnOff(settings.IsOrderFinishOn)}, " +
                $"night countdown {OnOff(settings.Countdowns.IsNightOn)}, wave countdown {OnOff(settings.Countdowns.IsWaveOn)}");
            SpeedPatches.KeySettings = settings;
        }, holdFastKeyText, holdSlowKeyText, customSpeedsText, isWheelOn, isOrderFinishOn, isNightCountdownOn, isWaveCountdownOn);
    }

    private static string DescribeCustom(KeysSettings settings) => settings.CustomSpeeds.Any(speed => speed != null)
        ? string.Join(", ", settings.CustomSpeeds.Select(speed => speed is { } scale ? SlowGear.Label(scale) : "none"))
        : "off";

    private static string OnOff(bool isOn) => isOn ? "on" : "off";

    private static KeyCode ParseKey(string setting, string text, Action<string> log)
    {
        if (Enum.TryParse<KeyCode>(text.Trim(), ignoreCase: true, out var key)) return key;

        log($"{setting} '{text}' is not a Unity key name, so that hold key is off");

        return KeyCode.None;
    }
}
