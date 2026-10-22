using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TimeControl.Core;

/// <summary>One button in the wait menu.</summary>
/// <param name="Id">What the button does, one of the <see cref="WaitMenu"/> ids.</param>
/// <param name="IsEnabled">A button for a target that cannot be waited for now is shown greyed out.</param>
public readonly record struct WaitMenuItem(int Id, string Label, bool IsEnabled = true);

/// <summary>One row of the wait menu: a line of text, then its buttons.</summary>
public sealed record WaitMenuLine(string Text, IReadOnlyList<WaitMenuItem> Items);

/// <summary>What the wait button and its menu show.</summary>
/// <param name="Page">The page whose speed buttons the wait button sits beside.</param>
/// <param name="IsActive">A wait or an alarm from the menu is running, which lights the button.</param>
/// <param name="Tip">The button's tooltip.</param>
public sealed record WaitMenuView(HudPage Page, bool IsActive, string Tip, IReadOnlyList<WaitMenuLine> Lines);

/// <summary>Everything the menu is drawn from.</summary>
/// <param name="Reading">The running clock.</param>
/// <param name="QuickAlarmAt">When the alarm set from the menu rings, on the running clock; null when none is set.</param>
/// <param name="AlarmSecond">The time of day the alarm in the settings rings at; null when none is set.</param>
/// <param name="IsBrewing">A brewing barrel has a brew in it, at home.</param>
/// <param name="IsBusy">The player's character has an order queued.</param>
/// <param name="CanStartWait">A wait can start now: the character is awake and no fast-forward of the game's own runs.</param>
public readonly record struct WaitMenuInput(
    HudPage Page,
    WaitUntil Wait,
    ClockReading Reading,
    long? QuickAlarmAt,
    int? AlarmSecond,
    bool IsBrewing,
    bool IsBusy,
    bool CanStartWait,
    bool IsChinese);

/// <summary>A click in the wait menu, as the ticker carries it out.</summary>
public abstract record WaitChoice
{
    private WaitChoice()
    {
    }

    /// <param name="Hours">For <see cref="WaitKind.Hours"/>, how many hours on; otherwise 0.</param>
    public sealed record WaitFor(WaitKind Kind, int Hours) : WaitChoice;

    public sealed record AlarmIn(int Hours) : WaitChoice;

    public sealed record CancelWait : WaitChoice;

    public sealed record CancelAlarm : WaitChoice;
}

/// <summary>
/// The wait button: a small clock button beside the speed buttons, the game's own at home and the mod's copy in the
/// countdown to the disaster, that opens a short menu. It starts a wait, sets an alarm a number of hours on, and shows
/// the wait or alarm running with a button to cancel it. A click is queued on the page and handed back to the ticker,
/// as the speed buttons' are.
/// </summary>
public static class WaitMenu
{
    public const int Morning = 1;
    public const int AlarmHour = 2;
    public const int Brew = 3;
    public const int Queue = 4;
    public const int CancelWaitId = 30;
    public const int CancelAlarmId = 31;

    internal const string ElementId = "tc-wait";
    private const string StyleId = "tc-wait-style";
    private const string ClicksQueue = "__tcWaitClicks";
    private const string Ok = "ok";
    private const string ClicksPrefix = "ok:";

    /// <summary>The first id of the buttons that wait a number of hours, and of those that set an alarm that far on.</summary>
    private const int WaitHoursBase = 10;
    private const int AlarmHoursBase = 20;

    /// <summary>Defines take(), which empties the page's click queue and answers "ok", or "ok:" and the ids clicked.</summary>
    private const string DefineTakeClicks =
        $"var take=function(){{var q=w.{ClicksQueue}||[];w.{ClicksQueue}=[];return q.length?'{ClicksPrefix}'+q.join(','):'{Ok}';}};";

    private const string Icon =
        "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16zm1-13h-2v6.4l5.2 3.1 1-1.7-4.2-2.5z\"/></svg>";

