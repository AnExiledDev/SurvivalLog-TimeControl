namespace TimeControl.Core;

/// <summary>
/// What the CoreUI1 page should show for a held slow speed: the slow button's tooltip carries the slow speed in use.
/// The page's own clock is left to the game.
/// </summary>
public readonly record struct PageClockUpdate(string SlowLabel);

public static class PageClock
{
    /// <summary>
    /// Whether to send the label now. A change goes out at once, and a label the page has not confirmed goes again on
    /// each refresh. A held label goes again once the page changed under it, or once the page watch reads a slow text
    /// that does not carry it, as after the game rewrites its texts on a language change. While nothing changes,
    /// nothing is sent, and with no slow speed held the page is left to the game.
    /// </summary>
    /// <param name="lastSent">The label last sent; empty when none is held.</param>
    /// <param name="isLastSentConfirmed">The page answered "ok" to the last label sent.</param>
    /// <param name="hasPageChanged">The page watch saw the page change since the last label was sent.</param>
    /// <param name="pageText">The slow speed's text as the page watch last read it, or null when it has not.</param>
    public static bool ShouldSend(string slowLabel, string lastSent, bool isLastSentConfirmed, bool isRefreshDue, bool hasPageChanged, string? pageText) =>
        slowLabel != lastSent
        || (isRefreshDue && !isLastSentConfirmed)
        || (hasPageChanged && !IsNeutral(slowLabel))
        || (isRefreshDue && pageText != null && !IsShown(slowLabel, pageText));

    /// <summary>
    /// Whether the page's slow text already reads as the script would leave it: its own text with the label after it,
    /// or bare when no label is held. A text with nothing of its own yet counts as shown, since the script leaves it.
    /// </summary>
    public static bool IsShown(string slowLabel, string pageText)
    {
        var bare = BareLength(pageText);

        if (bare == 0) return true;

        if (IsNeutral(slowLabel)) return pageText.Length == bare;

        var at = bare + 1;

        if (pageText.Length < at || pageText[bare] != ' ') return false;

        foreach (var ch in slowLabel)
        {
            if (!IsLabelChar(ch)) continue;

            if (at >= pageText.Length || pageText[at] != ch) return false;

            at++;
        }

        return at == pageText.Length;
    }

    /// <summary>The length of the text with any speed label at its end cut off, as the script's / \d+(\.\d+)?x$/ cuts it.</summary>
    private static int BareLength(string text)
    {
        var at = text.Length - 1;

        if (at < 0 || text[at] != 'x') return text.Length;

        var digitsEnd = --at;

        while (at >= 0 && text[at] is >= '0' and <= '9') at--;

        if (at == digitsEnd) return text.Length;

        if (at >= 0 && text[at] == '.')
        {
            var wholeEnd = --at;

            while (at >= 0 && text[at] is >= '0' and <= '9') at--;

            if (at == wholeEnd) return text.Length;
        }

        return at >= 0 && text[at] == ' ' ? at : text.Length;
    }

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
            if (IsLabelChar(ch)) kept[count++] = ch;
        }

        return new string(kept, 0, count);
    }

    private static bool IsLabelChar(char ch) => ch is >= '0' and <= '9' || ch == '.' || ch == 'x';
}
