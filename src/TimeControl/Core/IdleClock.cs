namespace TimeControl.Core;

/// <summary>
/// How long the player has stood idle while the day clock could have been frozen. Any action, or any moment the
/// freeze does not apply (paused, sleeping fast-forward, out exploring), starts the count again.
/// </summary>
public readonly record struct IdleClock(float IdleSeconds)
{
    public static IdleClock Start => new(0f);

    public IdleClock Advance(bool canFreeze, bool isBusy, float realSeconds) =>
        canFreeze && !isBusy && realSeconds > 0f ? new IdleClock(IdleSeconds + realSeconds) : Start;

    public bool IsFrozen(float delaySeconds) => IdleSeconds > 0f && IdleSeconds >= delaySeconds;
}
