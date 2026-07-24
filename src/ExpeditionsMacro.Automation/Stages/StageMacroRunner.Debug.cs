using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    public Task DebugNavigateStoryAsync(
        StoryPreset preset,
        IDetectorPack detector,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        DebugNavigateAsync(
            StageMode.Story,
            preset,
            raid: null,
            detector,
            playMenuKey,
            progress,
            cancellationToken);

    public Task DebugNavigateRaidAsync(
        RaidPreset preset,
        IDetectorPack detector,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        DebugNavigateAsync(
            StageMode.Raid,
            story: null,
            preset,
            detector,
            playMenuKey,
            progress,
            cancellationToken);

    private async Task DebugNavigateAsync(
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        char playMenuKey,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        story?.Validate();
        raid?.Validate();
        if (!char.IsAsciiLetter(playMenuKey))
        {
            throw new InvalidDataException(
                AppSettings.PlayMenuKeySetupInstructions);
        }

        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        int stableDetections = Math.Max(
            2,
            story?.StableDetections ??
            raid!.StableDetections);
        Focus(window);
        await EnsureClientSizeAsync(
            window,
            detector.Manifest.ClientWidth,
            detector.Manifest.ClientHeight,
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress(
            "Debug navigation",
            5,
            $"Opening {RouteLabel(mode, story, raid)}."));
        await EnsureGameModeSelectorAsync(
            window,
            mode,
            playMenuKey,
            detector,
            autoRecover: true,
            stableDetections,
            (phase, percent, message, state, confidence) =>
                progress?.Report(new MacroProgress(
                    phase,
                    percent,
                    message,
                    state,
                    confidence)),
            log: null,
            cancellationToken).ConfigureAwait(false);
        await NavigateToPrestartAsync(
            window,
            mode,
            story,
            raid,
            playMenuKey,
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress(
            "Debug navigation",
            100,
            $"{RouteLabel(mode, story, raid)} reached prestart.",
            "prestart",
            1));
    }
}
