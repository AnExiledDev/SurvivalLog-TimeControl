namespace TimeControl.Core;

/// <summary>
/// When the character's current order will be done, by the game clock, shown under the clock at home. Only the order
/// being carried out counts: the game sets an order's length when it starts, so the orders waiting behind it have
/// none to add up yet.
/// </summary>
public static class OrderFinish
{
    public const string ElementId = "tc-order-finish";

    private const int SecondsPerDay = 86400;

    /// <param name="clockSeconds">The game's clock in seconds; whole days are dropped.</param>
    /// <param name="secondsLeft">Game seconds until the current order is done; 0 or less when unknown or idle.</param>
    /// <returns>The note, such as "Current order done at 14:30"; empty when there is nothing to show.</returns>
    public static string Label(long clockSeconds, long secondsLeft, bool isChinese)
    {
        if (secondsLeft <= 0 || clockSeconds < 0) return "";

        var doneAt = (clockSeconds + secondsLeft) % SecondsPerDay;

        return $"{KeysTexts.OrderDoneAt.Pick(isChinese)} {doneAt / 3600:00}:{doneAt % 3600 / 60:00}";
    }

    /// <summary>Puts the note under the clock, above the countdowns; an empty label takes it off.</summary>
    public static string Script(string label) => ClockNotes.Script(ElementId, label, belowId: Countdowns.ElementId);
}
