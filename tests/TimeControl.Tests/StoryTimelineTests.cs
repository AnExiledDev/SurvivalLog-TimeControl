using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class StoryTransitionTests
{
    private static readonly IntPtr OldWorld = new(0x1000);
    private static readonly IntPtr NewWorld = new(0x2000);

    [Fact]
    public void The_move_into_the_disaster_is_under_way_while_the_old_world_still_runs()
    {
        var transition = new StoryTransition(OldWorld, StartedAt: 100f);

        Assert.True(transition.IsUnderWay(OldWorld, 100f));
        Assert.True(transition.IsUnderWay(OldWorld, 104.5f));
    }

    [Fact]
    public void A_new_world_means_the_move_has_finished()
    {
        Assert.False(new StoryTransition(OldWorld, 100f).IsUnderWay(NewWorld, 101f));
        Assert.False(new StoryTransition(OldWorld, 100f).IsUnderWay(IntPtr.Zero, 101f));
        Assert.False(new StoryTransition(IntPtr.Zero, 100f).IsUnderWay(IntPtr.Zero, 101f));
    }

    [Fact]
    public void A_world_kept_for_the_next_one_stops_holding_the_pauses_back_after_a_while()
    {
        var transition = new StoryTransition(OldWorld, StartedAt: 100f);

        Assert.True(transition.IsUnderWay(OldWorld, 100f + StoryTransition.MaxSeconds - 1f));
        Assert.False(transition.IsUnderWay(OldWorld, 100f + StoryTransition.MaxSeconds + 1f));
        Assert.False(transition.IsUnderWay(OldWorld, 3f));
    }
}

public class StoryTimelineTests
{
    [Fact]
    public void A_story_timeline_plays_until_its_length_has_passed_in_game_time()
    {
        var timeline = new StoryTimeline(StartedAt: 100f, LengthSeconds: 8d);

        Assert.True(timeline.IsPlaying(100f));
        Assert.True(timeline.IsPlaying(107.9f));
        Assert.False(timeline.IsPlaying(100f + 8f + StoryTimeline.MarginSeconds + 0.1f));
    }

    [Fact]
    public void A_paused_story_timeline_is_still_playing_however_long_the_pause()
    {
        // Game time stands still while paused, so the clock read on every check is the one it started at.
        var timeline = new StoryTimeline(StartedAt: 100f, LengthSeconds: 8d);

        Assert.True(timeline.IsPlaying(100.5f));
    }

    [Fact]
    public void Game_time_before_the_start_is_another_sessions_clock_so_nothing_plays()
    {
        Assert.False(new StoryTimeline(StartedAt: 500f, LengthSeconds: 8d).IsPlaying(3f));
    }

    [Fact]
    public void A_length_that_is_not_a_number_counts_as_none()
    {
        var timeline = new StoryTimeline(StartedAt: 100f, LengthSeconds: double.NaN);

        Assert.True(timeline.IsPlaying(100f));
        Assert.False(timeline.IsPlaying(100f + StoryTimeline.MarginSeconds + 0.1f));
        Assert.False(new StoryTimeline(100f, -5d).IsPlaying(100f + StoryTimeline.MarginSeconds + 0.1f));
    }
}
