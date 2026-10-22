using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Times of day as the settings write them, and moments on the running clock worked out from them.</summary>
public class ClockHourTests
{
    private const int H = ClockHour.Hour;

    [Theory]
    [InlineData("7", 7 * H)]
    [InlineData("07", 7 * H)]
    [InlineData("0", 0)]
    [InlineData("23", 23 * H)]
    [InlineData("7:30", 7 * H + 30 * 60)]
    [InlineData("07:05", 7 * H + 5 * 60)]
    [InlineData("21:30", 21 * H + 30 * 60)]
    [InlineData("23:59", 23 * H + 59 * 60)]
    [InlineData("  6:30 ", 6 * H + 30 * 60)]
    public void A_time_of_day_on_a_24_hour_clock_is_read(string text, int expected)
    {
        Assert.True(ClockHour.TryParse(text, out var secondOfDay));
        Assert.Equal(expected, secondOfDay);
    }

    [Fact]
    public void The_colon_a_chinese_keyboard_types_is_read_too()
    {
        Assert.True(ClockHour.TryParse("21" + (char)0xFF1A + "30", out var secondOfDay));
        Assert.Equal(21 * H + 30 * 60, secondOfDay);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_setting_means_no_time_set(string text)
    {
        Assert.True(ClockHour.TryParse(text, out var secondOfDay));
        Assert.Null(secondOfDay);
    }

    [Fact]
    public void A_missing_setting_means_no_time_set()
    {
        Assert.True(ClockHour.TryParse(null, out var secondOfDay));
        Assert.Null(secondOfDay);
    }

    [Theory]
    [InlineData("24")]
    [InlineData("7:5")]
    [InlineData("7:60")]
    [InlineData("7:300")]
    [InlineData("abc")]
    [InlineData("1:2:3")]
    [InlineData("-1")]
    [InlineData("+7")]
    [InlineData("7.5")]
    [InlineData("100")]
    [InlineData(":30")]
    [InlineData("7:")]
    public void Anything_else_is_unreadable_and_sets_no_time(string text)
    {
        Assert.False(ClockHour.TryParse(text, out var secondOfDay));
        Assert.Null(secondOfDay);
    }

    [Theory]
    [InlineData(6 * H, 7 * H, H)]
    [InlineData(23 * H, 1 * H, 2 * H)]
    [InlineData(7 * H, 7 * H, ClockHour.Day)]
    [InlineData(8 * H, 7 * H, 23 * H)]
    public void The_next_time_of_day_is_always_ahead_and_a_whole_day_on_when_it_is_now(int from, int to, int expected)
    {
        Assert.Equal(expected, ClockHour.SecondsUntil(from, to));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(7 * H + 5 * 60, "07:05")]
    [InlineData(23 * H + 59 * 60 + 59, "23:59")]
    [InlineData(ClockHour.Day + H, "01:00")]
    [InlineData(-H, "23:00")]
    public void A_time_of_day_shows_as_hours_and_minutes_and_wraps_past_midnight(int secondOfDay, string expected)
    {
        Assert.Equal(expected, ClockHour.Label(secondOfDay));
    }

    [Fact]
    public void A_reading_finds_the_next_time_the_clock_shows_a_time_of_day()
    {
        var reading = new ClockReading(100_000, 22 * H);

        Assert.Equal(100_000 + 9 * H, reading.Next(ClockHour.Morning));
        Assert.Equal(100_000 + ClockHour.Day, reading.Next(22 * H));
    }

    [Fact]
    public void A_reading_tells_the_time_of_day_of_a_later_moment_on_the_same_clock()
    {
        var reading = new ClockReading(100_000, 22 * H);

        Assert.Equal(2 * H, reading.SecondOfDayAt(100_000 + 4 * H));
        Assert.Equal(21 * H, reading.SecondOfDayAt(100_000 - H));
    }
}
