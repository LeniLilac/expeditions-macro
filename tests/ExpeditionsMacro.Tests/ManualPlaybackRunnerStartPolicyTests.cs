using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class ManualPlaybackRunnerStartPolicyTests
{
    public static TheoryData<string> RunnerFamilies =>
        new()
        {
            "Challenge",
            "Expedition",
            "Story/Raid",
            "Event",
        };

    [Theory]
    [MemberData(nameof(RunnerFamilies))]
    public void FirstEntry_AlwaysRetainsPrestartProof(
        string runnerFamily)
    {
        PlacementModel placement =
            ManualPlacement(
                verifyPrestart: false,
                startDelayMilliseconds: 725);

        bool requiresPrestart =
            ManualPlaybackStartPolicy.RequiresPrestart(
                placement,
                arrivedFromRepeatStage: false);

        Assert.True(
            requiresPrestart,
            $"{runnerFamily} must keep route-owned prestart proof on first entry.");
    }

    [Theory]
    [MemberData(nameof(RunnerFamilies))]
    public void RepeatStage_UsesTheConfiguredPrestartPolicy(
        string runnerFamily)
    {
        PlacementModel verified =
            ManualPlacement(
                verifyPrestart: true,
                startDelayMilliseconds: 725);
        PlacementModel delayOnly =
            ManualPlacement(
                verifyPrestart: false,
                startDelayMilliseconds: 725);

        Assert.True(
            ManualPlaybackStartPolicy.RequiresPrestart(
                verified,
                arrivedFromRepeatStage: true),
            $"{runnerFamily} must verify Repeat Stage when the setup keeps the safe default.");
        Assert.False(
            ManualPlaybackStartPolicy.RequiresPrestart(
                delayOnly,
                arrivedFromRepeatStage: true),
            $"{runnerFamily} must honor the explicit advanced Repeat Stage opt-out.");
    }

    [Theory]
    [MemberData(nameof(RunnerFamilies))]
    public async Task RepeatStage_DelayCompletesBeforePlayback(
        string runnerFamily)
    {
        PlacementModel placement =
            ManualPlacement(
                verifyPrestart: false,
                startDelayMilliseconds: 725);
        List<string> sequence = [];

        await ManualPlaybackStartPolicy
            .WaitBeforePlaybackAsync(
                placement,
                message =>
                {
                    Assert.Contains(
                        "725 ms",
                        message,
                        StringComparison.Ordinal);
                    sequence.Add("status");
                },
                (delay, _) =>
                {
                    Assert.Equal(725, delay);
                    sequence.Add("delay");
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        sequence.Add("playback");

        Assert.Equal(
            ["status", "delay", "playback"],
            sequence);
        Assert.NotEmpty(runnerFamily);
    }

    [Theory]
    [MemberData(nameof(RunnerFamilies))]
    public async Task VerifiedRepeatStage_DoesNotAddTheAdvancedDelay(
        string runnerFamily)
    {
        PlacementModel placement =
            ManualPlacement(
                verifyPrestart: true,
                startDelayMilliseconds: 725);
        int delayCalls = 0;
        int statusCalls = 0;

        await ManualPlaybackStartPolicy
            .WaitBeforePlaybackAsync(
                placement,
                _ => statusCalls++,
                (_, _) =>
                {
                    delayCalls++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

        Assert.Equal(0, statusCalls);
        Assert.Equal(0, delayCalls);
        Assert.NotEmpty(runnerFamily);
    }

    [Fact]
    public async Task ChallengeRepeat_UsesOnlyTheConfiguredAdvancedDelay()
    {
        PlacementModel placement =
            ManualPlacement(
                verifyPrestart: false,
                startDelayMilliseconds: 725);
        int fixedDelayCalls = 0;

        await ChallengeMacroRunner.WaitAfterRepeatStageAsync(
            placement,
            CancellationToken.None,
            (_, _) =>
            {
                fixedDelayCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(0, fixedDelayCalls);
    }

    [Fact]
    public async Task ChallengeRepeat_KeepsTransitionWaitWhenPrestartIsRequired()
    {
        PlacementModel placement =
            ManualPlacement(
                verifyPrestart: true,
                startDelayMilliseconds: 725);
        List<int> delays = [];

        await ChallengeMacroRunner.WaitAfterRepeatStageAsync(
            placement,
            CancellationToken.None,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal([3500], delays);
    }

    private static PlacementModel ManualPlacement(
        bool verifyPrestart,
        int startDelayMilliseconds) =>
        new()
        {
            Id = "manual-repeat-policy",
            Name = "Manual repeat policy",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = 1,
                StoryRunKind = StoryRunKind.Act,
                ActNumber = 1,
            },
            ManualInputRecordingId =
                "recording-repeat-policy",
            AdvancedSettings =
                new PlacementAdvancedSettings
                {
                    Enabled = true,
                    VerifyPrestartBeforeManualPlayback =
                        verifyPrestart,
                    ManualPlaybackStartDelayMilliseconds =
                        startDelayMilliseconds,
                },
            Steps = [],
            CreatedAt =
                new DateTimeOffset(
                    2026,
                    7,
                    29,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),
        };
}
