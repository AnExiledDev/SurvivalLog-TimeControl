namespace TimeControl.Core;

/// <summary>
/// Small lines of text under the clock at home, inside the clock's own group so they move with it. They are
/// see-through to clicks and never marked interactive, so nothing under them is lost.
/// </summary>
internal static class ClockNotes
{
    private const string Style =
        "display:block;margin-top:2px;font-size:12px;line-height:1.3;color:#B3E5FC;text-align:center;" +
        "white-space:pre-line;pointer-events:none;text-shadow:0 1px 2px rgba(0,0,0,0.8);";

    /// <param name="elementId">The note's element, found again by this id after every reload.</param>
    /// <param name="text">What the note says; empty takes it off every page.</param>
    /// <param name="belowId">A note that always sits below this one; it is moved back under it when this one is added.</param>
    public static string Script(string elementId, string text, string? belowId = null)
    {
        if (text.Length == 0) return "(function(){try{" + HudPages.RemoveFromFrames(elementId) + "return 'ok';}catch(e){return 'error: '+e;}})()";

        var keepBelow = belowId == null ? "" : $"var k=d.getElementById('{belowId}');if(k&&k.parentNode===g)g.appendChild(k);";

        return "(function(){try{" + HudPages.RemoveFromFrames(elementId, keepOn: HudPage.Home) + HudPages.FindFrame(HudPage.Home) +
               $"var d=w.document,n=d.getElementById('{elementId}'),t={PauseBadge.Quote(text)},g=d.querySelector('.clock-group');" +
               "if(!g){if(n&&n.parentNode)n.parentNode.removeChild(n);return 'no clock on the page';}" +
               $"if(!n){{n=d.createElement('div');n.id='{elementId}';}}" +
               "if(n.parentNode!==g){g.appendChild(n);" + keepBelow + "}" +
               $"n.style.cssText='{Style}';" +
               "if(n.textContent!==t)n.textContent=t;" +
               "return 'ok';}catch(e){return 'error: '+e;}})()";
    }
}
