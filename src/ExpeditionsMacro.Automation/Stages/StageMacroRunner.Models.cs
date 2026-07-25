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
        if (cameraMode == CameraPreparationMode.CameraModel)
        {
            CameraModel camera = models.Camera ??
                throw new InvalidDataException(
                    $"Choose a camera model for {Label(mode)}.");
            if (camera.Manifest.ClientWidth !=
                    detector.ClientWidth ||
                camera.Manifest.ClientHeight !=
                    detector.ClientHeight)
            {
                throw new InvalidDataException(
                    "The camera model and detector pack use different Roblox client sizes.");
            }
        }

        PlacementTarget expectedTarget = story is not null
            ? PlacementTarget.ForStory(story)
            : PlacementTarget.ForRaid(raid!);
        foreach (PlacementModel? placement in
                 new[]
                 {
                     models.PrestartPlacement,
                     models.DelayedPlacement,
                 })
        {
            if (placement is null) continue;
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
