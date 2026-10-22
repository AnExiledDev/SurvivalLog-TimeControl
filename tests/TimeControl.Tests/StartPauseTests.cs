using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Start paused: a world the game enters starts paused at home or in the countdown, once it has settled.</summary>
public class StartPauseTests
{
    [Theory]
    [InlineData(GameTimer.DayClock)]
    [InlineData(GameTimer.PrologueCountdown)]
    public void A_save_entered_at_home_or_in_the_countdown_starts_paused_once(GameTimer timer)
    {
        var step = StartPause.Next(StartPause.Entered, new StartSnapshot(IsOn: true, timer, IsNormalMode: true));

        Assert.Equal(new StartStep(StartPause.Done, ShouldPause: true), step);
        Assert.False(StartPause.Next(step.State, new StartSnapshot(true, timer, true)).ShouldPause);
    }

    [Fact]
    public void With_start_paused_off_nothing_is_paused()
    {
        var step = StartPause.Next(StartPause.Entered, new StartSnapshot(IsOn: false, GameTimer.DayClock, IsNormalMode: true));

        Assert.Equal(new StartStep(StartPause.Done, ShouldPause: false), step);
    }

    [Fact]
    public void An_expedition_is_left_running()
    {
        var step = StartPause.Next(StartPause.Entered, new StartSnapshot(IsOn: true, GameTimer.Expedition, IsNormalMode: true));

        Assert.Equal(new StartStep(StartPause.Done, ShouldPause: false), step);
    }

    [Fact]
    public void The_pause_waits_for_the_timer_and_the_mode_to_settle()
    {
        var state = StartPause.Entered;

        for (var i = 0; i < StartPause.MostChecks - 1; i++)
        {
            var settling = StartPause.Next(state, new StartSnapshot(true, i % 2 == 0 ? GameTimer.Other : GameTimer.DayClock, IsNormalMode: false));

            Assert.False(settling.ShouldPause);

            state = settling.State;
        }

        Assert.True(StartPause.Next(state, new StartSnapshot(true, GameTimer.DayClock, IsNormalMode: true)).ShouldPause);
    }

    [Fact]
    public void A_guided_start_or_story_scene_that_never_settles_is_left_running()
    {
        var state = StartPause.Entered;

        for (var i = 0; i < StartPause.MostChecks; i++) state = StartPause.Next(state, new StartSnapshot(true, GameTimer.DayClock, IsNormalMode: false)).State;

        Assert.Equal(StartPause.Done, state);
        Assert.False(StartPause.Next(state, new StartSnapshot(true, GameTimer.DayClock, IsNormalMode: true)).ShouldPause);
    }
}
