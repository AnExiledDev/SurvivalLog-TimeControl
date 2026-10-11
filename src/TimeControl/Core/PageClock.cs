using System;
using System.Globalization;

namespace TimeControl.Core;

/// <summary>
/// What the clock drawn in the CoreUI1 page should show. The page runs its own clock between the game's 30-second
/// resyncs at the game speed it was last told, so a slowed or frozen day clock has to be told to the page too, or the
/// drawn time runs ahead and snaps back. The slow button's tooltip also carries the slow speed in use.
/// </summary>
public readonly record struct PageClockUpdate(float? ClockScale, string SlowLabel);

public static class PageClock
{
    /// <summary>Finds the CoreUI1 frame from the root page, the same way WebUI Doctor's clock watch does.</summary>
    private const string FindFrame =
        "var fs=document.querySelectorAll('iframe'),w=null;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        "try{if(!/CoreUI1\\.html/i.test(String(c.location)))continue;}catch(e){continue;}w=c;break;}" +
        "if(!w)return 'no CoreUI1 frame';";

    /// <summary>
    /// The update to send now, or null when the page needs nothing. While the mod changes nothing the page is left to
    /// the game; on the change back, one last update puts the clock at game speed and strips the label.
    /// </summary>
    public static PageClockUpdate? Next(float clockFactor, float battleScale, string slowLabel, bool wasNeutralLastSent)
    {
        if (IsNeutral(clockFactor, slowLabel)) return wasNeutralLastSent ? null : new PageClockUpdate(battleScale, "");

        float? clockScale = clockFactor == 1f ? null : battleScale * clockFactor;

        return new PageClockUpdate(clockScale, slowLabel);
    }

    /// <summary>True when the mod changes nothing the page shows: the clock runs at game pace and no slow speed is held.</summary>
    public static bool IsNeutral(float clockFactor, string slowLabel) => clockFactor == 1f && slowLabel.Length == 0;

    public static string Script(PageClockUpdate update)
    {
        var clock = update.ClockScale is { } scale ? scale.ToString("0.#####", CultureInfo.InvariantCulture) : "null";
        var suffix = update.SlowLabel.Length == 0 ? "" : " " + Sanitize(update.SlowLabel);

        return "(function(){try{" + FindFrame +
               "var ce=w.eval('clockEngine'),s=w.eval('state');" +
               $"var k={clock};if(k!==null&&Math.abs(ce.getTimeScale()-k)>1e-4)ce.setTimeScale(k);" +
               $"var t=String(s.i18n_speedSlow||''),b=t.replace(/ \\d+(\\.\\d+)?x$/,''),n=b+'{suffix}';if(b.length&&t!==n)s.i18n_speedSlow=n;" +
               "return 'ok';}catch(e){return 'error: '+e;}})()";
    }

    /// <summary>The label is spliced into a quoted script string, so only the characters a speed label uses survive.</summary>
    private static string Sanitize(string label)
    {
        var kept = new char[label.Length];
        var count = 0;

        foreach (var ch in label)
        {
            if (ch is >= '0' and <= '9' || ch == '.' || ch == 'x') kept[count++] = ch;
        }

        return new string(kept, 0, count);
    }
}
