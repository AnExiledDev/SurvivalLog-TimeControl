using System;
using ToolkitWebUi = ExiledToolkit.Web.WebUi;

namespace TimeControl.Adapters;

/// <summary>
/// The one way the mod's page links send a script, through Exiled's Toolkit. Each answer goes to the callback the link
/// passes, and each problem goes to the link's <see cref="PageReply"/>.
/// </summary>
internal static class WebUi
{
    /// <summary>
    /// False when the game has no web view to run the script in, or the script could not run. Never throws: a page that
    /// cannot be reached costs what the script draws, never the ticker's pause handling.
    /// </summary>
    public static bool Send(string script, Il2CppSystem.Action<string> onReply, PageReply reply)
    {
        try
        {
            if (ToolkitWebUi.TryRun(script, onReply, out var failure)) return true;

            if (failure != null) reply.Take("could not run script: " + failure.Message);

            return false;
        }
        catch (Exception ex)
        {
            // The Toolkit catches the script's own errors; what is left is finding the web view.
            reply.Take("could not reach the web view: " + ex.Message);

            return false;
        }
    }
}

/// <summary>The page's last answer to one kind of script, logged once each time it changes.</summary>
internal sealed class PageReply
{
    public const string Ok = "ok";

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
