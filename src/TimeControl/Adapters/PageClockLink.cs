using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends <see cref="PageClock"/> updates and the <see cref="PauseBadge"/> into the web UI, and logs each distinct
/// problem the page reports for either.
/// </summary>
internal sealed class PageClockLink
{
    private const string Ok = "ok";

    private readonly PageReply _clockReply;
    private readonly PageReply _badgeReply;
    private bool _wasNeutralLastSent = true;
    private string _lastBadgeSent = "";
    private bool _isLastBadgeConfirmed = true;

    public PageClockLink(Action<string> log)
    {
        _clockReply = new PageReply("page clock", log);
        _badgeReply = new PageReply("pause badge", log);
    }

    public void Sync(float clockFactor, float battleScale, string slowLabel)
    {
        _clockReply.Report();

        if (PageClock.Next(clockFactor, battleScale, slowLabel, _wasNeutralLastSent) is not { } update) return;

        if (Run(() => PageClock.Script(update), _clockReply.Take, _clockReply)) _wasNeutralLastSent = PageClock.IsNeutral(clockFactor, slowLabel);
    }

    /// <param name="text">What the badge says; empty hides it.</param>
    /// <param name="isRefreshDue">Whether a shown badge, or an unconfirmed removal, is due to be sent again.</param>
    public void SyncBadge(string text, bool isRefreshDue)
    {
        _badgeReply.Report();

        if (!PauseBadge.ShouldSend(text, _lastBadgeSent, _isLastBadgeConfirmed, isRefreshDue)) return;

        if (!Run(() => PauseBadge.Script(text), reply => ConfirmBadge(text, reply), _badgeReply)) return;

        _lastBadgeSent = text;
        _isLastBadgeConfirmed = false;
    }

    /// <summary>Only the page's "ok" to the text still last sent confirms it; an older reply says nothing about it.</summary>
    private void ConfirmBadge(string sent, string? reply)
    {
        _badgeReply.Take(reply);

        if (reply == Ok && sent == _lastBadgeSent) _isLastBadgeConfirmed = true;
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
