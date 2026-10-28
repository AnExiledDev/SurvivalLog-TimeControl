using System.Text.RegularExpressions;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// ClockTicker is a MonoBehaviour that Il2CppInterop copies into the game at start. It tries to give the game
/// every instance method of it, and any method that takes or returns one of the mod's own types makes it log a
/// warning, or an "Assembly TimeControl.dll is not registered in il2cpp" error for the mod's enums. Only the
/// methods Unity calls need to reach the game; every other one carries HideFromIl2Cpp.
/// </summary>
public class TickerInjectionTests
{
    private static readonly string[] Files = ["ClockTicker.cs", "ClockTicker.Alerts.cs", "ClockTicker.Keys.cs", "ClockTicker.Wait.cs"];

    private static readonly string[] GameCalls = ["ClockTicker", "Update", "LateUpdate"];

    private static readonly Regex Declaration = new(@"^    (?:private|public|internal|protected)\s+(?!static\b)(?!const\b)(?:[\w<>\[\]?,.]+\s+)?(\w+)\(", RegexOptions.Compiled);

    [Fact]
    public void Only_the_methods_Unity_calls_are_given_to_the_game()
    {
        var shown = new List<string>();
        var hidden = new List<string>();

        foreach (var file in Files)
        {
            var lines = Source(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var match = Declaration.Match(lines[i]);

                if (!match.Success)
                {
                    continue;
                }

                (IsHidden(lines, i) ? hidden : shown).Add(match.Groups[1].Value);
            }
        }

        Assert.Equal(GameCalls.OrderBy(n => n), shown.OrderBy(n => n));
        Assert.Contains("IdleAfterHold", hidden);
        Assert.Contains("StartHold", hidden);
        Assert.Contains("UpdateSpeedKeys", hidden);
        Assert.True(hidden.Count > 40, $"only {hidden.Count} hidden methods found");
    }

    private static bool IsHidden(string[] lines, int declaration)
    {
        for (var i = declaration - 1; i >= 0 && lines[i].TrimStart().StartsWith('['); i--)
        {
            if (lines[i].Trim() == "[HideFromIl2Cpp]")
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Source(string file)
    {
        var name = "TimeControl.Tests.Ticker." + file;
        using var stream = typeof(TickerInjectionTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');
    }
}
