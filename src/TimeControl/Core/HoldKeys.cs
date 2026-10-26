using System;

namespace TimeControl.Core;

public enum HoldKind
{
    /// <summary>The mod's fastest speed.</summary>
    Fast,

    /// <summary>The slowest of the slow button's speeds.</summary>
    Slow,

    /// <summary>
    /// The game's own hold-to-speed-up key, pressed while paused: the game will not speed up a paused clock, so the mod
    /// runs it at that key's speed and pauses again on letting go.
    /// </summary>
    GameKey,
}

/// <summary>Where the game was when a hold started, which letting go puts back.</summary>
/// <param name="WasPaused">The game was paused, by anyone.</param>
/// <param name="WasPausedByKey">The pause was the player's, through the mod's pause key.</param>
/// <param name="Running">The speed the game ran at, or would resume to while paused.</param>
public readonly record struct HeldFrom(bool WasPaused, bool WasPausedByKey, SpeedTarget Running);

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsFastDown">The hold-fast key is down.</param>
/// <param name="IsSlowDown">The hold-slow key is down, and there is a slow speed to hold.</param>
/// <param name="CanHold">
/// The game's speed is the player's to change: a timer they wait out, not asleep, no fast-forward of the game's own and
/// no expedition being finished.
/// </param>
/// <param name="WasChangedByOthers">Anything but the speed keys changed the game's speed since the last check.</param>
/// <param name="Now">Where the game is now, kept if a hold starts.</param>
/// <param name="IsGameKeyDown">The game's hold-to-speed-up key is down, and was pressed while paused.</param>
public readonly record struct HoldSnapshot(bool IsFastDown, bool IsSlowDown, bool CanHold, bool WasChangedByOthers, HeldFrom Now, bool IsGameKeyDown = false);

public abstract record HoldCommand
{
    public static readonly HoldCommand Nothing = new None();

    private HoldCommand()
    {
    }

    public sealed record None : HoldCommand;

    /// <summary>Run at the held speed of this kind.</summary>
    public sealed record Start(HoldKind Kind) : HoldCommand;

    /// <summary>The keys were let go: put the game back where it was.</summary>
    public sealed record Release(HeldFrom From) : HoldCommand;

    /// <summary>Something else took the speed over; leave it, but resume from a pause to where the game was.</summary>
    public sealed record Cancel(HeldFrom From) : HoldCommand;
}

public readonly record struct HoldStep(HoldKeys State, HoldCommand Command);

/// <summary>
/// Holding a key runs the game at the mod's fastest or slowest speed, and letting go puts back the speed it had. Fast
/// wins while both keys are down. When anything else changes the speed during a hold, or the speed stops being the
/// player's to change, the hold ends where it is and waits for every key to come up, so a key still held never
/// takes the speed back.
/// </summary>
public abstract record HoldKeys
{
    public static readonly HoldKeys Start = new Idle();

    private HoldKeys()
    {
    }

    public sealed record Idle : HoldKeys;

    public sealed record Holding(HoldKind Kind, HeldFrom From) : HoldKeys;

    /// <summary>A hold that ended without the keys being let go, or keys pressed where no hold could start.</summary>
    public sealed record Spent : HoldKeys;

    private static readonly HoldKeys WaitForKeysUp = new Spent();

    public static HoldStep Next(HoldKeys state, HoldSnapshot seen) => state switch
    {
        Idle => FromIdle(seen),
        Holding holding => Hold(holding, seen),
        Spent => Stay(IsAnyDown(seen) ? state : Start),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "a hold keys state Next does not handle"),
    };

    private static HoldStep FromIdle(HoldSnapshot seen)
    {
        if (Wanted(seen) is not { } kind) return Stay(Start);

        if (!seen.CanHold) return Stay(WaitForKeysUp);

        return new HoldStep(new Holding(kind, seen.Now), new HoldCommand.Start(kind));
    }

    private static HoldStep Hold(Holding holding, HoldSnapshot seen)
    {
        if (seen.WasChangedByOthers || !seen.CanHold) return new HoldStep(WaitForKeysUp, new HoldCommand.Cancel(holding.From));

        if (Wanted(seen) is not { } kind) return new HoldStep(Start, new HoldCommand.Release(holding.From));

        return kind == holding.Kind ? Stay(holding) : new HoldStep(holding with { Kind = kind }, new HoldCommand.Start(kind));
    }

    private static HoldKind? Wanted(HoldSnapshot seen)
    {
        if (seen.IsFastDown) return HoldKind.Fast;

        if (seen.IsGameKeyDown) return HoldKind.GameKey;

        return seen.IsSlowDown ? HoldKind.Slow : null;
    }

    private static bool IsAnyDown(HoldSnapshot seen) => seen.IsFastDown || seen.IsSlowDown || seen.IsGameKeyDown;

    private static HoldStep Stay(HoldKeys state) => new(state, HoldCommand.Nothing);
}
