using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed class ExpeditionMacroRunner : IGameModeWorkflow
{
    private static readonly TimeSpan ExtractionTransitionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDismissalTimeout = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> RecoveryStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "afk", "disconnect", "lobby", "play", "map_select", "map_preview",
    };

    private readonly IRobloxAutomation _automation;
    private readonly CameraAlignmentEngine _camera;
    private readonly PlacementService _placements;
    private readonly IDiscordNotifier _discord;
    private readonly object _notificationGate = new();
    private readonly HashSet<Task> _pendingNotifications = [];

    public ExpeditionMacroRunner(
        IRobloxAutomation automation,
        CameraAlignmentEngine camera,
        PlacementService placements,
        IDiscordNotifier discord)
    {
        _automation = automation;
        _camera = camera;
        _placements = placements;
        _discord = discord;
    }

    public string GameId => "anime-expeditions";

    public string ModeId => "expeditions";

    public async Task RunAsync(
        ExpeditionPreset preset,
        CameraModel cameraModel,
        PlacementModel placementModel,
        IDetectorPack detector,
        string webhookUrl,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ExpeditionRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default,
        DateTimeOffset? stopAfterCurrentRunUtc = null,
        Func<Exception, CancellationToken, Task>? recoverableFailure = null)
    {
        preset.Validate();
        cameraModel.Manifest.Validate();
        placementModel.Validate();
        ValidateCompatibility(preset, cameraModel, placementModel, detector.Manifest);
        RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch runtime = Stopwatch.StartNew();
        int repeats = 0;
        int victories = 0;
        int defeats = 0;
        int recoveries = 0;
        int bossesSeen = 0;
        bool shiftLockEnabled = false;

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void PublishSummary() => summaryChanged?.Invoke(new ExpeditionRunSummary(startedAt, runtime.Elapsed, repeats, victories, defeats, recoveries, bossesSeen));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));

        Write($"Using Roblox window '{window.Title}'.");
        PublishSummary();
        try
        {
            Focus(window);
            await EnsureClientSizeAsync(window, detector.Manifest.ClientWidth, detector.Manifest.ClientHeight, Write, cancellationToken).ConfigureAwait(false);
            string? initial = await ProbeStableRecoveryStateAsync(window, detector, preset, allowNavigationEntry: true, cancellationToken).ConfigureAwait(false);
            if (initial is not null)
            {
                if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(initial)} was recognized, but automatic recovery is disabled.");
                bool unexpected = initial.Equals("disconnect", StringComparison.OrdinalIgnoreCase) ||
                    initial.Equals("afk", StringComparison.OrdinalIgnoreCase);
                if (unexpected)
                {
                    recoveries++;
                    PublishSummary();
                }
                await RecoverToPrestartAsync(window, initial, preset, detector, webhookUrl, unexpected, runtime, victories, defeats, Report, Write, cancellationToken).ConfigureAwait(false);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                {
                    Write("Challenge reset reached before the next Expedition run. Returning to Challenges.", MacroEventLevel.Success);
                    return;
                }
                bossesSeen = 0;
                PublishSummary();
                try
                {
                    Report("Waiting", 0, "Waiting for the Expedition prestart screen.");
                    bool prestartReady = await WaitForStateAsync(
                        window,
                        detector,
                        "start",
                        preset,
                        Report,
                        Write,
                        stopAfterCurrentRunUtc,
                        cancellationToken).ConfigureAwait(false);
                    if (!prestartReady)
                    {
                        Write("Challenge reset reached while waiting for the next Expedition run. Returning to Challenges.", MacroEventLevel.Success);
                        return;
                    }
                    Write("Prestart screen recognized. Preparing camera.", MacroEventLevel.Success);
                    await PrepareCameraAsync(window, preset, cameraModel, value => shiftLockEnabled = value, progress, Write, cancellationToken).ConfigureAwait(false);
                    shiftLockEnabled = false;
                    await ThrowIfRecoveryAsync(window, detector, preset, cancellationToken).ConfigureAwait(false);
                    if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                    {
                        Write("Challenge reset reached during Expedition preparation. Returning before starting the node.", MacroEventLevel.Success);
                        return;
                    }

                    Report("Placement", 0, "Placing the recorded prestart units.");
                    await PlaceStepsAsync(window, placementModel, placementModel.Steps, preset, Write, cancellationToken).ConfigureAwait(false);
                    Write($"Preplace pass sent {placementModel.Steps.Count} placement(s).");
                    await ThrowIfRecoveryAsync(window, detector, preset, cancellationToken).ConfigureAwait(false);
                    if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                    {
                        Write("Challenge reset reached during Expedition preparation. Returning before starting the node.", MacroEventLevel.Success);
                        return;
                    }

                    Report("Starting node", 0, "Starting the Expedition node.");
                    await ClickActionAsync(window, detector, "start", cancellationToken).ConfigureAwait(false);
                    await Task.Delay(2600, cancellationToken).ConfigureAwait(false);
                    RunTerminal terminal = await MonitorUntilRunEndAsync(
                        window,
                        preset,
                        placementModel,
                        detector,
                        value => { bossesSeen = value; PublishSummary(); },
                        Report,
                        Write,
                        cancellationToken).ConfigureAwait(false);

                    if (terminal.State == "victory") victories++;
                    else defeats++;
                    repeats++;
                    PublishSummary();
                    string detail = terminal.State == "victory" ? "The run reached the Victory screen." : "The run reached the Defeat screen.";
                    QueueNotification(webhookUrl, terminal.State, detail, terminal.Frame, runtime.Elapsed, victories, defeats, preset, Write);
                    if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                    {
                        Report("Completed", 100, "Current Expedition run finished. Returning to Challenges.", terminal.State, null);
                        Write("Challenge reset occurred during an Expedition run. Closing its results before switching modes.", MacroEventLevel.Information);
                        await CloseTerminalForModeSwitchAsync(window, detector, terminal, preset, Report, Write, cancellationToken).ConfigureAwait(false);
                        Write("Current Expedition run finished cleanly. Returning to Challenges.", MacroEventLevel.Success);
                        return;
                    }
                    if (terminal.State == "victory") Report("Completed", 100, "Extraction victory recognized. Repeating the stage.");
                    else if (ExpeditionRunPolicy.IsEarlyDefeat(preset, bossesSeen))
                    {
                        Report("Completed", 100, "Early defeat recognized before the extraction target. Repeating.");
                        Write($"Run ended after {bossesSeen} boss node(s), before the target of {preset.BossesBeforeExtract}.", MacroEventLevel.Warning);
                    }
                    else Report("Completed", 100, "Defeat recognized. Repeating the stage.");
                    await ClickActionAsync(window, detector, terminal.State, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(4500, cancellationToken).ConfigureAwait(false);
                }
                catch (CameraAlignmentException alignment)
                {
                    string detail = $"Skipping this Expedition run because camera alignment exhausted {alignment.Attempts} attempts (best {alignment.BestConfidence:P0}). No units were placed and the node was not started.";
                    Write(detail, MacroEventLevel.Warning, "camera_alignment_skipped", alignment.BestConfidence);
                    Report("Task skipped", 100, detail, "camera_alignment_skipped", alignment.BestConfidence);
                    if (recoverableFailure is not null)
                    {
                        try
                        {
                            await recoverableFailure(alignment, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception diagnosticsError)
                        {
                            Write($"Recoverable-failure diagnostics could not finish: {diagnosticsError.Message}", MacroEventLevel.Warning, "camera_alignment_skipped", alignment.BestConfidence);
                        }
                    }
                    QueueNotification(webhookUrl, "skipped", detail, TryCaptureClient(window, detector), runtime.Elapsed, victories, defeats, preset, Write);
                    await OpenPartyPreviewAfterAlignmentFailureAsync(window, detector, preset, Report, cancellationToken).ConfigureAwait(false);
                    Write(stopAfterCurrentRunUtc is null
                        ? "Expeditions stopped safely at the party preview after the alignment circuit breaker opened."
                        : "Expeditions returned to the party preview so the Challenge scheduler can continue.",
                        MacroEventLevel.Warning,
                        "camera_alignment_skipped",
                        alignment.BestConfidence);
                    return;
                }
                catch (RecoveryNeededException recovery)
                {
                    if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(recovery.State)} was recognized, but automatic recovery is disabled.", recovery);
                    recoveries++;
                    PublishSummary();
                    await RecoverToPrestartAsync(window, recovery.State, preset, detector, webhookUrl, notify: true, runtime, victories, defeats, Report, Write, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    string? recovery = await ProbeStableRecoveryStateAsync(window, detector, preset, allowNavigationEntry: false, cancellationToken).ConfigureAwait(false);
                    if (!preset.AutoRecover || recovery is null) throw;
                    recoveries++;
                    PublishSummary();
                    Write("An action failed while a recovery screen was visible; switching to automatic recovery.", MacroEventLevel.Warning);
                    await RecoverToPrestartAsync(window, recovery, preset, detector, webhookUrl, notify: true, runtime, victories, defeats, Report, Write, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (shiftLockEnabled)
            {
                try
                {
                    _automation.Focus(window);
                    await _automation.TapLeftControlAsync(window, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Window restoration still proceeds.
                }
            }
            await FlushNotificationsAsync(Write).ConfigureAwait(false);
        }
    }

    private async Task<ImageFrame> OpenPartyPreviewAfterAlignmentFailureAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _automation.ParkCursorAsync(window, cancellationToken).ConfigureAwait(false);
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            ImageFrame frame = CaptureClient(window, detector);
            if (ChallengeScreenDetector.Detect(frame).State == ChallengeScreenState.PostMatchPreview) return frame;
            (int X, int Y)? play = ChallengeScreenDetector.PlayAction(frame);
            if (play is null)
            {
                report("Task skipped", 100, $"Waiting for the Play control before leaving the unstarted Expedition ({attempt}/3).", "camera_alignment_skipped", null);
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                continue;
            }
            report("Task skipped", 100, $"Opening Play to leave the unstarted Expedition ({attempt}/3).", "camera_alignment_skipped", null);
            Focus(window);
            await _automation.ClickClientAsync(window, play.Value.X, play.Value.Y, cancellationToken).ConfigureAwait(false);
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(4);
            StableStateTracker<ChallengeScreenState> tracker = new(Math.Max(1, preset.StableDetections));
            while (DateTimeOffset.UtcNow < deadline)
            {
                ImageFrame current = CaptureClient(window, detector);
                ChallengeScreenMatch match = ChallengeScreenDetector.Detect(current);
                ChallengeScreenState? stable = tracker.Update(match.State == ChallengeScreenState.PostMatchPreview
                    ? ChallengeScreenState.PostMatchPreview
                    : ChallengeScreenState.None);
                if (stable == ChallengeScreenState.PostMatchPreview) return current;
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("Camera alignment was skipped, but the unstarted Expedition could not be exited after three Play attempts.");
    }

    private async Task PrepareCameraAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        CameraModel model,
        Action<bool> shiftLock,
        IProgress<MacroProgress>? progress,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.MoveCursorToClientCenterAsync(window, cancellationToken).ConfigureAwait(false);
        await Task.Delay(120, cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress("Camera preparation", 10, "Zooming out fully."));
        await _automation.ZoomOutFullyAsync(window, preset.ZoomTicks, cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress("Camera preparation", 25, "Enabling shift lock and clamping camera pitch."));
        await _automation.TapLeftControlAsync(window, cancellationToken).ConfigureAwait(false);
        shiftLock(true);
        try
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            await _automation.DragCameraAsync(window, 0, preset.PitchDragPixels, 90, cancellationToken).ConfigureAwait(false);
            await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            double score = await _camera.AlignAsync(
                model,
                window,
                manageShiftLock: false,
                progress: progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            log($"Camera alignment finished at {score:P0} confidence.", MacroEventLevel.Information, null, score);
        }
        finally
        {
            try
            {
                _automation.Focus(window);
                await _automation.TapLeftControlAsync(window, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                shiftLock(false);
            }
        }
        await Task.Delay(350, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RunTerminal> MonitorUntilRunEndAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        PlacementModel placement,
        IDetectorPack detector,
        Action<int> bossesChanged,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> stateTracker = new(preset.StableDetections);
        StableStateTracker<string> nodeTracker = new(preset.StableDetections);
        // Recovery abandons the active run and resets its observed boss progress.
        // Confirm it independently so one UI animation frame cannot trigger rejoin.
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        string? currentNode = null;
        int bosses = 0;
        report("Gameplay", 0, "Gameplay active. Watching node type, pauses, rewards, and run end.", null, null);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            string? stableNode = nodeTracker.Update(detector.CurrentNodeType(frame));
            if (stableNode is not null && !stableNode.Equals(currentNode, StringComparison.OrdinalIgnoreCase))
            {
                currentNode = stableNode;
                log($"Progress bar: current node is {stableNode}.", MacroEventLevel.Information, stableNode, null);
                if (stableNode == "boss")
                {
                    bosses++;
                    bossesChanged(bosses);
                    log($"Boss node count is now {bosses}.", MacroEventLevel.Information, stableNode, null);
                }
            }

            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? candidate = ExpeditionRunPolicy.PreferActiveState(detector.Manifest, scores, detector.Classify(scores));
            ThrowForStableRecovery(recoveryTracker, candidate, activeRunOnly: true);
            if (candidate is not null) report("Gameplay", 0, $"Detected {Label(candidate)}.", candidate, scores[candidate]);
            string? state = stateTracker.Update(candidate);
            if (state is null)
            {
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            stateTracker.Reset();
            double score = scores[state];
            log($"Recognized {state} at {score:P0} confidence.", MacroEventLevel.Success, state, score);
            if (state is "defeat" or "victory") return new RunTerminal(state, frame.Clone());
            if (state == "reward")
            {
                report("Reward", 0, "Selecting the first available reward card.", state, score);
                await ClickActionAsync(window, detector, "reward", frame, cancellationToken).ConfigureAwait(false);
                await Task.Delay(4300, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (state == "confirm")
            {
                await DismissNodeConfirmationAsync(window, detector, preset, frame, report, log, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (state == "extract_confirm")
            {
                if (ExpeditionRunPolicy.ShouldExtract(preset, bosses))
                {
                    ExtractionTransactionState transaction = new();
                    if (!transaction.TryBegin()) throw new InvalidOperationException("Could not begin extraction confirmation handling.");
                    await ConfirmExtractionAsync(window, detector, preset, transaction, frame, report, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    log("Extraction confirmation appeared while extraction is disabled.", MacroEventLevel.Warning, state, score);
                    await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                }
                continue;
            }
            if (state is "start" or "checkpoint" or "continue")
            {
                if (state == "checkpoint" && ExpeditionRunPolicy.ShouldExtract(preset, bosses))
                {
                    report("Extraction", 0, $"Extraction target met after {bosses} boss node(s).", state, score);
                    await ExtractAtCheckpointAsync(window, detector, preset, frame, report, log, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                await RetryRemainingUnitsAsync(window, placement, preset, detector, frame, log, cancellationToken).ConfigureAwait(false);
                report("Transition", 0, $"Continuing from the {state} pause.", state, score);
                // Placement retries can take several seconds. Re-capture the pause so
                // the click follows its current control rather than a stale frame.
                await ClickActionAsync(window, detector, state, cancellationToken).ConfigureAwait(false);
                if (state is "checkpoint" or "continue") await WaitForConfirmationAsync(window, detector, preset, report, log, cancellationToken).ConfigureAwait(false);
                await Task.Delay(2300, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExtractAtCheckpointAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ImageFrame checkpointFrame,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ExtractionTransactionState transaction = new();
        if (!transaction.TryBegin()) throw new InvalidOperationException("Could not begin checkpoint extraction.");
        log("Checkpoint has an Extract button; opening extraction confirmation.", MacroEventLevel.Information, "checkpoint", null);
        await ClickActionAsync(window, detector, "extract", checkpointFrame, cancellationToken).ConfigureAwait(false);
        bool found = await WaitForStateWithTimeoutAsync(window, detector, "extract_confirm", ExtractionTransitionTimeout, preset, report, cancellationToken).ConfigureAwait(false);
        if (!found)
        {
            throw new InvalidOperationException(
                "Extraction confirmation did not appear within 30 seconds. The macro stopped without clicking Extract again to avoid a delayed duplicate action.");
        }
        await ConfirmExtractionAsync(window, detector, preset, transaction, clientImage: null, report, cancellationToken).ConfigureAwait(false);
        log("Extraction confirmed. Waiting for Victory or an early Defeat screen.", MacroEventLevel.Success, "extract_confirm", null);
    }

    private async Task CloseTerminalForModeSwitchAsync(
        RobloxWindow window,
        IDetectorPack detector,
        RunTerminal terminal,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            if (!ExpeditionRunPolicy.IsStateDetected(detector.Manifest, scores, terminal.State)) return;

            report(
                "Transition",
                100,
                $"Closing the Expedition results before returning to Challenges ({attempt}/{maximumAttempts}).",
                terminal.State,
                scores[terminal.State]);
            await ClickActionAsync(window, detector, "expedition_terminal_close", frame, cancellationToken).ConfigureAwait(false);
            bool dismissed = await WaitForStateToClearAsync(
                window,
                detector,
                terminal.State,
                TimeSpan.FromSeconds(5),
                preset,
                report,
                cancellationToken).ConfigureAwait(false);
            if (dismissed) return;

            log(
                $"Expedition results remained visible after close attempt {attempt}/{maximumAttempts}.",
                MacroEventLevel.Warning,
                terminal.State,
                scores[terminal.State]);
        }

        throw new InvalidOperationException(
            $"The Expedition {terminal.State} screen remained visible after {maximumAttempts} focused close attempts.");
    }

    private async Task ConfirmExtractionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ExtractionTransactionState transaction,
        ImageFrame? clientImage,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        if (!transaction.TryConfirm()) throw new InvalidOperationException("Extraction confirmation was already clicked.");
        report("Extraction", 0, "Confirming extraction once and waiting for the dialog to close.", "extract_confirm", null);
        await ClickActionAsync(window, detector, "extract_confirm", clientImage, cancellationToken).ConfigureAwait(false);
        bool dismissed = await WaitForStateToClearAsync(
            window,
            detector,
            "extract_confirm",
            ExtractionTransitionTimeout,
            preset,
            report,
            cancellationToken).ConfigureAwait(false);
        if (!dismissed)
        {
            throw new InvalidOperationException(
                "Extraction confirmation remained visible for 30 seconds. The macro stopped without clicking Confirm again to avoid a delayed duplicate action.");
        }
        if (!transaction.TryComplete()) throw new InvalidOperationException("Could not complete extraction confirmation handling.");
        await Task.Delay(700, cancellationToken).ConfigureAwait(false);
    }

    private async Task RetryRemainingUnitsAsync(
        RobloxWindow window,
        PlacementModel placement,
        ExpeditionPreset preset,
        IDetectorPack detector,
        ImageFrame frame,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        HashSet<int> keys = placement.Steps.Select(step => step.UnitKey).ToHashSet();
        IReadOnlyList<int> remaining = detector.RemainingUnitKeys(frame, keys);
        if (remaining.Count == 0)
        {
            log("Hotbar check: all recorded unit slots are empty.", MacroEventLevel.Success, null, null);
            return;
        }
        log($"Hotbar check: retrying unit key(s) {string.Join(", ", remaining)}.", MacroEventLevel.Warning, null, null);
        PlacementStep[] steps = placement.Steps.Where(step => remaining.Contains(step.UnitKey)).ToArray();
        await PlaceStepsAsync(window, placement, steps, preset, log, cancellationToken).ConfigureAwait(false);
    }

    private Task PlaceStepsAsync(
        RobloxWindow window,
        PlacementModel placement,
        IReadOnlyList<PlacementStep> steps,
        ExpeditionPreset preset,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            placement,
            steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            preset.UnitKeyHoldMilliseconds,
            preset.UnitSelectDelayMilliseconds,
            stepSent: null,
            status: message => log(message, MacroEventLevel.Information, null, null),
            cancellationToken);

    private async Task RecoverToPrestartAsync(
        RobloxWindow window,
        string initialState,
        ExpeditionPreset preset,
        IDetectorPack detector,
        string webhookUrl,
        bool notify,
        Stopwatch runtime,
        int victories,
        int defeats,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        string state = initialState;
        log($"Automatic recovery started from {Label(state)}. Target: map {preset.MapNumber}, difficulty {preset.Difficulty}.", MacroEventLevel.Warning, state, null);
        if (notify)
        {
            ImageFrame? screenshot = TryCaptureClient(window, detector);
            QueueNotification(webhookUrl, "recovery", $"Automatic rejoin was needed after {Label(initialState)} was recognized.", screenshot, runtime.Elapsed, victories, defeats, preset, log);
        }
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (state)
            {
                case "afk":
                    report("Recovery", 0, "AFK Chamber recognized. Returning to the lobby before rejoining the configured route.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "afk", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "afk", TimeSpan.FromSeconds(20), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "disconnect":
                    report("Recovery", 0, "Disconnected. Clicking Reconnect and waiting for Roblox.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "disconnect", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "disconnect", TimeSpan.FromSeconds(12), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "lobby":
                    report("Recovery", 0, "Lobby recognized. Opening Play from the left navigation.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "lobby", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "lobby", TimeSpan.FromSeconds(15), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "play":
                    report("Recovery", 0, "Play screen recognized. Opening Expeditions.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "play", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "play", TimeSpan.FromSeconds(15), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "map_select":
                    await ConfigureMapAndDifficultyAsync(window, preset, detector, report, log, cancellationToken).ConfigureAwait(false);
                    report("Recovery", 0, "Map and difficulty verified. Selecting the stage.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "select_stage", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "map_select", TimeSpan.FromSeconds(15), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "map_preview":
                    report("Recovery", 0, "Teleport preview recognized. Starting the private stage.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "map_preview", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "map_preview", TimeSpan.FromSeconds(20), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "continue":
                    report("Recovery", 0, "Initial Expedition checkpoint recognized. Continuing to the prestart screen.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "continue", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "continue", TimeSpan.FromSeconds(20), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "start":
                    report("Recovery", 100, "Returned to the configured Expedition prestart screen.", state, null);
                    log("Automatic recovery completed.", MacroEventLevel.Success, state, null);
                    return;
                default:
                    state = await WaitForRecoveryChangeAsync(window, detector, string.Empty, TimeSpan.FromSeconds(20), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
            }
        }
    }

    private async Task ConfigureMapAndDifficultyAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ImageFrame current = CaptureClient(window, detector);
            int? selected = detector.SelectedMap(current);
            if (selected == preset.MapNumber) break;
            report("Recovery", 0, $"Selecting Expedition map {preset.MapNumber}.", "map_select", null);
            await ClickActionAsync(window, detector, $"map_{preset.MapNumber}", cancellationToken).ConfigureAwait(false);
            if (await WaitForSelectionAsync(window, detector, value => detector.SelectedMap(value), preset.MapNumber, TimeSpan.FromSeconds(3), preset, cancellationToken).ConfigureAwait(false)) break;
            log($"Map selection did not register (attempt {attempt}/3).", MacroEventLevel.Warning, "map_select", null);
            if (attempt == 3) throw new InvalidOperationException($"Map {preset.MapNumber} could not be selected. It may still be locked.");
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            report("Recovery", 0, $"Selecting difficulty {preset.Difficulty}.", "map_select", null);
            ImageFrame current = CaptureClient(window, detector);
            int? selected = detector.SelectedDifficulty(current);
            if (selected == preset.Difficulty) return;

            if (selected is null)
            {
                for (int index = 0; index < 3; index++)
                {
                    await ClickActionAsync(window, detector, "difficulty_minus", cancellationToken).ConfigureAwait(false);
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                }
                for (int index = 1; index < preset.Difficulty; index++)
                {
                    await ClickActionAsync(window, detector, "difficulty_plus", cancellationToken).ConfigureAwait(false);
                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                string action = selected < preset.Difficulty ? "difficulty_plus" : "difficulty_minus";
                for (int index = 0; index < Math.Abs(preset.Difficulty - selected.Value); index++)
                {
                    await ClickActionAsync(window, detector, action, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                }
            }

            // One positive active-difficulty match is sufficient. Transition frames are
            // ignored until the animation settles, so the macro never clicks away from
            // an already selected target merely to obtain a second matching frame.
            if (await WaitForSelectionAsync(window, detector, value => detector.SelectedDifficulty(value), preset.Difficulty, TimeSpan.FromSeconds(4.5), preset, cancellationToken).ConfigureAwait(false)) return;
            log($"Difficulty selection did not register (attempt {attempt}/3).", MacroEventLevel.Warning, "map_select", null);
        }
        throw new InvalidOperationException($"Difficulty {preset.Difficulty} could not be selected.");
    }

    private async Task<bool> WaitForSelectionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        Func<ImageFrame, int?> selector,
        int target,
        TimeSpan timeout,
        ExpeditionPreset preset,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            string? recovery = detector.RecoveryState(frame);
            ThrowForStableRecovery(recoveryTracker, recovery == "map_select" ? null : recovery);
            if (selector(frame) == target) return true;
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task<string?> WaitForRecoveryChangeAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string excluded,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> tracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        bool allowStandaloneContinue = excluded.Equals("map_preview", StringComparison.OrdinalIgnoreCase);
        bool captureErrorReported = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ImageFrame frame = CaptureClient(window, detector);
                IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
                string? state = ExpeditionRunPolicy.RecoveryTransition(
                    detector.Manifest,
                    scores,
                    detector.RecoveryState(frame),
                    allowStandaloneContinue);
                if (state is not null && scores.TryGetValue(state, out double score)) report("Recovery", 0, $"Detected {Label(state)}.", state, score);
                bool acceptedTransition = RecoveryStates.Contains(state ?? string.Empty) ||
                    state == "start" ||
                    (allowStandaloneContinue && state == "continue");
                if (!acceptedTransition) tracker.Reset();
                else
                {
                    string? stable = tracker.Update(state);
                    if (stable is not null && !stable.Equals(excluded, StringComparison.OrdinalIgnoreCase)) return stable;
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                if (!captureErrorReported)
                {
                    log($"Waiting for Roblox during recovery: {error.Message}", MacroEventLevel.Warning, null, null);
                    captureErrorReported = true;
                }
            }
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<bool> WaitForStateAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string desired,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        DateTimeOffset? stopAfterCurrentRunUtc,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> tracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc)) return false;
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? state = ExpeditionRunPolicy.PreferDesiredState(detector.Manifest, scores, desired, detector.Classify(scores));
            ThrowForStableRecovery(recoveryTracker, state, activeRunOnly: true);
            if (state is not null) report("Waiting", 0, $"Detected {Label(state)}.", state, scores[state]);
            if (tracker.Update(state) == desired)
            {
                log($"Recognized {desired} at {scores[desired]:P0} confidence.", MacroEventLevel.Success, desired, scores[desired]);
                return true;
            }
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> WaitForStateWithTimeoutAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string desired,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> tracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        while (DateTimeOffset.UtcNow < deadline)
        {
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? state = ExpeditionRunPolicy.PreferDesiredState(detector.Manifest, scores, desired, detector.Classify(scores));
            ThrowForStableRecovery(recoveryTracker, state, activeRunOnly: true);
            if (state is not null) report("Waiting", 0, $"Detected {Label(state)}.", state, scores[state]);
            if (tracker.Update(state) == desired) return true;
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task<bool> WaitForStateToClearAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string stateToClear,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        // Require at least three consecutive clear frames so a short detector
        // flicker cannot reopen the generic loop and authorize another click.
        StableStateTracker<string> clearTracker = new(Math.Max(3, preset.StableDetections));
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        while (DateTimeOffset.UtcNow < deadline)
        {
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? classified = ExpeditionRunPolicy.PreferActiveState(detector.Manifest, scores, detector.Classify(scores));
            ThrowForStableRecovery(recoveryTracker, classified, activeRunOnly: true);
            bool visible = ExpeditionRunPolicy.IsStateDetected(detector.Manifest, scores, stateToClear);
            if (visible)
            {
                clearTracker.Reset();
                report("Waiting", 0, $"Waiting for {Label(stateToClear)} to close.", stateToClear, scores[stateToClear]);
            }
            else if (clearTracker.Update("cleared") == "cleared")
            {
                return true;
            }
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task WaitForConfirmationAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (await WaitForStateWithTimeoutAsync(window, detector, "confirm", TimeSpan.FromSeconds(6), preset, report, cancellationToken).ConfigureAwait(false))
        {
            await DismissNodeConfirmationAsync(window, detector, preset, clientImage: null, report, log, cancellationToken).ConfigureAwait(false);
        }
        else log("Confirmation was not recognized within 6 seconds; returning to state monitoring.", MacroEventLevel.Warning, null, null);
    }

    private async Task DismissNodeConfirmationAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ImageFrame? clientImage,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ConfirmationDismissalState transaction = new();
        while (transaction.TryBeginAttempt())
        {
            ImageFrame frame = clientImage ?? CaptureClient(window, detector);
            clientImage = null;
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            if (!ExpeditionRunPolicy.IsStateDetected(detector.Manifest, scores, "confirm"))
            {
                if (!transaction.TryComplete()) throw new InvalidOperationException("Could not complete node confirmation handling.");
                return;
            }

            report(
                "Transition",
                0,
                transaction.Attempts == 1
                    ? "Confirming the node transition."
                    : $"Confirmation is still visible; retrying the focused click ({transaction.Attempts}/{ConfirmationDismissalState.MaximumAttempts}).",
                "confirm",
                scores["confirm"]);
            await ClickActionAsync(window, detector, "confirm", frame, cancellationToken).ConfigureAwait(false);
            bool dismissed = await WaitForStateToClearAsync(
                window,
                detector,
                "confirm",
                ConfirmationDismissalTimeout,
                preset,
                report,
                cancellationToken).ConfigureAwait(false);
            if (dismissed)
            {
                if (!transaction.TryComplete()) throw new InvalidOperationException("Could not complete node confirmation handling.");
                log($"Node confirmation closed after {transaction.Attempts} click attempt(s).", MacroEventLevel.Success, "confirm", null);
                return;
            }

            if (!transaction.TryMarkStillVisible()) throw new InvalidOperationException("Could not continue node confirmation handling.");
            log(
                $"Node confirmation remained visible after click attempt {transaction.Attempts}/{ConfirmationDismissalState.MaximumAttempts}.",
                MacroEventLevel.Warning,
                "confirm",
                scores["confirm"]);
        }

        throw new InvalidOperationException(
            $"The Continue Expedition confirmation remained visible after {ConfirmationDismissalState.MaximumAttempts} focused click attempts. " +
            "Roblox did not acknowledge the button; retry after the client is responsive.");
    }

    private async Task<bool> TryClickRecoveryAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        try
        {
            await ClickActionAsync(window, detector, state, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            log($"Recovery click '{state}' failed and will be retried: {error.Message}", MacroEventLevel.Warning, state, null);
            await Task.Delay(750, cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    private Task ClickActionAsync(RobloxWindow window, IDetectorPack detector, string state, CancellationToken cancellationToken) =>
        ClickActionAsync(window, detector, state, clientImage: null, cancellationToken);

    private async Task ClickActionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        ImageFrame? clientImage,
        CancellationToken cancellationToken)
    {
        Focus(window);
        ImageFrame actionFrame = clientImage ?? CaptureClient(window, detector);
        (int x, int y) = detector.ActionFor(state, actionFrame);
        await _automation.ClickClientAsync(window, x, y, cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame CaptureClient(RobloxWindow window, IDetectorPack detector)
    {
        Focus(window);
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width != detector.Manifest.ClientWidth || bounds.Height != detector.Manifest.ClientHeight) throw new InvalidOperationException("Roblox no longer matches the detector pack client size.");
        return _automation.CaptureClient(window);
    }

    private ImageFrame? TryCaptureClient(RobloxWindow window, IDetectorPack detector)
    {
        try { return CaptureClient(window, detector); }
        catch { return null; }
    }

    private async Task<string?> ProbeStableRecoveryStateAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        bool allowNavigationEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            string? first = RecoveryProbeCandidate(CaptureClient(window, detector), detector, allowNavigationEntry);
            if (first is null) return null;
            int required = ExpeditionRunPolicy.RecoveryStableDetections(preset);
            for (int observation = 1; observation < required; observation++)
            {
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
                string? current = RecoveryProbeCandidate(CaptureClient(window, detector), detector, allowNavigationEntry);
                if (!string.Equals(first, current, StringComparison.OrdinalIgnoreCase)) return null;
            }
            return first;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task ThrowIfRecoveryAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        CancellationToken cancellationToken)
    {
        string? state = await ProbeStableRecoveryStateAsync(window, detector, preset, allowNavigationEntry: false, cancellationToken).ConfigureAwait(false);
        if (state is not null) throw new RecoveryNeededException(state);
    }

    private static string? RecoveryProbeCandidate(ImageFrame frame, IDetectorPack detector, bool allowNavigationEntry)
    {
        IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
        if (ExpeditionRunPolicy.IsStateDetected(detector.Manifest, scores, "start")) return null;
        string? state = detector.RecoveryState(frame);
        return allowNavigationEntry || ExpeditionRunPolicy.CanEnterRecoveryDuringRun(state) ? state : null;
    }

    private static void ThrowForStableRecovery(StableStateTracker<string> tracker, string? state, bool activeRunOnly = false)
    {
        if (state is null || !RecoveryStates.Contains(state) || (activeRunOnly && !ExpeditionRunPolicy.CanEnterRecoveryDuringRun(state)))
        {
            tracker.Reset();
            return;
        }
        string? stable = tracker.Update(state);
        if (stable is not null) throw new RecoveryNeededException(stable);
    }

    private async Task EnsureClientSizeAsync(
        RobloxWindow window,
        int width,
        int height,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width == width && bounds.Height == height) return;
        log($"Restoring Roblox client size to {width} × {height}.", MacroEventLevel.Information, null, null);
        await _automation.ResizeClientAsync(window, width, height, cancellationToken).ConfigureAwait(false);
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        ClientBounds actual = _automation.GetClientBounds(window);
        if (actual.Width != width || actual.Height != height)
        {
            throw new InvalidOperationException($"Roblox did not accept the required {width} × {height} client size (actual: {actual.Width} × {actual.Height}).");
        }
    }

    private void QueueNotification(
        string webhookUrl,
        string eventName,
        string detail,
        ImageFrame? screenshot,
        TimeSpan runtime,
        int victories,
        int defeats,
        ExpeditionPreset preset,
        Action<string, MacroEventLevel, string?, double?> log)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;
        log($"Queued Discord {eventName} notification.", MacroEventLevel.Information, null, null);
        DiscordNotification notification = new()
        {
            WebhookUrl = webhookUrl,
            Event = eventName,
            Runtime = runtime,
            Victories = victories,
            Defeats = defeats,
            MapNumber = preset.MapNumber,
            Difficulty = preset.Difficulty,
            Detail = detail,
            Screenshot = screenshot?.Clone(),
        };
        Task sendTask = Task.Run(async () =>
        {
            try
            {
                await _discord.SendAsync(notification, CancellationToken.None).ConfigureAwait(false);
                log($"Discord {eventName} notification sent.", MacroEventLevel.Success, null, null);
            }
            catch (Exception error)
            {
                log($"Discord {eventName} notification failed: {error.Message}", MacroEventLevel.Warning, null, null);
            }
        });
        lock (_notificationGate) _pendingNotifications.Add(sendTask);
        _ = sendTask.ContinueWith(
            completed =>
            {
                lock (_notificationGate) _pendingNotifications.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task FlushNotificationsAsync(Action<string, MacroEventLevel, string?, double?> log)
    {
        Task[] pending;
        lock (_notificationGate) pending = _pendingNotifications.ToArray();
        if (pending.Length == 0) return;

        Task all = Task.WhenAll(pending);
        Task completed = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);
        if (completed != all)
        {
            log($"Stopped with {pending.Count(task => !task.IsCompleted)} Discord notification(s) still in flight.", MacroEventLevel.Warning, null, null);
            return;
        }
        await all.ConfigureAwait(false);
    }

    private static string Label(string value) => value.Equals("afk", StringComparison.OrdinalIgnoreCase)
        ? "AFK Chamber"
        : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' '));

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
    }

    private static void ValidateCompatibility(ExpeditionPreset preset, CameraModel camera, PlacementModel placement, DetectorPackManifest detector)
    {
        if (!string.Equals(preset.CameraModelId, camera.Manifest.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The preset camera model does not match the loaded model.");
        if (!string.Equals(preset.PlacementModelId, placement.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The preset placement model does not match the loaded model.");
        if (!string.Equals(preset.DetectorPackId, detector.PackId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The preset detector pack does not match the loaded pack.");
        if (camera.Manifest.ClientWidth != detector.ClientWidth || camera.Manifest.ClientHeight != detector.ClientHeight) throw new InvalidDataException("Camera model and detector pack use different Roblox client sizes.");
        if (placement.ClientWidth != detector.ClientWidth || placement.ClientHeight != detector.ClientHeight) throw new InvalidDataException("Placement model and detector pack use different Roblox client sizes.");
    }

    private sealed record RunTerminal(string State, ImageFrame Frame);

    private sealed class RecoveryNeededException : Exception
    {
        public RecoveryNeededException(string state) : base($"Recovery screen recognized: {state}.")
        {
            State = state;
        }

        public string State { get; }
    }
}
