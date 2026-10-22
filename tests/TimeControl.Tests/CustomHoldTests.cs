using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>A custom speed stays in place through pauses, and any other speed change lets it go.</summary>
public class CustomHoldTests
{
    private static readonly CustomHold Held = new CustomHold.Held(1.5f, PauseToggle.Normal);

    [Fact]
    public void A_pause_keeps_the_custom_speed_for_the_resume()
    {
        var paused = CustomHold.Next(Held, isPaused: true, PauseToggle.Normal, wasChangedByOthers: true);

        Assert.Equal(new CustomHold.Suspended(1.5f, PauseToggle.Normal), paused);
        Assert.Null(CustomHold.HeldScale(paused));
    }

    [Fact]
    public void Resuming_to_the_level_the_custom_speed_reads_as_brings_it_back()
    {
        var paused = CustomHold.Next(Held, isPaused: true, PauseToggle.Normal, wasChangedByOthers: false);
        var resumed = CustomHold.Next(paused, isPaused: false, PauseToggle.Normal, wasChangedByOthers: true);

        Assert.Equal(Held, resumed);
        Assert.Equal(1.5f, CustomHold.HeldScale(resumed));
    }

    [Fact]
    public void Resuming_to_another_level_lets_the_custom_speed_go()
    {
        var paused = CustomHold.Next(Held, isPaused: true, PauseToggle.Normal, wasChangedByOthers: false);

        Assert.Equal(CustomHold.Start, CustomHold.Next(paused, isPaused: false, PauseToggle.Triple, wasChangedByOthers: true));
    }

    [Fact]
    public void Another_speed_change_while_running_lets_the_custom_speed_go()
    {
        Assert.Equal(CustomHold.Start, CustomHold.Next(Held, isPaused: false, PauseToggle.Normal, wasChangedByOthers: true));
    }

    [Fact]
    public void Running_on_with_no_change_keeps_the_custom_speed()
    {
        Assert.Equal(Held, CustomHold.Next(Held, isPaused: false, PauseToggle.Normal, wasChangedByOthers: false));
    }

    [Fact]
    public void Staying_paused_keeps_waiting_for_the_resume()
    {
        var paused = new CustomHold.Suspended(1.5f, PauseToggle.Normal);

        Assert.Equal(paused, CustomHold.Next(paused, isPaused: true, PauseToggle.Double, wasChangedByOthers: true));
    }

    [Fact]
    public void With_no_custom_speed_there_is_nothing_to_hold()
    {
        Assert.Equal(CustomHold.Start, CustomHold.Next(CustomHold.Start, isPaused: false, PauseToggle.Normal, wasChangedByOthers: false));
        Assert.Null(CustomHold.HeldScale(CustomHold.Start));
    }
}
