using System.Text;

namespace TimeControl.Core;

/// <summary>Which pauses get a badge; fixed for the session.</summary>
public readonly record struct BadgeSettings(bool IsIdleBadgeOn, bool IsPauseKeyBadgeOn);

/// <summary>
/// A small chip on the game's HUD saying why the game stands still, so a stopped drone or a frozen clock never looks
/// like a bug. It wears the game's own idle-slowdown chip style (which the game ships but never shows) and sits where
/// that chip would, just above the speed buttons; where the page has no speed buttons it sits at the top centre.
/// </summary>
public static class PauseBadge
{
    public const string IdleText = "Paused: idle";
    public const string PauseKeyText = "Paused";

    /// <summary>The element the script owns in the page, found again by this id after every reload.</summary>
    private const string ElementId = "tc-pause-badge";

    /// <summary>The class of the game's own idle-slowdown chip, styled by the page's own stylesheet.</summary>
    private const string NativeChipClass = "idle-slow-chip";

    /// <summary>
    /// What the badge says now; empty hides it. Only a pause still holding the game gets one, and the idle pause
    /// names itself because it is the one that looks like a bug; any other pause keeps the game's own display.
    /// </summary>
    /// <param name="isIdleHolding">The idle pause made the pause and still owns it.</param>
    /// <param name="isPauseKeyHolding">The player's last speed change through the mod's keys was a pause.</param>
    public static string Text(BadgeSettings settings, bool isPaused, bool isIdleHolding, bool isPauseKeyHolding)
    {
        if (!isPaused) return "";

        if (isIdleHolding) return settings.IsIdleBadgeOn ? IdleText : "";

        return isPauseKeyHolding && settings.IsPauseKeyBadgeOn ? PauseKeyText : "";
    }

    /// <summary>
    /// A change goes out at once, so the badge leaves the moment time runs. A shown badge is also sent again on every
    /// page refresh, which puts it back after the page reloads or the speed buttons are redrawn. A removal is sent
    /// again on each refresh until the page confirms it, so a lost one cannot leave a stale badge up.
    /// </summary>
    /// <param name="isLastSentConfirmed">The page answered "ok" to the last text sent.</param>
    public static bool ShouldSend(string text, string lastSent, bool isLastSentConfirmed, bool isRefreshDue) =>
        text != lastSent || (isRefreshDue && (text.Length > 0 || !isLastSentConfirmed));

    public static string Script(string text) =>
        "(function(){try{" + PageClock.FindFrame +
        $"var d=w.document,b=d.getElementById('{ElementId}'),t={Quote(text)};" +
        "if(!t.length){if(b&&b.parentNode)b.parentNode.removeChild(b);return 'ok';}" +
        $"if(!b){{b=d.createElement('div');b.id='{ElementId}';b.className='{NativeChipClass}';}}" +
        "var a=d.querySelector('.speed-controls');" +
        "if(a){if(b.parentNode!==a)a.appendChild(b);b.style.cssText='';}" +
        "else{if(b.parentNode!==d.body)d.body.appendChild(b);" +
        "b.style.cssText='position:fixed;top:12px;left:50%;right:auto;bottom:auto;transform:translateX(-50%);z-index:9999;';}" +
        "if(b.textContent!==t)b.textContent=t;" +
        "return 'ok';}catch(e){return 'error: '+e;}})()";

    /// <summary>A JavaScript string literal for the text: quotes and backslashes escaped, anything outside printable ASCII as a \u escape.</summary>
    public static string Quote(string text)
    {
        var quoted = new StringBuilder("'", text.Length + 2);

        foreach (var ch in text)
        {
            if (ch is '\'' or '\\') quoted.Append('\\').Append(ch);
            else if (ch is >= ' ' and <= '~') quoted.Append(ch);
            else quoted.Append("\\u").Append(((int)ch).ToString("x4"));
        }

        return quoted.Append('\'').ToString();
    }
}
