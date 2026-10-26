using System;
using System.Collections.Generic;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends <see cref="PageClock"/> updates, the <see cref="PauseBadge"/>, the <see cref="SpeedButtons"/> and the
/// <see cref="PauseOverlay"/> style into the web UI, takes back the clicks on the speed buttons, and logs each distinct
/// problem the page reports for any of them. Each is sent when what it shows changes, when the page changed under it
/// since it was sent (the page generation, from <see cref="PageWatcher"/>), or until the page confirms it; while
/// nothing changes, only the small script that takes back the clicks goes out.
/// </summary>
internal sealed class PageClockLink
{
    private readonly PageReply _clockReply;
    private readonly PageReply _badgeReply;
    private readonly PageReply _buttonsReply;
    private readonly PageReply _overlayReply;
    private readonly PageReply _chipReply;
    private readonly Il2CppSystem.Action<string> _onButtonsShownReply;
    private readonly Il2CppSystem.Action<string> _onButtonsRemovedReply;
    private readonly List<int> _speedClicks = new();
    private string _lastClockSent = "";
    private bool _isLastClockConfirmed = true;
    private int _clockGeneration;
    private string _lastBadgeSent = "";
    private bool _isLastBadgeConfirmed = true;
    private int _badgeGeneration;
    private string _lastOverlaySent = "";
    private bool _isLastOverlayConfirmed = true;
    private int _overlayGeneration;
    private SpeedChipView? _lastChipSent;
    private bool _isLastChipConfirmed = true;
    private int _chipGeneration;
    private ButtonsOnPage _buttonsOnPage = ButtonsOnPage.Start;
    private int _buttonsGeneration;
    private SpeedButtonsView? _drawnScriptView;
    private string _drawnScript = "";

    public PageClockLink(Action<string> log)
    {
        _clockReply = new PageReply("page clock", log);
        _badgeReply = new PageReply("pause badge", log);
        _buttonsReply = new PageReply("speed buttons", log);
        _overlayReply = new PageReply("pause overlay", log);
        _chipReply = new PageReply("speed chip", log);
        _onButtonsShownReply = (Action<string>)(reply => TakeButtonsReply(wasShownWhenSent: true, reply));
        _onButtonsRemovedReply = (Action<string>)(reply => TakeButtonsReply(wasShownWhenSent: false, reply));
    }

    /// <param name="slowLabel">The slow speed held, for the slow button's tooltip; empty when none is.</param>
    /// <param name="generation">The page generation now.</param>
    /// <param name="pageText">The slow button's text as the page watch last read it; null when it has not.</param>
    public void Sync(string slowLabel, bool isRefreshDue, int generation, string? pageText)
    {
        _clockReply.Report();

        if (!PageClock.ShouldSend(slowLabel, _lastClockSent, _isLastClockConfirmed, isRefreshDue, generation != _clockGeneration, pageText)) return;

        if (WebUi.View(_clockReply) is not { } view) return;

        if (!WebUi.Send(view, PageClock.Script(new PageClockUpdate(slowLabel)), (Action<string>)(reply => ConfirmClock(slowLabel, reply)), _clockReply)) return;

        _lastClockSent = slowLabel;
        _isLastClockConfirmed = false;
        _clockGeneration = generation;
    }

    private void ConfirmClock(string sent, string? reply)
    {
        _clockReply.Take(reply);

        if (HudPages.IsSettled(reply) && sent == _lastClockSent) _isLastClockConfirmed = true;
    }

    /// <param name="text">What the badge says; empty hides it.</param>
    /// <param name="page">The page the badge goes on: the one showing the speed buttons.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed badge or removal is due to be sent again.</param>
    /// <param name="generation">The page generation now; a shown badge sent at an older one is sent again.</param>
    public void SyncBadge(string text, HudPage page, bool isRefreshDue, int generation)
    {
        _badgeReply.Report();

        if (!PauseBadge.ShouldSend(text, _lastBadgeSent, _isLastBadgeConfirmed, isRefreshDue, generation != _badgeGeneration)) return;

        if (WebUi.View(_badgeReply) is not { } view) return;

        if (!WebUi.Send(view, PauseBadge.Script(text, page), (Action<string>)(reply => ConfirmBadge(text, reply)), _badgeReply)) return;

        _lastBadgeSent = text;
        _isLastBadgeConfirmed = false;
        _badgeGeneration = generation;
    }

    /// <summary>
    /// Only an answer that settles the text still last sent confirms it; an older reply says nothing about it. A HUD
    /// page that is not open settles it too: the page watch sees it open, which sends a shown badge again.
    /// </summary>
    private void ConfirmBadge(string sent, string? reply)
    {
        _badgeReply.Take(reply);

        if (HudPages.IsSettled(reply) && sent == _lastBadgeSent) _isLastBadgeConfirmed = true;
    }

