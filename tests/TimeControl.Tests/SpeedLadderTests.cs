using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The custom speeds on the keys 5 to 9, and the ladder of speeds the mouse wheel steps through.</summary>
public class SpeedLadderTests
{
    private const float DoubleScale = 2f;
    private const float FastScale = 3f;

    private static readonly float[] SlowSpeeds = { 0.5f, 0.25f };
    private static readonly float[] FastMultiples = { 2f };
    private static readonly float?[] Customs = { 0.75f, null, 12f };

    private static IReadOnlyList<LadderStep> Ladder(float?[]? customs = null) =>
        SpeedLadder.Build(SlowSpeeds, DoubleScale, FastScale, FastMultiples, customs ?? Customs);

    [Fact]
    public void Each_custom_speed_stays_on_its_own_key_even_when_one_before_it_is_empty_or_unreadable()
    {
        Assert.Equal(new float?[] { 0.75f, null, 12f }, SpeedLadder.ParseCustom("0.75, ,12"));
        Assert.Equal(new float?[] { null, 2f }, SpeedLadder.ParseCustom("fast,2"));
        Assert.Equal(new float?[] { null }, SpeedLadder.ParseCustom("NaN"));
    }

    [Fact]
    public void Custom_speeds_are_kept_between_the_slowest_slow_speed_and_the_speed_cap()
    {
        Assert.Equal(new float?[] { SlowGear.Slowest, FastGear.FastestScale }, SpeedLadder.ParseCustom("0.01,100"));
    }

    [Fact]
    public void Only_the_keys_5_to_9_get_a_speed()
    {
        Assert.Equal(SpeedLadder.MaxCustomSpeeds, SpeedLadder.ParseCustom("1,2,3,4,5,6,7").Count);
    }

    [Fact]
    public void An_empty_setting_puts_no_speed_on_any_key()
    {
        Assert.Empty(SpeedLadder.ParseCustom(""));
        Assert.Empty(SpeedLadder.ParseCustom("   "));
    }

    [Theory]
    [InlineData(0.5f, PauseToggle.Normal)]
    [InlineData(1.9f, PauseToggle.Normal)]
    [InlineData(2f, PauseToggle.Double)]
    [InlineData(2.9f, PauseToggle.Double)]
    [InlineData(3f, PauseToggle.Triple)]
    [InlineData(12f, PauseToggle.Triple)]
    public void A_custom_speed_reads_as_the_game_level_the_game_would_give_it(float scale, int level)
    {
        Assert.Equal(level, SpeedLadder.LevelFor(scale, DoubleScale, FastScale));
    }

    [Fact]
    public void The_ladder_runs_slowest_to_fastest_through_slow_speeds_game_speeds_fast_gears_and_custom_speeds()
    {
        var ladder = Ladder();

        Assert.Equal(new[] { 0.25f, 0.5f, 0.75f, 1f, 2f, 3f, 6f, 12f }, ladder.Select(step => step.Scale));
        Assert.Equal(new SpeedTarget.SlowSpeed(1), ladder[0].Target);
        Assert.Equal(new SpeedTarget.Custom(0.75f), ladder[2].Target);
        Assert.Equal(new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, FastGear.GamesOwn)), ladder[5].Target);
        Assert.Equal(new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, 1)), ladder[6].Target);
    }

    [Fact]
    public void A_custom_speed_on_the_same_scale_as_a_game_speed_or_fast_gear_is_one_stop_reached_the_games_way()
    {
        var ladder = Ladder(new float?[] { 2f, 6f });

        Assert.Equal(6, ladder.Count);
        Assert.Equal(new SpeedTarget.Level(new GameSpeed(PauseToggle.Double, FastGear.GamesOwn)), ladder.Single(step => step.Scale == 2f).Target);
        Assert.Equal(new SpeedTarget.Level(new GameSpeed(PauseToggle.Triple, 1)), ladder.Single(step => step.Scale == 6f).Target);
    }

    [Theory]
    [InlineData(1f, 1, 2f)]
    [InlineData(1f, -2, 0.5f)]
    [InlineData(3f, 10, 12f)]
    [InlineData(0.75f, -10, 0.25f)]
    public void The_wheel_moves_that_many_stops_and_holds_at_the_ends(float current, int steps, float expected)
    {
        Assert.Equal(expected, SpeedLadder.Walk(Ladder(), current, steps)?.Scale);
    }

    [Theory]
    [InlineData(12f, 1)]
    [InlineData(0.25f, -1)]
    [InlineData(1f, 0)]
    public void Turning_past_an_end_or_not_at_all_changes_nothing(float current, int steps)
    {
        Assert.Null(SpeedLadder.Walk(Ladder(), current, steps));
    }

    [Theory]
    [InlineData(1.5f, 1, 2f)]
    [InlineData(1.5f, 2, 3f)]
    [InlineData(1.5f, -1, 1f)]
    [InlineData(1.5f, -2, 0.75f)]
    public void From_a_speed_between_two_stops_the_first_notch_goes_to_the_nearer_stop_that_way(float current, int steps, float expected)
    {
        Assert.Equal(expected, SpeedLadder.Walk(Ladder(), current, steps)?.Scale);
    }

    [Fact]
    public void A_speed_within_a_hair_of_a_stop_counts_as_on_it()
    {
        Assert.Equal(3f, SpeedLadder.Walk(Ladder(), 2.0005f, 1)?.Scale);
    }
}
