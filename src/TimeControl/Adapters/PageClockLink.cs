using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>Sends <see cref="PageClock"/> updates into the web UI and logs each distinct problem the page reports.</summary>
internal sealed class PageClockLink
{
    private const string Ok = "ok";

    private readonly Action<string> _log;
    private bool _wasNeutralLastSent = true;
    private string _reply = Ok;
    private string _lastLoggedReply = Ok;

    public PageClockLink(Action<string> log) => _log = log;

    public void Sync(float clockFactor, float battleScale, string slowLabel)
    {
        ReportReply();

        if (PageClock.Next(clockFactor, battleScale, slowLabel, _wasNeutralLastSent) is not { } update) return;

        if (Run(PageClock.Script(update))) _wasNeutralLastSent = PageClock.IsNeutral(clockFactor, slowLabel);
    }

    private void ReportReply()
    {
        if (_reply == _lastLoggedReply) return;

        _lastLoggedReply = _reply;
        _log(_reply == Ok ? "page clock: reachable again" : "page clock: " + _reply);
    }

    private bool Run(string script)
    {
        var webView = BaseSingleton<ReduxUISystem>.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(script, (Action<string>)(reply => _reply = reply ?? "no reply"));

            return true;
        }
        catch (Exception ex)
        {
            _reply = "could not run script: " + ex.Message;

            return false;
        }
    }
}
