namespace TimeControl.Core;

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsFinishing">The expedition is being finished, and the game's trip home is under way.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="RunningLevel">The speed level the game last ran at, which a resume goes back to.</param>
/// <param name="WasPauseRefused">A pause asked for since the last check was refused.</param>
public readonly record struct FinishSnapshot(bool IsFinishing, bool IsPaused, int RunningLevel, bool WasPauseRefused);

/// <param name="ResumeAt">The speed level to resume the game at now, or null to leave its speed alone.</param>
/// <param name="TellsOfRefusal">Say that pausing does nothing until the trip home is over.</param>
public readonly record struct FinishStep(ExpeditionFinish State, int? ResumeAt, bool TellsOfRefusal);

/// <summary>
/// Finishing an expedition: the game's trip home waits on game time, so a pause would hold it forever. The game itself
/// cannot pause on an expedition, so a pause standing when the finish starts is one made through this mod, and it is
/// resumed once; until the trip home is over, a pause asked for is refused, and the first refusal of each finish is
/// told.
/// </summary>
public abstract record ExpeditionFinish
{
    public static readonly ExpeditionFinish Start = new NotFinishing();

    private static readonly Finishing Started = new(HasToldOfRefusal: false);

    private ExpeditionFinish()
    {
    }

    /// <summary>No expedition is being finished.</summary>
    public sealed record NotFinishing : ExpeditionFinish;

    /// <summary>The expedition is being finished, and any pause standing when it started was resumed.</summary>
    /// <param name="HasToldOfRefusal">A refused pause was already told of during this finish.</param>
    public sealed record Finishing(bool HasToldOfRefusal) : ExpeditionFinish;

    /// <summary>The resume happens on the first check that sees the finish, so a pause made after it is not undone again.</summary>
    public static FinishStep Next(ExpeditionFinish state, FinishSnapshot seen) => (state, seen) switch
    {
        (_, { IsFinishing: false }) => Stay(Start),
        (Finishing finishing, _) => NoteRefusal(finishing, seen),
        (_, { IsPaused: true }) => NoteRefusal(Started, seen) with { ResumeAt = PauseToggle.RunningLevel(seen.RunningLevel) },
        _ => NoteRefusal(Started, seen),
    };

    /// <summary>A speed change asked for while the expedition is being finished is refused when it would pause the game.</summary>
    public static bool RefusesSpeed(int level, bool isFinishing) => isFinishing && level == PauseToggle.Paused;

    private static FinishStep NoteRefusal(Finishing finishing, FinishSnapshot seen) =>
        seen.WasPauseRefused && !finishing.HasToldOfRefusal
            ? new FinishStep(finishing with { HasToldOfRefusal = true }, null, TellsOfRefusal: true)
            : Stay(finishing);

    private static FinishStep Stay(ExpeditionFinish state) => new(state, null, TellsOfRefusal: false);
}
