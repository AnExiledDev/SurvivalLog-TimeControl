using System;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>
/// The mouse wheel over the speed buttons, the game's own or the mod's copy, steps the speed up or down. The page
/// counts the wheel's notches and the plugin takes the count on its next check, as it takes clicks on its buttons.
/// While the wheel is on, a turn over the buttons is kept from the rest of the page, so the camera does not zoom too.
/// </summary>
public static class SpeedWheel
{
    private const string Ok = "ok";
    private const string StepsPrefix = "ok:";

    /// <summary>The notches turned since the last take, up counting positive; kept on each page's window.</summary>
    private const string StepsQueue = "__tcWheelSteps";

    private const string IsOnFlag = "__tcWheelOn";

    /// <summary>Marks a group of speed buttons that already has the listener, so each gets exactly one.</summary>
    private const string HookedMark = "__tcWheel";

    /// <summary>
    /// Switches the wheel on or off on both HUD pages, gives every group of speed buttons the listener once, and takes
    /// the notches turned since the last call.
    /// </summary>
    /// <returns>A script replying "ok", or "ok:" and the sum of the notches when any were turned.</returns>
    public static string Script(bool isOn) => isOn ? OnScript : OffScript;

    /// <summary>
    /// Only takes the notches turned since the last take, on both HUD pages; the small script sent on the checks
    /// between the ones that hook buttons.
    /// </summary>
    /// <returns>A script replying "ok", or "ok:" and the sum of the notches when any were turned.</returns>
    public static readonly string PollScript =
        "(function(){try{var fs=document.querySelectorAll('iframe'),sum=0;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        $"try{{if(!c.{StepsQueue})continue;sum+=c.{StepsQueue};c.{StepsQueue}=0;}}catch(e){{continue;}}}}" +
        $"return sum?'{StepsPrefix}'+sum:'{Ok}';}}catch(e){{return 'error: '+e;}}}})()";

    private static readonly string OnScript = Build(isOn: true);
    private static readonly string OffScript = Build(isOn: false);

    private static string Build(bool isOn) =>
        "(function(){try{var fs=document.querySelectorAll('iframe'),sum=0;" +
        $"var hook=function(c){{return function(e){{if(!c.{IsOnFlag})return;" +
        "if(e.preventDefault)e.preventDefault();if(e.stopPropagation)e.stopPropagation();" +
        $"var s=e.deltaY<0?1:e.deltaY>0?-1:0;if(s)c.{StepsQueue}=(c.{StepsQueue}||0)+s;}};}};" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        $"try{{if(!/\\/({HudPages.FileName(HudPage.Countdown)}|{HudPages.FileName(HudPage.Home)})\\.html/i.test(String(c.location)))continue;}}catch(e){{continue;}}" +
        $"c.{IsOnFlag}={(isOn ? "true" : "false")};" +
        "var gs=c.document.querySelectorAll('.speed-controls');" +
        $"for(var j=0;j<gs.length;j++){{if(gs[j].{HookedMark})continue;gs[j].{HookedMark}=true;" +
        "gs[j].addEventListener('wheel',hook(c),{passive:false});}" +
        $"sum+=c.{StepsQueue}||0;c.{StepsQueue}=0;}}" +
        $"return sum?'{StepsPrefix}'+sum:'{Ok}';}}catch(e){{return 'error: '+e;}}}})()";

    public static bool IsOk(string? reply) => reply == Ok || (reply?.StartsWith(StepsPrefix, StringComparison.Ordinal) ?? false);

    /// <summary>The notches the page counted, up positive and down negative; 0 for any other reply.</summary>
    public static int Steps(string? reply)
    {
        if (reply == null || !reply.StartsWith(StepsPrefix, StringComparison.Ordinal)) return 0;

        return int.TryParse(reply.Substring(StepsPrefix.Length), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var steps) ? steps : 0;
    }
}
