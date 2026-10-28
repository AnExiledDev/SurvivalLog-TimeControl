using System;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;

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

    public static void Apply(Harmony harmony, Action<string> log)
    {
        Patch(harmony, typeof(CookingComponent), nameof(CookingComponent.StartCooking), "a stove", log);
        Patch(harmony, typeof(BrewComponent), nameof(BrewComponent.BeginBrew), "a barrel", log);
        Patch(harmony, typeof(DyeComponent), nameof(DyeComponent.Begin), "the dye vat", log);
        Patch(harmony, typeof(ShredderComponent), nameof(ShredderComponent.BeginShred), "the shredder", log);
        Patch(harmony, typeof(CompressorComponent), nameof(CompressorComponent.BeginCompress), "the compressor", log);
    }

    private static void Patch(Harmony harmony, Type type, string method, string machine, Action<string> log)
    {
        try
        {
            harmony.Patch(AccessTools.Method(type, method), postfix: new HarmonyMethod(typeof(MachineJobs), nameof(AfterStart)));
        }
        catch (Exception ex)
        {
            log($"could not hook {type.Name}.{method}, so a job started at {machine} stays paused with its menu open: {ex.Message}");
        }
    }

    private static void AfterStart() => Started++;
}
