using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementSetupFallbackTests
{
    [Fact]
    public void ExactStepModeRoute_WithOnlyStartGame_IsEmptyOverride()
    {
        PlacementModel model = Model(
            StoryActTarget(),
            recordingId: null);

        Assert.True(
            PlacementSetupCatalog
                .IsEmptyRouteOverride(model));
    }

    [Fact]
    public void SharedCategory_WithOnlyStartGame_RemainsConfigured()
    {
        PlacementModel model = Model(
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = 1,
                StoryRunKind = StoryRunKind.Act,
                ActNumber = PlacementSetupCatalog
                    .SharedStoryActNumber,
            },
            recordingId: null);

        Assert.False(
            PlacementSetupCatalog
                .IsEmptyRouteOverride(model));
    }

    [Fact]
    public void RecordingAssignment_RemainsAnExactOverride()
    {
        PlacementModel model = Model(
            StoryActTarget(),
            "recording-one");

        Assert.False(
            PlacementSetupCatalog
                .IsEmptyRouteOverride(model));
    }

    [Fact]
    public void ExactRoute_WithAnyAction_RemainsAnOverride()
    {
        PlacementModel model = Model(
            StoryActTarget(),
            recordingId: null) with
        {
            Steps =
            [
                PlacementTimelinePolicy
                    .CreateStartGameStep(),
                new PlacementStep
                {
                    Kind = MatchStepKind.Delay,
                    UnitKey = 0,
                    X = 0,
                    Y = 0,
                    Phase = PlacementPhase.AfterStart,
                    DelayDurationMilliseconds = 500,
                    DelayAfterMilliseconds = 0,
                },
            ],
        };

        Assert.False(
            PlacementSetupCatalog
                .IsEmptyRouteOverride(model));
    }

    private static PlacementTarget StoryActTarget() =>
        new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = 1,
            StoryRunKind = StoryRunKind.Act,
            ActNumber = 2,
        };

    private static PlacementModel Model(
        PlacementTarget target,
        string? recordingId) =>
        new()
        {
            Id = PlacementSetupCatalog.IdFor(target),
            Name = PlacementSetupCatalog.NameFor(target),
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            ManualInputRecordingId = recordingId,
            Steps =
            [
                PlacementTimelinePolicy
                    .CreateStartGameStep(),
            ],
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        };
}
