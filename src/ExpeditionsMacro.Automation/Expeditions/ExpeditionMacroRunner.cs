using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner : IGameModeWorkflow
{
    private readonly IRobloxAutomation _automation;
    private readonly FastNoAlignPreparationSession _fastNoAlign;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;
    private readonly ManualInputRouteService? _manualInputs;

    public ExpeditionMacroRunner(
        IRobloxAutomation automation,
        PlacementService placements,
        TeamSelectionService teams,
        IDiscordNotifier discord,
        FastNoAlignPreparationSession? fastNoAlign = null,
        ManualInputRouteService? manualInputs = null)
    {
        _automation = automation;
        _fastNoAlign = fastNoAlign ??
            new FastNoAlignPreparationSession(
                new CameraPosePreparationService(automation));
        _placements = placements;
        _teams = teams;
        _discord = discord;
        _manualInputs = manualInputs;
    }

    public string GameId => "anime-expeditions";

    public string ModeId => "expeditions";

    public async Task RunAsync(
        ExpeditionPreset preset,
        PlacementModel placementModel,
        IDetectorPack detector,
        string webhookUrl,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ExpeditionRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default,
        DateTimeOffset? stopAfterCurrentRunUtc = null,
        int? maximumRuns = null,
        char? unitMenuKey = null,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>>?
            continueScheduledRoute = null,
        MacroRunTotals? macroTotals = null,
        char cancelPlacementKey =
            AppSettings.DefaultCancelPlacementKeyChar)
    {
        if (maximumRuns is < 1) throw new ArgumentOutOfRangeException(nameof(maximumRuns));
        preset.Validate();
        CameraPreparationExecutionPolicy.ValidateForExecution(
            preset.CameraPreparationMode,
            "The selected Expedition preset");
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        placementModel.Validate();
        ValidateCompatibility(
            preset,
            placementModel,
            detector.Manifest);
        ManualInputRecording? manualRecording =
            await ManualInputMatchPlayback.ResolveAsync(
                    _manualInputs,
                    placementModel,
                    cancellationToken)
                .ConfigureAwait(false);
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
        RepeatedRoutePreparationState preparation = new(preset.TeamSlot);

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void PublishSummary() => summaryChanged?.Invoke(new ExpeditionRunSummary(startedAt, runtime.Elapsed, repeats, victories, defeats, recoveries, bossesSeen));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));
        DiscordRunTarget reportTarget = new(preset.MapNumber, preset.Difficulty, string.Empty);
        DiscordRunReporter reporter = new(_discord, webhookUrl, "Expeditions Macro", "expeditions", Write, macroTotals);

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
                    bool arrivedFromRepeatStage =
                        preparation
                            .ConfirmRepeatStagePrestart();
                    bool requirePrestart =
                        ManualPlaybackStartPolicy
                            .RequiresPrestart(
                                placementModel,
                                arrivedFromRepeatStage);
                    if (requirePrestart)
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
                    }
                    await PrepareMatchAsync(
                        window,
                        preset,
                        unitMenuKey,
                        preparation,
                        arrivedFromRepeatStage,
                        progress,
                        Write,
                        cancellationToken).ConfigureAwait(false);
                    await ThrowIfRecoveryAsync(window, detector, preset, cancellationToken).ConfigureAwait(false);
                    if (ExpeditionRunPolicy.StopDeadlineReached(DateTimeOffset.UtcNow, stopAfterCurrentRunUtc))
                    {
                        Write("Challenge reset reached during Expedition preparation. Returning before starting the node.", MacroEventLevel.Success);
                        return;
                    }

                    ExpeditionMatchStart? started =
                        await BeginConfiguredMatchAsync(
                                window,
                                 preset,
                                 placementModel,
                                 manualRecording,
                                 detector,
                                progress,
                                stopAfterCurrentRunUtc,
                                Report,
                                Write,
                                cancelPlacementKey,
                                cancellationToken)
                            .ConfigureAwait(false);
                    if (started is null)
                    {
                        return;
                    }
                    RunTerminal terminal = await MonitorUntilRunEndAsync(
                        window,
                        preset,
                        placementModel,
                        started.BeforeStart,
                        started.AfterStart,
                        detector,
                        started.Runtime,
                        value => { bossesSeen = value; PublishSummary(); },
                        Report,
                        Write,
                        cancelPlacementKey,
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
                        started.Runtime.Elapsed);
                    if (continueScheduledRoute is not null)
                    {
                        bool repeated =
                            await CompleteScheduledHandoffAsync(
                                window,
                                detector,
                                terminal,
                                preset,
                                placementModel,
                                playMenuKey,
                                preparation,
                                started.Runtime.Elapsed,
                                continueScheduledRoute,
                                Report,
                                Write,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (repeated)
                        {
                            continue;
                        }
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
                    preparation.MarkRepeatStageRequested();
                    if (ManualPlaybackStartPolicy
                        .RequiresPrestart(
                            placementModel,
                            arrivedFromRepeatStage: true))
                    {
                        await Task.Delay(4500, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (RecoveryNeededException recovery)
                {
                    if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(recovery.State)} was recognized, but automatic recovery is disabled.", recovery);
                    preparation.Invalidate();
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
                    preparation.Invalidate();
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

        throw new RobloxUiUnavailableException(
            $"The Continue Expedition confirmation remained visible after {ConfirmationDismissalState.MaximumAttempts} focused click attempts. " +
            "Roblox did not acknowledge the button; retry after the client is responsive.");
    }

    private ImageFrame CaptureClient(RobloxWindow window, IDetectorPack detector)
    {
        Focus(window);
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width != detector.Manifest.ClientWidth || bounds.Height != detector.Manifest.ClientHeight) throw new RobloxSessionUnavailableException("Roblox no longer matches the detector pack client size.");
        return _automation.CaptureClient(window);
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
            throw new RobloxSessionUnavailableException($"Roblox did not accept the required {width} × {height} client size (actual: {actual.Width} × {actual.Height}).");
        }
    }

}
