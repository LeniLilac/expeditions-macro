using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private readonly IRobloxAutomation _automation;
    private readonly FastNoAlignPreparationSession
        _fastNoAlign;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;
    private readonly MatchLobbyNavigator _lobby;
    private readonly ManualInputRouteService? _manualInputs;

    public EventMacroRunner(
        IRobloxAutomation automation,
        PlacementService placements,
        TeamSelectionService teams,
        IDiscordNotifier discord,
        FastNoAlignPreparationSession fastNoAlign,
        ManualInputRouteService? manualInputs = null)
    {
        _automation = automation;
        _placements = placements;
        _teams = teams;
        _discord = discord;
        _fastNoAlign = fastNoAlign;
        _manualInputs = manualInputs;
        _lobby = new MatchLobbyNavigator(automation);
    }

    public async Task<StageRunResult> RunAsync(
        EventPreset preset,
        PlacementModel placement,
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
            AppSettings.DefaultCancelPlacementKeyChar)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(detector);
        preset.Validate();
        placement.ValidateCompatibility(
            CameraPreparationMode.FastNoAlign,
            PlacementTarget.ForEvent(preset));
        if (!char.IsAsciiLetter(playMenuKey))
        {
            throw new InvalidDataException(
                AppSettings.PlayMenuKeySetupInstructions);
        }
        if (preset.TeamSlot > 0 &&
            unitMenuKey is null)
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Unit Inventory key to match Anime Expeditions before using an Event route that changes teams.");
        }
        ManualInputRecording? manualRecording =
            await ManualInputMatchPlayback.ResolveAsync(
                    _manualInputs,
                    placement,
                    cancellationToken)
                .ConfigureAwait(false);

        RobloxWindow window =
            _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        Focus(window);
        await EnsureClientSizeAsync(
            window,
            detector.Manifest.ClientWidth,
            detector.Manifest.ClientHeight,
            cancellationToken).ConfigureAwait(false);

        Stopwatch totalRuntime = Stopwatch.StartNew();
        int attempts = 0;
        int victories = 0;
        int defeats = 0;
        TimeSpan matchRuntimeTotal = TimeSpan.Zero;
        StageRunResult? last = null;
        RepeatedRoutePreparationState preparation =
            new(preset.TeamSlot);
        DiscordRunTarget reportTarget =
            new(0, 0, RouteLabel(preset));
        DiscordRunReporter reporter = new(
            _discord,
            webhookUrl,
            "Event Macro",
            "event",
            Write,
            macroTotals);

        Write(
            $"Using Roblox window '{window.Title}' ({window.ProcessDescription}).");
        await reporter.SendAsync(
            "started",
            $"{RouteLabel(preset)} is starting.",
            TryCaptureClient(window, detector),
            totalRuntime.Elapsed,
            victories,
            defeats,
            reportTarget,
            cancellationToken).ConfigureAwait(false);

        while (attempts <= preset.DefeatRetries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool repeatedPrestart =
                preparation.ConfirmRepeatStagePrestart();
            if (!repeatedPrestart)
            {
                attempts++;
                Report(
                    "Navigation",
                    8,
                    $"Opening {RouteLabel(preset)} (attempt {attempts}/{preset.DefeatRetries + 1}).");
                await NavigateToPrestartAsync(
                    window,
                    preset,
                    detector,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                attempts = 1;
                Report(
                    "Navigation",
                    8,
                    $"Repeat Stage returned to {RouteLabel(preset)} prestart.");
            }

            await PrepareMatchAsync(
                window,
                preset,
                placement,
                unitMenuKey,
                preparation,
                repeatedPrestart,
                detector,
                progress,
                cancellationToken).ConfigureAwait(false);

            PlacementMatchExecutionPlan execution =
                PlacementExecutionPlan.ForMatch(
                    placement);
            if (execution.BeforeStart.Count > 0)
            {
                Report(
                    "Placement",
                    45,
                    "Placing before-start units.");
                await PlayPlacementAsync(
                    window,
                    preset,
                    placement,
                    execution.BeforeStart,
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
            }

            StableScreenAction<EventScreenMatch>? liveStart =
                await StableScreenActionWaiter.WaitAsync(
                        EventScreenState.Prestart,
                        preset.StableDetections,
                        () => EventScreenDetector.Detect(
                            CaptureClient(window, detector)),
                        static match => match.State,
                        static match =>
                            match.ActionX is int x &&
                            match.ActionY is int y
                                ? (x, y)
                                : null,
                        TimeSpan.FromSeconds(12),
                        TimeSpan.FromMilliseconds(
                            Math.Max(
                                200,
                                preset.PollMilliseconds)),
                        cancellationToken)
                    .ConfigureAwait(false);
            if (liveStart is null)
            {
                throw new RobloxUiUnavailableException(
                    "The Event Start Game button disappeared before it could be clicked.");
            }

            Stopwatch matchRuntime;
            if (execution.ManualPlayback)
            {
                if (_manualInputs is null ||
                    manualRecording is null)
                {
                    throw new InvalidOperationException(
                        "Manual input playback is unavailable.");
                }
                matchRuntime =
                    await ManualInputMatchPlayback.PlayAsync(
                        _manualInputs,
                        window,
                        manualRecording,
                        progress,
                        matchStarting: null,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                matchRuntime = Stopwatch.StartNew();
                await ClickAsync(
                    window,
                    liveStart.Value.X,
                    liveStart.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(
                    1800,
                    cancellationToken).ConfigureAwait(false);
            }
            EventTerminalObservation terminal =
                await RunMatchAsync(
                    window,
                    preset,
                    placement,
                    detector,
                    matchRuntime,
                    cancelPlacementKey,
                    execution.ManualPlayback,
                    cancellationToken).ConfigureAwait(false);
            StageRunOutcome outcome =
                terminal.State ==
                    EventScreenState.Victory
                    ? StageRunOutcome.Victory
                    : StageRunOutcome.Defeat;
            matchRuntimeTotal += matchRuntime.Elapsed;
            if (outcome == StageRunOutcome.Victory)
            {
                victories++;
            }
            else
            {
                defeats++;
            }
            last = new StageRunResult(
                outcome,
                matchRuntimeTotal,
                attempts,
                victories,
                defeats,
                terminal.Frame);
            Write(
                $"{RouteLabel(preset)} ended in {outcome}.",
                outcome == StageRunOutcome.Victory
                    ? MacroEventLevel.Success
                    : MacroEventLevel.Warning,
                outcome.ToString().ToLowerInvariant());
            await reporter.SendAsync(
                outcome == StageRunOutcome.Victory
                    ? "victory"
                    : "defeat",
                $"{RouteLabel(preset)} ended in {outcome}.",
                terminal.Frame,
                totalRuntime.Elapsed,
                victories,
                defeats,
                reportTarget,
                cancellationToken,
                matchRuntime.Elapsed).ConfigureAwait(false);

            if (continueScheduledRoute is not null)
            {
                bool repeated =
                    await CompleteScheduledHandoffAsync(
                        window,
                        terminal,
                        preparation,
                        detector,
                        playMenuKey,
                        preset.StableDetections,
                        outcome,
                        matchRuntime.Elapsed,
                        continueScheduledRoute,
                        Report,
                        Write,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (repeated)
                {
                    attempts = 0;
                    continue;
                }
                return last;
            }

            await OpenGameModeSelectorAsync(
                window,
                detector,
                playMenuKey,
                cancellationToken).ConfigureAwait(false);
            if (outcome == StageRunOutcome.Victory ||
                attempts > preset.DefeatRetries)
            {
                return last;
            }
        }

        return last ??
            throw new InvalidOperationException(
                "The Event run ended without a terminal result.");

        void Write(
            string message,
            MacroEventLevel level =
                MacroEventLevel.Information,
            string? state = null,
            double? confidence = null) =>
            log?.Invoke(
                new MacroEvent(
                    DateTimeOffset.Now,
                    level,
                    message,
                    state,
                    confidence));

        void Report(
            string phase,
            int percent,
            string message,
            string? state = null,
            double? confidence = null) =>
            progress?.Report(
                new MacroProgress(
                    phase,
                    percent,
                    message,
                    state,
                    confidence));
    }

    private static string RouteLabel(
        EventPreset preset)
    {
        if (preset.Act != EventAct.Act1)
        {
            return $"Villain Invasion - Act {(int)preset.Act}";
        }

        string angle =
            preset.SpawnRoute == EventSpawnRoute.Angle2
                ? "Angle 2"
                : "Angle 1";
        return $"Villain Invasion - Act 1 - {angle}";
    }
}
