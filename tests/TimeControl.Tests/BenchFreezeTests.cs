using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class BenchFreezeTests
{
    [Fact]
    public void A_freeze_the_open_workbench_made_is_lifted_so_the_speed_keys_work()
    {
        Assert.True(BenchFreeze.Lifts(isOn: true, isBenchOpen: true, wasFrozen: false, isFrozen: true));
    }

    [Fact]
    public void A_freeze_that_was_already_on_belongs_to_someone_else_and_stays()
    {
        Assert.False(BenchFreeze.Lifts(isOn: true, isBenchOpen: true, wasFrozen: true, isFrozen: true));
    }

    [Fact]
    public void A_craft_finishing_with_the_window_closed_keeps_the_game_freeze()
    {
        Assert.False(BenchFreeze.Lifts(isOn: true, isBenchOpen: false, wasFrozen: false, isFrozen: true));
    }

    [Fact]
    public void With_the_setting_off_the_game_freeze_stays()
    {
        Assert.False(BenchFreeze.Lifts(isOn: false, isBenchOpen: true, wasFrozen: false, isFrozen: true));
    }

    [Fact]
    public void Nothing_is_lifted_when_the_game_did_not_freeze()
    {
        Assert.False(BenchFreeze.Lifts(isOn: true, isBenchOpen: true, wasFrozen: false, isFrozen: false));
    }
}
