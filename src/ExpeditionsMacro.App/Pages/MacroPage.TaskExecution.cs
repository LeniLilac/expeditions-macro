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
        char cancelPlacementKey,
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
                cancelPlacementKey,
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
                cancelPlacementKey,
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
                cancelPlacementKey,
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
                cancelPlacementKey,
                macroTotals,
                progress,
                cancellationToken),
            MacroTaskKind.Event => ExecuteEventAsync(
                task,
                recordResult,
                webhook,
                discordUserId,
                playMenuKey,
                unitMenuKey,
                cancelPlacementKey,
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
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        ChallengePreset preset;
        IReadOnlyDictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> models;
        if (task.UsesPlacementSetup)
        {
            (preset, models) =
                await BuildChallengeSetupAsync(
                    task,
                    cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            preset = await _services.ChallengePresets
                .LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Challenge preset '{task.PresetId}' could not be loaded.");
            CameraPreparationExecutionPolicy.ValidateForExecution(
                preset.CameraPreparationMode,
                "The selected Challenge preset");
            preset.ValidateReady();
            models = await LoadChallengeModelsAsync(
                preset,
                cancellationToken)
                .ConfigureAwait(false);
        }
        IDetectorPack detector = await LoadDetectorAsync(
            preset.DetectorPackId,
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
            maximumCompletedRuns: 1,
            returnWhenUnavailable: true,
            unitMenuKey: unitMenuKey,
            macroTotals: macroTotals,
            cancelPlacementKey: cancelPlacementKey)
            .ConfigureAwait(false);

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
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        ExpeditionPreset preset;
        PlacementModel placement;
        if (task.UsesPlacementSetup)
        {
            (preset, placement) =
                await BuildExpeditionSetupAsync(
                    task,
                    cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            preset = await _services.Presets
                .LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Expedition preset '{task.PresetId}' could not be loaded.");
            CameraPreparationExecutionPolicy.ValidateForExecution(
                preset.CameraPreparationMode,
                "The selected Expedition preset");
            placement = await _services.PlacementModels
                .LoadAsync(
                    preset.PlacementModelId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "The selected Expedition placement model could not be loaded.");
        }
        IDetectorPack detector = await LoadDetectorAsync(
            preset.DetectorPackId,
            cancellationToken).ConfigureAwait(false);
        ExpeditionRunSummary? summary = null;
        await _services.Expeditions.RunAsync(
            preset,
            placement,
            detector,
            webhook,
            playMenuKey,
            progress,
            entry => DispatchLog(entry),
            value => summary = value,
            cancellationToken,
            stopAfterCurrentRunUtc: null,
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
                    token).ConfigureAwait(false),
            macroTotals: macroTotals,
            cancelPlacementKey: cancelPlacementKey)
            .ConfigureAwait(false);

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
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        StoryPreset preset;
        StageRuntimeModels models;
        if (task.UsesPlacementSetup)
        {
            (preset, models) =
                await BuildStorySetupAsync(
                    task,
                    cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            preset = await _services.StoryPresets
                .LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Story preset '{task.PresetId}' could not be loaded.");
            CameraPreparationExecutionPolicy.ValidateForExecution(
                preset.CameraPreparationMode,
                "The selected Story preset");
            models = await LoadStageModelsAsync(
                preset.CameraPreparationMode,
                preset.PrestartPlacementModelId,
                cancellationToken)
                .ConfigureAwait(false);
        }
        IDetectorPack detector = await LoadDetectorAsync(
            AnimeExpeditionsDetectorSpec.PackId,
            cancellationToken).ConfigureAwait(false);
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
                        token).ConfigureAwait(false),
                macroTotals: macroTotals,
                cancelPlacementKey:
                    cancelPlacementKey).ConfigureAwait(false);
        return ToScheduledResult(result);
    }

    private async Task<ScheduledTaskResult> ExecuteRaidAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        RaidPreset preset;
        StageRuntimeModels models;
        if (task.UsesPlacementSetup)
        {
            (preset, models) =
                await BuildRaidSetupAsync(
                    task,
                    cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            preset = await _services.RaidPresets
                .LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Raid preset '{task.PresetId}' could not be loaded.");
            CameraPreparationExecutionPolicy.ValidateForExecution(
                preset.CameraPreparationMode,
                "The selected Raid preset");
            models = await LoadStageModelsAsync(
                preset.CameraPreparationMode,
                preset.PrestartPlacementModelId,
                cancellationToken)
                .ConfigureAwait(false);
        }
        IDetectorPack detector = await LoadDetectorAsync(
            AnimeExpeditionsDetectorSpec.PackId,
            cancellationToken).ConfigureAwait(false);
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
                        token).ConfigureAwait(false),
                macroTotals: macroTotals,
                cancelPlacementKey:
                    cancelPlacementKey).ConfigureAwait(false);
        return ToScheduledResult(result);
    }

}
