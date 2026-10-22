using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends the <see cref="SpeedWheel"/> and the notes under the clock (<see cref="OrderFinish"/>, <see cref="Countdowns"/>)
/// into the web UI, takes back the wheel's notches, and logs each distinct problem the page reports for any of them.
/// </summary>
internal sealed class KeysPage
{
    private const string Ok = "ok";

    private readonly PageReply _wheelReply;
    private readonly PageReply _orderReply;
    private readonly PageReply _countdownReply;
    private int _wheelSteps;
    private bool _isLastWheelSentOn;
    private bool _isWheelOffConfirmed = true;
    private string _lastOrderSent = "";
    private bool _isLastOrderConfirmed = true;
    private string _lastCountdownSent = "";
    private bool _isLastCountdownConfirmed = true;

    public KeysPage(Action<string> log)
    {
        _wheelReply = new PageReply("speed wheel", log);
        _orderReply = new PageReply("order finish time", log);
        _countdownReply = new PageReply("countdowns", log);
    }

    /// <summary>
    /// Switches the wheel on or off over the speed buttons. While on it is sent on every check, which also takes the
    /// notches and hooks buttons drawn since; an off is sent until the page confirms it.
    /// </summary>
    public void SyncWheel(bool isOn)
    {
        _wheelReply.Report();

        if (!isOn && _isWheelOffConfirmed) return;

        if (!Run(() => SpeedWheel.Script(isOn), reply => TakeWheelReply(isOn, reply), _wheelReply)) return;

        _isLastWheelSentOn = isOn;
        _isWheelOffConfirmed = false;
    }

    /// <summary>The notches the page handed back since the last call, up positive.</summary>
    public int TakeWheelSteps()
    {
        var steps = _wheelSteps;

        _wheelSteps = 0;

        return steps;
    }

    private void TakeWheelReply(bool wasOn, string? reply)
    {
        var isOk = SpeedWheel.IsOk(reply);

        _wheelReply.Take(isOk ? Ok : reply);
        _wheelSteps += SpeedWheel.Steps(reply);

        if (isOk && !wasOn && !_isLastWheelSentOn) _isWheelOffConfirmed = true;
    }

    /// <param name="label">The order's finish time; empty takes it off.</param>
    /// <param name="isRefreshDue">Whether a shown note, or an unconfirmed removal, is due to be sent again.</param>
    public void SyncOrderFinish(string label, bool isRefreshDue)
    {
        _orderReply.Report();

        if (!PauseBadge.ShouldSend(label, _lastOrderSent, _isLastOrderConfirmed, isRefreshDue)) return;

        if (!Run(() => OrderFinish.Script(label), reply => ConfirmOrder(label, reply), _orderReply)) return;

        _lastOrderSent = label;
        _isLastOrderConfirmed = false;
    }

    private void ConfirmOrder(string sent, string? reply)
    {
        _orderReply.Take(reply);

        if (reply == Ok && sent == _lastOrderSent) _isLastOrderConfirmed = true;
    }

    /// <param name="text">The countdowns, one per line; empty takes them off.</param>
    /// <param name="isRefreshDue">Whether shown countdowns, or an unconfirmed removal, are due to be sent again.</param>
    public void SyncCountdown(string text, bool isRefreshDue)
    {
        _countdownReply.Report();

        if (!PauseBadge.ShouldSend(text, _lastCountdownSent, _isLastCountdownConfirmed, isRefreshDue)) return;

        if (!Run(() => Countdowns.Script(text), reply => ConfirmCountdown(text, reply), _countdownReply)) return;

        _lastCountdownSent = text;
        _isLastCountdownConfirmed = false;
    }

    private void ConfirmCountdown(string sent, string? reply)
    {
        _countdownReply.Take(reply);

        if (reply == Ok && sent == _lastCountdownSent) _isLastCountdownConfirmed = true;
    }

    /// <summary>
    /// Never throws: a page that cannot be reached costs the wheel or a note, never the ticker's pause handling. The
    /// script is only built once there is a page to run it in.
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
