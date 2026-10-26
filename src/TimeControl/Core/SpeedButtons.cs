using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TimeControl.Core;

/// <summary>What the mod's speed buttons show: the page they go on, the level lit, and the slow speed held, if any.</summary>
public readonly record struct SpeedButtonsView(HudPage Page, int ActiveLevel, string SlowLabel, bool IsChinese = false);

/// <summary>
/// The game's speed buttons (slow, normal, fast, fastest and pause) are missing in the countdown to the disaster, whose
/// HUD page has none, and on expeditions, where the game keeps its unlocks locked so they never switch on. There the mod
/// draws a copy in the game's own style. A click goes through the same speed change as the game's button, so the slow
/// and fast speeds, the pause badge and the speed to resume to all behave as they do at home.
/// </summary>
public static class SpeedButtons
{
    internal const string ElementId = "tc-speed-buttons";
    private const string StyleId = "tc-speed-style";
    private const string ClicksQueue = "__tcSpeedClicks";
    private const string Ok = "ok";
    private const string ClicksPrefix = "ok:";

    /// <summary>The answer to <see cref="CollectScript"/> when the page has lost the buttons.</summary>
    public const string NotOnPage = "buttons not on the page";

    /// <summary>Defines take(), which empties the page's click queue and answers "ok", or "ok:" and the levels clicked.</summary>
    private const string DefineTakeClicks =
        $"var take=function(){{var q=w.{ClicksQueue}||[];w.{ClicksQueue}=[];return q.length?'{ClicksPrefix}'+q.join(','):'{Ok}';}};";

    /// <summary>
    /// The game's own look for its speed buttons, scoped to the mod's group: the countdown page has none of it, and the
    /// copy on the main page matches what that page already gives the group.
    /// </summary>
    private const string Style =
        "#tc-speed-buttons{position:relative;display:flex;gap:4px;background-color:rgba(30,30,30,0.85);padding:2px;border-radius:6px;border:1px solid rgba(255,255,255,0.12);width:fit-content;}" +
        "#tc-speed-buttons .speed-btn{position:relative;width:30px;height:30px;background:transparent;border:1px solid transparent;border-radius:4px;color:rgba(255,255,255,0.6);font-size:10px;cursor:pointer;transition:all 0.2s;padding:0;display:flex;align-items:center;justify-content:center;line-height:1;outline:none;}" +
        "#tc-speed-buttons .speed-btn:active{transform:scale(0.95);}" +
        "#tc-speed-buttons .speed-btn.active{background-color:rgba(255,193,7,0.15);color:#FFC107;border-color:rgba(255,193,7,0.3);font-weight:bold;box-shadow:inset 0 0 4px rgba(255,193,7,0.2);}" +
        "#tc-speed-buttons .speed-tooltip{position:absolute;bottom:130%;left:50%;transform:translateX(-50%);background-color:rgba(20,20,20,0.95);border:1px solid rgba(255,255,255,0.2);border-radius:4px;padding:6px 10px;color:#eee;font-size:12px;font-weight:normal;white-space:nowrap;pointer-events:none;opacity:0;visibility:hidden;transition:opacity 0.2s ease,visibility 0.2s ease,transform 0.2s ease;box-shadow:0 4px 12px rgba(0,0,0,0.5);z-index:200;}" +
        "#tc-speed-buttons .speed-tooltip::after{content:\"\";position:absolute;top:100%;left:50%;transform:translateX(-50%);border-width:5px 5px 0 5px;border-style:solid;border-color:rgba(20,20,20,0.95) transparent transparent transparent;}" +
        "#tc-speed-buttons .speed-btn:hover{background-color:rgba(255,255,255,0.1);color:white;}" +
        "#tc-speed-buttons .speed-btn:hover .speed-tooltip{opacity:1;visibility:visible;transform:translateX(-50%) translateY(-2px);}" +
        "#tc-speed-buttons .idle-slow-chip{position:absolute;bottom:calc(100% + 6px);right:0;background-color:rgba(30,30,30,0.85);border:1px solid rgba(255,193,7,0.3);border-radius:4px;padding:2px 7px;color:#FFC107;font-size:10.5px;white-space:nowrap;pointer-events:none;}";

