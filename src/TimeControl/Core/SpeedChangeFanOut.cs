using System.Collections.Generic;

namespace TimeControl.Core;

/// <summary>
/// Counts how many distinct time managers one frame's speed change reached. A live game has exactly one; more means
/// leftover game worlds are still answering the game's events, which is how one trade can be delivered many times.
/// </summary>
public sealed class SpeedChangeFanOut
{
    private readonly HashSet<long> _reachedThisFrame = new();
    private int _frame = -1;
    private int _largest;

    public void Record(int frame, long timeManager)
    {
        if (frame != _frame)
        {
            _frame = frame;
            _reachedThisFrame.Clear();
        }

        _reachedThisFrame.Add(timeManager);

        if (_reachedThisFrame.Count > _largest) _largest = _reachedThisFrame.Count;
    }

    /// <summary>The most time managers a single frame's speed change reached since the last call; 0 when none changed speed.</summary>
    public int TakeLargest()
    {
        var largest = _largest;

        _largest = 0;

        return largest;
    }
}
