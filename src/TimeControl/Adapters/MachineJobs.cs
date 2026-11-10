using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Logic;

namespace TimeControl.Adapters;

/// <summary>
/// Counts the jobs machines start: a stove cooking, a barrel brewing, the dye vat, the shredder and the compressor.
/// Each runs on the machine's own timer, so the menu pause reads the count to let the game run once one starts from
/// its menu. The game starts these only when the player asks, from the machine's menu. A hook that fails to go on
/// leaves that machine's job paused with its menu, logged once.
/// </summary>
internal static class MachineJobs
{
    /// <summary>How many machine jobs have started since the game loaded; only ever compared with an earlier read.</summary>
    public static int Started { get; private set; }

    public static void Apply(PatchKit hooks)
    {
        Patch(hooks, typeof(CookingComponent), nameof(CookingComponent.StartCooking), "a stove");
        Patch(hooks, typeof(BrewComponent), nameof(BrewComponent.BeginBrew), "a barrel");
        Patch(hooks, typeof(DyeComponent), nameof(DyeComponent.Begin), "the dye vat");
        Patch(hooks, typeof(ShredderComponent), nameof(ShredderComponent.BeginShred), "the shredder");
        Patch(hooks, typeof(CompressorComponent), nameof(CompressorComponent.BeginCompress), "the compressor");
    }

    private static void Patch(PatchKit hooks, Type type, string method, string machine) =>
        hooks.Patch(typeof(MachineJobs), type, method, postfix: nameof(AfterStart),
            consequence: $"so a job started at {machine} stays paused with its menu open");

    private static void AfterStart() => Started++;
}
