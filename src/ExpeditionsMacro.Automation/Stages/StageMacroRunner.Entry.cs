using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    public Task<StageRunResult> RunStoryAsync(
        StoryPreset preset,
        StageRuntimeModels models,
        IDetectorPack detector,
        string webhookUrl,
        char playMenuKey,
        char? unitMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>>?
            continueScheduledRoute = null,
        MacroRunTotals? macroTotals = null,
        char cancelPlacementKey =
            AppSettings
                .DefaultCancelPlacementKeyChar,
        StageWaveObjective? waveObjective = null) =>
        RunAsync(
            StageMode.Story,
            preset,
            models,
            detector,
            webhookUrl,
            playMenuKey,
            unitMenuKey,
            progress,
            log,
            cancellationToken,
            continueScheduledRoute,
            macroTotals,
            cancelPlacementKey,
            waveObjective);

    public Task<StageRunResult> RunRaidAsync(
        RaidPreset preset,
        StageRuntimeModels models,
        IDetectorPack detector,
        string webhookUrl,
        char playMenuKey,
        char? unitMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>>?
            continueScheduledRoute = null,
        MacroRunTotals? macroTotals = null,
        char cancelPlacementKey =
            AppSettings
                .DefaultCancelPlacementKeyChar) =>
        RunAsync(
            StageMode.Raid,
            preset,
            models,
            detector,
            webhookUrl,
            playMenuKey,
            unitMenuKey,
            progress,
            log,
            cancellationToken,
            continueScheduledRoute,
            macroTotals,
            cancelPlacementKey,
            waveObjective: null);
}
