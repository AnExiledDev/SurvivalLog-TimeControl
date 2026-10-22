using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends the <see cref="WaitMenu"/> into the web UI, takes back the clicks on it, and logs each distinct problem the
/// page reports.
/// </summary>
internal sealed class WaitPageLink
{
    private const string Ok = "ok";

    private readonly PageReply _reply;
    private readonly List<int> _clicks = new();
    private string _lastSent = "";
    private bool _isLastSentConfirmed = true;

    public WaitPageLink(Action<string> log)
    {
        _reply = new PageReply("wait menu", log);
    }

    /// <summary>
    /// Sent on the badge's terms: a shown menu goes again on every refresh, which puts it back after the page reloads
    /// and hands back its clicks, and a removal goes again until the page confirms it.
    /// </summary>
    /// <param name="view">What the wait button shows; null takes it off the page and drops any clicks not yet taken.</param>
    /// <param name="isRefreshDue">Whether a shown menu, or an unconfirmed removal, is due to be sent again.</param>
    public void Sync(WaitMenuView? view, bool isRefreshDue)
    {
        _reply.Report();

        if (view == null) _clicks.Clear();

        var script = view == null ? "" : WaitMenu.Script(view);

        if (!PauseBadge.ShouldSend(script, _lastSent, _isLastSentConfirmed, isRefreshDue)) return;

        if (!Run(() => script.Length > 0 ? script : WaitMenu.RemovalScript(), reply => TakeReply(script, reply), _reply)) return;

        _lastSent = script;
        _isLastSentConfirmed = false;
    }

    /// <summary>The clicks on the wait menu the page has handed back since the last call, oldest first.</summary>
    public IReadOnlyList<int> TakeClicks()
    {
        if (_clicks.Count == 0) return Array.Empty<int>();

        var clicks = _clicks.ToArray();

        _clicks.Clear();

        return clicks;
    }

    /// <summary>
    /// Only the page's answer to the script still last sent confirms it. Clicks are kept only while the menu is meant
    /// to show, so one made just before it was taken away is never applied later.
    /// </summary>
    private void TakeReply(string sent, string? reply)
    {
        var isOk = WaitMenu.IsOk(reply);

        _reply.Take(isOk ? Ok : reply);

        if (isOk && sent == _lastSent) _isLastSentConfirmed = true;

        if (_lastSent.Length > 0) _clicks.AddRange(WaitMenu.Clicks(reply));
    }

    /// <summary>
    /// Never throws: a page that cannot be reached costs the wait button, never the ticker's pause handling. The script
    /// is only built once there is a page to run it in.
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

    /// <summary>The page's last answer to the wait menu's script, logged once each time it changes.</summary>
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
