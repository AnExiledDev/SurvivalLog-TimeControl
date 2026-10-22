using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>How long until night and the next wave, by the game clock, as shown under the clock at home.</summary>
public class CountdownsTests
{
    private const int Hour = 3600;
    private const int Day = 24 * Hour;

    private static readonly CountdownSettings BothOn = new(IsNightOn: true, IsWaveOn: true);

    [Theory]
    [InlineData(7 * Hour, 12 * Hour)]
    [InlineData(19 * Hour - 30, 30)]
    [InlineData(3 * Day + 15 * Hour, 4 * Hour)]
    public void During_the_day_night_counts_down_to_19_00(int clock, int expected)
    {
        Assert.Equal(expected, Countdowns.NightIn(clock));
    }

    [Theory]
    [InlineData(19 * Hour)]
    [InlineData(23 * Hour)]
    [InlineData(Day + 3 * Hour)]
    [InlineData(7 * Hour - 1)]
    public void At_night_there_is_no_night_countdown(int clock)
    {
        Assert.Null(Countdowns.NightIn(clock));
    }

    [Fact]
    public void The_wave_countdown_goes_to_the_soonest_wave_still_to_come_today()
    {
        Assert.Equal(3 * Hour, Countdowns.WaveIn(2 * Day + 10 * Hour, new[] { 9, 22, 13 }, lureSecondsLeft: 0));
    }

    [Fact]
    public void A_lure_rigs_wave_counts_when_it_comes_first()
    {
        Assert.Equal(1800, Countdowns.WaveIn(10 * Hour, new[] { 13 }, lureSecondsLeft: 1800));
        Assert.Equal(3 * Hour, Countdowns.WaveIn(10 * Hour, new[] { 13 }, lureSecondsLeft: 5 * Hour));
        Assert.Equal(1800, Countdowns.WaveIn(10 * Hour, Array.Empty<int>(), lureSecondsLeft: 1800));
    }

    [Fact]
    public void With_every_wave_started_or_past_there_is_no_wave_countdown()
    {
        Assert.Null(Countdowns.WaveIn(13 * Hour, new[] { 9, 13 }, lureSecondsLeft: 0));
        Assert.Null(Countdowns.WaveIn(13 * Hour, Array.Empty<int>(), lureSecondsLeft: 0));
    }

    [Fact]
    public void Each_countdown_that_is_on_gets_its_own_line()
    {
        Assert.Equal("Night in 3h 20m\nWave in 45m", Countdowns.Text(12000, 2700, BothOn, isChinese: false));
        Assert.Equal("Wave in 45m", Countdowns.Text(12000, 2700, BothOn with { IsNightOn = false }, isChinese: false));
        Assert.Equal("Night in 3h 20m", Countdowns.Text(12000, 2700, BothOn with { IsWaveOn = false }, isChinese: false));
    }

    [Fact]
    public void A_countdown_with_nothing_to_count_down_to_shows_no_line()
    {
        Assert.Equal("Wave in 45m", Countdowns.Text(null, 2700, BothOn, isChinese: false));
        Assert.Equal("", Countdowns.Text(null, null, BothOn, isChinese: false));
        Assert.Equal("", Countdowns.Text(12000, 2700, new CountdownSettings(false, false), isChinese: false));
    }

    [Fact]
    public void In_chinese_the_countdowns_use_the_games_words()
    {
        Assert.Equal("距入夜 3小时20分钟\n距尸潮 45分钟", Countdowns.Text(12000, 2700, BothOn, isChinese: true));
    }

    [Theory]
    [InlineData(1, "1m")]
    [InlineData(59, "1m")]
    [InlineData(60, "1m")]
    [InlineData(61, "2m")]
    [InlineData(3600, "1h")]
    [InlineData(3601, "1h 1m")]
    [InlineData(12000, "3h 20m")]
    public void Minutes_round_up_so_a_countdown_never_reads_zero_while_time_is_left(int seconds, string expected)
    {
        Assert.Equal(expected, Countdowns.Duration(seconds, isChinese: false));
    }
}
