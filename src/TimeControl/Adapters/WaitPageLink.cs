using System;
using System.Collections.Generic;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends the <see cref="WaitMenu"/> into the web UI, takes back the clicks on it, and logs each distinct problem the
/// page reports. The menu is built and sent in full only when what it shows changes, when the page changed under it,
/// or until the page confirms it; between, only the small script that takes back the clicks goes out.
/// </summary>
internal sealed class WaitPageLink
{
    private readonly PageReply _reply;
    private readonly Il2CppSystem.Action<string> _onCollectReply;
    private readonly List<int> _clicks = new();
    private WaitMenuInput? _builtInput;
    private string _builtScript = "";
    private string _lastSent = "";
    private bool _isLastSentConfirmed = true;
    private int _sentGeneration;

    public WaitPageLink(Action<string> log)
    {
        _reply = new PageReply("wait menu", log);
        _onCollectReply = (Action<string>)TakeCollectReply;
    }

    /// <summary>
    /// Sent on the badge's terms: a change goes out at once, a shown menu goes again once the page changed under it,
    /// and anything the page has not confirmed goes again on each refresh. A shown menu that needs nothing sent has its
    /// clicks taken on each refresh.
    /// </summary>
    /// <param name="input">What the menu is drawn from; null takes it off the page and drops any clicks not yet taken.</param>
    /// <param name="isRefreshDue">Whether the clicks, or anything unconfirmed, are due to be sent for.</param>
    /// <param name="generation">The page generation now; a menu sent at an older one is sent again.</param>
    public void Sync(WaitMenuInput? input, bool isRefreshDue, int generation)
    {
        _reply.Report();

        if (input == null) _clicks.Clear();

        var script = input is { } shown ? ScriptFor(shown) : "";

        if (PauseBadge.ShouldSend(script, _lastSent, _isLastSentConfirmed, isRefreshDue, generation != _sentGeneration))
        {
            if (WebUi.View(_reply) is not { } view) return;

            if (!WebUi.Send(view, script.Length > 0 ? script : WaitMenu.RemovalScript(), (Action<string>)(reply => TakeReply(script, reply)), _reply)) return;

            _lastSent = script;
            _isLastSentConfirmed = false;
            _sentGeneration = generation;

            return;
        }

        if (!isRefreshDue || input is not { } collected || script.Length == 0) return;

        if (WebUi.View(_reply) is { } webView) WebUi.Send(webView, WaitMenu.CollectScript(collected.Page), _onCollectReply, _reply);
    }

    /// <summary>The menu is built again only when the input draws a different one; a clock that only ran on does not.</summary>
    private string ScriptFor(WaitMenuInput input)
    {
        if (_builtInput is { } built && WaitMenu.IsSameMenu(built, input)) return _builtScript;

        _builtScript = WaitMenu.Script(WaitMenu.View(input));
        _builtInput = input;

        return _builtScript;
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
    /// Only an answer that settles the script still last sent confirms it; a HUD page that is not open settles it too,
    /// since the page watch sees it open, which sends the menu again.
    /// </summary>
    private void TakeReply(string sent, string? reply)
    {
        var isOk = WaitMenu.IsOk(reply);

        _reply.Take(isOk ? PageReply.Ok : reply);

        if ((isOk || HudPages.IsClosedReply(reply)) && sent == _lastSent) _isLastSentConfirmed = true;

        AddClicks(reply);
    }

    /// <summary>A page that lost the menu, or could not run the script, has the menu sent in full on the next refresh.</summary>
    private void TakeCollectReply(string? reply)
    {
        var isOk = WaitMenu.IsOk(reply);

        _reply.Take(isOk ? PageReply.Ok : reply);

        if (!isOk && !HudPages.IsClosedReply(reply)) _isLastSentConfirmed = false;

        AddClicks(reply);
    }

    /// <summary>
    /// Clicks are kept only while the menu is meant to show, so one made just before it was taken away is never applied
    /// later. They ask for a check on the frame they arrive, so a click acts at once.
    /// </summary>
    private void AddClicks(string? reply)
    {
        if (_lastSent.Length == 0) return;

        var clicks = WaitMenu.Clicks(reply);

        if (clicks.Count == 0) return;

        _clicks.AddRange(clicks);
        ClockTicker.WantCheck();
    }
}
