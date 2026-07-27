using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<IReadOnlyDictionary<
        ChallengeMapId,
        ManualInputRecording>> ResolveManualRecordingsAsync(
        ChallengePreset preset,
        IReadOnlyDictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> mapModels,
        CancellationToken cancellationToken)
    {
        Dictionary<ChallengeMapId, ManualInputRecording>
            recordings = [];
        foreach (ChallengeMapProfile profile in
                 preset.Maps)
        {
            ChallengeMapRuntimeModels models =
                mapModels[profile.Map];
            ManualInputRecording? recording =
                await ManualInputMatchPlayback.ResolveAsync(
                        _manualInputs,
                        models.PrestartPlacement,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (recording is not null)
            {
                recordings.Add(
                    profile.Map,
                    recording);
            }
        }
        return recordings;
    }

    private async Task PrepareCameraAsync(
        RobloxWindow window,
        ChallengePreset preset,
        CameraModel? model,
        Action<string, int, string, string?, double?>
            report,
        Action<string, MacroEventLevel, string?, double?>
            log,
        CancellationToken cancellationToken)
    {
        if (preset.CameraPreparationMode ==
            CameraPreparationMode.FastNoAlign)
        {
            bool prepared =
                await _fastNoAlign.EnsurePreparedAsync(
                    window,
                    preset.ZoomTicks,
                    preset.PitchDragPixels,
                    new Progress<MacroProgress>(
                        value => report(
                            value.Phase,
                            value.Percent,
                            value.Message,
                            value.DetectedState,
                            value.Confidence)),
                    cancellationToken).ConfigureAwait(false);
            log(
                prepared
                    ? "Fast no align prepared zoom and pitch without changing yaw."
                    : "Fast no align reused the camera pose preserved from the previous match.",
                MacroEventLevel.Success,
                prepared
                    ? "fast_no_align"
                    : "fast_no_align_reused",
                null);
            return;
        }

        double score =
            await _camera.PrepareAndAlignAsync(
                model ??
                    throw new InvalidDataException(
                        "Choose a camera model for this Challenge map."),
                window,
                preset.ZoomTicks,
                preset.PitchDragPixels,
                progress: new Progress<MacroProgress>(
                    value => report(
                        value.Phase,
                        value.Percent,
                        value.Message,
                        value.DetectedState,
                        value.Confidence)),
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        log(
            $"Camera alignment finished at {score:P0} confidence.",
            MacroEventLevel.Success,
            null,
            score);
    }

    private Task PlaceAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel model,
        Action<string, MacroEventLevel, string?, double?>
            log,
        char cancelPlacementKey,
        CancellationToken cancellationToken) =>
        PlaceAsync(
            window,
            preset,
            model,
            model.Steps,
            log,
            cancelPlacementKey,
            cancellationToken);

    private Task PlaceAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps,
        Action<string, MacroEventLevel, string?, double?>
            log,
        char cancelPlacementKey,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            model,
            steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            preset.UnitKeyHoldMilliseconds,
            preset.UnitSelectDelayMilliseconds,
            cancelPlacementKey,
            stepSent: null,
            status: message => log(
                message,
                MacroEventLevel.Information,
                null,
                null),
            cancellationToken);

    private static void ValidateRuntimeModels(
        ChallengePreset preset,
        IReadOnlyDictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> mapModels,
        DetectorPackManifest detector)
    {
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            if (!mapModels.TryGetValue(
                    profile.Map,
                    out ChallengeMapRuntimeModels? models))
            {
                throw new InvalidDataException(
                    $"Models for {Label(profile.Map)} were not loaded.");
            }
            ValidateCameraModel(
                preset,
                profile,
                models,
                detector);
            PlacementTarget expectedTarget =
                PlacementTarget.ForChallenge(profile.Map);
            foreach (PlacementModel placement in
                     new[]
                     {
                         models.PrestartPlacement,
                         models.DelayedPlacement,
                     }
                     .Where(model => model is not null)
                     .Cast<PlacementModel>())
            {
                placement.ValidateCompatibility(
                    preset.CameraPreparationMode,
                    expectedTarget);
                if (placement.ClientWidth !=
                        detector.ClientWidth ||
                    placement.ClientHeight !=
                        detector.ClientHeight)
                {
                    throw new InvalidDataException(
                        $"A {Label(profile.Map)} placement model uses a different Roblox client size.");
                }
            }
        }
    }

    private static void ValidateCameraModel(
        ChallengePreset preset,
        ChallengeMapProfile profile,
        ChallengeMapRuntimeModels models,
        DetectorPackManifest detector)
    {
        if (preset.CameraPreparationMode !=
            CameraPreparationMode.CameraModel)
        {
            return;
        }
        CameraModel camera = models.Camera ??
            throw new InvalidDataException(
                $"Choose a camera model for {Label(profile.Map)}.");
        if (camera.Manifest.ClientWidth !=
                detector.ClientWidth ||
            camera.Manifest.ClientHeight !=
                detector.ClientHeight)
        {
            throw new InvalidDataException(
                $"The {Label(profile.Map)} camera model uses a different Roblox client size.");
        }
    }

    private static char ValidatePlayMenuKey(
        char value)
    {
        char normalized =
            char.ToUpperInvariant(value);
        if (!char.IsAsciiLetter(normalized))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Play Menu key to match Anime Expeditions' Toggle Play Menu binding.");
        }
        return normalized;
    }

    private static void ValidateTeamKey(
        bool required,
        char? value)
    {
        if (!required)
        {
            return;
        }
        if (value is null ||
            !char.IsAsciiLetter(value.Value))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Unit Inventory key to match Anime Expeditions' Toggle Unit Inventory binding before using a saved team.");
        }
    }
}
