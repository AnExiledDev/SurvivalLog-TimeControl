using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using TimeControl.Core;

namespace TimeControl.Adapters;

/// <summary>
/// Lifts the world freeze the game makes in player menus (<see cref="MenuFreeze"/>), checked on every tick of the clock check, so the speed
/// keys and buttons work whatever menu is open. The game makes that freeze through BattleLogicWorld.OnPause, from the
/// PauseBattleLogic event each menu sends as it opens and from moments inside some menus, such as a craft finishing at
/// the workbench. The menus are read from the game's own record of the menus it shows. Every read fails soft, logged
/// at most a few times.
/// </summary>
internal static class GameFreeze
{
    private const int MaxLoggedFailures = 3;

    private static Il2CppSystem.Type[]? _playerMenus;
    private static Il2CppSystem.Type[]? _heldScreens;
    private static MenuFreeze _state = MenuFreeze.Start;
    private static int _failures;

    /// <summary>Called on every clock check while a game is running.</summary>
    public static void Update(float elapsed)
    {
        try
        {
            var world = BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;

            if (world == null)
            {
                _state = MenuFreeze.Start;

                return;
            }

            var isFrozen = world.IsPause;

            // Menus are read only while frozen, so a running game pays for one field read a check. A freeze that is the
            // game's needs no reads, and the held screens matter only over a player menu.
            var ui = isFrozen && _state is not MenuFreeze.Games && BaseSingleton<ReduxUISystem>.IsInstanceCreated ? BaseSingleton<ReduxUISystem>.Instance : null;
            var isPlayerMenuOpen = ui != null && IsAnyActive(ui, _playerMenus ??= PlayerMenus());
            var seen = new FreezeSnapshot(
                isFrozen,
                isPlayerMenuOpen,
                isPlayerMenuOpen && IsAnyActive(ui!, _heldScreens ??= HeldScreens()),
                elapsed);
            var step = MenuFreeze.Next(_state, seen);

            _state = step.State;

            if (!step.Lifts) return;

            world.OnResume();
            ClockTicker.LogFileOnly("menu: the game stopped time as a menu opened, lifted it so the speed stays yours");
        }
        catch (Exception ex)
        {
            _state = MenuFreeze.Start;
            _failures++;

            if (_failures <= MaxLoggedFailures) ClockTicker.LogFileOnly($"menu: failed reading the game's freeze (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }

    public static bool IsAnyActive(ReduxUISystem ui, Il2CppSystem.Type[] types)
    {
        foreach (var type in types)
        {
            if (ui.IsUIActive(type)) return true;
        }

        return false;
    }

    /// <summary>Menus the player opens to use something, each of which freezes the world as it opens.</summary>
    private static Il2CppSystem.Type[] PlayerMenus() =>
    [
        Il2CppType.Of<WebUI_ToolTable>(), Il2CppType.Of<WebUI_BackpackUI>(), Il2CppType.Of<WebUI_PhoneSMS>(), Il2CppType.Of<WebUI_TradeUI>(),
        .. OtherMenus(),
    ];

    /// <summary>The player menus apart from the workbench, the bag, the phone and trade, which each have a setting of their own.</summary>
    public static Il2CppSystem.Type[] OtherMenus() =>
    [
        Il2CppType.Of<WebUI_Heater>(), Il2CppType.Of<WebUI_Compressor>(), Il2CppType.Of<WebUI_Shredder>(), Il2CppType.Of<WebUI_Cooking>(),
        Il2CppType.Of<WebUI_Brew>(), Il2CppType.Of<WebUI_MaterialRack>(), Il2CppType.Of<WebUI_AidDepot>(), Il2CppType.Of<WebUI_AddFertilizer>(),
        Il2CppType.Of<WebUI_RatCage>(), Il2CppType.Of<WebUI_RobotPanel>(), Il2CppType.Of<WebUI_GreenhouseBuild>(), Il2CppType.Of<WebUI_DroneHub>(),
        Il2CppType.Of<WebUI_TrapPanel>(), Il2CppType.Of<WebUI_Dye>(), Il2CppType.Of<WebUI_PlantPanel>(), Il2CppType.Of<WebUI_FuelGenerator>(),
        Il2CppType.Of<WebUI_ShopUI>(), Il2CppType.Of<WebUI_BuildShop>(), Il2CppType.Of<WebUI_Codex>(), Il2CppType.Of<WebUI_Achievement>(),
        Il2CppType.Of<WebUI_DataStatistics>(), Il2CppType.Of<WebUI_DossierUI>(), Il2CppType.Of<WebUI_SurvivalLogs>(), Il2CppType.Of<WebUI_ExploreMap>(),
    ];

    /// <summary>Screens the game must keep time stopped for: stories, endings, settlements, choices, rewards and the settings.</summary>
    private static Il2CppSystem.Type[] HeldScreens() =>
    [
        Il2CppType.Of<WebUI_OutSetting>(), Il2CppType.Of<WebUI_Settlement>(), Il2CppType.Of<WebUI_DailySettlement>(), Il2CppType.Of<WebUI_EndlessSettlement>(),
        Il2CppType.Of<WebUI_EndingShow>(), Il2CppType.Of<WebUI_EndingChoice>(), Il2CppType.Of<WebUI_RogueSelect>(), Il2CppType.Of<WebUI_RebirthUpgrade>(),
        Il2CppType.Of<WebUI_EventChoice>(), Il2CppType.Of<UI_DynamicEventChose>(), Il2CppType.Of<WebUI_SkillLevelUpPopup>(), Il2CppType.Of<WebUI_TowerWaveReport>(),
        Il2CppType.Of<WebUI_RewardList>(), Il2CppType.Of<UI_ExploreReward>(), Il2CppType.Of<UI_SentryFeedback>(), Il2CppType.Of<WebUI_PlanTalent>(),
        Il2CppType.Of<UI_PlanTalent>(), Il2CppType.Of<UI_TransitionStory>(),
    ];
}
