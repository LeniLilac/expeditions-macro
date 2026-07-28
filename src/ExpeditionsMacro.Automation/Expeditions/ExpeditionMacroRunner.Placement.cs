using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Automation.Placement;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task RetryRemainingUnitsAsync(
        RobloxWindow window,
        PlacementModel placement,
        IReadOnlyList<PlacementStep> eligibleSteps,
        ExpeditionPreset preset,
        IDetectorPack detector,
        ImageFrame frame,
        Action<string, MacroEventLevel, string?, double?>
            log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        HashSet<int> keys = eligibleSteps
            .Select(step => step.UnitKey)
            .ToHashSet();
        IReadOnlyList<int> remaining =
            detector.RemainingUnitKeys(frame, keys);
        if (remaining.Count == 0)
        {
            log(
                "Hotbar check: all recorded unit slots are empty.",
                MacroEventLevel.Success,
                null,
                null);
            return;
        }
        log(
            $"Hotbar check: retrying unit key(s) {string.Join(", ", remaining)}.",
            MacroEventLevel.Warning,
            null,
            null);
        PlacementStep[] steps = eligibleSteps
            .Where(step => remaining.Contains(step.UnitKey))
            .ToArray();
        await PlaceStepsAsync(
            window,
            placement,
            steps,
            preset,
            log,
            cancelPlacementKey,
            cancellationToken).ConfigureAwait(false);
    }

    private Task PlaceStepsAsync(
        RobloxWindow window,
        PlacementModel placement,
        IReadOnlyList<PlacementStep> steps,
        ExpeditionPreset preset,
        Action<string, MacroEventLevel, string?, double?>
            log,
        char cancelPlacementKey,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            placement,
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

    private static void ValidateCompatibility(
        ExpeditionPreset preset,
        PlacementModel placement,
        DetectorPackManifest detector)
    {
        if (!string.Equals(
                preset.PlacementModelId,
                placement.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The preset placement model does not match the loaded model.");
        }
        if (!string.Equals(
                preset.DetectorPackId,
                detector.PackId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The preset detector pack does not match the loaded pack.");
        }
        placement.ValidateCompatibility(
            preset.CameraPreparationMode,
            PlacementTarget.ForExpedition(preset));
        if (placement.ClientWidth != detector.ClientWidth ||
            placement.ClientHeight != detector.ClientHeight)
        {
            throw new InvalidDataException(
                "Placement model and detector pack use different Roblox client sizes.");
        }
    }

}
