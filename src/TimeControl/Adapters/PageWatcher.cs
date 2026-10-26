using System;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Sends the <see cref="PageWatch"/> look at the page pace and counts the page's changes, so the page links send the
/// badge, the chip, the notes and the buttons again only after the page changed under them.
/// </summary>
internal sealed class PageWatcher
{
    private readonly PageReply _reply;
    private readonly Il2CppSystem.Action<string> _onReply;
    private PageGeneration _seen = PageGeneration.Start;

    public PageWatcher(Action<string> log)
    {
        _reply = new PageReply("page watch", log);
        _onReply = (Action<string>)Take;
    }

    /// <summary>How many times the page changed so far, as the watch saw it; what a page link sent at an older number may be gone.</summary>
    public int Generation => _seen.Number;

    /// <summary>The slow speed's text on the main HUD page at the last look; null before one was read.</summary>
    public string? SlowText => _seen.Last?.SlowText;

    public void Look()
    {
        _reply.Report();

        if (WebUi.View(_reply) is { } view) WebUi.Send(view, PageWatch.Script, _onReply, _reply);
    }

    private void Take(string reply)
    {
        var look = PageWatch.Read(reply);

        _reply.Take(look != null ? PageReply.Ok : reply);
        _seen = PageWatch.Next(_seen, look);
    }
}
