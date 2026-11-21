using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace TimeControl.Adapters;

/// <summary>
/// Whether a screen the player opens is up: a menu, a station, a shop, a map, the settings or a screen the game waits
/// on, such as a choice or a reward. The game counts every screen it opens or closes (UIViewLayer.ShowQueueVersion),
/// so the screens are read again only when that count moves; while nothing opens or closes, a check reads one number.
/// Every read fails soft, logged at most a few times.
/// </summary>
internal static class MenuScreens
{
    private const int MaxLoggedFailures = 3;

    private static Il2CppSystem.Type[]? _screens;
    private static int? _readAtCount;
    private static bool _isOpen;
    private static int _failures;

    public static bool IsAnyOpen()
    {
        try
        {
            var ui = GameSingleton.Existing<ReduxUISystem>();

            if (ui == null)
            {
                _readAtCount = null;

                return false;
            }

            var count = UIViewLayer.ShowQueueVersion;

            if (_readAtCount == count) return _isOpen;

            _isOpen = GameFreeze.IsAnyActive(ui, _screens ??= Screens());
            _readAtCount = count;

            return _isOpen;
        }
        catch (Exception ex)
        {
            _readAtCount = null;
            _failures++;

            if (_failures <= MaxLoggedFailures) ClockTicker.LogFileOnly($"menu: failed reading which screens are open (logged {_failures} of {MaxLoggedFailures}): {ex}");

            return false;
        }
    }

    /// <summary>The menus whose freeze the mod lifts, the screens the game keeps time stopped for, and the rest the player opens.</summary>
    private static Il2CppSystem.Type[] Screens() =>
    [
        .. GameFreeze.PlayerMenus(), .. GameFreeze.HeldScreens(),
        Il2CppType.Of<WebUI_TrashCan>(), Il2CppType.Of<WebUI_DailyFert>(), Il2CppType.Of<WebUI_CampPanel>(), Il2CppType.Of<WebUI_PawnShop>(),
        Il2CppType.Of<WebUI_VehicleShop>(), Il2CppType.Of<WebUI_FeedingUI>(), Il2CppType.Of<WebUI_RenovationPanel>(), Il2CppType.Of<WebUI_VasePanel>(),
        Il2CppType.Of<WebUI_TrapSelectPanel>(), Il2CppType.Of<WebUI_MinimapFull>(), Il2CppType.Of<WebUI_PreDisasterMap>(), Il2CppType.Of<WebUI_SignalStation>(),
        Il2CppType.Of<WebUI_TruthRecorder>(), Il2CppType.Of<WebUI_TVGame>(), Il2CppType.Of<WebUI_MusicPanel>(),
    ];
}
