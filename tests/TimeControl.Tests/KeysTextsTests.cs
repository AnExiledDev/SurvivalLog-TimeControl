using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>Every word under the clock has an English and a Chinese version, and the game's language picks between them.</summary>
public class KeysTextsTests
{
    public static TheoryData<int> Entries()
    {
        var data = new TheoryData<int>();

        foreach (var index in Enumerable.Range(0, KeysTexts.All.Count)) data.Add(index);

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_text_has_plain_english_and_a_real_chinese_translation(int index)
    {
        var phrase = KeysTexts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
        Assert.Contains(phrase.Zh, ch => ch is >= '\u4e00' and <= '\u9fff');
        Assert.NotEqual(phrase.En, phrase.Zh);
    }

    [Fact]
    public void No_text_is_listed_twice()
    {
        Assert.Equal(KeysTexts.All.Count, KeysTexts.All.Distinct().Count());
    }
}
