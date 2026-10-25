using System.Collections.Generic;
using System.Linq;

namespace TimeControl.Core;

/// <summary>Spots a new order by its id, so finished orders dropping off the queue never read as one.</summary>
public static class OrderIds
{
    public static bool HasNew(IReadOnlyCollection<long> before, IReadOnlyCollection<long> now) =>
        now.Any(id => !before.Contains(id));
}

/// <summary>
/// The work under way while a menu is open: the ids of the player's orders, and the furniture ids of the workbenches
/// crafting. A craft at a workbench runs on the bench's own timer, not as an order, so it is watched on its own. The
/// two kinds of id are kept apart, as a furniture id can equal an order id.
/// </summary>
public readonly record struct MenuWork(IReadOnlyCollection<long> Orders, IReadOnlyCollection<long> CraftingBenches)
{
    /// <summary>An order or a craft is in <paramref name="now"/> that was not in <paramref name="before"/>.</summary>
    public static bool HasNew(MenuWork before, MenuWork now) =>
        OrderIds.HasNew(before.Orders, now.Orders) || OrderIds.HasNew(before.CraftingBenches, now.CraftingBenches);
}
