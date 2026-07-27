using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task<ManualInputRecording?>
        ValidateAndResolveManualRecordingAsync(
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        CameraPreparationMode cameraMode,
        StageRuntimeModels models,
        DetectorPackManifest detector,
        CancellationToken cancellationToken)
    {
        models.Camera?.Manifest.Validate();
        models.PrestartPlacement?.Validate();
        models.DelayedPlacement?.Validate();
        ValidateCompatibility(
            mode,
            story,
            raid,
            cameraMode,
            models,
            detector);
        return await ManualInputMatchPlayback.ResolveAsync(
                _manualInputs,
                models.PrestartPlacement,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
