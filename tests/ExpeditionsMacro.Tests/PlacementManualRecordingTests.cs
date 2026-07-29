using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementManualRecordingTests
{
    [Fact]
    public void ManualRecording_AllowsSetupWithoutNormalSteps()
    {
        PlacementModel model =
            ManualPlacement();

        model.Validate();

        Assert.Empty(model.Steps);
        Assert.Equal(
            "recording-one",
            model.ManualInputRecordingId);
    }

    [Fact]
    public void EmptySetupStillRequiresStepsOrRecording()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => (ManualPlacement() with
                {
                    ManualInputRecordingId = null,
                }).Validate());

        Assert.Contains(
            "steps or manual recording",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualRecordingRequiresFastNoAlignSetup()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => (ManualPlacement() with
                {
                    CameraPreparationMode =
                        CameraPreparationMode
                            .CameraModel,
                    Target = null,
                }).Validate());

        Assert.Contains(
            "Fast no align",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImpossibilityThresholdHasBoundedRange()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => (ManualPlacement() with
                {
                    ImpossibilityThresholdMinutes =
                        PlacementModel
                            .MaximumImpossibilityThresholdMinutes +
                        1,
                }).Validate());

        Assert.Contains(
            "threshold",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualRecording_ReplacesEveryNormalPlacementPhase()
    {
        PlacementModel model =
            ManualPlacement() with
            {
                Steps =
                [
                    Step(
                        PlacementPhase.BeforeStart),
                    Step(
                        PlacementPhase.AfterStart),
                ],
            };

        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                model);

        Assert.True(execution.ManualPlayback);
        Assert.Empty(execution.BeforeStart);
        Assert.Empty(execution.AfterStart);
    }

    [Fact]
    public void ClearingRecordingAssignmentRestoresPreservedSteps()
    {
        PlacementStep before =
            Step(PlacementPhase.BeforeStart) with
            {
                PlacementId = "before",
            };
        PlacementStep after =
            Step(PlacementPhase.AfterStart) with
            {
                PlacementId = "after",
                X = 140,
            };
        PlacementModel recordingMode =
            ManualPlacement() with
            {
                Steps = [before, after],
            };
        PlacementModel stepMode =
            recordingMode with
            {
                ManualInputRecordingId = null,
            };

        Assert.True(
            ManualInputRouteService.IsConfigured(
                recordingMode));
        Assert.False(
            ManualInputRouteService.IsConfigured(
                stepMode));
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                stepMode);
        Assert.Equal(
            [before],
            execution.BeforeStart);
        Assert.Equal(
            [after],
            execution.AfterStart);
    }

    [Fact]
    public async Task RepositoryFindsEveryPlacementThatReferencesRecording()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-recording-reference-{Guid.NewGuid():N}");
        try
        {
            PlacementModelRepository repository =
                new(new AppPaths(root));
            await repository.SaveAsync(
                ManualPlacement());
            await repository.SaveAsync(
                ManualPlacement() with
                {
                    Id = "placement-two",
                    Name = "Placement two",
                    ManualInputRecordingId =
                        "other-recording",
                });

            IReadOnlyList<PlacementModel> references =
                await repository
                    .ListReferencingManualRecordingAsync(
                        "RECORDING-ONE");

            PlacementModel reference =
                Assert.Single(references);
            Assert.Equal(
                "placement-one",
                reference.Id);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private static PlacementModel ManualPlacement() =>
        new()
        {
            Id = "placement-one",
            Name = "Placement one",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode =
                    PlacementTargetMode.Expedition,
                MapNumber = 0,
                ActNumber = 0,
            },
            ManualInputRecordingId =
                "recording-one",
            Steps = [],
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step(
        PlacementPhase phase) =>
        new()
        {
            UnitKey = 1,
            X = 100,
            Y = 200,
            DelayAfterMilliseconds = 0,
            Phase = phase,
        };
}