    /// <summary>The game's buttons in its own order, each with the game's icon and the page texts it builds its tooltip from.</summary>
    private static readonly Button[] Buttons =
    {
        new(PauseToggle.Slow, "i18n_speedSlow", Texts.SpeedSlow, "i18n_hotkeySpeedSlow", "<path d=\"M9 4v11l8-5.5z\"/><rect x=\"5\" y=\"17.5\" width=\"14\" height=\"2\" rx=\"1\"/>"),
        new(PauseToggle.Normal, "i18n_speedNormal", Texts.SpeedNormal, "i18n_hotkeySpeed1x", "<path d=\"M8 5v14l11-7z\"/>"),
        new(PauseToggle.Double, "i18n_speedFast", Texts.SpeedFast, "i18n_hotkeySpeed2x", "<path d=\"M4 18l8.5-6L4 6v12zm9-12v12l8.5-6L13 6z\"/>"),
        new(PauseToggle.Triple, "i18n_speedMax", Texts.SpeedFastest, "i18n_hotkeySpeed3x", "<path d=\"M2 18l6-6-6-6v12zm7-12v12l6-6-6-6zm7 0v12l6-6-6-6z\"/>"),
        new(PauseToggle.Paused, "i18n_speedPause", Texts.SpeedPause, "i18n_hotkeySpeedPause", "<path d=\"M6 5h4v14H6zm8 0h4v14h-4z\"/>"),
    };

    /// <summary>
    /// Only where the game shows none, the countdown to the disaster and expeditions, and only where the mod answers the
    /// game's speed keys too: never in the game's other modes, such as its guided start.
    /// </summary>
    /// <param name="timer">The timer as the mod controls it, so an expedition with TimeControlOnExpeditions off is not one.</param>
    public static bool ShouldShow(bool isOn, bool isNormalMode, GameTimer timer) =>
        isOn && (timer is GameTimer.PrologueCountdown or GameTimer.Expedition) && SpeedKeys.IsModeForKeys(isNormalMode, timer);

    /// <summary>The countdown to the disaster has its own HUD page; an expedition uses the main one, as home does.</summary>
    public static HudPage Page(GameTimer timer) => timer == GameTimer.PrologueCountdown ? HudPage.Countdown : HudPage.Home;

    /// <summary>
    /// The pause button is lit while paused, as the game lights its own; otherwise the button for the speed level, which
    /// is the slow one on every slow speed and the fastest one on every fast speed.
    /// </summary>
    public static SpeedButtonsView View(GameTimer timer, bool isPaused, int speedLevel, string slowLabel, bool isChinese = false) =>
        new(Page(timer), isPaused ? PauseToggle.Paused : speedLevel, slowLabel, isChinese);

    /// <summary>Whether the page drew or removed the buttons: it answers "ok", or "ok:" and the clicks made since the last check.</summary>
    public static bool IsOk(string? reply) => reply == Ok || (reply?.StartsWith(ClicksPrefix, StringComparison.Ordinal) ?? false);

