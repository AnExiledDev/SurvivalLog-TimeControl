using System;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>What one look at the page found.</summary>
/// <param name="Hash">A fingerprint of everything the mod's page scripts sit on; it changes when any of it does.</param>
/// <param name="SlowText">The slow speed's text on the main HUD page, empty when that page is not open.</param>
public readonly record struct PageLook(int Hash, string SlowText);

/// <summary>
/// One small script, sent at the page pace, that tells the plugin when the page changed under what the mod put on it:
/// a page opened, closed or reloaded, the game redrew a group of speed buttons or the clock or order queue the mod's
/// things hang from, a speed button moved, the bottom-left buttons hid or moved, or the language or a key binding
/// changed the texts the mod's tooltips read. The mod's badge, chip, notes and buttons are sent again only then, or
/// when what they show changes, rather than on every refresh.
/// </summary>
public static class PageWatch
{
    private const string Prefix = "ok:";

    /// <summary>
    /// The page texts the mod's speed button tooltips read, on both HUD pages. The slow speed's text is left out of
    /// the fingerprint, since the mod writes it itself, and handed back on its own instead.
    /// </summary>
    private const string StateKeys =
        "['i18n_speedNormal','i18n_speedFast','i18n_speedMax','i18n_speedPause','i18n_hotkey'," +
        "'i18n_hotkeySpeedSlow','i18n_hotkeySpeed1x','i18n_hotkeySpeed2x','i18n_hotkeySpeed3x','i18n_hotkeySpeedPause']";

    /// <summary>
    /// Each frame's window and each element the mod hangs something on gets a number of its own the first time it is
    /// seen, kept on the element, so one the game rebuilt reads as new even where it looks the same; the root page
    /// keeps a stamp of its own, so a reload of the whole web view, which starts the numbers again, still reads as new. Elements the mod
    /// made itself are only counted where they are the group of speed buttons, which the badge and the chip sit in.
    /// </summary>
    /// <returns>A script replying "ok:", the fingerprint, "|" and the slow speed's text.</returns>
    public static readonly string Script =
        "(function(){try{var R=document,fs=R.querySelectorAll('iframe'),h=0,st='';" +
        "var add=function(v){v=String(v);for(var k=0;k<v.length;k++)h=(h*31+v.charCodeAt(k))|0;h=(h*31+124)|0;};" +
        "var tag=function(o){if(!o.__tcTag)o.__tcTag=R.__tcTagN=(R.__tcTagN||0)+1;return o.__tcTag;};" +
        "if(!R.__tcRoot)R.__tcRoot=String(Date.now())+String(Math.random());add(R.__tcRoot);" +
        $"var K={StateKeys},A=['.clock-group','.queue-flow-wrapper','.action-group-middle'];" +
        "for(var i=0;i<fs.length;i++){try{var c=fs[i].contentWindow;if(!c)continue;var f=String(c.location),d=c.document;" +
        "add(f);add(tag(c));" +
        "var gs=d.querySelectorAll('.speed-controls');for(var j=0;j<gs.length;j++){add(tag(gs[j]));" +
        "var bs=gs[j].querySelectorAll('.speed-btn');for(var b=0;b<bs.length;b++)add(bs[b].offsetLeft+','+bs[b].offsetTop+','+bs[b].offsetWidth);}" +
        "for(var a=0;a<A.length;a++){var e=d.querySelector(A[a]);add(e?tag(e):0);}" +
        "var bl=d.querySelector('.bottom-left');add(!bl?'-':c.getComputedStyle(bl).display==='none'?'h':Math.round(bl.getBoundingClientRect().right));" +
        $"if(!/\\/({HudPages.FileName(HudPage.Countdown)}|{HudPages.FileName(HudPage.Home)})\\.html/i.test(f))continue;" +
        "var s=null;try{s=c.eval('state');}catch(e){}if(!s)continue;" +
        "for(var n=0;n<K.length;n++)add(s[K[n]]);" +
        $"if(/\\/{HudPages.FileName(HudPage.Home)}\\.html/i.test(f))st=String(s.i18n_speedSlow||'');" +
        "}catch(e){continue;}}" +
        $"return '{Prefix}'+h+'|'+st;}}catch(e){{return 'error: '+e;}}}})()";

    /// <summary>What the page's answer says, or null for an answer that is not one, such as an error.</summary>
    public static PageLook? Read(string? reply)
    {
        if (reply == null || !reply.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        var bar = reply.IndexOf('|', Prefix.Length);

        if (bar < 0) return null;

        if (!int.TryParse(reply.AsSpan(Prefix.Length, bar - Prefix.Length), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var hash)) return null;

        return new PageLook(hash, reply.Substring(bar + 1));
    }

    /// <summary>
    /// Counts the changes the plugin has seen. Each look that differs from the one before, or that could not be read,
    /// is a new generation, so a page the watch cannot read has everything sent again as it was before the watch.
    /// </summary>
    public static PageGeneration Next(PageGeneration seen, PageLook? look) =>
        look is { } read && seen.Last is { } last && last.Hash == read.Hash
            ? seen with { Last = read }
            : new PageGeneration(seen.Number + 1, look);
}

/// <summary>How many times the page changed, as far as the watch saw, and the last look at it.</summary>
public readonly record struct PageGeneration(int Number, PageLook? Last)
{
    public static readonly PageGeneration Start = new(0, null);
}
