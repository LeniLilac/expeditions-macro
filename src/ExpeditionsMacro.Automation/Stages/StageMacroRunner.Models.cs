using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private Task PlayPlacementAsync(
        RobloxWindow window,
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps,
        StoryPreset? story,
        RaidPreset? raid,
        char cancelPlacementKey,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            model,
            steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            story?.UnitKeyHoldMilliseconds ??
                raid!.UnitKeyHoldMilliseconds,
            story?.UnitSelectDelayMilliseconds ??
                raid!.UnitSelectDelayMilliseconds,
            cancelPlacementKey,
            stepSent: null,
            status: null,
            cancellationToken);

    private static void ValidateCompatibility(
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        CameraPreparationMode cameraMode,
        StageRuntimeModels models,
        DetectorPackManifest detector)
    {
        PlacementTarget expectedTarget = story is not null
            ? PlacementTarget.ForStory(story)
            : PlacementTarget.ForRaid(raid!);
        PlacementModel? placement = models.Placement;
        if (placement is not null)
        {
            placement.ValidateCompatibility(
                cameraMode,
                expectedTarget);
            if (placement.ClientWidth !=
                    detector.ClientWidth ||
                placement.ClientHeight !=
                    detector.ClientHeight)
            {
                throw new InvalidDataException(
                    "A selected placement model uses a different Roblox client size.");
            }
        }
    }
}
