using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends <see cref="PageClock"/> updates, the <see cref="PauseBadge"/>, the <see cref="SpeedButtons"/> and the
/// <see cref="PauseOverlay"/> style into the web UI, takes back the clicks on the speed buttons, and logs each distinct
/// problem the page reports for any of them.
/// </summary>
internal sealed class PageClockLink
{
    private const string Ok = "ok";

    private readonly PageReply _clockReply;
    private readonly PageReply _badgeReply;
    private readonly PageReply _buttonsReply;
    private readonly PageReply _overlayReply;
    private readonly List<int> _speedClicks = new();
    private bool _wasNeutralLastSent = true;
    private string _lastBadgeSent = "";
    private bool _isLastBadgeConfirmed = true;
    private string _lastOverlaySent = "";
    private bool _isLastOverlayConfirmed = true;
    private ButtonsOnPage _buttonsOnPage = ButtonsOnPage.Start;
    private SpeedButtonsView? _drawnScriptView;
    private string _drawnScript = "";

    public PageClockLink(Action<string> log)
    {
        _clockReply = new PageReply("page clock", log);
        _badgeReply = new PageReply("pause badge", log);
        _buttonsReply = new PageReply("speed buttons", log);
        _overlayReply = new PageReply("pause overlay", log);
    }

    public void Sync(float clockFactor, float battleScale, string slowLabel)
    {
        _clockReply.Report();

        if (PageClock.Next(clockFactor, battleScale, slowLabel, _wasNeutralLastSent) is not { } update) return;

        if (Run(() => PageClock.Script(update), _clockReply.Take, _clockReply)) _wasNeutralLastSent = PageClock.IsNeutral(clockFactor, slowLabel);
    }

    /// <param name="text">What the badge says; empty hides it.</param>
    /// <param name="page">The page the badge goes on: the one showing the speed buttons.</param>
    /// <param name="isRefreshDue">Whether a shown badge, or an unconfirmed removal, is due to be sent again.</param>
    public void SyncBadge(string text, HudPage page, bool isRefreshDue)
    {
        _badgeReply.Report();

        if (!PauseBadge.ShouldSend(text, _lastBadgeSent, _isLastBadgeConfirmed, isRefreshDue)) return;

        if (!Run(() => PauseBadge.Script(text, page), reply => ConfirmBadge(text, reply), _badgeReply)) return;

        _lastBadgeSent = text;
        _isLastBadgeConfirmed = false;
    }

    /// <summary>Only the page's "ok" to the text still last sent confirms it; an older reply says nothing about it.</summary>
    private void ConfirmBadge(string sent, string? reply)
    {
        _badgeReply.Take(reply);

        if (reply == Ok && sent == _lastBadgeSent) _isLastBadgeConfirmed = true;
    }

    /// <param name="css">The style for the game's pause strip; empty takes it off.</param>
    /// <param name="isRefreshDue">Whether a style on the page, or an unconfirmed removal, is due to be sent again.</param>
    /// <remarks>Sent on the badge's terms: the strip's page reloads and closes as the badge's does.</remarks>
    public void SyncPauseOverlay(string css, bool isRefreshDue)
    {
        _overlayReply.Report();

        if (!PauseBadge.ShouldSend(css, _lastOverlaySent, _isLastOverlayConfirmed, isRefreshDue)) return;

        if (!Run(() => PauseOverlay.Script(css), reply => ConfirmOverlay(css, reply), _overlayReply)) return;

        _lastOverlaySent = css;
        _isLastOverlayConfirmed = false;
    }

    private void ConfirmOverlay(string sent, string? reply)
    {
        _overlayReply.Take(reply);

        if (reply == Ok && sent == _lastOverlaySent) _isLastOverlayConfirmed = true;
    }

    /// <param name="view">What the speed buttons show; null takes them off the page and drops any clicks not yet taken.</param>
    /// <param name="isRefreshDue">Whether shown buttons are due a full draw, or an unconfirmed removal a resend.</param>
    public void SyncSpeedButtons(SpeedButtonsView? view, bool isRefreshDue)
    {
        _buttonsReply.Report();

        var isShown = view is not null;

        if (!isShown) _speedClicks.Clear();

        var next = ButtonsSync.Next(view, _buttonsOnPage, isRefreshDue);

        if (next == ButtonsScript.None) return;

        if (!Run(() => ButtonsScriptFor(next, view), reply => TakeButtonsReply(isShown, reply), _buttonsReply)) return;

        _buttonsOnPage = ButtonsSync.AfterSending(next, view, _buttonsOnPage);
    }

    private string ButtonsScriptFor(ButtonsScript next, SpeedButtonsView? view) => (next, view) switch
    {
        (ButtonsScript.Draw, { } shown) => DrawScript(shown),
        (ButtonsScript.Collect, { } shown) => SpeedButtons.CollectScript(shown.Page),
        _ => SpeedButtons.RemovalScript(),
    };

    /// <summary>The full script is built once per view, since it is resent unchanged on every refresh while the view holds.</summary>
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

    private void TakeButtonsReply(bool wasShownWhenSent, string? reply)
    {
        var taken = ButtonsSync.TakeReply(_buttonsOnPage, wasShownWhenSent, reply);

        _buttonsReply.Take(SpeedButtons.IsOk(reply) ? Ok : reply);
        _buttonsOnPage = taken.OnPage;
        _speedClicks.AddRange(taken.Clicks);
    }

    /// <summary>
    /// Never throws: a page that cannot be reached costs the drawn clock or the badge, never the ticker's pause
    /// handling. The script is only built once there is a page to run it in.
    /// </summary>
    private static bool Run(Func<string> script, Action<string> onReply, PageReply reply)
    {
        try
        {
            var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;
            var webView = ui?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

            if (webView == null) return false;

            webView.ExecuteJavaScript(script(), onReply);

            return true;
        }
        catch (Exception ex)
        {
            reply.Take("could not run script: " + ex.Message);

            return false;
        }
    }

    /// <summary>The page's last answer to one kind of script, logged once each time it changes.</summary>
    private sealed class PageReply
    {
        private readonly string _name;
        private readonly Action<string> _log;
        private string _reply = Ok;
        private string _lastLogged = Ok;

        public PageReply(string name, Action<string> log)
        {
            _name = name;
            _log = log;
        }

        public void Take(string? reply) => _reply = reply ?? "no reply";

        public void Report()
        {
            if (_reply == _lastLogged) return;

            _lastLogged = _reply;
            _log(_reply == Ok ? $"{_name}: reachable again" : $"{_name}: {_reply}");
        }
    }
}