    /// <summary>The levels clicked since the last check, oldest first. Anything that is not one of the five buttons' levels is dropped.</summary>
    public static IReadOnlyList<int> Clicks(string? reply)
    {
        if (reply == null || !reply.StartsWith(ClicksPrefix, StringComparison.Ordinal)) return Array.Empty<int>();

        var clicks = new List<int>();

        foreach (var part in reply.Substring(ClicksPrefix.Length).Split(','))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && IsButtonLevel(level)) clicks.Add(level);
        }

        return clicks;
    }

    /// <summary>
    /// Draws the buttons on the view's page and takes them off any other, lights the active one and hands back the
    /// clicks. Where the page shows the game's own buttons after all, the mod's copy is taken away. Clicks queued by a
    /// copy that was taken away are dropped, so a click made just before the buttons hid is never applied later. The
    /// clicks are taken last, so a draw that fails leaves them queued for the next script.
    /// </summary>
    public static string Script(SpeedButtonsView view) =>
        "(function(){try{" + HudPages.RemoveFromFrames(ElementId, keepOn: view.Page) + HudPages.FindFrame(view.Page) +
        $"var d=w.document,g=d.getElementById('{ElementId}');if(!g)w.{ClicksQueue}=[];" + DefineTakeClicks +
        $"if(d.querySelector('.speed-controls:not(#{ElementId})')){{if(g&&g.parentNode)g.parentNode.removeChild(g);return take();}}" +
        $"if(!d.getElementById('{StyleId}')){{var st=d.createElement('style');st.id='{StyleId}';st.textContent='{Style}';(d.head||d.body).appendChild(st);}}" +
        $"if(!g){{g=d.createElement('div');g.id='{ElementId}';g.className='speed-controls';g.setAttribute('data-interactive','');" +
        $"var L=[{IconList()}];for(var i=0;i<L.length;i++){{var bt=d.createElement('button');bt.className='speed-btn';" +
        "bt.setAttribute('data-level',String(L[i][0]));bt.innerHTML=L[i][1]+'<div class=\"speed-tooltip\"></div>';" +
        $"bt.onclick=function(){{(w.{ClicksQueue}=w.{ClicksQueue}||[]).push(+this.getAttribute('data-level'));}};g.appendChild(bt);}}}}" +
        "var put=function(p,css){if(g.parentNode!==p)p.appendChild(g);if(g.getAttribute('data-tc-css')!==css){g.style.cssText=css;g.setAttribute('data-tc-css',css);}};" +
        Placement(view.Page) +
        "var s=null;try{s=w.eval('state');}catch(e){}" +
        $"var T={{{TooltipTable(view.SlowLabel, view.IsChinese)}}},bs=g.querySelectorAll('.speed-btn');" +
        "for(var j=0;j<bs.length;j++){var b=bs[j],l=+b.getAttribute('data-level'),k=T[l];if(!k)continue;" +
        $"var on=l==={view.ActiveLevel.ToString(CultureInfo.InvariantCulture)};if(b.classList.contains('active')!==on)b.classList.toggle('active',on);" +
        "var t=(s&&s[k[0]]?String(s[k[0]]):k[1])+(s&&s.i18n_hotkey&&s[k[2]]?' ('+s.i18n_hotkey+':'+s[k[2]]+')':'');" +
        "var tt=b.querySelector('.speed-tooltip');if(tt&&tt.textContent!==t)tt.textContent=t;}" +
        "return take();}catch(e){return 'error: '+e;}})()";

    /// <summary>
    /// The small script sent between full draws: it only hands back the clicks. Buttons the page has lost (it
    /// reloaded) answer <see cref="NotOnPage"/> so the next check draws them again, and their clicks are dropped. Where
    /// the copy stepped aside for the game's own buttons, there is nothing to draw and the answer is "ok".
    /// </summary>
    /// <remarks>Built once per page, as it is sent on most checks while the buttons show.</remarks>
    public static string CollectScript(HudPage page) => page == HudPage.Countdown ? CollectOnCountdown : CollectAtHome;

    private static readonly string CollectOnCountdown = BuildCollect(HudPage.Countdown);
    private static readonly string CollectAtHome = BuildCollect(HudPage.Home);
    private static readonly string Removal = "(function(){try{" + HudPages.RemoveFromFrames(ElementId) + $"return '{Ok}';}}catch(e){{return 'error: '+e;}}}})()";

    private static string BuildCollect(HudPage page) =>
        "(function(){try{" + HudPages.FindFrame(page) +
        $"var d=w.document;if(!d.getElementById('{ElementId}')){{w.{ClicksQueue}=[];return d.querySelector('.speed-controls')?'{Ok}':'{NotOnPage}';}}" +
        DefineTakeClicks + "return take();}catch(e){return 'error: '+e;}})()";

    /// <summary>Takes the buttons off every page.</summary>
    public static string RemovalScript() => Removal;

    /// <summary>
    /// On the main page the copy goes where the game's own buttons sit, beside the order queue. The countdown page
    /// has no such place, so the copy sits at the bottom just right of the page's bottom-left buttons, and hides with
    /// them in the planning view.
    /// </summary>
    private static string Placement(HudPage page) => page switch
    {
        HudPage.Home =>
            "var p=d.querySelector('.queue-flow-wrapper')||d.querySelector('.action-group-middle');" +
            "if(p)put(p,'');else put(d.body,'position:fixed;bottom:14px;left:84px;z-index:25;');",
        HudPage.Countdown =>
            "var bl=d.querySelector('.bottom-left'),hid=!!bl&&w.getComputedStyle(bl).display==='none';" +
            "var x=bl&&!hid?Math.round(bl.getBoundingClientRect().right)+14:30;" +
            "put(d.body,'position:fixed;bottom:30px;left:'+x+'px;z-index:25;'+(hid?'display:none;':''));",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "a HUD page with no place for the speed buttons"),
    };

    private static string IconList() => string.Join(",", Buttons.Select(button =>
        $"[{button.Level},'<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\">{button.Icon}</svg>']"));

    /// <summary>
    /// Per level: the page text naming the button, the English name used where the page has no such text, and the
    /// page text for its key. The slow button's English name carries the slow speed held, as the game's tooltip does.
    /// </summary>
    private static string TooltipTable(string slowLabel, bool isChinese)
    {
        var slowSuffix = PageClock.Sanitize(slowLabel) is { Length: > 0 } label ? " " + label : "";

        return string.Join(",", Buttons.Select(button =>
        {
            var name = button.Name.Pick(isChinese);
            var fallbackName = button.Level == PauseToggle.Slow ? name + slowSuffix : name;

            return $"{button.Level}:['{button.NameText}',{PauseBadge.Quote(fallbackName)},'{button.KeyText}']";
        }));
    }

    private static bool IsButtonLevel(int level) => level is >= PauseToggle.Paused and <= PauseToggle.Slow;

    private readonly record struct Button(int Level, string NameText, Phrase Name, string KeyText, string Icon);
}
