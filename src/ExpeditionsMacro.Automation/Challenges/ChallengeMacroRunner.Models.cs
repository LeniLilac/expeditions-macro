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
                        models.Placement,
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
        Action<string, int, string, string?, double?>
            report,
        Action<string, MacroEventLevel, string?, double?>
            log,
        CancellationToken cancellationToken)
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
            PlacementTarget expectedTarget =
                PlacementTarget.ForChallenge(profile.Map);
            PlacementModel? placement = models.Placement;
            if (placement is not null)
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
