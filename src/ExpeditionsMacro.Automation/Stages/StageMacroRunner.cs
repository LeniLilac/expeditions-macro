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
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private readonly IRobloxAutomation _automation;
    private readonly FastNoAlignPreparationSession _fastNoAlign;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;
    private readonly ManualInputRouteService? _manualInputs;

    public StageMacroRunner(
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

    private async Task<StageRunResult> RunAsync(
        StageMode mode,
        object preset,
        StageRuntimeModels models,
        IDetectorPack detector,
        string webhookUrl,
        char playMenuKey,
        char? unitMenuKey,
        IProgress<MacroProgress>? progress,
        Action<MacroEvent>? log,
        CancellationToken cancellationToken,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>>?
            continueScheduledRoute,
        MacroRunTotals? macroTotals,
        char cancelPlacementKey)
    {
        StoryPreset? story = preset as StoryPreset;
        RaidPreset? raid = preset as RaidPreset;
        if ((mode == StageMode.Story) != (story is not null) || (mode == StageMode.Raid) != (raid is not null))
        {
            throw new ArgumentException("The stage preset does not match the requested mode.", nameof(preset));
        }

        story?.Validate(requireModels: true);
        raid?.Validate(requireModels: true);
        CameraPreparationMode cameraMode =
            story?.CameraPreparationMode ??
            raid!.CameraPreparationMode;
        CameraPreparationExecutionPolicy.ValidateForExecution(
            cameraMode,
            $"The selected {Label(mode)} preset");
        ManualInputRecording? manualRecording =
            await ValidateAndResolveManualRecordingAsync(
                    mode,
                    story,
                    raid,
                    cameraMode,
                    models,
                    detector.Manifest,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!char.IsAsciiLetter(playMenuKey)) throw new InvalidDataException(AppSettings.PlayMenuKeySetupInstructions);

        int teamSlot = story?.TeamSlot ?? raid!.TeamSlot;
        if (teamSlot > 0 && unitMenuKey is null)
        {
            throw new InvalidDataException("Scroll down to Controls on the Dashboard, then set Toggle Unit Inventory key to match Anime Expeditions before using a preset that changes teams.");
        }

        int retries = story?.DefeatRetries ?? raid!.DefeatRetries;
        bool autoRecover = story?.AutoRecover ?? raid!.AutoRecover;
        int stableDetections = Math.Max(2, story?.StableDetections ?? raid!.StableDetections);
        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        Stopwatch totalRuntime = Stopwatch.StartNew();
        int attempts = 0;
        int victories = 0;
        int defeats = 0;
        TimeSpan matchRuntimeTotal = TimeSpan.Zero;
        StageRunResult? last = null;
        RepeatedRoutePreparationState preparation = new(teamSlot);

        Write($"Using Roblox window '{window.Title}' ({window.ProcessDescription}).");
        Focus(window);
        await EnsureClientSizeAsync(window, detector.Manifest.ClientWidth, detector.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
        string route = RouteLabel(mode, story, raid);
        DiscordRunTarget reportTarget = new(0, 0, route);
        DiscordRunReporter reporter = new(_discord, webhookUrl, $"{Label(mode)} Macro", mode.ToString().ToLowerInvariant(), Write, macroTotals);
        await reporter.SendAsync(
            "started",
            $"{route} is starting.",
            TryCaptureClient(window, detector),
            totalRuntime.Elapsed,
            victories,
            defeats,
            reportTarget,
            cancellationToken).ConfigureAwait(false);

        while (attempts <= retries)
        {
            bool matchCompleted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool repeatedPrestartReady =
                    preparation.ConfirmRepeatStagePrestart();
                if (repeatedPrestartReady)
                {
                    attempts = 1;
                    Report("Navigation", 8, $"Repeat Stage returned to {RouteLabel(mode, story, raid)} prestart.");
                }
                else
                {
                    bool recoveredBeforeNavigation = await EnsureGameModeSelectorAsync(
                        window,
                        mode,
                        playMenuKey,
                        detector,
                        autoRecover,
                        stableDetections,
                        Report,
                        Write,
                        cancellationToken).ConfigureAwait(false);
                    if (recoveredBeforeNavigation) preparation.Invalidate();

                    attempts++;
                    Report("Navigation", 8, $"Opening {RouteLabel(mode, story, raid)} (attempt {attempts}/{retries + 1}).");
                    await NavigateToPrestartAsync(window, mode, story, raid, playMenuKey, detector, stableDetections, cancellationToken).ConfigureAwait(false);
                }

                await PrepareMatchAsync(
                    window,
                    mode,
                    story,
                    raid,
                    models,
                    unitMenuKey,
                    preparation,
                    repeatedPrestartReady,
                    progress,
                    detector,
                    stableDetections,
                    Report,
                    Write,
                    cancellationToken).ConfigureAwait(false);

                (Stopwatch matchRuntime, bool manualPlayback) =
                    await BeginConfiguredMatchAsync(
                            window,
                            mode,
                            models,
                            story,
                            raid,
                            detector,
                            stableDetections,
                            manualRecording,
                            progress,
                            Report,
                            cancelPlacementKey,
                            cancellationToken)
                        .ConfigureAwait(false);

                TerminalObservation terminal =
                    await RunConfiguredMatchAsync(
                        window,
                        models,
                        story,
                        raid,
                        detector,
                        matchRuntime,
                        stableDetections,
                        cancelPlacementKey,
                        manualPlayback,
                        cancellationToken).ConfigureAwait(false);
                StageRunOutcome outcome = terminal.State == StageScreenState.Victory ? StageRunOutcome.Victory : StageRunOutcome.Defeat;
                matchCompleted = true;
                matchRuntimeTotal += matchRuntime.Elapsed;
                if (outcome == StageRunOutcome.Victory) victories++;
                else defeats++;
                last = new StageRunResult(outcome, matchRuntimeTotal, attempts, victories, defeats, terminal.Frame);
                Write($"{RouteLabel(mode, story, raid)} ended in {outcome}.", outcome == StageRunOutcome.Victory ? MacroEventLevel.Success : MacroEventLevel.Warning, outcome.ToString().ToLowerInvariant());
                await reporter.SendAsync(
                    outcome == StageRunOutcome.Victory ? "victory" : "defeat",
                    $"{route} ended in {outcome}.",
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
                            mode,
                            terminal,
                            preparation,
                            models.Placement,
                            detector,
                            playMenuKey,
                            autoRecover,
                            stableDetections,
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

                bool recoveredAfterResult = await EnsureGameModeSelectorAsync(
                    window,
                    mode,
                    playMenuKey,
                    detector,
                    autoRecover,
                    stableDetections,
                    Report,
                    Write,
                    cancellationToken).ConfigureAwait(false);
                if (recoveredAfterResult) preparation.Invalidate();
                if (outcome == StageRunOutcome.Victory || attempts > retries) return last;
                Write($"Retrying after defeat ({attempts}/{retries + 1}).", MacroEventLevel.Warning);
            }
            catch (StageRecoveryException recovery)
            {
                if (!autoRecover)
                {
                    throw new InvalidOperationException($"{RecoveryLabel(recovery.State)} was recognized, but automatic recovery is disabled.", recovery);
                }

                if (!matchCompleted) attempts = Math.Max(0, attempts - 1);
                preparation.Invalidate();
                string detail = $"{RouteLabel(mode, story, raid)} was interrupted by {RecoveryLabel(recovery.State)}. Returning through automatic recovery.";
                Write(detail, MacroEventLevel.Warning, recovery.State, null);
                Report("Recovery", 0, detail, recovery.State, null);
                await reporter.SendAsync(
                    "recovery",
                    detail,
                    TryCaptureClient(window, detector),
                    totalRuntime.Elapsed,
                    victories,
                    defeats,
                    reportTarget,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return last ?? throw new InvalidOperationException("The stage run ended without a terminal result.");

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));
    }

    private async Task ClickAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.ClickClientAsync(window, x, y, cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame CaptureClient(RobloxWindow window, IDetectorPack detector)
    {
        Focus(window);
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width != detector.Manifest.ClientWidth || bounds.Height != detector.Manifest.ClientHeight)
        {
            throw new RobloxSessionUnavailableException("Roblox no longer matches the detector pack client size.");
        }
        return _automation.CaptureClient(window);
    }

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new RobloxSessionUnavailableException("Windows could not focus Roblox.");
    }

    private static string Label(StageMode mode) => mode == StageMode.Story ? "Story" : "Raid";

    private static bool IsRootRecovery(string? state) => state is "afk" or "disconnect" or "lobby";

    private static string RecoveryLabel(string state) => state switch
    {
        "afk" => "the AFK Chamber",
        "disconnect" => "a Roblox disconnect",
        "lobby" => "the lobby",
        "play" => "the Play menu",
        _ => state,
    };

    private static string RouteLabel(StageMode mode, StoryPreset? story, RaidPreset? raid)
    {
        if (mode == StageMode.Raid) return $"Spirit City - Act {(int)raid!.Act}";
        string run = story!.RunKind switch
        {
            StoryRunKind.Act => $"Act {story.ActNumber} ({(story.HardMode ? "Hard" : "Normal")})",
            StoryRunKind.Infinite => "Infinite",
            StoryRunKind.Mastery => "Mastery",
            _ => story.RunKind.ToString(),
        };
        return $"{MapLabel(story.Map)} - {run}";
    }

    private static string MapLabel(ChallengeMapId map) => map switch
    {
        ChallengeMapId.SchoolGrounds => "School Grounds",
        ChallengeMapId.FlowerForest => "Flower Forest",
        ChallengeMapId.RoseKingdom => "Rose Kingdom",
        ChallengeMapId.FairyKingForest => "Fairy King Forest",
        ChallengeMapId.KingsTomb => "King's Tomb",
        _ => map.ToString(),
    };

    private sealed record TerminalObservation(StageScreenState State, double Confidence, ImageFrame Frame);

    private sealed class StageRecoveryException : Exception
    {
        public StageRecoveryException(string state) : base($"Stage recovery screen recognized: {state}.") => State = state;

        public string State { get; }
    }
}
