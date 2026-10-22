using System;

namespace TimeControl.Core;

/// <summary>The character's needs, each as a fraction of its maximum; null where unread.</summary>
/// <param name="Health">The game shows it as fitness.</param>
/// <param name="Vitality">The game shows it as life.</param>
public readonly record struct NeedLevels(float? Satiety, float? Morale, float? Stamina, float? Health, float? Vitality);

/// <summary>What the game shows on one check for the alerts read off it; null where not watched or unreadable.</summary>
/// <param name="Hour">The hour of the game's day, 0 to 23.</param>
/// <param name="Ailments">Illnesses the character has.</param>
/// <param name="HouseDamage">Damage zombies have done to the house, doors and windows, in the current or last wave.</param>
/// <param name="TrapDamage">Damage zombies have done to traps in the current or last wave.</param>
/// <param name="UnreadMessages">Unread text messages on the phone, trade messages apart.</param>
/// <param name="UnreadTrades">Unread trade messages on the phone.</param>
/// <param name="PlantsRipened">Crops that ripened since the last check.</param>
public readonly record struct AlertReadings(
    int? Hour,
    NeedLevels Needs,
    int? Ailments,
    int? HouseDamage,
    int? TrapDamage,
    bool? IsPowerOut,
    int? UnreadMessages,
    int? UnreadTrades,
    int PlantsRipened);

/// <param name="Raised">The alerts that started since the last check.</param>
public readonly record struct AlertReadingStep(AlertMemory Memory, AlertSet Raised);

/// <summary>
/// Turns what the game shows into alerts that start: only a change from the last check counts, so a reading taken for
/// the first time, after a load or once its alert is switched on, is where things stand and not news.
/// </summary>
/// <param name="Last">The last check's readings.</param>
/// <param name="LowNeeds">The needs that are low, kept until they rise a margin above the line.</param>
public sealed record AlertMemory(AlertReadings Last, AlertSet LowNeeds)
{
    public const int DefaultLowNeedPercent = 20;

    /// <summary>How far, in percent, a low need has to rise above the line before falling to it counts again.</summary>
    public const int LowNeedMargin = 5;

    /// <summary>The game's night, from its own day clock: from 19:00 until 7:00.</summary>
    public const int NightStartHour = 19;
    public const int NightEndHour = 7;

    public static readonly AlertMemory Start = new(default(AlertReadings), AlertSet.None);

    private static readonly Alert[] NeedAlerts = { Alert.LowSatiety, Alert.LowMorale, Alert.LowStamina, Alert.LowHealth, Alert.LowVitality };

    public static int ClampPercent(int percent) => Math.Clamp(percent, 1, 90);

    public static bool IsNight(int hour) => hour < NightEndHour || hour >= NightStartHour;

    public static AlertReadingStep Next(AlertMemory memory, AlertReadings now, int lowNeedPercent)
    {
        var before = memory.Last;
        var (lowNeeds, raised) = ReadNeeds(memory, now.Needs, ClampPercent(lowNeedPercent));

        raised = raised
            .With(Alert.HouseHit, IsFirstHit(before.HouseDamage, now.HouseDamage))
            .With(Alert.TrapHit, IsFirstHit(before.TrapDamage, now.TrapDamage))
            .With(Alert.PowerOut, before.IsPowerOut == false && now.IsPowerOut == true)
            .With(Alert.Sick, HasRisen(before.Ailments, now.Ailments))
            .With(Alert.PhoneMessage, HasRisen(before.UnreadMessages, now.UnreadMessages))
            .With(Alert.TradeMessage, HasRisen(before.UnreadTrades, now.UnreadTrades))
            .With(Alert.PlantRipe, now.PlantsRipened > 0)
            .With(Alert.Dusk, before.Hour is { } dayHour && now.Hour is { } duskHour && !IsNight(dayHour) && IsNight(duskHour))
            .With(Alert.Dawn, before.Hour is { } nightHour && now.Hour is { } dawnHour && IsNight(nightHour) && !IsNight(dawnHour));

        return new AlertReadingStep(new AlertMemory(now, lowNeeds), raised);
    }

    /// <summary>
    /// A need is low at or below the line, and stays low until it rises <see cref="LowNeedMargin"/> above it, so one
    /// wavering at the line pauses once. Falling to the line counts only from a known level above it.
    /// </summary>
    private static (AlertSet LowNeeds, AlertSet Raised) ReadNeeds(AlertMemory memory, NeedLevels now, int lowNeedPercent)
    {
        var lowNeeds = AlertSet.None;
        var raised = AlertSet.None;

        foreach (var alert in NeedAlerts)
        {
            if (Level(now, alert) is not { } level) continue;

            var wasLow = memory.LowNeeds.Has(alert);
            var line = (wasLow ? lowNeedPercent + LowNeedMargin : lowNeedPercent) / 100f;

            if (level > line) continue;

            lowNeeds = lowNeeds.With(alert);
            raised = raised.With(alert, !wasLow && Level(memory.Last.Needs, alert) != null);
        }

        return (lowNeeds, raised);
    }

    private static float? Level(NeedLevels needs, Alert alert) => alert switch
    {
        Alert.LowSatiety => needs.Satiety,
        Alert.LowMorale => needs.Morale,
        Alert.LowStamina => needs.Stamina,
        Alert.LowHealth => needs.Health,
        Alert.LowVitality => needs.Vitality,
        _ => throw new ArgumentOutOfRangeException(nameof(alert), alert, "not a need"),
    };

    /// <summary>
    /// The damage count starts each wave at zero, so the first damage of a wave is a rise from zero, or a drop from
    /// the last wave's total where the new wave's first bite came within one check of its start.
    /// </summary>
    private static bool IsFirstHit(int? before, int? now) =>
        before is { } last && now is { } current && current > 0 && (last <= 0 || current < last);

    private static bool HasRisen(int? before, int? now) => before is { } last && now is { } current && current > last;
}
