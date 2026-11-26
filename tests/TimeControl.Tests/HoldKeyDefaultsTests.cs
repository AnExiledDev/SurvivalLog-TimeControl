using System.Text.Json;
using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// A hold key does nothing on a key the game binds to one of its own actions, so a default on a game key ships a
/// hold key that never works. Game 1.1.22055 bound X to Transplant Vines, which was HoldFastKey's default.
/// </summary>
public class HoldKeyDefaultsTests
{
    [Theory]
    [Trait("Category", "GameBuild")]
    [InlineData(HoldKeys.DefaultFastKey)]
    [InlineData(HoldKeys.DefaultSlowKey)]
    public void A_default_hold_key_is_no_key_the_game_binds(string key)
    {
        using var input = JsonDocument.Parse(File.ReadAllText(DumpFile("input.json")));
        var taken = input.RootElement.EnumerateObject()
            .Where(action => action.Value.GetProperty("defaultKey").GetString() == key)
            .Select(action => action.Name)
            .ToArray();

        Assert.Empty(taken);
    }

    private static string DumpFile(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "game-ref", "BepInEx", "data-dump", name);

            if (File.Exists(file)) return file;
        }

        throw new FileNotFoundException("no game-ref/BepInEx/data-dump above the tests; run tools/refresh-game-ref.sh");
    }
}
