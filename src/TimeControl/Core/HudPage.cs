using System;

namespace TimeControl.Core;

/// <summary>
/// The game's two HUD pages. Before the disaster it opens only its countdown HUD (CoreUI0), which has no speed
/// buttons; from the disaster on, at home and on expeditions, it opens its main HUD (CoreUI1), which has them.
/// </summary>
public enum HudPage
{
    Countdown,
    Home,
}

public static class HudPages
{
    public static string FileName(HudPage page) => page switch
    {
        HudPage.Countdown => "CoreUI0",
        HudPage.Home => "CoreUI1",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "a HUD page with no file"),
    };

    /// <summary>
    /// Finds the page's frame from the root page as <c>w</c>, the same way WebUI Doctor's clock watch does, and
    /// returns from the script when the page is not open.
    /// </summary>
    internal static string FindFrame(HudPage page) => FindFrame(FileName(page), $"'no {FileName(page)} frame'");

    /// <summary>
    /// Whether a script's answer says its HUD page is not open, as <see cref="FindFrame(HudPage)"/> answers. Nothing was
    /// lost then: the page watch sees the page open, which sends what goes on it again.
    /// </summary>
    public static bool IsClosedReply(string? reply) => reply == ClosedCountdown || reply == ClosedHome;

    /// <summary>
    /// Whether an answer settles what was sent, so it is not sent again on the next refresh: the page answered "ok", or
    /// the HUD page it goes on is not open.
    /// </summary>
    public static bool IsSettled(string? reply) => reply == "ok" || IsClosedReply(reply);

    private static readonly string ClosedCountdown = $"no {FileName(HudPage.Countdown)} frame";
    private static readonly string ClosedHome = $"no {FileName(HudPage.Home)} frame";

    /// <summary>
    /// Finds any web UI page's frame by its file name as <c>w</c>, and returns the given reply when the page is not
    /// open. The name must follow a slash, so a page whose name only ends in it is not taken for it.
    /// </summary>
    /// <param name="replyWhenClosed">A JavaScript expression the script returns when the page is not open.</param>
    internal static string FindFrame(string fileName, string replyWhenClosed) =>
        "var fs=document.querySelectorAll('iframe'),w=null;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        $"try{{if(!/\\/{fileName}\\.html/i.test(String(c.location)))continue;}}catch(e){{continue;}}w=c;break;}}" +
        $"if(!w)return {replyWhenClosed};";

    /// <summary>
    /// Removes the element with this id from every frame, or from every frame but the given page's, so something the
    /// mod moved to another page does not stay behind on the one it left.
    /// </summary>
    internal static string RemoveFromFrames(string elementId, HudPage? keepOn = null)
    {
        var skipKept = keepOn is { } page ? $"if(/{FileName(page)}\\.html/i.test(String(c.location)))continue;" : "";

        return "(function(){var fs=document.querySelectorAll('iframe');" +
               "for(var i=0;i<fs.length;i++){try{var c=fs[i].contentWindow;if(!c)continue;" + skipKept +
               $"var o=c.document.getElementById('{elementId}');if(o&&o.parentNode)o.parentNode.removeChild(o);}}catch(e){{}}}}}})();";
    }
}
