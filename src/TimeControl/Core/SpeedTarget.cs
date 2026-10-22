using System;

namespace TimeControl.Core;

/// <summary>
/// A speed the speed keys, the hold keys or the mouse wheel can put the game at: one of the game's levels (with the
/// fast gear on its high speed), one of the mod's slow speeds, or a custom scale that is none of those.
/// </summary>
public abstract record SpeedTarget
{
    private SpeedTarget()
    {
    }

    /// <summary>A game speed level, on the given fast gear when it is the high speed.</summary>
    public sealed record Level(GameSpeed Speed) : SpeedTarget;

    /// <summary>The slow button's speed at this place in SlowSpeeds.</summary>
    public sealed record SlowSpeed(int Index) : SpeedTarget;

    /// <summary>A custom speed from SpeedKeys5To9, held on top of the game level it reads as.</summary>
    public sealed record Custom(float Scale) : SpeedTarget;
}

/// <summary>One stop on the mouse wheel's ladder of speeds: the scale it runs at and how to get there.</summary>
public readonly record struct LadderStep(float Scale, SpeedTarget Target);

/// <summary>Turns a <see cref="SpeedTarget"/> into words for the log.</summary>
public static class SpeedTargets
{
    public static string Describe(SpeedTarget target) => target switch
    {
        SpeedTarget.Level level when level.Speed.Gear > FastGear.GamesOwn => $"speed level {level.Speed.Level} on fast gear {level.Speed.Gear}",
        SpeedTarget.Level level => $"speed level {level.Speed.Level}",
        SpeedTarget.SlowSpeed slow => $"slow speed {slow.Index + 1}",
        SpeedTarget.Custom custom => $"custom speed {SlowGear.Label(custom.Scale)}",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "a speed target with no description"),
    };
}
