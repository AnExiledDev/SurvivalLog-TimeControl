namespace TimeControl.Core;

/// <summary>
/// What the CoreUI1 page should show for a held slow speed: the slow button's tooltip carries the slow speed in use.
/// The page's own clock is left to the game.
/// </summary>
public readonly record struct PageClockUpdate(string SlowLabel);

public static class PageClock
{
    /// <summary>
    /// The update to send now, or null when the page needs nothing. While no slow speed is held the page is left to the
    /// game; on the change back, one last update strips the label.
    /// </summary>
    public static PageClockUpdate? Next(string slowLabel, bool wasNeutralLastSent) =>
        IsNeutral(slowLabel) && wasNeutralLastSent ? null : new PageClockUpdate(slowLabel);

    /// <summary>True when the mod changes nothing the page shows: no slow speed is held.</summary>
    public static bool IsNeutral(string slowLabel) => slowLabel.Length == 0;

    public static string Script(PageClockUpdate update)
    {
        var suffix = update.SlowLabel.Length == 0 ? "" : " " + Sanitize(update.SlowLabel);

        return "(function(){try{" + HudPages.FindFrame(HudPage.Home) +
               "var s=w.eval('state');" +
               $"var t=String(s.i18n_speedSlow||''),b=t.replace(/ \\d+(\\.\\d+)?x$/,''),n=b+'{suffix}';if(b.length&&t!==n)s.i18n_speedSlow=n;" +
               "return 'ok';}catch(e){return 'error: '+e;}})()";
    }

    /// <summary>The label is spliced into a quoted script string, so only the characters a speed label uses survive.</summary>
    internal static string Sanitize(string label)
    {
        var kept = new char[label.Length];
        var count = 0;

        foreach (var ch in label)
        {
            if (ch is >= '0' and <= '9' || ch == '.' || ch == 'x') kept[count++] = ch;
        }

        return new string(kept, 0, count);
    }
}
