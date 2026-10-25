using System.Collections.Generic;
using System.Linq;

namespace TimeControl.Core;

/// <summary>Spots a new order by its id, so finished orders dropping off the queue never read as one.</summary>
public static class OrderIds
{
    public static bool HasNew(IReadOnlyCollection<long> before, IReadOnlyCollection<long> now) =>
        now.Any(id => !before.Contains(id));
}
