using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class MenuFreezeTests
{
    private static FreezeSnapshot Frozen(bool menu = false, bool held = false, float elapsed = 0.1f) => new(true, menu, held, elapsed);

    [Fact]
    public void A_freeze_with_a_player_menu_open_is_lifted_so_the_speed_keys_work()
    {
        var step = MenuFreeze.Next(MenuFreeze.Start, Frozen(menu: true));

        Assert.True(step.Lifts);
    }

    [Fact]
    public void A_freeze_made_as_the_menu_opens_is_lifted_once_the_menu_shows()
    {
        var first = MenuFreeze.Next(MenuFreeze.Start, Frozen());
        var shown = MenuFreeze.Next(first.State, Frozen(menu: true));

        Assert.False(first.Lifts);
        Assert.True(shown.Lifts);
    }

    [Fact]
    public void A_freeze_no_player_menu_followed_belongs_to_the_game_and_stays()
    {
        var state = MenuFreeze.Start;

        for (var i = 0; i < 12; i++) state = MenuFreeze.Next(state, Frozen()).State;

        var menuLater = MenuFreeze.Next(state, Frozen(menu: true));

        Assert.False(menuLater.Lifts);
    }

    [Fact]
    public void A_screen_the_game_holds_time_for_keeps_its_freeze_even_over_a_menu()
    {
        var step = MenuFreeze.Next(MenuFreeze.Start, Frozen(menu: true, held: true));

        Assert.False(step.Lifts);
    }

    [Fact]
    public void Once_the_game_ends_its_own_freeze_the_next_menu_freeze_is_lifted_again()
    {
        var state = MenuFreeze.Start;

        for (var i = 0; i < 12; i++) state = MenuFreeze.Next(state, Frozen()).State;

        state = MenuFreeze.Next(state, new FreezeSnapshot(false, false, false, 0.1f)).State;

        Assert.True(MenuFreeze.Next(state, Frozen(menu: true)).Lifts);
    }

    [Fact]
    public void Nothing_is_lifted_when_the_world_is_not_frozen()
    {
        var step = MenuFreeze.Next(MenuFreeze.Start, new FreezeSnapshot(false, true, false, 0.1f));

        Assert.False(step.Lifts);
    }
}
