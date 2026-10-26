using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;
using Vuplex.WebView;

namespace TimeControl.Adapters;

/// <summary>
/// The game's web view, looked up once a frame for every script the mod sends in it, and the one way the mod's page
/// links send a script. Each answer goes to a callback the link made once, so a send costs the script and nothing
/// more.
/// </summary>
internal static class WebUi
{
    private static int _frame = -1;
    private static IWebView? _view;

    /// <summary>The web view, or null when the game has none to run a script in. Never throws.</summary>
    public static IWebView? View(PageReply reply)
    {
        var frame = Time.frameCount;

        if (frame == _frame) return _view;

        _frame = frame;
        _view = null;

        try
        {
            var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

            _view = ui?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
        }
        catch (Exception ex)
        {
            reply.Take("could not reach the web view: " + ex.Message);
        }

        return _view;
    }

    /// <summary>Never throws: a page that cannot be reached costs what the script draws, never the ticker's pause handling.</summary>
    public static bool Send(IWebView view, string script, Il2CppSystem.Action<string> onReply, PageReply reply)
    {
        try
        {
            view.ExecuteJavaScript(script, onReply);

            return true;
        }
        catch (Exception ex)
        {
            reply.Take("could not run script: " + ex.Message);
            _frame = -1;

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
