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
    internal static string FindFrame(HudPage page) =>
        "var fs=document.querySelectorAll('iframe'),w=null;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        $"try{{if(!/{FileName(page)}\\.html/i.test(String(c.location)))continue;}}catch(e){{continue;}}w=c;break;}}" +
        $"if(!w)return 'no {FileName(page)} frame';";

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
