using System;

namespace TimeControl.Core;

/// <summary>The alarm set in the settings.</summary>
/// <param name="SecondOfDay">The time of day it rings at, or null when no alarm is set.</param>
/// <param name="IsDaily">It rings every day; otherwise only the first time the clock reaches it.</param>
public readonly record struct AlarmSettings(int? SecondOfDay, bool IsDaily);

/// <summary>What the ticker saw on one check.</summary>
/// <param name="Timer">The timer running, as the mod controls it.</param>
/// <param name="Reading">The running clock, or null when it could not be read.</param>
/// <param name="CanRing">
/// The game is where an alarm may pause it: the character is awake, no fast-forward of the game's own runs and no
/// expedition is being finished.
/// </param>
public readonly record struct AlarmSnapshot(GameTimer Timer, ClockReading? Reading, bool CanRing);

/// <summary>Which alarm rang.</summary>
public enum AlarmRing
{
    /// <summary>The alarm set in the settings.</summary>
    Set,

    /// <summary>An alarm set from the wait menu, a number of hours ahead.</summary>
    Quick,
}

/// <param name="Rang">The alarm that rang on this check, which pauses the game; null when none did.</param>
public readonly record struct AlarmStep(AlarmClock State, AlarmRing? Rang);

/// <summary>
/// Alarm clock: pauses the game once when the clock reaches a time of day, at home and in the countdown to the
/// disaster. The alarm from the settings rings every day, or only the first time; one set from the wait menu rings
/// once, a number of hours on. An alarm that comes due while the character sleeps rings when they wake, and one that
/// comes due while the game is already paused is spent without changing anything.
/// </summary>
/// <param name="SetAlarmAt">When the settings' alarm next rings, on the clock of <paramref name="Timer"/>; null when it is not armed.</param>
/// <param name="QuickAlarmAt">When the menu's alarm rings, on the clock of <paramref name="Timer"/>; null when none is set.</param>
/// <param name="Timer">The timer both alarms count on.</param>
/// <param name="ArmedFor">The settings <paramref name="SetAlarmAt"/> was worked out from.</param>
/// <param name="SpentOnce">Settings whose one-time alarm has rung; it stays quiet until they change.</param>
public sealed record AlarmClock(long? SetAlarmAt, long? QuickAlarmAt, GameTimer Timer, AlarmSettings ArmedFor, AlarmSettings? SpentOnce)
{
    public static readonly AlarmClock Start = new(null, null, GameTimer.Other, default, null);

    /// <summary>A new world has a clock of its own, so nothing armed on the old one carries over; a spent one-time alarm stays spent.</summary>
    public static AlarmClock ForNewWorld(AlarmClock state) => Start with { ArmedFor = state.ArmedFor, SpentOnce = state.SpentOnce };

    /// <summary>Alarms ring on the clock of the day at home and on the countdown to the disaster.</summary>
    public static bool CanRingUnder(GameTimer timer) => WaitUntil.CanWait(timer);

    /// <summary>Sets the menu's alarm a number of hours ahead, in place of one already set.</summary>
    public static AlarmClock SetQuick(AlarmClock state, ClockReading reading, GameTimer timer, int hours)
    {
        var onClock = Follow(state, timer);

        return onClock with { QuickAlarmAt = reading.Total + (long)hours * ClockHour.Hour };
    }

    public static AlarmClock CancelQuick(AlarmClock state) => state with { QuickAlarmAt = null };

    /// <summary>
    /// The settings' alarm is worked out again whenever the timer or the settings change, so a new time in the settings
    /// or a new clock never rings at a stale moment. The menu's alarm only lives on the clock it was set on.
    /// </summary>
    public static AlarmStep Next(AlarmClock state, AlarmSnapshot seen, AlarmSettings settings)
    {
        if (!CanRingUnder(seen.Timer) || seen.Reading is not { } reading) return Stay(state with { SetAlarmAt = null, QuickAlarmAt = null, Timer = seen.Timer });

        var armed = Arm(Follow(state, seen.Timer), reading, settings);

        if (!seen.CanRing) return Stay(armed);

        if (armed.QuickAlarmAt is { } quickAt && reading.Total >= quickAt) return new AlarmStep(armed with { QuickAlarmAt = null }, AlarmRing.Quick);

        if (armed.SetAlarmAt is not { } setAt || reading.Total < setAt) return Stay(armed);

        var rung = settings.IsDaily && settings.SecondOfDay is { } daily
            ? armed with { SetAlarmAt = reading.Next(daily) }
            : armed with { SetAlarmAt = null, SpentOnce = settings };

        return new AlarmStep(rung, AlarmRing.Set);
    }

    /// <summary>A new timer is a new clock, on which the alarms set on the old one mean nothing.</summary>
    private static AlarmClock Follow(AlarmClock state, GameTimer timer) =>
        state.Timer == timer ? state : state with { SetAlarmAt = null, QuickAlarmAt = null, Timer = timer };

    private static AlarmClock Arm(AlarmClock state, ClockReading reading, AlarmSettings settings)
    {
        if (state.ArmedFor != settings) state = state with { SetAlarmAt = null, ArmedFor = settings, SpentOnce = null };

        if (state.SetAlarmAt != null || settings.SecondOfDay is not { } secondOfDay || state.SpentOnce == settings) return state;

        return state with { SetAlarmAt = reading.Next(secondOfDay) };
    }

    private static AlarmStep Stay(AlarmClock state) => new(state, null);
}