    /// <param name="view">The chip on the speed button holding the mod's speed; null takes it off.</param>
    /// <param name="page">The page the chip goes on: the one showing the speed buttons.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed chip or removal is due to be sent again.</param>
    /// <param name="generation">The page generation now; a shown chip sent at an older one is sent again.</param>
    public void SyncSpeedChip(SpeedChipView? view, HudPage page, bool isRefreshDue, int generation)
    {
        _chipReply.Report();

        if (!SpeedChip.ShouldSend(view, _lastChipSent, _isLastChipConfirmed, isRefreshDue, generation != _chipGeneration)) return;

        if (WebUi.View(_chipReply) is not { } webView) return;

        if (!WebUi.Send(webView, SpeedChip.Script(view, page), (Action<string>)(reply => ConfirmChip(view, reply)), _chipReply)) return;

        _lastChipSent = view;
        _isLastChipConfirmed = false;
        _chipGeneration = generation;
    }

    private void ConfirmChip(SpeedChipView? sent, string? reply)
    {
        _chipReply.Take(reply);

        if (HudPages.IsSettled(reply) && sent == _lastChipSent) _isLastChipConfirmed = true;
    }

    /// <param name="css">The style for the game's pause strip; empty takes it off.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed style or removal is due to be sent again.</param>
    /// <param name="generation">The page generation now; a style sent at an older one is sent again.</param>
    /// <remarks>Sent on the badge's terms: the strip's page reloads and closes as the badge's does.</remarks>
    public void SyncPauseOverlay(string css, bool isRefreshDue, int generation)
    {
        _overlayReply.Report();

        if (!PauseBadge.ShouldSend(css, _lastOverlaySent, _isLastOverlayConfirmed, isRefreshDue, generation != _overlayGeneration)) return;

        if (WebUi.View(_overlayReply) is not { } view) return;

        if (!WebUi.Send(view, PauseOverlay.Script(css), (Action<string>)(reply => ConfirmOverlay(css, reply)), _overlayReply)) return;

        _lastOverlaySent = css;
        _isLastOverlayConfirmed = false;
        _overlayGeneration = generation;
    }

    private void ConfirmOverlay(string sent, string? reply)
    {
        _overlayReply.Take(reply);

        if (reply == PageReply.Ok && sent == _lastOverlaySent) _isLastOverlayConfirmed = true;
    }

    /// <param name="view">What the speed buttons show; null takes them off the page and drops any clicks not yet taken.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed removal is due a resend.</param>
    /// <param name="isPollDue">Whether the clicks are due to be taken; a collect waits for it.</param>
    /// <param name="generation">The page generation now; buttons drawn at an older one are drawn again in full.</param>
    public void SyncSpeedButtons(SpeedButtonsView? view, bool isRefreshDue, bool isPollDue, int generation)
    {
        _buttonsReply.Report();

        var isShown = view is not null;

        if (!isShown) _speedClicks.Clear();

        var next = ButtonsSync.Next(view, _buttonsOnPage, isRefreshDue, generation != _buttonsGeneration);

        if (next == ButtonsScript.None || (next == ButtonsScript.Collect && !isPollDue)) return;

        if (WebUi.View(_buttonsReply) is not { } webView) return;

        if (!WebUi.Send(webView, ButtonsScriptFor(next, view), isShown ? _onButtonsShownReply : _onButtonsRemovedReply, _buttonsReply)) return;

        _buttonsOnPage = ButtonsSync.AfterSending(next, view, _buttonsOnPage);

        if (next == ButtonsScript.Draw) _buttonsGeneration = generation;
    }

    private string ButtonsScriptFor(ButtonsScript next, SpeedButtonsView? view) => (next, view) switch
    {
        (ButtonsScript.Draw, { } shown) => DrawScript(shown),
        (ButtonsScript.Collect, { } shown) => SpeedButtons.CollectScript(shown.Page),
        _ => SpeedButtons.RemovalScript(),
    };

    /// <summary>The full script is built once per view, since it is sent again unchanged after the page changes.</summary>
    private string DrawScript(SpeedButtonsView view)
    {
        if (_drawnScriptView == view) return _drawnScript;

        _drawnScript = SpeedButtons.Script(view);
        _drawnScriptView = view;

        return _drawnScript;
    }

    /// <summary>The clicks on the speed buttons the page has handed back since the last call, oldest first.</summary>
    public IReadOnlyList<int> TakeSpeedClicks()
    {
        if (_speedClicks.Count == 0) return Array.Empty<int>();

        var clicks = _speedClicks.ToArray();

        _speedClicks.Clear();

        return clicks;
    }

    /// <summary>A click handed back asks for a check on the frame it arrives, so it acts at once.</summary>
    private void TakeButtonsReply(bool wasShownWhenSent, string? reply)
    {
        var taken = ButtonsSync.TakeReply(_buttonsOnPage, wasShownWhenSent, reply);

        _buttonsReply.Take(SpeedButtons.IsOk(reply) ? PageReply.Ok : reply);
        _buttonsOnPage = taken.OnPage;

        if (taken.Clicks.Count == 0) return;

        _speedClicks.AddRange(taken.Clicks);
        ClockTicker.WantCheck();
    }
}
