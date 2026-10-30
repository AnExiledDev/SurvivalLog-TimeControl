using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TimeControl.Core;
using UnityEngine;

namespace TimeControl.Adapters;

/// <summary>
/// Whether a story scene is playing, such as the move into the disaster. That move starts as the countdown ends
/// (Director_CountDownEnd.Enter), which clears the character's order and waits on game time on a black screen; then
/// the story screen (UI_TransitionStory) shows, and the new world plays its timeline through
/// BattleMain.WaitTimelineOrSkip, which counts down with the frame time. While any of them is up the mod's own pauses
/// stand aside, or the screen stays black. Every read fails soft, logged at most a few times.
/// </summary>
internal static class StoryWatch
{
    private const int MaxLoggedFailures = 3;

    private static Action<string> _log = _ => { };
    private static bool _isHooked;
    private static StoryTimeline? _timeline;
    private static StoryTransition? _transition;
    private static Il2CppSystem.Type? _storyScreen;
    private static int _failures;

    public static void Hook(Harmony harmony, Action<string> log)
    {
        _log = log;

        if (_isHooked) return;

        _isHooked = true;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(Director_CountDownEnd), nameof(Director_CountDownEnd.Enter)), prefix: new HarmonyMethod(typeof(StoryWatch), nameof(BeforeCountDownEnd)));
            log("the move into the disaster watched");
        }
        catch (Exception ex)
        {
            log($"could not hook Director_CountDownEnd.Enter, idle freeze and alerts may still pause the move into the disaster: {ex.Message}");
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BattleMain), "WaitTimelineOrSkip"), prefix: new HarmonyMethod(typeof(StoryWatch), nameof(BeforeTimeline)));
            log("story scenes watched");
        }
        catch (Exception ex)
        {
            log($"could not hook BattleMain.WaitTimelineOrSkip, idle freeze and alerts may still pause a story scene: {ex.Message}");
        }
    }

    /// <summary>The move into the disaster, a story timeline or the story screen is up.</summary>
    public static bool IsPlaying()
    {
        try
        {
            if (_transition is { } transition)
            {
                if (transition.IsUnderWay(LiveWorld(), Time.time)) return true;

                _transition = null;
            }

            if (_timeline is { } timeline && timeline.IsPlaying(Time.time)) return true;

            var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

            return ui != null && ui.IsUIActive(_storyScreen ??= Il2CppType.Of<UI_TransitionStory>());
        }
        catch (Exception ex)
        {
            _failures++;

            if (_failures <= MaxLoggedFailures) _log($"story: failed reading whether a story scene plays (logged {_failures} of {MaxLoggedFailures}): {ex}");

            return false;
        }
    }

    private static IntPtr LiveWorld() =>
        BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance?.Pointer ?? IntPtr.Zero : IntPtr.Zero;

    private static void BeforeCountDownEnd()
    {
        try
        {
            _transition = new StoryTransition(LiveWorld(), Time.time);
            ClockTicker.LogFileOnly("story: the countdown ended, the mod's pauses wait until the disaster has started");
        }
        catch (Exception ex)
        {
            _failures++;

            if (_failures <= MaxLoggedFailures) _log($"story: failed marking the move into the disaster (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }

    private static void BeforeTimeline(double __0)
    {
        try
        {
            _timeline = new StoryTimeline(Time.time, __0);
        }
        catch (Exception ex)
        {
            _failures++;

            if (_failures <= MaxLoggedFailures) _log($"story: failed marking a story timeline (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }
}
