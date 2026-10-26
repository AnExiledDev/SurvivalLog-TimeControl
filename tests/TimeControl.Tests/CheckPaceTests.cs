using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>
/// The ticker checks a few times a second rather than on every frame, and on the frame a key asks for a check. Frames
/// here run at 60 a second, each taking the check due on it as the ticker does.
/// </summary>
public class CheckPaceTests
{
    private const float Frame = 1f / 60f;

    private static readonly HeldFrom RunningNormal = new(false, false, new SpeedTarget.Level(new GameSpeed(PauseToggle.Normal, FastGear.GamesOwn)));

    private static float At(int frame) => frame * Frame;

    [Fact]
    public void A_hold_key_starts_its_hold_on_the_frame_it_is_pressed_not_on_the_next_paced_check()
    {
        const int pressedOn = 4;
        var pace = CheckPace.Start;
        var hold = HoldKeys.Start;
        int? startedOn = null;

        for (var frame = 0; frame < 30 && startedOn == null; frame++)
        {
            var isDown = frame >= pressedOn;

            // The key's first frame down asks for a check, as the key patch does.
            if (CheckPace.Take(pace, At(frame), isWanted: frame == pressedOn) is not { } due) continue;

            pace = due.Pace;

            var step = HoldKeys.Next(hold, new HoldSnapshot(isDown, false, true, false, RunningNormal, false));

            hold = step.State;

            if (step.Command is HoldCommand.Start) startedOn = frame;
        }

        Assert.Equal(pressedOn, startedOn);
    }

    [Fact]
    public void With_nothing_asking_the_checks_run_ten_times_a_second_not_on_every_frame()
    {
        var pace = CheckPace.Start;
        var checks = 0;

        for (var frame = 0; frame < 120; frame++)
        {
            if (CheckPace.Take(pace, At(frame), isWanted: false) is not { } due) continue;

            pace = due.Pace;
            checks++;
        }

        Assert.InRange(checks, 17, 21);
    }

    [Fact]
    public void Each_check_counts_the_time_since_the_check_before_it_asked_for_or_not()
    {
        var pace = CheckPace.Start;
        float? firstAt = null;
        var lastAt = 0f;
        var counted = 0f;

        for (var frame = 0; frame < 120; frame++)
        {
            if (CheckPace.Take(pace, At(frame), isWanted: frame % 7 == 3) is not { } due) continue;

            pace = due.Pace;
            firstAt ??= At(frame);
            lastAt = At(frame);
            counted += due.ElapsedSeconds;
        }

        var span = lastAt - firstAt!.Value;

        Assert.InRange(counted, span - 0.001f, span + 0.001f);
    }

    [Fact]
    public void A_check_asked_for_counts_the_next_paced_check_on_from_itself()
    {
        var first = CheckPace.Take(CheckPace.Start, 0f, isWanted: false)!.Value;
        var asked = CheckPace.Take(first.Pace, 0.05f, isWanted: true)!.Value;

        Assert.Null(CheckPace.Take(asked.Pace, 0.11f, isWanted: false));
        Assert.NotNull(CheckPace.Take(asked.Pace, 0.16f, isWanted: false));
    }
}

/// <summary>
/// The wave is read off the game a few times a second rather than on every check. The alert must still pause soon
/// after the wave starts, and the reads must run well below the checks.
/// </summary>
public class DangerWatchTests
{
    private const float Frame = 1f / 60f;
    private const float WaveStartsAt = 1.53f;

    private static readonly AlertSettings Defaults = new(IsWaveOn: true, IsEmergencyOn: true, IsDroneArrivalOn: false, IsQueueEmptyOn: false);
    private static readonly AlertSnapshot Quiet = new(CanPause: true, IsPaused: false, IsWaveActive: false, EmergenciesFired: 0, DronesLanded: 0, IsBusy: false,
        ElapsedSeconds: 0.1f, WasSpeedChangedByHand: false);

    /// <summary>Runs frames for the given seconds the way the ticker does: the check due, the danger read when due, then the alert step.</summary>
    private static (float? PausedAt, int Checks, int Reads) Run(float seconds, float waveStartsAt)
    {
        var pace = CheckPace.Start;
        var danger = DangerWatch.Start;
        var alerts = AlertPause.Start;
        var checks = 0;
        var reads = 0;

        for (var frame = 0; frame * Frame < seconds; frame++)
        {
            var now = frame * Frame;

            if (CheckPace.Take(pace, now, isWanted: false) is not { } due) continue;

            pace = due.Pace;
            checks++;

            if (danger.IsDue(now))
            {
                danger = danger.Read(now, now >= waveStartsAt);
                reads++;
            }

            var step = AlertPause.Next(alerts, Quiet with { IsWaveActive = danger.IsWaveActive, ElapsedSeconds = due.ElapsedSeconds }, Defaults);

            alerts = step.State;

            if (step.PauseFor == Alert.Wave) return (now, checks, reads);
        }

        return (null, checks, reads);
    }

    [Fact]
    public void A_wave_pauses_the_game_within_a_read_and_a_check_of_its_start()
    {
        var (pausedAt, _, _) = Run(5f, WaveStartsAt);

        Assert.NotNull(pausedAt);
        Assert.InRange(pausedAt!.Value, WaveStartsAt, WaveStartsAt + DangerWatch.ReadSeconds + CheckPace.Seconds + Frame);
    }

    [Fact]
    public void With_no_wave_nothing_pauses_and_the_game_is_read_far_less_often_than_it_is_checked()
    {
        var (pausedAt, checks, reads) = Run(2f, float.MaxValue);

        Assert.Null(pausedAt);
        Assert.InRange(reads, 1, (int)(2f / DangerWatch.ReadSeconds) + 1);
        Assert.True(checks >= 2 * reads, $"{checks} checks for {reads} reads");
    }

    [Fact]
    public void A_wave_read_stands_between_reads()
    {
        var read = DangerWatch.Start.Read(1f, isWaveActive: true);

        Assert.False(read.IsDue(1f + DangerWatch.ReadSeconds / 2));
        Assert.True(read.IsWaveActive);
        Assert.True(read.IsDue(1f + DangerWatch.ReadSeconds));
    }
}
