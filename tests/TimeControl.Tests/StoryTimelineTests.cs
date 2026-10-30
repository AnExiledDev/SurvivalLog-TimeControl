using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

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
