using System;
using System.Collections.Generic;

namespace TimeControl.Core;

/// <summary>Which speed buttons script a check sends the page, if any.</summary>
public enum ButtonsScript
{
    None,
    Draw,
    Collect,
    Remove,
}

/// <summary>What the page last got about the speed buttons, as far as its answers tell.</summary>
public abstract record ButtonsOnPage
{
    public static readonly ButtonsOnPage Start = new Removed(IsConfirmed: true);

    private ButtonsOnPage()
    {
    }

    /// <param name="NeedsRedraw">The page answered something other than "ok" since, so the buttons may be missing.</param>
    public sealed record Drawn(SpeedButtonsView View, bool NeedsRedraw) : ButtonsOnPage;

    /// <param name="IsConfirmed">The page answered "ok" to the removal.</param>
    public sealed record Removed(bool IsConfirmed) : ButtonsOnPage;
}

/// <summary>A page answer taken: what the page now has, and the clicks to apply.</summary>
public readonly record struct ButtonsReply(ButtonsOnPage OnPage, IReadOnlyList<int> Clicks);

/// <summary>
/// Keeps the mod's speed buttons on the page in step with the view, as cheaply as the clicks allow: a full draw only
/// when something changed, and between draws only the small script that takes back the clicks.
/// </summary>
public static class ButtonsSync
{
    private static readonly IReadOnlyList<int> NoClicks = Array.Empty<int>();

    /// <summary>
    /// Shown buttons are drawn in full when the view changes and when the page changed under them, which follows the
    /// page's layout and texts; buttons the page may have lost are drawn again on the next refresh, so a page that
    /// stays closed is not drawn into on every check. Every other check only collects the clicks. A removal goes out
    /// at once, then again on each refresh until the page confirms it.
    /// </summary>
    /// <param name="hasPageChanged">The page watch saw the page change since the buttons were last drawn.</param>
    public static ButtonsScript Next(SpeedButtonsView? view, ButtonsOnPage onPage, bool isRefreshDue, bool hasPageChanged) => (view, onPage) switch
    {
        ({ } shown, ButtonsOnPage.Drawn { NeedsRedraw: false } drawn) when drawn.View == shown && !hasPageChanged => ButtonsScript.Collect,
        ({ } shown, ButtonsOnPage.Drawn { NeedsRedraw: true } drawn) when drawn.View == shown && !hasPageChanged && !isRefreshDue => ButtonsScript.None,
        ({ }, _) => ButtonsScript.Draw,
        (null, ButtonsOnPage.Drawn) => ButtonsScript.Remove,
        (null, ButtonsOnPage.Removed { IsConfirmed: false }) when isRefreshDue => ButtonsScript.Remove,
        _ => ButtonsScript.None,
    };

    /// <summary>What the page has once a script went out, before it answers.</summary>
    public static ButtonsOnPage AfterSending(ButtonsScript sent, SpeedButtonsView? view, ButtonsOnPage onPage) => (sent, view) switch
    {
        (ButtonsScript.Draw, { } shown) => new ButtonsOnPage.Drawn(shown, NeedsRedraw: false),
        (ButtonsScript.Remove, _) => new ButtonsOnPage.Removed(IsConfirmed: false),
        _ => onPage,
    };

    /// <summary>
    /// An answer to a draw or a collect hands back its clicks only while the buttons are still drawn, so a click made
    /// just before they hid is never applied. A HUD page that is not open leaves them as they are: the page watch sees
    /// it open, which draws them. Any other answer asks for a full draw. Only "ok" to a removal, while the buttons are
    /// still off, confirms it.
    /// </summary>
    /// <param name="wasShownWhenSent">The answer is to a draw or a collect, not to a removal.</param>
    public static ButtonsReply TakeReply(ButtonsOnPage onPage, bool wasShownWhenSent, string? reply)
    {
        var isOk = SpeedButtons.IsOk(reply);

        return (onPage, wasShownWhenSent) switch
        {
            (ButtonsOnPage.Drawn, true) when isOk => new ButtonsReply(onPage, SpeedButtons.Clicks(reply)),
            (ButtonsOnPage.Drawn, true) when HudPages.IsClosedReply(reply) => new ButtonsReply(onPage, NoClicks),
            (ButtonsOnPage.Drawn drawn, true) => new ButtonsReply(drawn with { NeedsRedraw = true }, NoClicks),
            (ButtonsOnPage.Removed, false) when isOk => new ButtonsReply(new ButtonsOnPage.Removed(IsConfirmed: true), NoClicks),
            _ => new ButtonsReply(onPage, NoClicks),
        };
    }
}
