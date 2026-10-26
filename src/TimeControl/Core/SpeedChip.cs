namespace TimeControl.Core;

/// <summary>The speed the game really runs at, shown on the button that holds it.</summary>
/// <param name="Level">The button's speed level: the slow one or the fastest one.</param>
public readonly record struct SpeedChipView(int Level, string Label);

/// <summary>
/// A small chip on the top right corner of the slow or the fastest speed button, saying the speed the mod holds on
/// it, so a second or third press of the same key shows which speed is in use. It goes on the game's own buttons or
/// the mod's copy, wherever the page shows them, and never takes a click.
/// </summary>
public static class SpeedChip
{
    private const string ElementId = "tc-speed-chip";

    /// <summary>Centred on the button's top right corner; the place itself is set from the button's offsets.</summary>
    private const string Look =
        "position:absolute;transform:translate(-50%,-50%);z-index:5;pointer-events:none;background-color:#FFC107;color:#1e1e1e;" +
        "font-size:8px;font-weight:bold;line-height:10px;padding:0 3px;border-radius:5px;white-space:nowrap;box-shadow:0 0 2px rgba(0,0,0,0.6);";

    /// <summary>How far in from the corner the chip's centre sits, so it overlaps the button rather than floating off it.</summary>
    private const int CornerInset = 3;

    private static readonly string Removal = "(function(){try{" + HudPages.RemoveFromFrames(ElementId) + "return 'ok';}catch(e){return 'error: '+e;}})()";

    /// <summary>
    /// The chip to show now, or null for none. Only a speed the mod holds gets one: a slow speed, or a fast speed above
    /// the game's own high speed. Paused, the game lights its pause button and the chip goes.
    /// </summary>
    /// <param name="heldSlow">The slow speed the mod holds, if any.</param>
    /// <param name="heldFast">The fast speed the mod holds above the game's high speed, if any.</param>
    public static SpeedChipView? View(bool isPaused, float? heldSlow, float? heldFast)
    {
        if (isPaused) return null;

        if (heldFast is { } fast) return new SpeedChipView(PauseToggle.Triple, SlowGear.Label(fast));

        return heldSlow is { } slow ? new SpeedChipView(PauseToggle.Slow, SlowGear.Label(slow)) : null;
    }

    /// <summary>
    /// The speed the game's hold-to-speed-up key runs at while it is held, for the chip on the fastest button, or null
    /// when it is not held or runs at the game's own speed.
    /// </summary>
    public static float? HoldKeyFast(bool isHoldKeyRunning, float gameFastSpeed, float multiple) =>
        isHoldKeyRunning ? FastGear.HoldKeyScale(gameFastSpeed, multiple) : null;

    /// <summary>
    /// A change goes out at once; a shown chip is sent again once the page changed under it, which puts it back after
    /// the page redraws its buttons, and anything the page has not confirmed is sent again on each refresh until it
    /// does. While nothing changes, nothing is sent.
    /// </summary>
    /// <param name="hasPageChanged">The page watch saw the page change since the last chip was sent.</param>
    public static bool ShouldSend(SpeedChipView? view, SpeedChipView? lastSent, bool isLastSentConfirmed, bool isRefreshDue, bool hasPageChanged) =>
        view != lastSent || (isRefreshDue && !isLastSentConfirmed) || (hasPageChanged && view is not null);

    /// <summary>
    /// Puts the chip on the button for the view's level on the given page, or takes it off when there is no view or
    /// no such button. The mod's copy marks each button with its level; the game's own buttons run slow, normal, fast,
    /// fastest, pause (the slow one only where the game shows it), so the fastest one is second to last and the slow
    /// one first of five.
    /// </summary>
    /// <param name="page">The page showing the speed buttons. The chip is taken off every other page.</param>
    /// <remarks>
    /// With no view the chip is taken off every page, answering "ok" even with no HUD page open, so a removal sent
    /// from a menu is confirmed rather than sent again on every refresh.
    /// </remarks>
    public static string Script(SpeedChipView? view, HudPage page = HudPage.Home)
    {
        if (view is not { } shown) return Removal;

        var level = shown.Level;
        var label = PauseBadge.Quote(PageClock.Sanitize(shown.Label));

        return "(function(){try{" + HudPages.RemoveFromFrames(ElementId, keepOn: page) + HudPages.FindFrame(page) +
               $"var d=w.document,c=d.getElementById('{ElementId}'),t={label},L={level},a=d.querySelector('.speed-controls'),b=null;" +
               "if(a&&t.length){var bs=a.querySelectorAll('.speed-btn');" +
               "for(var i=0;i<bs.length;i++)if(bs[i].getAttribute('data-level')===String(L))b=bs[i];" +
               $"if(!b&&bs.length>=4&&bs[0].getAttribute('data-level')===null)b=L==={PauseToggle.Slow}?(bs.length===5?bs[0]:null):bs[bs.length-2];}}" +
               "if(!b){if(c&&c.parentNode)c.parentNode.removeChild(c);return 'ok';}" +
               $"if(!c){{c=d.createElement('div');c.id='{ElementId}';}}if(c.parentNode!==a)a.appendChild(c);" +
               $"var css='{Look}left:'+(b.offsetLeft+b.offsetWidth-{CornerInset})+'px;top:'+(b.offsetTop+{CornerInset})+'px;';" +
               "if(c.getAttribute('data-tc-css')!==css){c.style.cssText=css;c.setAttribute('data-tc-css',css);}if(c.textContent!==t)c.textContent=t;" +
               "return 'ok';}catch(e){return 'error: '+e;}})()";
    }
}
