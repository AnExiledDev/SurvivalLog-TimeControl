using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace TimeControl.Adapters;

/// <summary>
/// The workbench as time sees it: whether its window is open and which benches are crafting. A craft runs on the
/// bench's own timer, not as an order. Every read fails soft.
/// </summary>
internal static class Workbench
{
    /// <summary>The game's HandMadeState.Making: the bench's craft timer is running.</summary>
    private const int MakingState = 1;

    /// <summary>The game's type for the workbench window, looked up the first time it is asked about.</summary>
    private static Il2CppSystem.Type? _toolTableUiType;

    /// <summary>Whether the workbench window is open, by the game's own record of the menus it shows.</summary>
    public static bool IsOpen()
    {
        var ui = BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;

        return ui != null && ui.IsUIActive(_toolTableUiType ??= Il2CppType.Of<WebUI_ToolTable>());
    }

    /// <summary>Whether any workbench is crafting, stopping at the first; false when unreadable.</summary>
    public static bool IsAnyCrafting()
    {
        try
        {
            var world = BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;
            var benches = world?._ToolTableManager?.ToolTableMap;

            if (benches == null) return false;

            foreach (var bench in benches)
            {
                if (bench.Value != null && (int)bench.Value.State == MakingState) return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The furniture ids of the workbenches whose craft timer is running; null when unreadable.</summary>
    public static HashSet<long>? CraftingBenches()
    {
        try
        {
            var world = BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;
            var benches = world?._ToolTableManager?.ToolTableMap;

            if (benches == null) return null;

            var crafting = new HashSet<long>();

            foreach (var bench in benches)
            {
                if (bench.Value != null && (int)bench.Value.State == MakingState) crafting.Add(bench.Key);
            }

            return crafting;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
