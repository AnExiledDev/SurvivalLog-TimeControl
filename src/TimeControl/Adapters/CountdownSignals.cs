using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace TimeControl.Adapters;

/// <summary>
/// What the notes under the clock read off the live world: the game clock, how long the character's current order has
/// left, and when the next zombie wave comes. Each read fails soft to "unknown", logged once, so a game update that
/// moves one costs that note and nothing else.
/// </summary>
internal static class CountdownSignals
{
    /// <summary>The game's show type for a planned event that is a zombie wave.</summary>
    private const int WaveShowType = 1;

    /// <summary>The lure rig's phase while its wave is still on the way.</summary>
    private const int LureWaveComing = 1;

    private const string LureTier = "zombielure.tier";
    private const string LurePhase = "zombielure.phase";
    private const string LureStartSecond = "zombielure.start_sec";

    private static readonly HashSet<string> LoggedFailures = new();

    internal static Action<string> Log = _ => { };

    /// <summary>The game clock in seconds since the world began, or null when it cannot be read.</summary>
    public static long? TotalSeconds(GameTimeManager time) => Read("the game clock", time, static t => (long?)t.GetTotalSeconds(), null);

    /// <summary>Game seconds until the character's current order is done; 0 when idle or unknown.</summary>
    public static int OrderSecondsLeft() => Read("the current order's time left", 0, static _ => ReadOrderSecondsLeft(), 0);

    /// <summary>The hours of the day the game has a zombie wave planned for; none when unknown.</summary>
    public static IReadOnlyList<int> WaveHours() => Read("the planned waves", 0, static _ => ReadWaveHours(), Array.Empty<int>());

    /// <summary>Game seconds until a thrown lure rig's wave; 0 when none is coming or unknown.</summary>
    public static int LureSecondsLeft(long totalSeconds) => Read("the lure rig's wave", totalSeconds, static s => ReadLureSecondsLeft(s), 0);

    private static BattleLogicWorld? World() => BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

    /// <summary>The order being carried out runs as an effect tree whose time left the effect manager keeps.</summary>
    private static int ReadOrderSecondsLeft()
    {
        if (World() is not { } world) return 0;

        var roleId = world._AgentManager?.GetLeadingRoleId() ?? 0;
        var source = roleId == 0 ? null : world._ActionManager?.GetActionSource(roleId);
        var taskId = source?._CurrentTaskBt_k__BackingField ?? 0;

        if (taskId == 0) return 0;

        return world._BTEffectManager?.GetDuration(taskId) ?? 0;
    }

    /// <summary>The game plans the day's events ahead; a wave is one whose crisis is shown as a wave.</summary>
    private static IReadOnlyList<int> ReadWaveHours()
    {
        var planned = World()?._DynamicEventManager?._CacheArgs_k__BackingField;

        if (planned == null || planned.Count == 0 || !BaseSingleton<ConfigManager>.IsInstanceCreated) return Array.Empty<int>();

        var config = BaseSingleton<ConfigManager>.Instance;
        var hours = new List<int>();

        for (var i = 0; i < planned.Count; i++)
        {
            var args = planned[i];

            if (args == null || config?.GetCrisisEvents(args._EventId_k__BackingField)?._CrisisShowType_k__BackingField != WaveShowType) continue;

            hours.Add(args._TriggerHour_k__BackingField);
        }

        return hours;
    }

    /// <summary>A thrown lure rig sets the game second its wave starts, and its phase until the wave comes.</summary>
    private static int ReadLureSecondsLeft(long totalSeconds)
    {
        if (GameCounter.GetCounter(LureTier) <= 0 || GameCounter.GetCounter(LurePhase) != LureWaveComing) return 0;

        var left = GameCounter.GetCounter(LureStartSecond) - 1 - totalSeconds;

        return left > 0 && left <= int.MaxValue ? (int)left : 0;
    }

    /// <summary>The read takes what it reads from as an argument, so a static lambda serves and no closure is made per read.</summary>
    private static T Read<TState, T>(string what, TState state, Func<TState, T> read, T unknown)
    {
        try
        {
            return read(state);
        }
        catch (Exception ex)
        {
            if (LoggedFailures.Add(what)) Log($"could not read {what}, so the note under the clock that needs it is left off (logged once): {ex.Message}");

            return unknown;
        }
    }
}
