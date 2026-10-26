namespace TimeControl.Core;

/// <summary>A check due now: the pace counted on from it, and the seconds since the check before.</summary>
public readonly record struct CheckDue(CheckPace Pace, float ElapsedSeconds);

/// <summary>
/// When the ticker checks: ten times a second, and also on the frame a key or a click asks for one, so a hold key, a
/// custom speed key or a click on the mod's buttons acts on the frame it is seen rather than up to a check later. The
/// rest of the frames do nothing.
/// </summary>
/// <param name="NextAt">The time the next check falls due without being asked for.</param>
/// <param name="LastAt">The time of the last check; negative before the first.</param>
public readonly record struct CheckPace(float NextAt, float LastAt)
{
    public const float Seconds = 0.1f;

    public static readonly CheckPace Start = new(0f, -1f);

    /// <summary>The check due this frame, or null when none is.</summary>
    /// <param name="isWanted">A key or a click is waiting on a check.</param>
    public static CheckDue? Take(CheckPace pace, float now, bool isWanted)
    {
        if (now < pace.NextAt && !isWanted) return null;

        var elapsed = pace.LastAt < 0f ? 0f : now - pace.LastAt;

        return new CheckDue(new CheckPace(now + Seconds, now), elapsed);
    }
}

/// <summary>
/// The wave and the alerts after the queue running out are read off the game a few times a second rather than on
/// every check: each read asks the game for several objects, and a quarter of a second is still well inside the
/// moment the danger starts. Between reads the last wave reading stands, so a wave that started is never missed.
/// </summary>
/// <param name="NextReadAt">The time the next read falls due.</param>
/// <param name="IsWaveActive">The wave as the last read found it.</param>
public readonly record struct DangerWatch(float NextReadAt, bool IsWaveActive)
{
    public const float ReadSeconds = 0.25f;

    public static readonly DangerWatch Start = new(0f, false);

    public bool IsDue(float now) => now >= NextReadAt;

    /// <summary>What a read found, with the next read counted on from now.</summary>
    public DangerWatch Read(float now, bool isWaveActive) => new(now + ReadSeconds, isWaveActive);
}
