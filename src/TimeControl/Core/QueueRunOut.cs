namespace TimeControl.Core;

/// <summary>When the player's order queue has run out, shared by the queue pause and the queue slow-down.</summary>
public static class QueueRunOut
{
    /// <summary>Longer than the moment between two queued orders, when the game reads the queue as empty.</summary>
    public const float SettleSeconds = 0.3f;

    /// <summary>
    /// The game reads the queue as empty for a moment between two queued orders, so it has run out only once it has
    /// stayed empty for <see cref="SettleSeconds"/>; an order in that time means it never ran out. Each run-out
    /// counts once.
    /// </summary>
    /// <param name="emptySeconds">How long the queue has read empty since it last read busy, 0 while busy; null when no run-out is pending.</param>
    public static (float? EmptySeconds, bool HasRunOut) Track(float? emptySeconds, bool isBusy, float elapsedSeconds)
    {
        if (isBusy) return (0f, false);

        if (emptySeconds is not { } before) return (null, false);

        var after = before + elapsedSeconds;

        return after >= SettleSeconds ? (null, true) : (after, false);
    }
}
