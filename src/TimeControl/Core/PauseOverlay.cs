using System;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>
/// The game's own "paused" strip: a dark band with the pause sign across the lower middle of the screen, drawn by its
/// SpeedUp page whenever the game stands still. The page already lets clicks through, but the band draws over
/// whatever sits there, such as the phone's reply choices, so the mod makes it see-through. It also keeps the band out
/// of hit-testing in its own right, in case the page ever stops letting clicks through. The game never unpauses from a
/// click on the band, so nothing is lost by that.
/// </summary>
public static class PauseOverlay
{
    public const float DefaultOpacity = 0.5f;

    private const string Ok = "ok";

    /// <summary>The game's page that draws the band.</summary>
    private const string PageFile = "SpeedUp";

    /// <summary>The style element the script owns in that page, found again by this id after every reload.</summary>
    private const string StyleId = "tc-pause-overlay-style";

    /// <summary>From 0 (gone) to 1 (as the game draws it); anything that is not a number keeps the default.</summary>
    public static float ClampOpacity(float opacity) => float.IsFinite(opacity) ? Math.Clamp(opacity, 0f, 1f) : DefaultOpacity;

    /// <summary>
    /// The style for the band only while it shows the pause, not the game's sleep or slowdown notices. Fully opaque
    /// leaves the game's band as it is, so the style is empty and none is put on the page.
    /// </summary>
    public static string Css(float opacity)
    {
        var clamped = ClampOpacity(opacity);
        var shown = clamped.ToString("0.##", CultureInfo.InvariantCulture);

        if (clamped >= 1f) return "";

        return $".speedup-strip.is-pause{{opacity:{shown}!important;pointer-events:none!important;}}";
    }

    /// <summary>
    /// Puts the style on the game's page, or with an empty style takes it off. With the page not open there is nothing
    /// to change, so that is "ok" too: the page opens again without the style, and the next refresh puts it back.
    /// </summary>
    public static string Script(string css) =>
        "(function(){try{" + HudPages.FindFrame(PageFile, $"'{Ok}'") +
        $"var d=w.document,s=d.getElementById('{StyleId}'),c={PauseBadge.Quote(css)};" +
        $"if(!c.length){{if(s&&s.parentNode)s.parentNode.removeChild(s);return '{Ok}';}}" +
        $"if(!s){{s=d.createElement('style');s.id='{StyleId}';(d.head||d.body).appendChild(s);}}" +
        "if(s.textContent!==c)s.textContent=c;" +
        $"return '{Ok}';}}catch(e){{return 'error: '+e;}}}})()";
}
