using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task NavigateToPrestartAsync(
        RobloxWindow window,
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        char playMenuKey,
        IDetectorPack detector,
        int stableDetections,
        CancellationToken cancellationToken)
    {
        await EnsureGameModeSelectorAsync(
            window,
            mode,
            playMenuKey,
            detector,
            autoRecover: false,
            stableDetections,
            report: null,
            log: null,
            cancellationToken).ConfigureAwait(false);
        (int tileX, int tileY) =
            StageScreenDetector.ModeTileAction(mode);
        await ClickAsync(
            window,
            tileX,
            tileY,
            cancellationToken).ConfigureAwait(false);

        (int X, int Y) selectStage =
            mode == StageMode.Story
                ? await SelectStoryOptionsAsync(
                        window,
                        story!,
                        detector,
                        stableDetections,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await SelectRaidOptionsAsync(
                        window,
                        raid!,
                        detector,
                        stableDetections,
                        cancellationToken)
                    .ConfigureAwait(false);
        await ClickAsync(
            window,
            selectStage.X,
            selectStage.Y,
            cancellationToken).ConfigureAwait(false);

        StageScreenMatch preview =
            await WaitForStateAsync(
                window,
                StageScreenState.PreviewReady,
                NavigationTimeout,
                detector,
                stableDetections,
                cancellationToken).ConfigureAwait(false);
        if (preview.ActionX is not int previewX ||
            preview.ActionY is not int previewY)
        {
            throw new RobloxUiUnavailableException(
                $"The {Label(mode)} preview Start button could not be located.");
        }
        await ClickAsync(
            window,
            previewX,
            previewY,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            StageScreenState.Prestart,
            TimeSpan.FromSeconds(45),
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(int X, int Y)>
        SelectStoryOptionsAsync(
            RobloxWindow window,
            StoryPreset story,
            IDetectorPack detector,
            int stableDetections,
            CancellationToken cancellationToken)
    {
        await WaitForStateAsync(
            window,
            StageScreenState.StorySelector,
            NavigationTimeout,
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);
        Focus(window);
        await _automation.ScrollClientAsync(
            window,
            20,
            cancellationToken).ConfigureAwait(false);
        if (StageScreenDetector.StoryMapRequiresLaterScroll(
                story.Map))
        {
            Focus(window);
            await _automation.ScrollClientAsync(
                window,
                -10,
                cancellationToken).ConfigureAwait(false);
        }
        (int mapX, int mapY) =
            StageScreenDetector.StoryMapAction(story.Map);
        await ClickAsync(
            window,
            mapX,
            mapY,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            StageScreenState.StoryDetail,
            NavigationTimeout,
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);

        (int runX, int runY) =
            StageScreenDetector.StoryRunAction(
                story.RunKind,
                story.ActNumber);
        StageOptionSelectionWaitResult<
            StorySelectionObservation> selectedRun =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        token => ClickAsync(
                            window,
                            runX,
                            runY,
                            token),
                        stableDetections,
                        () => ObserveStorySelection(
                            window,
                            detector),
                        observation =>
                            observation.Selection.Matches(
                                story.RunKind,
                                story.ActNumber,
                                expectedHardMode: null),
                        story.RunKind == StoryRunKind.Act
                            ? null
                            : observation =>
                                SelectionAction(
                                    observation.Selection.ActionX,
                                    observation.Selection.ActionY),
                        observation =>
                            RootRecoveryFor(
                                observation.RecoveryState),
                        NavigationTimeout,
                        TimeSpan.FromMilliseconds(180),
                        cancellationToken)
                    .ConfigureAwait(false);
        ThrowIfSelectionFailed(
            selectedRun,
            $"{RouteLabel(StageMode.Story, story, null)} run option");

        if (story.RunKind != StoryRunKind.Act)
        {
            return RequireSelectionAction(
                selectedRun,
                "Story");
        }

        (int difficultyX, int difficultyY) =
            StageScreenDetector.StoryDifficultyAction(
                story.HardMode);
        StageOptionSelectionWaitResult<
            StorySelectionObservation> selectedDifficulty =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        token => ClickAsync(
                            window,
                            difficultyX,
                            difficultyY,
                            token),
                        stableDetections,
                        () => ObserveStorySelection(
                            window,
                            detector),
                        observation =>
                            observation.Selection.Matches(
                                StoryRunKind.Act,
                                story.ActNumber,
                                story.HardMode),
                        observation =>
                            SelectionAction(
                                observation.Selection.ActionX,
                                observation.Selection.ActionY),
                        observation =>
                            RootRecoveryFor(
                                observation.RecoveryState),
                        NavigationTimeout,
                        TimeSpan.FromMilliseconds(180),
                        cancellationToken)
                    .ConfigureAwait(false);
        ThrowIfSelectionFailed(
            selectedDifficulty,
            $"{RouteLabel(StageMode.Story, story, null)} difficulty");
        return RequireSelectionAction(
            selectedDifficulty,
            "Story");
    }

    private async Task<(int X, int Y)>
        SelectRaidOptionsAsync(
            RobloxWindow window,
            RaidPreset raid,
            IDetectorPack detector,
            int stableDetections,
            CancellationToken cancellationToken)
    {
        await WaitForStateAsync(
            window,
            StageScreenState.RaidSelector,
            NavigationTimeout,
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);
        (int mapX, int mapY) =
            StageScreenDetector.RaidMapAction;
        await ClickAsync(
            window,
            mapX,
            mapY,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            StageScreenState.RaidDetail,
            NavigationTimeout,
            detector,
            stableDetections,
            cancellationToken).ConfigureAwait(false);

        (int actX, int actY) =
            StageScreenDetector.RaidActAction(raid.Act);
        StageOptionSelectionWaitResult<
            RaidSelectionObservation> selectedAct =
                await StageOptionSelectionWaiter
                    .ClickOnceAndWaitAsync(
                        token => ClickAsync(
                            window,
                            actX,
                            actY,
                            token),
                        stableDetections,
                        () => ObserveRaidSelection(
                            window,
                            detector),
                        observation =>
                            observation.Selection.Matches(
                                raid.Act),
                        observation =>
                            SelectionAction(
                                observation.Selection.ActionX,
                                observation.Selection.ActionY),
                        observation =>
                            RootRecoveryFor(
                                observation.RecoveryState),
                        NavigationTimeout,
                        TimeSpan.FromMilliseconds(180),
                        cancellationToken)
                    .ConfigureAwait(false);
        ThrowIfSelectionFailed(
            selectedAct,
            $"{RouteLabel(StageMode.Raid, null, raid)} act");
        return RequireSelectionAction(
            selectedAct,
            "Raid");
    }

    private StorySelectionObservation ObserveStorySelection(
        RobloxWindow window,
        IDetectorPack detector)
    {
        ImageFrame frame =
            CaptureClient(window, detector);
        return new StorySelectionObservation(
            StageOptionSelectionDetector.DetectStory(frame),
            detector.RecoveryState(frame));
    }

    private RaidSelectionObservation ObserveRaidSelection(
        RobloxWindow window,
        IDetectorPack detector)
    {
        ImageFrame frame =
            CaptureClient(window, detector);
        return new RaidSelectionObservation(
            StageOptionSelectionDetector.DetectRaid(frame),
            detector.RecoveryState(frame));
    }

    private static string? RootRecoveryFor(
        string? state) =>
        IsRootRecovery(state)
            ? state
            : null;

    private static (int X, int Y)? SelectionAction(
        int? x,
        int? y) =>
        x is int actionX &&
        y is int actionY
            ? (actionX, actionY)
            : null;

    private static void ThrowIfSelectionFailed<
        TObservation>(
        StageOptionSelectionWaitResult<TObservation> result,
        string label)
        where TObservation : class
    {
        if (result.Interruption is string interruption)
        {
            throw new StageRecoveryException(
                interruption);
        }
        if (!result.Succeeded)
        {
            throw new RobloxUiUnavailableException(
                $"The selected {label} could not be verified. Select Stage was not clicked.");
        }
    }

    private static (int X, int Y)
        RequireSelectionAction<TObservation>(
            StageOptionSelectionWaitResult<TObservation> result,
            string mode)
        where TObservation : class
    {
        if (result.ActionX is not int x ||
            result.ActionY is not int y)
        {
            throw new RobloxUiUnavailableException(
                $"The {mode} Select Stage button could not be located.");
        }
        return (x, y);
    }

    private sealed record StorySelectionObservation(
        StoryOptionSelectionMatch Selection,
        string? RecoveryState);

    private sealed record RaidSelectionObservation(
        RaidOptionSelectionMatch Selection,
        string? RecoveryState);
}
