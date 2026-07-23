using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner : IGameModeWorkflow
{
    private static readonly TimeSpan ExtractionTransitionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDismissalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GameModeHandoffTimeout = TimeSpan.FromSeconds(90);

    internal enum GameModeHandoffCommand
    {
        Complete,
        ChangeGamemode,
        PressPlayKey,
        Wait,
    }

    private static readonly HashSet<string> RecoveryStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "afk", "disconnect", "lobby", "play", "map_select", "map_preview",
        "post_match_party",
    };

    private readonly IRobloxAutomation _automation;
    private readonly CameraAlignmentEngine _camera;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;

    public ExpeditionMacroRunner(
        IRobloxAutomation automation,
        CameraAlignmentEngine camera,
        PlacementService placements,
        TeamSelectionService teams,
        IDiscordNotifier discord)
    {
        _automation = automation;
        _camera = camera;
        _placements = placements;
        _teams = teams;
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
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ExpeditionRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default,
        DateTimeOffset? stopAfterCurrentRunUtc = null,
        Func<Exception, CancellationToken, Task>? recoverableFailure = null,
        int? maximumRuns = null,
        char? unitMenuKey = null,
        Func<int, int, TimeSpan, CancellationToken, Task<bool>>? continueScheduledRoute = null)
    {
        if (maximumRuns is < 1) throw new ArgumentOutOfRangeException(nameof(maximumRuns));
        preset.Validate();
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        cameraModel.Manifest.Validate();
        placementModel.Validate();
        ValidateCompatibility(preset, cameraModel, placementModel, detector.Manifest);
        ValidateTeamKey(preset.TeamSlot > 0, unitMenuKey);
        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch runtime = Stopwatch.StartNew();
        int repeats = 0;
        int victories = 0;
        int defeats = 0;
        int recoveries = 0;
        int bossesSeen = 0;
        bool teamLoaded = preset.TeamSlot == 0;

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void PublishSummary() => summaryChanged?.Invoke(new ExpeditionRunSummary(startedAt, runtime.Elapsed, repeats, victories, defeats, recoveries, bossesSeen));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));
        DiscordRunTarget reportTarget = new(preset.MapNumber, preset.Difficulty, string.Empty);
        DiscordRunReporter reporter = new(_discord, webhookUrl, "Expeditions Macro", "expeditions", Write);

        Write($"Using Roblox window '{window.Title}' ({window.ProcessDescription}).");
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
                await RecoverToPrestartAsync(window, initial, preset, detector, reporter, unexpected, runtime, victories, defeats, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
            }
            reporter.Queue(
                "started",
                $"Map {preset.MapNumber}, Difficulty {preset.Difficulty} is starting.",
                TryCaptureClient(window, detector),
                runtime.Elapsed,
                victories,
                defeats,
                reportTarget);

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
                    if (!teamLoaded)
                    {
                        await _teams.SelectAsync(window, preset.TeamSlot, unitMenuKey!.Value, progress, cancellationToken).ConfigureAwait(false);
                        teamLoaded = true;
                    }
                    await PrepareCameraAsync(window, preset, cameraModel, progress, Write, cancellationToken).ConfigureAwait(false);
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
                    Stopwatch matchRuntime = Stopwatch.StartNew();
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
                    reporter.Queue(
                        terminal.State,
                        detail,
                        terminal.Frame,
                        runtime.Elapsed,
                        victories,
                        defeats,
                        reportTarget,
                        matchRuntime.Elapsed);
                    if (continueScheduledRoute is not null)
                    {
                        bool repeatSameRoute = await continueScheduledRoute(
                            terminal.State == "victory" ? 1 : 0,
                            terminal.State == "defeat" ? 1 : 0,
                            matchRuntime.Elapsed,
                            cancellationToken).ConfigureAwait(false);
                        if (repeatSameRoute)
                        {
                            Report("Completed", 100, "The same Expedition preset is next. Repeating the stage.", terminal.State, null);
                            Write("The scheduler kept the same Expedition route; using Repeat Stage instead of reopening Play.", MacroEventLevel.Success, "repeat_stage", null);
                            await ClickActionAsync(window, detector, terminal.State, terminal.Frame, cancellationToken).ConfigureAwait(false);
                            await Task.Delay(4500, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        Report("Completed", 100, "Scheduled Expedition match finished. Returning to shared navigation.", terminal.State, null);
                        Write("The next scheduled route is different. Leaving the completed Expedition through the Play selector.", MacroEventLevel.Information);
                        await OpenPlayMenuForModeSwitchAsync(window, detector, terminal, preset, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    if (maximumRuns is int maximum && repeats >= maximum)
                    {
                        Report("Completed", 100, "Scheduled Expedition match finished. Returning to the task list.", terminal.State, null);
                        Write("Scheduled Expedition match finished. Opening the Play interface before returning to the task scheduler.", MacroEventLevel.Information);
                        await OpenPlayMenuForModeSwitchAsync(window, detector, terminal, preset, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                    {
                        Report("Completed", 100, "Current Expedition run finished. Returning to Challenges.", terminal.State, null);
                        Write("Challenge reset occurred during an Expedition run. Closing its results before switching modes.", MacroEventLevel.Information);
                        await OpenPlayMenuForModeSwitchAsync(window, detector, terminal, preset, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
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
                catch (CameraWorldNotRenderedException world)
                    when (preset.AutoRecover)
                {
                    recoveries++;
                    PublishSummary();
                    string detail =
                        $"Map {preset.MapNumber}, Difficulty {preset.Difficulty} loaded without world geometry. Returning through Play and retrying the same Expedition without placement.";
                    Write(
                        detail,
                        MacroEventLevel.Warning,
                        "camera_world_missing",
                        world.BestConfidence);
                    Report(
                        "Recovery",
                        0,
                        detail,
                        "camera_world_missing",
                        world.BestConfidence);
                    reporter.Queue(
                        "recovery",
                        detail,
                        TryCaptureClient(window, detector),
                        runtime.Elapsed,
                        victories,
                        defeats,
                        reportTarget);
                    await CompleteGameModeHandoffAsync(
                        window,
                        detector,
                        preset,
                        playMenuKey,
                        "camera_world_missing",
                        pressPlayFirst: true,
                        Report,
                        Write,
                        cancellationToken).ConfigureAwait(false);
                    await RecoverToPrestartAsync(
                        window,
                        "play",
                        preset,
                        detector,
                        reporter,
                        notify: false,
                        runtime,
                        victories,
                        defeats,
                        playMenuKey,
                        Report,
                        Write,
                        cancellationToken).ConfigureAwait(false);
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
                    reporter.Queue("skipped", detail, TryCaptureClient(window, detector), runtime.Elapsed, victories, defeats, reportTarget);
                    await OpenPlayMenuAfterAlignmentFailureAsync(window, detector, preset, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
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
                    await RecoverToPrestartAsync(window, recovery.State, preset, detector, reporter, notify: true, runtime, victories, defeats, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
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
                    await RecoverToPrestartAsync(window, recovery, preset, detector, reporter, notify: true, runtime, victories, defeats, playMenuKey, Report, Write, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await reporter.FlushAsync().ConfigureAwait(false);
        }
    }

    private Task<ImageFrame> OpenPlayMenuAfterAlignmentFailureAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        OpenPlayMenuAsync(
            window,
            detector,
            preset,
            playMenuKey,
            "Task skipped",
            "camera_alignment_skipped",
            report,
            log,
            cancellationToken);

    private async Task PrepareCameraAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        CameraModel model,
        IProgress<MacroProgress>? progress,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        double score = await _camera.PrepareAndAlignAsync(
            model,
            window,
            preset.ZoomTicks,
            preset.PitchDragPixels,
            progress,
            cancellationToken).ConfigureAwait(false);
        log($"Camera alignment finished at {score:P0} confidence.", MacroEventLevel.Information, null, score);
        await Task.Delay(350, cancellationToken).ConfigureAwait(false);
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
        await ConfirmExtractionAsync(window, detector, preset, transaction, clientImage: null, report, log, cancellationToken).ConfigureAwait(false);
        log("Extraction confirmed. Waiting for Victory or an early Defeat screen.", MacroEventLevel.Success, "extract_confirm", null);
    }

    private async Task ConfirmExtractionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ExtractionTransactionState transaction,
        ImageFrame? clientImage,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (!transaction.TryConfirm()) throw new InvalidOperationException("Extraction confirmation was already clicked.");
        ConfirmationDismissalState dismissal = new();
        while (dismissal.TryBeginAttempt())
        {
            ImageFrame frame = clientImage ?? CaptureClient(window, detector);
            clientImage = null;
            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            if (!ExpeditionRunPolicy.IsStateDetected(detector.Manifest, scores, "extract_confirm"))
            {
                if (!dismissal.TryComplete() || !transaction.TryComplete())
                {
                    throw new InvalidOperationException("Could not complete extraction confirmation handling.");
                }
                return;
            }

            report(
                "Extraction",
                0,
                dismissal.Attempts == 1
                    ? "Confirming extraction and waiting for the dialog to close."
                    : $"Extraction confirmation is still visible; retrying the focused click ({dismissal.Attempts}/{ConfirmationDismissalState.MaximumAttempts}).",
                "extract_confirm",
                scores["extract_confirm"]);
            await ClickActionAsync(window, detector, "extract_confirm", frame, cancellationToken).ConfigureAwait(false);
            bool dismissed = await WaitForStateToClearAsync(
                window,
                detector,
                "extract_confirm",
                ConfirmationDismissalTimeout,
                preset,
                report,
                cancellationToken).ConfigureAwait(false);
            if (dismissed)
            {
                if (!dismissal.TryComplete() || !transaction.TryComplete())
                {
                    throw new InvalidOperationException("Could not complete extraction confirmation handling.");
                }
                log(
                    $"Extraction confirmation closed after {dismissal.Attempts} focused click attempt(s).",
                    MacroEventLevel.Success,
                    "extract_confirm",
                    null);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!dismissal.TryMarkStillVisible())
            {
                throw new InvalidOperationException("Could not continue extraction confirmation handling.");
            }
            log(
                $"Extraction confirmation remained visible after click attempt {dismissal.Attempts}/{ConfirmationDismissalState.MaximumAttempts}.",
                MacroEventLevel.Warning,
                "extract_confirm",
                scores["extract_confirm"]);
        }

        throw new InvalidOperationException(
            $"The Extraction confirmation remained visible after {ConfirmationDismissalState.MaximumAttempts} focused click attempts. " +
            "Roblox did not acknowledge the button; retry after the client is responsive.");
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
        DiscordRunReporter reporter,
        bool notify,
        Stopwatch runtime,
        int victories,
        int defeats,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        string state = initialState;
        log($"Automatic recovery started from {Label(state)}. Target: map {preset.MapNumber}, difficulty {preset.Difficulty}.", MacroEventLevel.Warning, state, null);
        if (notify)
        {
            ImageFrame? screenshot = TryCaptureClient(window, detector);
            reporter.Queue(
                "recovery",
                $"Automatic rejoin was needed after {Label(initialState)} was recognized.",
                screenshot,
                runtime.Elapsed,
                victories,
                defeats,
                new DiscordRunTarget(preset.MapNumber, preset.Difficulty, string.Empty));
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
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(detector.RecoveryState(candidate), "lobby", StringComparison.OrdinalIgnoreCase),
                        candidate => string.Equals(detector.RecoveryState(candidate), "play", StringComparison.OrdinalIgnoreCase),
                        (key, token) => _automation.TapLetterKeyAsync(window, key, token),
                        async (timeout, token) => string.Equals(
                            await WaitForRecoveryChangeAsync(window, detector, "lobby", timeout, preset, report, log, token).ConfigureAwait(false),
                            "play",
                            StringComparison.OrdinalIgnoreCase),
                        attempt => report("Recovery", 0, $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", state, null),
                        attempt => log($"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", MacroEventLevel.Warning, state, null),
                        cancellationToken).ConfigureAwait(false);
                    state = "play";
                    break;
                case "play":
                    report("Recovery", 0, "Play screen recognized. Opening Expeditions.", state, null);
                    if (!await TryClickRecoveryAsync(window, detector, "play", log, cancellationToken).ConfigureAwait(false)) break;
                    state = await WaitForRecoveryChangeAsync(window, detector, "play", TimeSpan.FromSeconds(15), preset, report, log, cancellationToken).ConfigureAwait(false) ?? state;
                    break;
                case "post_match_party":
                    report(
                        "Recovery",
                        0,
                        "A previous party is still open. Returning to the shared game-mode selector.",
                        state,
                        null);
                    await CompleteGameModeHandoffAsync(
                        window,
                        detector,
                        preset,
                        playMenuKey,
                        state,
                        pressPlayFirst: false,
                        report,
                        log,
                        cancellationToken).ConfigureAwait(false);
                    state = "play";
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
        DateTimeOffset deadline =
            DateTimeOffset.UtcNow + GameModeHandoffTimeout;
        StableStateTracker<string> tracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        while (DateTimeOffset.UtcNow < deadline)
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
        throw new TimeoutException(
            $"Timed out waiting for {Label(desired)}.");
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
        if (state is not null &&
            (allowNavigationEntry ||
                ExpeditionRunPolicy.CanEnterRecoveryDuringRun(state)))
        {
            return state;
        }
        if (!allowNavigationEntry) return null;
        ChallengeScreenState navigation =
            ChallengeScreenDetector.Detect(frame).State;
        return navigation is
            ChallengeScreenState.Victory or
            ChallengeScreenState.Defeat or
            ChallengeScreenState.PostMatchPreview or
            ChallengeScreenState.PostMatchHud
            ? "post_match_party"
            : null;
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

    private static string Label(string value) => value.Equals("afk", StringComparison.OrdinalIgnoreCase)
        ? "AFK Chamber"
        : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' '));

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
    }

    private static char ValidatePlayMenuKey(char value)
    {
        char normalized = char.ToUpperInvariant(value);
        if (!char.IsAsciiLetter(normalized))
        {
            throw new InvalidDataException(
                "Set the Play menu key under Settings > Controls so it matches Anime Expeditions' Toggle Play Menu binding.");
        }

        return normalized;
    }

    private static void ValidateTeamKey(bool required, char? value)
    {
        if (!required) return;
        if (value is null || !char.IsAsciiLetter(value.Value))
        {
            throw new InvalidDataException(
                "Set the Unit menu key under Settings > Controls so it matches Anime Expeditions' Units binding before using a saved team.");
        }
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