    /// <summary>
    /// The look of the game's speed buttons for the wait button. The menu opens above it and grows to the left, or to
    /// the right in the countdown, whose speed buttons sit at the screen's left edge.
    /// </summary>
    private const string Style =
        "#tc-wait{position:relative;display:flex;align-items:center;}" +
        "#tc-wait .tc-wait-btn{position:relative;width:30px;height:30px;background:transparent;border:1px solid transparent;border-radius:4px;color:rgba(255,255,255,0.6);cursor:pointer;padding:0;display:flex;align-items:center;justify-content:center;outline:none;transition:all 0.2s;}" +
        "#tc-wait .tc-wait-btn:hover{background-color:rgba(255,255,255,0.1);color:white;}" +
        "#tc-wait .tc-wait-btn.active{background-color:rgba(255,193,7,0.15);color:#FFC107;border-color:rgba(255,193,7,0.3);}" +
        "#tc-wait .tc-wait-tip{position:absolute;bottom:130%;left:50%;transform:translateX(-50%);background-color:rgba(20,20,20,0.95);border:1px solid rgba(255,255,255,0.2);border-radius:4px;padding:6px 10px;color:#eee;font-size:12px;white-space:nowrap;pointer-events:none;opacity:0;visibility:hidden;transition:opacity 0.2s ease,visibility 0.2s ease;z-index:200;}" +
        "#tc-wait .tc-wait-btn:hover .tc-wait-tip{opacity:1;visibility:visible;}" +
        "#tc-wait.tc-open .tc-wait-tip{display:none;}" +
        "#tc-wait .tc-wait-menu{display:none;position:absolute;bottom:calc(100% + 8px);right:0;flex-direction:column;gap:5px;background-color:rgba(20,20,20,0.95);border:1px solid rgba(255,255,255,0.2);border-radius:6px;padding:7px;white-space:nowrap;box-shadow:0 4px 12px rgba(0,0,0,0.5);z-index:210;}" +
        "#tc-wait.tc-open .tc-wait-menu{display:flex;}" +
        "#tc-wait.tc-left .tc-wait-menu{right:auto;left:0;}" +
        "#tc-wait .tc-wait-row{display:flex;align-items:center;gap:4px;}" +
        "#tc-wait .tc-wait-text{color:#eee;font-size:12px;margin-right:auto;padding-right:8px;}" +
        "#tc-wait .tc-wait-item{background-color:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.15);border-radius:4px;color:#eee;font-size:12px;padding:3px 8px;cursor:pointer;outline:none;}" +
        "#tc-wait .tc-wait-item:hover{background-color:rgba(255,193,7,0.15);color:#FFC107;border-color:rgba(255,193,7,0.3);}" +
        "#tc-wait .tc-wait-item:disabled{opacity:0.35;cursor:default;background-color:rgba(255,255,255,0.06);color:#eee;border-color:rgba(255,255,255,0.15);}";

    private static readonly int[] HourSteps = { 1, 2, 4, 8 };

    /// <summary>
    /// The running wait and the menu's alarm come first, each with its cancel button; then the targets to wait for,
    /// the hours to wait, and the hours an alarm can be set ahead. The settings' alarm time is a target only when set;
    /// the brew and the queue are greyed out when there is nothing to wait for, and every wait while none can start.
    /// </summary>
    public static WaitMenuView View(WaitMenuInput input)
    {
        var isChinese = input.IsChinese;
        var cancel = WaitTexts.Cancel.Pick(isChinese);
        var lines = new List<WaitMenuLine>();

        if (input.Wait is WaitUntil.Waiting waiting)
        {
            lines.Add(new WaitMenuLine(WaitTexts.Waiting.Pick(isChinese) + GoalLabel(waiting.Goal, input.Reading, isChinese), new[] { new WaitMenuItem(CancelWaitId, cancel) }));
        }

        if (input.QuickAlarmAt is { } quickAt)
        {
            lines.Add(new WaitMenuLine(WaitTexts.AlarmAt.Pick(isChinese) + ClockHour.Label(input.Reading.SecondOfDayAt(quickAt)), new[] { new WaitMenuItem(CancelAlarmId, cancel) }));
        }

        var canWait = input.CanStartWait;
        var targets = new List<WaitMenuItem> { new(Morning, WaitTexts.Morning.Pick(isChinese), canWait) };

        if (input.AlarmSecond is { } alarmSecond) targets.Add(new WaitMenuItem(AlarmHour, WaitTexts.Alarm.Pick(isChinese) + ClockHour.Label(alarmSecond), canWait));

        targets.Add(new WaitMenuItem(Brew, WaitTexts.BrewDone.Pick(isChinese), canWait && input.IsBrewing));
        targets.Add(new WaitMenuItem(Queue, WaitTexts.QueueDone.Pick(isChinese), canWait && input.IsBusy));

        lines.Add(new WaitMenuLine(WaitTexts.WaitButton.Pick(isChinese), targets));
        lines.Add(new WaitMenuLine("", HourSteps.Select(hours => new WaitMenuItem(WaitHoursBase + Step(hours), WaitTexts.HoursOn(hours).Pick(isChinese), canWait)).ToArray()));
        lines.Add(new WaitMenuLine(WaitTexts.AlarmIn.Pick(isChinese), HourSteps.Select(hours => new WaitMenuItem(AlarmHoursBase + Step(hours), WaitTexts.Hours(hours).Pick(isChinese))).ToArray()));

        return new WaitMenuView(input.Page, input.Wait is WaitUntil.Waiting || input.QuickAlarmAt != null, WaitTexts.WaitButton.Pick(isChinese), lines);
    }

