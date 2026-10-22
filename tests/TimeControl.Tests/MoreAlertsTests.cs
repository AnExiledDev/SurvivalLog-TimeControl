using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

public class MoreAlertsTests
{
    private static readonly AlertSettings NoneOn = new(IsWaveOn: false, IsEmergencyOn: false, IsDroneArrivalOn: false, IsQueueEmptyOn: false);
    private static readonly AlertSnapshot Quiet = new(CanPause: true, IsPaused: false, IsWaveActive: false, EmergenciesFired: 0, DronesLanded: 0, IsBusy: true,
        ElapsedSeconds: 0.1f, WasSpeedChangedByHand: false);

    private static AlertSettings On(params Alert[] alerts) => NoneOn with { MoreOn = AlertSet.Of(alerts) };

    private static AlertSnapshot Raising(params Alert[] alerts) => Quiet with { MoreRaised = AlertSet.Of(alerts) };

    /// <summary>The alerts paused for after a world has settled on quiet checks.</summary>
    private static Alert[] PausesAfterSettling(AlertSettings settings, params AlertSnapshot[] checks)
    {
        var state = AlertPause.Start;
        var pauses = new List<Alert>();

        foreach (var seen in Enumerable.Repeat(Quiet, AlertPause.SettleChecks).Concat(checks))
        {
            var step = AlertPause.Next(state, seen, settings);

            if (step.PauseFor is { } alert) pauses.Add(alert);

            state = step.State;
        }

        return pauses.ToArray();
    }

    [Fact]
    public void An_alert_that_is_switched_on_pauses_the_game_once()
    {
        Assert.Equal(new[] { Alert.PowerOut }, PausesAfterSettling(On(Alert.PowerOut), Raising(Alert.PowerOut), Quiet with { IsPaused = true }));
    }

    [Fact]
    public void An_alert_that_is_switched_off_never_pauses()
    {
        Assert.Empty(PausesAfterSettling(On(Alert.Dusk), Raising(Alert.Dawn), Raising(Alert.Sick)));
    }

    [Fact]
    public void Turning_on_any_further_alert_counts_as_having_an_alert_on()
    {
        Assert.False(NoneOn.IsAnyOn);
        Assert.True(On(Alert.PlantRipe).IsAnyOn);
    }

    [Fact]
    public void Two_alerts_starting_together_pause_for_the_one_that_needs_the_player_most()
    {
        var settings = On(Alert.HouseHit, Alert.Dusk, Alert.LowSatiety);

        Assert.Equal(new[] { Alert.HouseHit }, PausesAfterSettling(settings, Raising(Alert.Dusk, Alert.HouseHit, Alert.LowSatiety)));
    }

    [Fact]
    public void The_original_alerts_still_come_before_the_further_ones()
    {
        var settings = On(Alert.HouseHit) with { IsWaveOn = true };

        Assert.Equal(new[] { Alert.Wave }, PausesAfterSettling(settings, Raising(Alert.HouseHit) with { IsWaveActive = true }));
    }

    [Fact]
    public void An_alert_starting_while_the_game_is_paused_is_spent_and_not_saved_for_the_resume()
    {
        Assert.Empty(PausesAfterSettling(On(Alert.PhoneMessage), Raising(Alert.PhoneMessage) with { IsPaused = true }, Quiet, Quiet));
    }

    [Fact]
    public void An_alert_starting_where_the_mod_does_not_pause_is_spent()
    {
        Assert.Empty(PausesAfterSettling(On(Alert.Dawn), Raising(Alert.Dawn) with { CanPause = false }, Quiet));
    }

    [Fact]
    public void An_alert_starting_while_the_game_is_held_for_another_is_spent()
    {
        var settings = On(Alert.PowerOut, Alert.Sick);
        var held = Quiet with { IsPaused = true };

        Assert.Equal(new[] { Alert.PowerOut }, PausesAfterSettling(settings, Raising(Alert.PowerOut), held with { MoreRaised = AlertSet.Of(Alert.Sick) }, Quiet));
    }

    [Fact]
    public void Each_new_start_of_an_alert_pauses_again_after_the_player_resumed()
    {
        var settings = On(Alert.TradeMessage);

        Assert.Equal(new[] { Alert.TradeMessage, Alert.TradeMessage },
            PausesAfterSettling(settings, Raising(Alert.TradeMessage), Quiet with { IsPaused = true }, Quiet, Raising(Alert.TradeMessage)));
    }

    [Fact]
    public void Alerts_starting_while_a_new_world_settles_are_not_news()
    {
        var state = AlertPause.Start;
        var pauses = new List<Alert>();

        for (var i = 0; i < AlertPause.SettleChecks; i++)
        {
            var step = AlertPause.Next(state, Raising(Alert.PlantRipe), On(Alert.PlantRipe));

            if (step.PauseFor is { } alert) pauses.Add(alert);

            state = step.State;
        }

        Assert.Empty(pauses);
    }

    [Fact]
    public void Every_alert_has_badge_text_in_both_languages_listed_with_the_rest()
    {
        foreach (var alert in Enum.GetValues<Alert>())
        {
            var text = PauseBadge.AlertText(alert);

            Assert.Contains(text, Texts.All);
        }
    }

    [Fact]
    public void Every_alert_fits_in_an_alert_set()
    {
        foreach (var alert in Enum.GetValues<Alert>())
        {
            Assert.True(AlertSet.Of(alert).Has(alert));
            Assert.Equal(alert, AlertSet.Of(alert).First());
        }
    }

    [Fact]
    public void An_alert_set_keeps_its_alerts_apart_and_names_the_earliest_first()
    {
        var set = AlertSet.Of(Alert.Dawn, Alert.Sick, Alert.HouseHit);

        Assert.Equal(Alert.HouseHit, set.First());
        Assert.Equal(Alert.Sick, set.Without(Alert.HouseHit).First());
        Assert.False(set.Has(Alert.TrapHit));
        Assert.Equal(AlertSet.Of(Alert.Sick), set.Within(AlertSet.Of(Alert.Sick, Alert.Dusk)));
        Assert.Null(AlertSet.None.First());
        Assert.Equal(set, set.With(Alert.Dusk, isIn: false));
    }
}
