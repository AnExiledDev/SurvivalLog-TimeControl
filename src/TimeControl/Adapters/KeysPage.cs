using System;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends the <see cref="SpeedWheel"/> and the notes under the clock (<see cref="OrderFinish"/>, <see cref="Countdowns"/>)
/// into the web UI, takes back the wheel's notches, and logs each distinct problem the page reports for any of them.
/// </summary>
internal sealed class KeysPage
{
    private readonly PageReply _wheelReply;
    private readonly PageReply _orderReply;
    private readonly PageReply _countdownReply;
    private readonly Il2CppSystem.Action<string> _onWheelOnReply;
    private readonly Il2CppSystem.Action<string> _onWheelOffReply;
    private int _wheelSteps;
    private bool _isLastWheelSentOn;
    private bool _isWheelOffConfirmed = true;
    private bool _isWheelOnConfirmed;
    private int _wheelGeneration;
    private string _lastOrderSent = "";
    private bool _isLastOrderConfirmed = true;
    private int _orderGeneration;
    private string _lastCountdownSent = "";
    private bool _isLastCountdownConfirmed = true;
    private int _countdownGeneration;

    public KeysPage(Action<string> log)
    {
        _wheelReply = new PageReply("speed wheel", log);
        _orderReply = new PageReply("order finish time", log);
        _countdownReply = new PageReply("countdowns", log);
        _onWheelOnReply = (Action<string>)(reply => TakeWheelReply(wasOn: true, reply));
        _onWheelOffReply = (Action<string>)(reply => TakeWheelReply(wasOn: false, reply));
    }

    /// <summary>
    /// Switches the wheel on or off over the speed buttons, at once. While on, the full script goes again once the page
    /// changed, which hooks the buttons it drew since, or until the page confirms it; otherwise only the small script
    /// that takes the notches goes out, when a poll is due. An off goes again at the poll pace until the page confirms it.
    /// </summary>
    /// <param name="isPollDue">Whether the notches are due to be taken.</param>
    /// <param name="generation">The page generation now.</param>
    public void SyncWheel(bool isOn, bool isPollDue, int generation)
    {
        _wheelReply.Report();

        if (!isOn && _isWheelOffConfirmed) return;

        var isSwitching = isOn != _isLastWheelSentOn;

        if (!isSwitching && !isPollDue) return;

        var isHooking = isSwitching || !isOn || !_isWheelOnConfirmed || generation != _wheelGeneration;

        if (WebUi.View(_wheelReply) is not { } view) return;

        var script = !isHooking ? SpeedWheel.PollScript : SpeedWheel.Script(isOn);

        if (!WebUi.Send(view, script, isOn ? _onWheelOnReply : _onWheelOffReply, _wheelReply)) return;

        if (!isHooking) return;

        _isLastWheelSentOn = isOn;
        _isWheelOffConfirmed = false;
        _isWheelOnConfirmed = false;
        _wheelGeneration = generation;
    }

    /// <summary>The notches the page handed back since the last call, up positive.</summary>
    public int TakeWheelSteps()
    {
        var steps = _wheelSteps;

        _wheelSteps = 0;

        return steps;
    }

    /// <summary>Notches handed back ask for a check on the frame they arrive, so the turn acts at once.</summary>
    private void TakeWheelReply(bool wasOn, string? reply)
    {
        var isOk = SpeedWheel.IsOk(reply);
        var steps = SpeedWheel.Steps(reply);

        _wheelReply.Take(isOk ? PageReply.Ok : reply);

        if (isOk && wasOn && _isLastWheelSentOn) _isWheelOnConfirmed = true;

        if (isOk && !wasOn && !_isLastWheelSentOn) _isWheelOffConfirmed = true;

        if (steps == 0) return;

        _wheelSteps += steps;
        ClockTicker.WantCheck();
    }

    /// <param name="label">The order's finish time; empty takes it off.</param>
    /// <param name="isRefreshDue">Whether an unconfirmed note or removal is due to be sent again.</param>
    /// <param name="generation">The page generation now; a note sent at an older one is sent again.</param>
    public void SyncOrderFinish(string label, bool isRefreshDue, int generation)
    {
        _orderReply.Report();

        if (!PauseBadge.ShouldSend(label, _lastOrderSent, _isLastOrderConfirmed, isRefreshDue, generation != _orderGeneration)) return;

        if (WebUi.View(_orderReply) is not { } view) return;

        if (!WebUi.Send(view, OrderFinish.Script(label), (Action<string>)(reply => ConfirmOrder(label, reply)), _orderReply)) return;

        _lastOrderSent = label;
        _isLastOrderConfirmed = false;
        _orderGeneration = generation;
    }

    private void ConfirmOrder(string sent, string? reply)
    {
        _orderReply.Take(reply);

        if (HudPages.IsSettled(reply) && sent == _lastOrderSent) _isLastOrderConfirmed = true;
    }

    /// <param name="text">The countdowns, one per line; empty takes them off.</param>
    /// <param name="isRefreshDue">Whether unconfirmed countdowns or a removal are due to be sent again.</param>
    /// <param name="generation">The page generation now; countdowns sent at an older one are sent again.</param>
    public void SyncCountdown(string text, bool isRefreshDue, int generation)
    {
        _countdownReply.Report();

        if (!PauseBadge.ShouldSend(text, _lastCountdownSent, _isLastCountdownConfirmed, isRefreshDue, generation != _countdownGeneration)) return;

        if (WebUi.View(_countdownReply) is not { } view) return;

        if (!WebUi.Send(view, Countdowns.Script(text), (Action<string>)(reply => ConfirmCountdown(text, reply)), _countdownReply)) return;

        _lastCountdownSent = text;
        _isLastCountdownConfirmed = false;
        _countdownGeneration = generation;
    }

    private void ConfirmCountdown(string sent, string? reply)
    {
        _countdownReply.Take(reply);

        if (HudPages.IsSettled(reply) && sent == _lastCountdownSent) _isLastCountdownConfirmed = true;
    }
}
