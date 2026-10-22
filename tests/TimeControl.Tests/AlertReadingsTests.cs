using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class AlertReadingsTests
{
    private static readonly AlertReadings Nothing = default;

    /// <summary>The alerts raised on each check after the first, with the default low-need line.</summary>
    private static AlertSet[] Raised(params AlertReadings[] checks) => RaisedAt(AlertMemory.DefaultLowNeedPercent, checks);

    private static AlertSet[] RaisedAt(int lowNeedPercent, params AlertReadings[] checks)
    {
        var memory = AlertMemory.Start;
        var raised = new List<AlertSet>();

        foreach (var now in checks)
        {
            var step = AlertMemory.Next(memory, now, lowNeedPercent);

            raised.Add(step.Raised);
            memory = step.Memory;
        }

        return raised.ToArray();
    }

    private static AlertReadings Satiety(float? level) => Nothing with { Needs = new NeedLevels(level, null, null, null, null) };

    private static AlertReadings HouseDamage(int? damage) => Nothing with { HouseDamage = damage };

    private static AlertReadings Hour(int? hour) => Nothing with { Hour = hour };

    [Fact]
    public void A_need_falling_to_the_line_raises_its_alert()
    {
        var raised = Raised(Satiety(0.5f), Satiety(0.2f));

        Assert.Equal(AlertSet.Of(Alert.LowSatiety), raised[1]);
    }

    [Fact]
    public void A_need_wavering_at_the_line_raises_once_until_it_climbs_clear_of_the_margin()
    {
        var raised = Raised(Satiety(0.5f), Satiety(0.19f), Satiety(0.24f), Satiety(0.18f), Satiety(0.26f), Satiety(0.2f));

        Assert.True(raised[1].Has(Alert.LowSatiety));
        Assert.True(raised[2].IsEmpty);
        Assert.True(raised[3].IsEmpty);
        Assert.True(raised[4].IsEmpty);
        Assert.True(raised[5].Has(Alert.LowSatiety));
    }

    [Fact]
    public void A_need_already_low_at_the_first_reading_is_where_things_stand_not_news()
    {
        var raised = Raised(Satiety(0.1f), Satiety(0.05f), Satiety(0.01f));

        Assert.All(raised, set => Assert.True(set.IsEmpty));
    }

    [Fact]
    public void A_need_that_could_not_be_read_last_time_does_not_count_as_falling()
    {
        var raised = Raised(Satiety(0.5f), Satiety(null), Satiety(0.1f));

        Assert.All(raised, set => Assert.True(set.IsEmpty));
    }

    [Fact]
    public void Each_need_raises_its_own_alert()
    {
        var full = Nothing with { Needs = new NeedLevels(0.9f, 0.9f, 0.9f, 0.9f, 0.9f) };
        var lowFitness = Nothing with { Needs = new NeedLevels(0.9f, 0.9f, 0.9f, 0.1f, 0.9f) };
        var lowLifeAndMorale = Nothing with { Needs = new NeedLevels(0.9f, 0.1f, 0.9f, 0.9f, 0.1f) };

        Assert.Equal(AlertSet.Of(Alert.LowHealth), Raised(full, lowFitness)[1]);
        Assert.Equal(AlertSet.Of(Alert.LowMorale, Alert.LowVitality), Raised(full, lowLifeAndMorale)[1]);
    }

    [Fact]
    public void The_player_sets_where_the_low_need_line_is()
    {
        Assert.True(RaisedAt(50, Satiety(0.6f), Satiety(0.45f))[1].Has(Alert.LowSatiety));
        Assert.True(RaisedAt(10, Satiety(0.6f), Satiety(0.15f))[1].IsEmpty);
    }

    [Fact]
    public void The_low_need_line_stays_between_one_and_ninety_percent()
    {
        Assert.Equal(1, AlertMemory.ClampPercent(0));
        Assert.Equal(1, AlertMemory.ClampPercent(-20));
        Assert.Equal(20, AlertMemory.ClampPercent(20));
        Assert.Equal(90, AlertMemory.ClampPercent(100));
    }

    [Fact]
    public void The_first_damage_of_a_wave_to_the_house_raises_its_alert_and_more_damage_does_not()
    {
        var raised = Raised(HouseDamage(0), HouseDamage(4), HouseDamage(9), HouseDamage(9));

        Assert.Equal(AlertSet.Of(Alert.HouseHit), raised[1]);
        Assert.True(raised[2].IsEmpty);
        Assert.True(raised[3].IsEmpty);
    }

    [Fact]
    public void A_new_wave_reset_to_zero_raises_again_on_its_first_damage()
    {
        var raised = Raised(HouseDamage(9), HouseDamage(0), HouseDamage(3));

        Assert.True(raised[1].IsEmpty);
        Assert.Equal(AlertSet.Of(Alert.HouseHit), raised[2]);
    }

    [Fact]
    public void A_new_wave_biting_within_one_check_of_its_start_shows_as_a_drop_and_still_raises()
    {
        var raised = Raised(HouseDamage(9), HouseDamage(3));

        Assert.Equal(AlertSet.Of(Alert.HouseHit), raised[1]);
    }

    [Fact]
    public void Damage_already_done_at_the_first_reading_is_not_news()
    {
        Assert.True(Raised(HouseDamage(null), HouseDamage(5))[1].IsEmpty);
        Assert.True(Raised(HouseDamage(5))[0].IsEmpty);
    }

    [Fact]
    public void Trap_damage_raises_its_own_alert()
    {
        var raised = Raised(Nothing with { TrapDamage = 0 }, Nothing with { TrapDamage = 2 });

        Assert.Equal(AlertSet.Of(Alert.TrapHit), raised[1]);
    }

    [Fact]
    public void The_power_going_out_raises_its_alert_each_time_it_goes()
    {
        var on = Nothing with { IsPowerOut = false };
        var off = Nothing with { IsPowerOut = true };
        var raised = Raised(on, off, off, on, off);

        Assert.Equal(AlertSet.Of(Alert.PowerOut), raised[1]);
        Assert.True(raised[2].IsEmpty);
        Assert.True(raised[3].IsEmpty);
        Assert.Equal(AlertSet.Of(Alert.PowerOut), raised[4]);
    }

    [Fact]
    public void Power_already_out_at_the_first_reading_is_not_news()
    {
        Assert.All(Raised(Nothing with { IsPowerOut = true }, Nothing with { IsPowerOut = true }), set => Assert.True(set.IsEmpty));
    }

    [Fact]
    public void Catching_an_illness_raises_its_alert_and_getting_better_does_not()
    {
        var raised = Raised(Nothing with { Ailments = 0 }, Nothing with { Ailments = 1 }, Nothing with { Ailments = 1 }, Nothing with { Ailments = 0 });

        Assert.Equal(AlertSet.Of(Alert.Sick), raised[1]);
        Assert.True(raised[2].IsEmpty);
        Assert.True(raised[3].IsEmpty);
    }

    [Fact]
    public void A_new_text_message_and_a_new_trade_message_raise_their_own_alerts()
    {
        var before = Nothing with { UnreadMessages = 2, UnreadTrades = 0 };

        Assert.Equal(AlertSet.Of(Alert.PhoneMessage), Raised(before, before with { UnreadMessages = 3 })[1]);
        Assert.Equal(AlertSet.Of(Alert.TradeMessage), Raised(before, before with { UnreadTrades = 1 })[1]);
        Assert.True(Raised(before, before with { UnreadMessages = 0 })[1].IsEmpty);
    }

    [Fact]
    public void A_crop_ripening_raises_its_alert_from_the_first_check()
    {
        var raised = Raised(Nothing with { PlantsRipened = 1 }, Nothing, Nothing with { PlantsRipened = 3 });

        Assert.Equal(AlertSet.Of(Alert.PlantRipe), raised[0]);
        Assert.True(raised[1].IsEmpty);
        Assert.Equal(AlertSet.Of(Alert.PlantRipe), raised[2]);
    }

    [Fact]
    public void Night_falling_and_ending_raise_dusk_and_dawn_once_each()
    {
        var raised = Raised(Hour(18), Hour(19), Hour(23), Hour(0), Hour(6), Hour(7), Hour(8));

        Assert.Equal(AlertSet.Of(Alert.Dusk), raised[1]);
        Assert.True(raised[2].IsEmpty);
        Assert.True(raised[3].IsEmpty);
        Assert.True(raised[4].IsEmpty);
        Assert.Equal(AlertSet.Of(Alert.Dawn), raised[5]);
        Assert.True(raised[6].IsEmpty);
    }

    [Fact]
    public void An_hour_read_for_the_first_time_is_not_a_change_of_day()
    {
        Assert.True(Raised(Hour(null), Hour(20))[1].IsEmpty);
        Assert.True(Raised(Hour(20))[0].IsEmpty);
    }

    [Fact]
    public void Night_runs_from_seven_in_the_evening_until_seven_in_the_morning()
    {
        Assert.True(AlertMemory.IsNight(19));
        Assert.True(AlertMemory.IsNight(0));
        Assert.True(AlertMemory.IsNight(6));
        Assert.False(AlertMemory.IsNight(7));
        Assert.False(AlertMemory.IsNight(18));
    }
}
