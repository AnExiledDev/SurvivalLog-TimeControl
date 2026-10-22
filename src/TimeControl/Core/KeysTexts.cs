using System.Collections.Generic;

namespace TimeControl.Core;

/// <summary>The words shown under the clock at home. Chinese uses the game's own terms.</summary>
public static class KeysTexts
{
    public static readonly Phrase OrderDoneAt = new("Current order done at", "当前行为完成于");
    public static readonly Phrase NightIn = new("Night in", "距入夜");
    public static readonly Phrase WaveIn = new("Wave in", "距尸潮");
    public static readonly Phrase Hours = new("h", "小时");
    public static readonly Phrase Minutes = new("m", "分钟");

    public static IReadOnlyList<Phrase> All { get; } = new[] { OrderDoneAt, NightIn, WaveIn, Hours, Minutes };
}
