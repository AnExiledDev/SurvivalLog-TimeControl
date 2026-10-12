using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends <see cref="PageClock"/> updates, the <see cref="PauseBadge"/> and the <see cref="SpeedButtons"/> into the web
/// UI, takes back the clicks on the speed buttons, and logs each distinct problem the page reports for any of them.
/// </summary>
internal sealed class PageClockLink
{
    private const string Ok = "ok";

    private readonly PageReply _clockReply;
    private readonly PageReply _badgeReply;
    private readonly PageReply _buttonsReply;
    private readonly List<int> _speedClicks = new();
    private bool _wasNeutralLastSent = true;
    private string _lastBadgeSent = "";
    private bool _isLastBadgeConfirmed = true;
    private bool _wereButtonsShownLastSent;
    private bool _isLastButtonsSentConfirmed = true;

    public PageClockLink(Action<string> log)
    {
        _clockReply = new PageReply("page clock", log);
        _badgeReply = new PageReply("pause badge", log);
        _buttonsReply = new PageReply("speed buttons", log);
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

    /// <param name="view">What the speed buttons show; null takes them off the page and drops any clicks not yet taken.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed removal is due to be sent again.</param>
    public void SyncSpeedButtons(SpeedButtonsView? view, bool isRefreshDue)
    {
        _buttonsReply.Report();

        var isShown = view is not null;

        if (!isShown) _speedClicks.Clear();

        if (!SpeedButtons.ShouldSend(isShown, _wereButtonsShownLastSent, _isLastButtonsSentConfirmed, isRefreshDue)) return;

        Func<string> script = view is { } shown ? () => SpeedButtons.Script(shown) : SpeedButtons.RemovalScript;

        if (!Run(script, reply => TakeButtonsReply(isShown, reply), _buttonsReply)) return;

        _wereButtonsShownLastSent = isShown;
        _isLastButtonsSentConfirmed = false;
    }

    /// <summary>The clicks on the speed buttons the page has handed back since the last call, oldest first.</summary>
    public IReadOnlyList<int> TakeSpeedClicks()
    {
        if (_speedClicks.Count == 0) return Array.Empty<int>();

        var clicks = _speedClicks.ToArray();

        _speedClicks.Clear();

        return clicks;
    }

    /// <summary>
    /// Clicks are kept only while the buttons are still shown, so a reply that lands after they hid adds none. Only
    /// the page's "ok" to a removal still last sent confirms it.
    /// </summary>
    private void TakeButtonsReply(bool wasShownWhenSent, string? reply)
    {
        var isOk = SpeedButtons.IsOk(reply);

        _buttonsReply.Take(isOk ? Ok : reply);

        if (!isOk) return;

        if (wasShownWhenSent && _wereButtonsShownLastSent) _speedClicks.AddRange(SpeedButtons.Clicks(reply));
        else if (!wasShownWhenSent && !_wereButtonsShownLastSent) _isLastButtonsSentConfirmed = true;
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
