using System.Linq;
using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Every word the wait menu shows has an English and a Chinese version.</summary>
public class WaitTextsTests
{
    public static TheoryData<int> Entries()
    {
        var data = new TheoryData<int>();

        foreach (var index in Enumerable.Range(0, WaitTexts.All.Count)) data.Add(index);

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_text_has_plain_english_and_a_real_chinese_translation(int index)
    {
        var phrase = WaitTexts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
        Assert.Contains(phrase.Zh, ch => ch is >= '一' and <= '鿿');
        Assert.NotEqual(phrase.En, phrase.Zh);
    }

    [Fact]
    public void No_text_is_listed_twice()
    {
        Assert.Equal(WaitTexts.All.Count, WaitTexts.All.Distinct().Count());
    }

    [Fact]
    public void Hours_read_as_a_number_of_hours_and_as_hours_on_from_now()
    {
        Assert.Equal(new Phrase("8h", "8小时"), WaitTexts.Hours(8));
        Assert.Equal(new Phrase("+2h", "+2小时"), WaitTexts.HoursOn(2));
    }
}
