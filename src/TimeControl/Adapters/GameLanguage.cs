using System;
using GameCore.HotUpdate;

namespace TimeControl.Adapters;

/// <summary>
/// Asks the game which language it is showing, only once the game has made its settings (a singleton read early
/// builds itself too soon). Anything that goes wrong reads as English.
/// </summary>
internal static class GameLanguage
{
    public static bool IsChinese()
    {
        try
        {
            return BaseSingleton<SettingManager>.IsInstanceCreated && BaseSingleton<SettingManager>.Instance?.IsChinese() == true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
