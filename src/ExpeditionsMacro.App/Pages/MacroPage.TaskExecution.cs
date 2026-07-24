using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private Task<ScheduledTaskResult> ExecuteTaskAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        Dispatcher.BeginInvoke(() => CurrentTaskText.Text = $"Current task: {Label(task.Kind)} - {task.Name}");
        return task.Kind switch
        {
            MacroTaskKind.Challenge => ExecuteChallengeAsync(
                task,
                webhook,
                discordUserId,
                playMenuKey,
                unitMenuKey,
                macroTotals,
                challengeRotation,
                progress,
                cancellationToken),
            MacroTaskKind.Expedition => ExecuteExpeditionAsync(
                task,
                recordResult,
                webhook,
                discordUserId,
                playMenuKey,
                unitMenuKey,
                macroTotals,
                progress,
                cancellationToken),
            MacroTaskKind.Story => ExecuteStoryAsync(
                task,
                recordResult,
                webhook,
                discordUserId,
                playMenuKey,
                unitMenuKey,
                macroTotals,
                progress,
                cancellationToken),
            MacroTaskKind.Raid => ExecuteRaidAsync(
                task,
                recordResult,
                webhook,
                discordUserId,
                playMenuKey,
                unitMenuKey,
                macroTotals,
                progress,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(task),
                task.Kind,
                "Unknown macro task type."),
        };
    }

    private async Task<ScheduledTaskResult> ExecuteChallengeAsync(
        MacroTaskDefinition task,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        ChallengePreset preset = await _services.ChallengePresets
            .LoadAsync(task.PresetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Challenge preset '{task.PresetId}' could not be loaded.");
        preset.ValidateReady();
        IDetectorPack detector = await LoadDetectorAsync(
            preset.DetectorPackId,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> models =
            await LoadChallengeModelsAsync(
                preset,
                cancellationToken).ConfigureAwait(false);
        ChallengeRunSummary? summary = null;
        await _services.Challenges.RunAsync(
            preset,
            models,
            detector,
            challengeRotation,
            webhook,
            playMenuKey,
            progress,
            entry => DispatchLog(entry),
            value => summary = value,
            cancellationToken,
            (error, token) => HandleRecoverableFailureAsync(
                "Challenge Macro",
                webhook,
                discordUserId,
                error,
                token),
            maximumCompletedRuns: 1,
            returnWhenUnavailable: true,
            unitMenuKey,
            macroTotals).ConfigureAwait(false);

        ChallengeRunSummary result = summary
            ?? throw new InvalidOperationException(
                "Challenge task returned without a run summary.");
        return result.Completed > 0
            ? new ScheduledTaskResult(
                result.Victories,
                result.Defeats,
                result.Runtime)
            : new ScheduledTaskResult(
                0,
                0,
                result.Runtime,
                result.WaitingUntilUtc
                    ?? DateTimeOffset.UtcNow + SafeSkipDelay,
                Skipped: true);
    }

    private async Task<ScheduledTaskResult> ExecuteExpeditionAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        ExpeditionPreset preset = await _services.Presets
            .LoadAsync(task.PresetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Expedition preset '{task.PresetId}' could not be loaded.");
        CameraModel camera = await _services.CameraModels
            .LoadAsync(preset.CameraModelId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The selected Expedition camera model could not be loaded.");
        PlacementModel placement = await _services.PlacementModels
            .LoadAsync(preset.PlacementModelId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The selected Expedition placement model could not be loaded.");
        IDetectorPack detector = await LoadDetectorAsync(
            preset.DetectorPackId,
            cancellationToken).ConfigureAwait(false);
        ExpeditionRunSummary? summary = null;
        await _services.Expeditions.RunAsync(
            preset,
            camera,
            placement,
            detector,
            webhook,
            playMenuKey,
            progress,
            entry => DispatchLog(entry),
            value => summary = value,
            cancellationToken,
            stopAfterCurrentRunUtc: null,
            recoverableFailure: (error, token) =>
                HandleRecoverableFailureAsync(
                    "Expeditions Macro",
                    webhook,
                    discordUserId,
                    error,
                    token),
            maximumRuns: null,
            unitMenuKey,
            continueScheduledRoute: async (
                victories,
                defeats,
                runtime,
                token) =>
                await recordResult(
                    new ScheduledTaskResult(
                        victories,
                        defeats,
                        runtime),
                    token).ConfigureAwait(false)
                == ScheduledTaskContinuation.RepeatStage,
            macroTotals: macroTotals).ConfigureAwait(false);

        ExpeditionRunSummary result = summary
            ?? throw new InvalidOperationException(
                "Expedition task returned without a run summary.");
        return result.Repeats > 0
            ? new ScheduledTaskResult(
                result.Victories,
                result.Defeats,
                result.Runtime)
            : new ScheduledTaskResult(
                0,
                0,
                result.Runtime,
                DateTimeOffset.UtcNow + SafeSkipDelay,
                Skipped: true);
    }

    private async Task<ScheduledTaskResult> ExecuteStoryAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        StoryPreset preset = await _services.StoryPresets
            .LoadAsync(task.PresetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Story preset '{task.PresetId}' could not be loaded.");
        StageRuntimeModels models = await LoadStageModelsAsync(
            preset.CameraModelId,
            preset.PrestartPlacementModelId,
            preset.DelayedPlacementModelId,
            cancellationToken).ConfigureAwait(false);
        IDetectorPack detector = await LoadDetectorAsync(
            AnimeExpeditionsDetectorSpec.PackId,
            cancellationToken).ConfigureAwait(false);
        try
        {
            StageRunResult result = await _services.Stages.RunStoryAsync(
                preset,
                models,
                detector,
                webhook,
                playMenuKey,
                unitMenuKey,
                progress,
                entry => DispatchLog(entry),
                cancellationToken,
                continueScheduledRoute: async (
                    victories,
                    defeats,
                    runtime,
                    token) =>
                    await recordResult(
                        new ScheduledTaskResult(
                            victories,
                            defeats,
                            runtime),
                        token).ConfigureAwait(false)
                    == ScheduledTaskContinuation.RepeatStage,
                macroTotals: macroTotals).ConfigureAwait(false);
            return ToScheduledResult(result);
        }
        catch (CameraAlignmentException error)
        {
            await HandleRecoverableFailureAsync(
                "Story Macro",
                webhook,
                discordUserId,
                error,
                cancellationToken).ConfigureAwait(false);
            DispatchLog(new MacroEvent(
                DateTimeOffset.Now,
                MacroEventLevel.Warning,
                $"Story task skipped for {SafeSkipDelay.TotalMinutes:0} minutes after camera alignment failed.",
                "camera_alignment_skipped",
                error.BestConfidence));
            return new ScheduledTaskResult(
                0,
                0,
                TimeSpan.Zero,
                DateTimeOffset.UtcNow + SafeSkipDelay,
                Skipped: true);
        }
    }

    private async Task<ScheduledTaskResult> ExecuteRaidAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        RaidPreset preset = await _services.RaidPresets
            .LoadAsync(task.PresetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Raid preset '{task.PresetId}' could not be loaded.");
        StageRuntimeModels models = await LoadStageModelsAsync(
            preset.CameraModelId,
            preset.PrestartPlacementModelId,
            preset.DelayedPlacementModelId,
            cancellationToken).ConfigureAwait(false);
        IDetectorPack detector = await LoadDetectorAsync(
            AnimeExpeditionsDetectorSpec.PackId,
            cancellationToken).ConfigureAwait(false);
        try
        {
            StageRunResult result = await _services.Stages.RunRaidAsync(
                preset,
                models,
                detector,
                webhook,
                playMenuKey,
                unitMenuKey,
                progress,
                entry => DispatchLog(entry),
                cancellationToken,
                continueScheduledRoute: async (
                    victories,
                    defeats,
                    runtime,
                    token) =>
                    await recordResult(
                        new ScheduledTaskResult(
                            victories,
                            defeats,
                            runtime),
                        token).ConfigureAwait(false)
                    == ScheduledTaskContinuation.RepeatStage,
                macroTotals: macroTotals).ConfigureAwait(false);
            return ToScheduledResult(result);
        }
        catch (CameraAlignmentException error)
        {
            await HandleRecoverableFailureAsync(
                "Raid Macro",
                webhook,
                discordUserId,
                error,
                cancellationToken).ConfigureAwait(false);
            DispatchLog(new MacroEvent(
                DateTimeOffset.Now,
                MacroEventLevel.Warning,
                $"Raid task skipped for {SafeSkipDelay.TotalMinutes:0} minutes after camera alignment failed.",
                "camera_alignment_skipped",
                error.BestConfidence));
            return new ScheduledTaskResult(
                0,
                0,
                TimeSpan.Zero,
                DateTimeOffset.UtcNow + SafeSkipDelay,
                Skipped: true);
        }
    }
}
