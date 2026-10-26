using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// A sleep the mod wakes ends as a finished sleep, and gets Vim and Vigor only once the character has slept the game's
/// minimum for waking early: 3 hours. A shorter one goes without it, as a sleep the player cancels does.
/// </summary>
public class SleepRestTests
{
    /// <summary>The minimum on the sleep's own timer; the game scales the hours by its own time rate.</summary>
    private const float Needed = 900f;

    [Fact]
    public void The_minimum_is_the_three_hours_the_game_asks_for()
    {
        Assert.Equal(3f, SleepRest.MinHours);
    }

    [Fact]
    public void A_sleep_of_the_minimum_or_longer_earns_the_buff()
    {
        Assert.True(SleepRest.EarnsBuff(Needed, Needed));
        Assert.True(SleepRest.EarnsBuff(Needed * 2, Needed));
    }

    [Fact]
    public void A_sleep_just_short_of_the_minimum_does_not()
    {
        Assert.False(SleepRest.EarnsBuff(Needed - 0.01f, Needed));
        Assert.False(SleepRest.EarnsBuff(0f, Needed));
    }

    [Fact]
    public void A_bed_with_a_finish_step_gives_the_buff_after_the_minimum_and_holds_it_back_before()
    {
        Assert.Equal(WakeReward.Finished, SleepRest.Plan(earnsBuff: true, hasFinishStep: true, rewardPending: true));
        Assert.Equal(WakeReward.BuffHeldBack, SleepRest.Plan(earnsBuff: false, hasFinishStep: true, rewardPending: true));
    }

    /// <summary>The bed that gives its reward on a 6 hour timer instead of a finish step.</summary>
    [Fact]
    public void A_bed_with_a_timed_reward_gets_it_once_the_minimum_is_slept()
    {
        Assert.Equal(WakeReward.TimedRewardFired, SleepRest.Plan(earnsBuff: true, hasFinishStep: false, rewardPending: true));
    }

    [Fact]
    public void A_bed_with_a_timed_reward_gets_nothing_before_the_minimum_or_twice()
    {
        Assert.Equal(WakeReward.NothingMore, SleepRest.Plan(earnsBuff: false, hasFinishStep: false, rewardPending: true));
        Assert.Equal(WakeReward.NothingMore, SleepRest.Plan(earnsBuff: true, hasFinishStep: false, rewardPending: false));
    }

    [Fact]
    public void Only_the_held_sleep_loses_its_buff_and_only_once()
    {
        var hold = new BuffHold();

        hold.Hold(42);

        Assert.False(hold.Take(7));
        Assert.True(hold.Take(42));
        Assert.False(hold.Take(42));
        Assert.False(hold.IsAny);
    }

    [Fact]
    public void A_later_hold_replaces_the_earlier_and_a_clear_lets_every_buff_through()
    {
        var hold = new BuffHold();

        hold.Hold(1);
        hold.Hold(2);

        Assert.False(hold.Take(1));
        Assert.True(hold.Take(2));

        hold.Hold(3);
        hold.Clear();

        Assert.False(hold.IsAny);
        Assert.False(hold.Take(3));
    }
}
