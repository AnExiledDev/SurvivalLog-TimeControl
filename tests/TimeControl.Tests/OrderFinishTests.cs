using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>When the character's current order is done, by the game clock, as shown under the clock at home.</summary>
public class OrderFinishTests
{
    private const int Hour = 3600;
    private const int Day = 24 * Hour;

    [Fact]
    public void The_note_gives_the_clock_time_the_order_is_done()
    {
        Assert.Equal("Current order done at 14:30", OrderFinish.Label(14 * Hour, 1800, isChinese: false));
    }

    [Fact]
    public void Whole_days_on_the_clock_are_dropped()
    {
        Assert.Equal("Current order done at 09:05", OrderFinish.Label(5 * Day + 9 * Hour, 300, isChinese: false));
    }

    [Fact]
    public void An_order_done_after_midnight_shows_the_next_days_time()
    {
        Assert.Equal("Current order done at 00:10", OrderFinish.Label(23 * Hour + 50 * 60, 1200, isChinese: false));
    }

    [Fact]
    public void Seconds_short_of_a_minute_do_not_round_the_time_up()
    {
        Assert.Equal("Current order done at 14:00", OrderFinish.Label(14 * Hour, 59, isChinese: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void With_no_order_running_there_is_no_note(int secondsLeft)
    {
        Assert.Equal("", OrderFinish.Label(14 * Hour, secondsLeft, isChinese: false));
    }

    [Fact]
    public void An_unreadable_clock_shows_no_note()
    {
        Assert.Equal("", OrderFinish.Label(-1, 1800, isChinese: false));
    }

    [Fact]
    public void In_chinese_the_note_uses_the_games_word_for_an_order()
    {
        Assert.Equal("当前行为完成于 14:30", OrderFinish.Label(14 * Hour, 1800, isChinese: true));
    }
}
