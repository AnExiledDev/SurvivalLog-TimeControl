namespace TimeControl.Core;

/// <summary>What the ticker saw on one check.</summary>
/// <param name="IsFinishing">The expedition is being finished, and the game's trip home is under way.</param>
/// <param name="IsPaused">The game is paused, by anyone.</param>
/// <param name="RunningLevel">The speed level the game last ran at, which a resume goes back to.</param>
public readonly record struct FinishSnapshot(bool IsFinishing, bool IsPaused, int RunningLevel);

/// <param name="ResumeAt">The speed level to resume the game at now, or null to leave its speed alone.</param>
public readonly record struct FinishStep(ExpeditionFinish State, int? ResumeAt);

/// <summary>
/// Finishing an expedition: the game's trip home waits on game time, so a pause would hold it forever. The game itself
/// cannot pause on an expedition, so a pause standing when the finish starts is one made through this mod, and it is
/// resumed once; until the trip home is over, a pause asked for is refused.
/// </summary>
public abstract record ExpeditionFinish
{
    public static readonly ExpeditionFinish Start = new NotFinishing();

    private static readonly ExpeditionFinish Started = new Finishing();

    private ExpeditionFinish()
    {
    }

    /// <summary>No expedition is being finished.</summary>
    public sealed record NotFinishing : ExpeditionFinish;

    /// <summary>The expedition is being finished, and any pause standing when it started was resumed.</summary>
    public sealed record Finishing : ExpeditionFinish;

    /// <summary>The resume happens on the first check that sees the finish, so a pause made after it is not undone again.</summary>
    public static FinishStep Next(ExpeditionFinish state, FinishSnapshot seen) => (state, seen) switch
    {
        (_, { IsFinishing: false }) => Stay(Start),
        (Finishing, _) => Stay(state),
        (_, { IsPaused: true }) => new FinishStep(Started, PauseToggle.RunningLevel(seen.RunningLevel)),
        _ => Stay(Started),
    };

    /// <summary>A speed change asked for while the expedition is being finished is refused when it would pause the game.</summary>
    public static bool RefusesSpeed(int level, bool isFinishing) => isFinishing && level == PauseToggle.Paused;

    private static FinishStep Stay(ExpeditionFinish state) => new(state, null);
}