    /// <summary>What a button does; null for an id no button has.</summary>
    public static WaitChoice? ChoiceFor(int id) => id switch
    {
        Morning => new WaitChoice.WaitFor(WaitKind.Morning, 0),
        AlarmHour => new WaitChoice.WaitFor(WaitKind.AlarmHour, 0),
        Brew => new WaitChoice.WaitFor(WaitKind.Brew, 0),
        Queue => new WaitChoice.WaitFor(WaitKind.Queue, 0),
        CancelWaitId => new WaitChoice.CancelWait(),
        CancelAlarmId => new WaitChoice.CancelAlarm(),
        > WaitHoursBase and <= WaitHoursBase + 4 => new WaitChoice.WaitFor(WaitKind.Hours, HourSteps[id - WaitHoursBase - 1]),
        > AlarmHoursBase and <= AlarmHoursBase + 4 => new WaitChoice.AlarmIn(HourSteps[id - AlarmHoursBase - 1]),
        _ => null,
    };

    /// <summary>
    /// Where a wait for the target ends, or null when there is nothing to wait for: no alarm time set, no brew in a
    /// barrel, or no order queued.
    /// </summary>
    /// <param name="brewDoneAt">When the first brew in a barrel is done, on the running clock; null when none is brewing.</param>
    public static WaitGoal? GoalFor(WaitChoice.WaitFor choice, ClockReading reading, int? alarmSecond, long? brewDoneAt, bool isBusy) => choice.Kind switch
    {
        WaitKind.Morning => new WaitGoal.At(reading.Next(ClockHour.Morning), WaitKind.Morning),
        WaitKind.AlarmHour => alarmSecond is { } second ? new WaitGoal.At(reading.Next(second), WaitKind.AlarmHour) : null,
        WaitKind.Hours => choice.Hours > 0 ? new WaitGoal.At(reading.Total + (long)choice.Hours * ClockHour.Hour, WaitKind.Hours) : null,
        WaitKind.Brew => brewDoneAt is { } doneAt ? new WaitGoal.At(doneAt, WaitKind.Brew) : null,
        WaitKind.Queue => isBusy ? new WaitGoal.QueueDone(0f) : null,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice.Kind, "a wait target GoalFor does not handle"),
    };

    /// <summary>Whether the page drew or removed the menu: it answers "ok", or "ok:" and the clicks made since the last script.</summary>
    public static bool IsOk(string? reply) => reply == Ok || (reply?.StartsWith(ClicksPrefix, StringComparison.Ordinal) ?? false);

    /// <summary>The ids clicked since the last script, oldest first. Anything no button has is dropped.</summary>
    public static IReadOnlyList<int> Clicks(string? reply)
    {
        if (reply == null || !reply.StartsWith(ClicksPrefix, StringComparison.Ordinal)) return Array.Empty<int>();

        var clicks = new List<int>();

        foreach (var part in reply.Substring(ClicksPrefix.Length).Split(','))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && ChoiceFor(id) != null) clicks.Add(id);
        }

        return clicks;
    }

    /// <summary>
    /// Puts the wait button at the end of the page's speed buttons and takes it off any other page, then hands back the
    /// clicks. A page with no speed buttons gets no wait button. The menu is only rebuilt when what it shows changes, so
    /// it stays open across scripts; a click on a target closes it. Clicks queued by a button that was taken away are
    /// dropped, so a click made just before it went is never applied later.
    /// </summary>
    public static string Script(WaitMenuView view) =>
        "(function(){try{" + HudPages.RemoveFromFrames(ElementId, keepOn: view.Page) + HudPages.FindFrame(view.Page) +
        $"var d=w.document,g=d.getElementById('{ElementId}');if(!g)w.{ClicksQueue}=[];" + DefineTakeClicks +
        $"var a=d.querySelector('.speed-controls');if(!a){{if(g&&g.parentNode)g.parentNode.removeChild(g);w.{ClicksQueue}=[];return '{Ok}';}}" +
        $"if(!d.getElementById('{StyleId}')){{var st=d.createElement('style');st.id='{StyleId}';st.textContent='{Style}';(d.head||d.body).appendChild(st);}}" +
        $"if(!g){{g=d.createElement('div');g.id='{ElementId}';g.setAttribute('data-interactive','');" +
        $"var nb=d.createElement('button');nb.className='tc-wait-btn';nb.innerHTML='{Icon}<div class=\"tc-wait-tip\"></div>';" +
        "nb.onclick=function(){var o=this.parentNode;if(o)o.classList.toggle('tc-open');};g.appendChild(nb);" +
        "var nm=d.createElement('div');nm.className='tc-wait-menu';nm.setAttribute('data-interactive','');g.appendChild(nm);}" +
        "if(g.parentNode!==a)a.appendChild(g);" +
        "var b=g.querySelector('.tc-wait-btn'),m=g.querySelector('.tc-wait-menu'),tp=g.querySelector('.tc-wait-tip');" +
        $"var on={(view.IsActive ? "true" : "false")};if(b.classList.contains('active')!==on)b.classList.toggle('active',on);" +
        $"var lf={(view.Page == HudPage.Countdown ? "true" : "false")};if(g.classList.contains('tc-left')!==lf)g.classList.toggle('tc-left',lf);" +
        $"var t={PauseBadge.Quote(view.Tip)};if(tp&&tp.textContent!==t)tp.textContent=t;" +
        $"var D={Lines(view.Lines)},K=JSON.stringify(D);" +
        "if(m.getAttribute('data-tc-key')!==K){m.setAttribute('data-tc-key',K);while(m.children.length)m.removeChild(m.children[0]);" +
        "for(var i=0;i<D.length;i++){var r=d.createElement('div');r.className='tc-wait-row';" +
        "if(D[i][0]){var s=d.createElement('span');s.className='tc-wait-text';s.textContent=D[i][0];r.appendChild(s);}" +
        "for(var j=0;j<D[i][1].length;j++){var it=D[i][1][j],x=d.createElement('button');x.className='tc-wait-item';" +
        "x.setAttribute('data-id',String(it[0]));x.textContent=it[1];x.disabled=!it[2];" +
        $"x.onclick=function(){{if(this.disabled)return;(w.{ClicksQueue}=w.{ClicksQueue}||[]).push(+this.getAttribute('data-id'));" +
        "var o=this.parentNode&&this.parentNode.parentNode&&this.parentNode.parentNode.parentNode;if(o)o.classList.toggle('tc-open',false);};" +
        "r.appendChild(x);}m.appendChild(r);}}" +
        "return take();}catch(e){return 'error: '+e;}})()";

    /// <summary>Takes the wait button off every page.</summary>
    public static string RemovalScript() =>
        "(function(){try{" + HudPages.RemoveFromFrames(ElementId) + $"return '{Ok}';}}catch(e){{return 'error: '+e;}}}})()";

    /// <summary>The menu's rows as a JavaScript array: [text, [[id, label, enabled], ...]] per row.</summary>
    private static string Lines(IReadOnlyList<WaitMenuLine> lines)
    {
        var script = new StringBuilder("[");

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) script.Append(',');

            script.Append('[').Append(PauseBadge.Quote(lines[i].Text)).Append(",[");
            script.Append(string.Join(",", lines[i].Items.Select(item =>
                $"[{item.Id.ToString(CultureInfo.InvariantCulture)},{PauseBadge.Quote(item.Label)},{(item.IsEnabled ? "true" : "false")}]")));
            script.Append("]]");
        }

        return script.Append(']').ToString();
    }

    private static string GoalLabel(WaitGoal goal, ClockReading reading, bool isChinese) => goal switch
    {
        WaitGoal.At { Kind: WaitKind.Morning } => WaitTexts.Morning.Pick(isChinese),
        WaitGoal.At { Kind: WaitKind.Brew } => WaitTexts.BrewDone.Pick(isChinese),
        WaitGoal.At at => ClockHour.Label(reading.SecondOfDayAt(at.Total)),
        WaitGoal.QueueDone => WaitTexts.QueueDone.Pick(isChinese),
        _ => throw new ArgumentOutOfRangeException(nameof(goal), goal, "a wait goal with no label"),
    };

    /// <summary>The place of an hour step among the steps, from 1.</summary>
    private static int Step(int hours) => Array.IndexOf(HourSteps, hours) + 1;
}
