using System;

namespace TimeControl.Core;

/// <summary>
/// A story timeline the game is playing, such as the start of the disaster. The game counts its length down with the
/// frame time, which stands still while paused, so a pause holds the screen black until time runs again.
/// </summary>
/// <param name="StartedAt">The game time it started at.</param>
/// <param name="LengthSeconds">Its length in game seconds, as the game waits it out.</param>
public readonly record struct StoryTimeline(float StartedAt, double LengthSeconds)
{
    /// <summary>Game seconds past its length that it still counts as playing, so the story screen after it has opened.</summary>
    public const float MarginSeconds = 1f;

    /// <summary>
    /// Playing until its length has passed in game time. A length that is not a number counts as none, which the game
    /// waits out at once; game time read before the start means another session's clock, so it is not playing.
    /// </summary>
    public bool IsPlaying(float gameTimeNow)
    {
        var length = double.IsFinite(LengthSeconds) ? Math.Max(0d, LengthSeconds) : 0d;
        var played = gameTimeNow - StartedAt;

        return played >= 0f && played < length + MarginSeconds;
    }
}
