using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed class StageMacroRunner
{
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromHours(12);

    private readonly IRobloxAutomation _automation;
    private readonly CameraAlignmentEngine _camera;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;

    internal enum GameModeHandoffCommand
    {
        Complete,
        ChangeGamemode,
        Back,
        PressPlayKey,
    }

    public StageMacroRunner(
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
        Func<int, int, TimeSpan, CancellationToken, Task<bool>>? continueScheduledRoute = null) =>
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
            continueScheduledRoute);

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
        Func<int, int, TimeSpan, CancellationToken, Task<bool>>? continueScheduledRoute = null) =>
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
            continueScheduledRoute);

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
        Func<int, int, TimeSpan, CancellationToken, Task<bool>>? continueScheduledRoute)
    {
        StoryPreset? story = preset as StoryPreset;
        RaidPreset? raid = preset as RaidPreset;
        if ((mode == StageMode.Story) != (story is not null) || (mode == StageMode.Raid) != (raid is not null))
        {
            throw new ArgumentException("The stage preset does not match the requested mode.", nameof(preset));
        }

        story?.Validate(requireModels: true);
        raid?.Validate(requireModels: true);
        models.Camera.Manifest.Validate();
        models.PrestartPlacement?.Validate();
        models.DelayedPlacement?.Validate();
        ValidateCompatibility(models, detector.Manifest);
        if (!char.IsAsciiLetter(playMenuKey)) throw new InvalidDataException(AppSettings.PlayMenuKeySetupInstructions);

        int teamSlot = story?.TeamSlot ?? raid!.TeamSlot;
        if (teamSlot > 0 && unitMenuKey is null)
        {
            throw new InvalidDataException("Set the Unit menu key under Settings > Controls before using a preset that changes teams.");
        }

        int retries = story?.DefeatRetries ?? raid!.DefeatRetries;
        bool autoRecover = story?.AutoRecover ?? raid!.AutoRecover;
        int stableDetections = Math.Max(2, story?.StableDetections ?? raid!.StableDetections);
        RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        Stopwatch totalRuntime = Stopwatch.StartNew();
        int attempts = 0;
        int victories = 0;
        int defeats = 0;
        TimeSpan matchRuntimeTotal = TimeSpan.Zero;
        StageRunResult? last = null;
        bool teamLoaded = teamSlot == 0;
        bool repeatedPrestartReady = false;

        Write($"Using Roblox window '{window.Title}' ({window.ProcessDescription}).");
        Focus(window);
        await EnsureClientSizeAsync(window, detector.Manifest.ClientWidth, detector.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);

        while (attempts <= retries)
        {
            bool matchCompleted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (repeatedPrestartReady)
                {
                    repeatedPrestartReady = false;
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
                    if (recoveredBeforeNavigation) teamLoaded = teamSlot == 0;

                    attempts++;
                    Report("Navigation", 8, $"Opening {RouteLabel(mode, story, raid)} (attempt {attempts}/{retries + 1}).");
                    await NavigateToPrestartAsync(window, mode, story, raid, playMenuKey, detector, stableDetections, cancellationToken).ConfigureAwait(false);
                }

                if (!teamLoaded)
                {
                    Report("Team", 14, $"Prestart recognized. Loading Team {teamSlot}.");
                    RequirePrestartForTeamLoad(StageScreenDetector.Detect(CaptureClient(window, detector)));
                    await _teams.SelectAsync(window, teamSlot, unitMenuKey!.Value, progress, cancellationToken).ConfigureAwait(false);
                    await WaitForStateAsync(
                        window,
                        StageScreenState.Prestart,
                        NavigationTimeout,
                        detector,
                        stableDetections,
                        cancellationToken).ConfigureAwait(false);
                    teamLoaded = true;
                    Write($"Team {teamSlot} loaded from the confirmed {Label(mode)} prestart screen.", MacroEventLevel.Success);
                }

                int zoomTicks = story?.ZoomTicks ?? raid!.ZoomTicks;
                int pitchDragPixels = story?.PitchDragPixels ?? raid!.PitchDragPixels;
                Report("Camera", 20, "Preparing and aligning the camera.");
                double confidence = await _camera.PrepareAndAlignAsync(
                    models.Camera,
                    window,
                    zoomTicks,
                    pitchDragPixels,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                Write($"Camera alignment finished at {confidence:P0} confidence.", MacroEventLevel.Success, "camera", confidence);

                if (models.PrestartPlacement is not null)
                {
                    Report("Placement", 45, "Placing before-start units.");
                    await PlayPlacementAsync(window, models.PrestartPlacement, story, raid, cancellationToken).ConfigureAwait(false);
                }

                ImageFrame prestart = CaptureClient(window, detector);
                StageScreenMatch prestartMatch = StageScreenDetector.Detect(prestart);
                if (prestartMatch.State != StageScreenState.Prestart || prestartMatch.ActionX is null || prestartMatch.ActionY is null)
                {
                    throw new InvalidOperationException($"The {Label(mode)} Start Game button disappeared before it could be clicked.");
                }
                await ClickAsync(window, prestartMatch.ActionX.Value, prestartMatch.ActionY.Value, cancellationToken).ConfigureAwait(false);
                await Task.Delay(1800, cancellationToken).ConfigureAwait(false);

                Stopwatch matchRuntime = Stopwatch.StartNew();
                TerminalObservation terminal = await RunMatchAsync(window, models.DelayedPlacement, story, raid, detector, matchRuntime, stableDetections, cancellationToken).ConfigureAwait(false);
                StageRunOutcome outcome = terminal.State == StageScreenState.Victory ? StageRunOutcome.Victory : StageRunOutcome.Defeat;
                matchCompleted = true;
                matchRuntimeTotal += matchRuntime.Elapsed;
                if (outcome == StageRunOutcome.Victory) victories++;
                else defeats++;
                last = new StageRunResult(outcome, matchRuntimeTotal, attempts, victories, defeats, terminal.Frame);
                Write($"{RouteLabel(mode, story, raid)} ended in {outcome}.", outcome == StageRunOutcome.Victory ? MacroEventLevel.Success : MacroEventLevel.Warning, outcome.ToString().ToLowerInvariant());
                await TryNotifyAsync(webhookUrl, mode, story, raid, outcome, terminal.Frame, totalRuntime.Elapsed, victories, defeats, Write, cancellationToken).ConfigureAwait(false);

                if (continueScheduledRoute is not null)
                {
                    bool repeatSameRoute = await continueScheduledRoute(
                        outcome == StageRunOutcome.Victory ? 1 : 0,
                        outcome == StageRunOutcome.Defeat ? 1 : 0,
                        matchRuntime.Elapsed,
                        cancellationToken).ConfigureAwait(false);
                    if (repeatSameRoute)
                    {
                        (int X, int Y)? repeat = StageScreenDetector.RepeatStageAction(terminal.Frame, terminal.State);
                        if (repeat is null)
                        {
                            throw new InvalidOperationException($"The {Label(mode)} Repeat Stage button could not be located.");
                        }
                        Report("Handoff", 100, $"The same {Label(mode)} preset is next. Repeating the stage.", "repeat_stage", terminal.Confidence);
                        Write($"The scheduler kept the same {Label(mode)} route; using Repeat Stage instead of reopening Play.", MacroEventLevel.Success, "repeat_stage", terminal.Confidence);
                        await ClickAsync(window, repeat.Value.X, repeat.Value.Y, cancellationToken).ConfigureAwait(false);
                        await WaitForStateAsync(
                            window,
                            StageScreenState.Prestart,
                            TimeSpan.FromSeconds(45),
                            detector,
                            stableDetections,
                            cancellationToken).ConfigureAwait(false);
                        repeatedPrestartReady = true;
                        attempts = 0;
                        continue;
                    }
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
                if (recoveredAfterResult) teamLoaded = teamSlot == 0;
                if (continueScheduledRoute is not null) return last;
                if (outcome == StageRunOutcome.Victory || attempts > retries) return last;
                Write($"Retrying after defeat ({attempts}/{retries + 1}).", MacroEventLevel.Warning);
            }
            catch (CameraAlignmentException alignment)
            {
                Report(
                    "Handoff",
                    100,
                    $"Camera alignment failed. Returning to shared navigation before {Label(mode)} is skipped.",
                    "camera_alignment_skipped",
                    alignment.BestConfidence);
                try
                {
                    await EnsureGameModeSelectorAsync(
                        window,
                        mode,
                        playMenuKey,
                        detector,
                        autoRecover,
                        stableDetections,
                        Report,
                        Write,
                        cancellationToken).ConfigureAwait(false);
                    Write(
                        $"{Label(mode)} returned to the game-mode selector after camera alignment failed.",
                        MacroEventLevel.Success,
                        "game_mode_selector",
                        null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception handoffError)
                {
                    throw new InvalidOperationException(
                        $"{Label(mode)} camera alignment failed and shared navigation could not be restored, so the task scheduler was stopped.",
                        new AggregateException(alignment, handoffError));
                }
                throw;
            }
            catch (StageRecoveryException recovery)
            {
                if (!autoRecover)
                {
                    throw new InvalidOperationException($"{RecoveryLabel(recovery.State)} was recognized, but automatic recovery is disabled.", recovery);
                }

                if (!matchCompleted) attempts = Math.Max(0, attempts - 1);
                teamLoaded = teamSlot == 0;
                string detail = $"{RouteLabel(mode, story, raid)} was interrupted by {RecoveryLabel(recovery.State)}. Returning through automatic recovery.";
                Write(detail, MacroEventLevel.Warning, recovery.State, null);
                Report("Recovery", 0, detail, recovery.State, null);
                await TryNotifyRecoveryAsync(webhookUrl, mode, story, raid, detail, TryCaptureClient(window, detector), totalRuntime.Elapsed, Write, cancellationToken).ConfigureAwait(false);
            }
        }

        return last ?? throw new InvalidOperationException("The stage run ended without a terminal result.");

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));
    }

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
        (int tileX, int tileY) = StageScreenDetector.ModeTileAction(mode);
        await ClickAsync(window, tileX, tileY, cancellationToken).ConfigureAwait(false);

        if (mode == StageMode.Story)
        {
            await WaitForStateAsync(window, StageScreenState.StorySelector, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
            Focus(window);
            await _automation.ScrollClientAsync(window, 20, cancellationToken).ConfigureAwait(false);
            if (StageScreenDetector.StoryMapRequiresLaterScroll(story!.Map))
            {
                Focus(window);
                await _automation.ScrollClientAsync(window, -10, cancellationToken).ConfigureAwait(false);
            }
            (int mapX, int mapY) = StageScreenDetector.StoryMapAction(story.Map);
            await ClickAsync(window, mapX, mapY, cancellationToken).ConfigureAwait(false);
            await WaitForStateAsync(window, StageScreenState.StoryDetail, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
            (int runX, int runY) = StageScreenDetector.StoryRunAction(story.RunKind, story.ActNumber);
            await ClickAsync(window, runX, runY, cancellationToken).ConfigureAwait(false);
            if (story.RunKind == StoryRunKind.Act)
            {
                (int difficultyX, int difficultyY) = StageScreenDetector.StoryDifficultyAction(story.HardMode);
                await ClickAsync(window, difficultyX, difficultyY, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await WaitForStateAsync(window, StageScreenState.RaidSelector, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
            (int mapX, int mapY) = StageScreenDetector.RaidMapAction;
            await ClickAsync(window, mapX, mapY, cancellationToken).ConfigureAwait(false);
            await WaitForStateAsync(window, StageScreenState.RaidDetail, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
            (int actX, int actY) = StageScreenDetector.RaidActAction(raid!.Act);
            await ClickAsync(window, actX, actY, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        ImageFrame detail = CaptureClient(window, detector);
        StageScreenMatch detailMatch = StageScreenDetector.Detect(detail);
        if (detailMatch.State is not (StageScreenState.StoryDetail or StageScreenState.RaidDetail) || detailMatch.ActionX is null || detailMatch.ActionY is null)
        {
            throw new InvalidOperationException($"The {Label(mode)} Select Stage button could not be located.");
        }
        await ClickAsync(window, detailMatch.ActionX.Value, detailMatch.ActionY.Value, cancellationToken).ConfigureAwait(false);
        StageScreenMatch preview = await WaitForStateAsync(window, StageScreenState.PreviewReady, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
        (int previewX, int previewY) = StageScreenDetector.PreviewStartAction(CaptureClient(window, detector));
        await ClickAsync(window, preview.ActionX ?? previewX, preview.ActionY ?? previewY, cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(window, StageScreenState.Prestart, TimeSpan.FromSeconds(45), detector, stableDetections, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TerminalObservation> RunMatchAsync(
        RobloxWindow window,
        PlacementModel? delayedPlacement,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        CancellationToken cancellationToken)
    {
        int delaySeconds = story?.DelayedPlacementSeconds ?? raid!.DelayedPlacementSeconds;
        bool placed = delayedPlacement is null;
        StableStateTracker<string> terminalTracker = new(stableDetections);
        StableStateTracker<string> recoveryTracker = new(stableDetections);
        StableStateTracker<string> rewardTracker = new(stableDetections);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + MatchTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch state = StageScreenDetector.Detect(frame);
            string? terminalCandidate = state.State switch
            {
                StageScreenState.Victory => "victory",
                StageScreenState.Defeat => "defeat",
                _ => null,
            };
            if (terminalTracker.Update(terminalCandidate) is string terminal)
            {
                return new TerminalObservation(
                    terminal == "victory" ? StageScreenState.Victory : StageScreenState.Defeat,
                    state.Confidence,
                    frame.Clone());
            }

            string? recovery = detector.RecoveryState(frame);
            if (state.State == StageScreenState.GameModeSelector) recovery = "play";
            if (recoveryTracker.Update(IsRootRecovery(recovery) || recovery == "play" ? recovery : null) is string stableRecovery)
            {
                throw new StageRecoveryException(stableRecovery);
            }

            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? detectorState = detector.Classify(scores);
            string? reward = detectorState?.Equals("reward", StringComparison.OrdinalIgnoreCase) == true ? "reward" : null;
            if (rewardTracker.Update(reward) is not null)
            {
                (int rewardX, int rewardY) = detector.ActionFor("reward", frame);
                await ClickAsync(window, rewardX, rewardY, cancellationToken).ConfigureAwait(false);
                await Task.Delay(3600, cancellationToken).ConfigureAwait(false);
                rewardTracker.Reset();
                continue;
            }

            if (!placed && matchRuntime.Elapsed >= TimeSpan.FromSeconds(delaySeconds))
            {
                placed = true;
                await PlayPlacementAsync(window, delayedPlacement!, story, raid, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Math.Max(200, story?.PollMilliseconds ?? raid!.PollMilliseconds), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for the {RouteLabel(story is null ? StageMode.Raid : StageMode.Story, story, raid)} result.");
    }

    private Task PlayPlacementAsync(
        RobloxWindow window,
        PlacementModel model,
        StoryPreset? story,
        RaidPreset? raid,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            model,
            model.Steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            story?.UnitKeyHoldMilliseconds ?? raid!.UnitKeyHoldMilliseconds,
            story?.UnitSelectDelayMilliseconds ?? raid!.UnitSelectDelayMilliseconds,
            stepSent: null,
            status: null,
            cancellationToken);

    private async Task<bool> EnsureGameModeSelectorAsync(
        RobloxWindow window,
        StageMode mode,
        char playMenuKey,
        IDetectorPack detector,
        bool autoRecover,
        int stableDetections,
        Action<string, int, string, string?, double?>? report,
        Action<string, MacroEventLevel, string?, double?>? log,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + RecoveryTimeout;
        StableStateTracker<string> recoveryTracker = new(stableDetections);
        int playMenuAttempts = 0;
        string? lastRecovery = null;
        bool recovered = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current = StageScreenDetector.Detect(frame);
            if (current.State == StageScreenState.GameModeSelector) return recovered;

            string? recovery = detector.RecoveryState(frame);
            string? stableRecovery = recoveryTracker.Update(IsRootRecovery(recovery) ? recovery : null);
            if (stableRecovery is not null)
            {
                if (!autoRecover) throw new StageRecoveryException(stableRecovery);
                recovered = true;
                if (!string.Equals(lastRecovery, stableRecovery, StringComparison.OrdinalIgnoreCase))
                {
                    lastRecovery = stableRecovery;
                    log?.Invoke($"Automatic {Label(mode)} recovery started from {RecoveryLabel(stableRecovery)}.", MacroEventLevel.Warning, stableRecovery, null);
                }

                if (stableRecovery == "lobby")
                {
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(detector.RecoveryState(candidate), "lobby", StringComparison.OrdinalIgnoreCase),
                        candidate => StageScreenDetector.Detect(candidate).State == StageScreenState.GameModeSelector,
                        (key, token) => _automation.TapLetterKeyAsync(window, key, token),
                        (timeout, token) => TryWaitForStateAsync(window, StageScreenState.GameModeSelector, timeout, detector, stableDetections, token),
                        attempt => report?.Invoke("Recovery", 0, $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", stableRecovery, null),
                        attempt => log?.Invoke($"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", MacroEventLevel.Warning, stableRecovery, null),
                        cancellationToken).ConfigureAwait(false);
                    return recovered;
                }
                else
                {
                    report?.Invoke("Recovery", 0, stableRecovery == "disconnect"
                        ? "Disconnected. Rejoining Roblox."
                        : "AFK Chamber recognized. Returning to the lobby.", stableRecovery, null);
                    (int x, int y) = detector.ActionFor(stableRecovery, frame);
                    await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(stableRecovery == "disconnect" ? 5000 : 2200, cancellationToken).ConfigureAwait(false);
                }
                recoveryTracker.Reset();
                continue;
            }

            (int X, int Y)? changeMode = StageScreenDetector.PostMatchChangeModeAction(frame);
            switch (SelectGameModeHandoffCommand(current.State, changeMode is not null))
            {
                case GameModeHandoffCommand.Complete:
                    return recovered;
                case GameModeHandoffCommand.ChangeGamemode:
                    report?.Invoke("Handoff", 0, $"Leaving the completed {Label(mode)} party through Change Gamemode.", "stage_change_gamemode", current.Confidence);
                    await ClickAsync(window, changeMode!.Value.X, changeMode.Value.Y, cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    if (await TryWaitForStateAsync(window, StageScreenState.GameModeSelector, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false)) return recovered;
                    continue;
                case GameModeHandoffCommand.Back:
                    report?.Invoke("Handoff", 0, $"Leaving the nested {Label(mode)} interface through Back.", "stage_back", current.Confidence);
                    (int backX, int backY) = StageScreenDetector.SelectorBackAction;
                    await ClickAsync(window, backX, backY, cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    if (await TryWaitForStateAsync(window, StageScreenState.GameModeSelector, NavigationTimeout, detector, stableDetections, cancellationToken).ConfigureAwait(false)) return recovered;
                    continue;
                case GameModeHandoffCommand.PressPlayKey:
                    if (playMenuAttempts >= LobbyPlayNavigator.MaximumAttempts)
                    {
                        throw new PlayMenuBindingException(char.ToUpperInvariant(playMenuKey));
                    }
                    playMenuAttempts++;
                    report?.Invoke("Navigation", 0, playMenuAttempts == 1
                        ? $"Opening the Play menu with {playMenuKey}."
                        : $"Retrying the {playMenuKey} Play-menu key ({playMenuAttempts}/{LobbyPlayNavigator.MaximumAttempts}).", "play_menu_key", null);
                    Focus(window);
                    // Anime Expeditions accepts the Play binding while a terminal is still open and
                    // transitions through the post-match party before exposing Change Gamemode.
                    await _automation.TapLetterKeyAsync(window, playMenuKey, cancellationToken).ConfigureAwait(false);
                    GameModeHandoffCommand? transition = await TryWaitForPlayKeyTransitionAsync(
                        window,
                        detector,
                        stableDetections,
                        TimeSpan.FromSeconds(4),
                        cancellationToken).ConfigureAwait(false);
                    if (transition == GameModeHandoffCommand.Complete) return recovered;
                    if (transition == GameModeHandoffCommand.ChangeGamemode) playMenuAttempts = 0;
                    continue;
                default:
                    throw new InvalidOperationException("The stage handoff policy returned an unknown command.");
            }
        }

        StageScreenMatch last = StageScreenDetector.Detect(CaptureClient(window, detector));
        throw new TimeoutException($"Timed out opening the Play menu. Last detected state: {last.State} ({last.Confidence:P0}).");
    }

    internal static GameModeHandoffCommand SelectGameModeHandoffCommand(
        StageScreenState state,
        bool hasStageChangeModeAction) => state switch
        {
            StageScreenState.GameModeSelector => GameModeHandoffCommand.Complete,
            StageScreenState.Victory or StageScreenState.Defeat => GameModeHandoffCommand.PressPlayKey,
            StageScreenState.PostMatchPreview when hasStageChangeModeAction => GameModeHandoffCommand.ChangeGamemode,
            StageScreenState.PostMatchPreview or StageScreenState.PostMatchHud => GameModeHandoffCommand.PressPlayKey,
            StageScreenState.StorySelector or StageScreenState.RaidSelector or StageScreenState.PreviewReady => GameModeHandoffCommand.Back,
            _ => GameModeHandoffCommand.PressPlayKey,
        };

    private async Task<GameModeHandoffCommand?> TryWaitForPlayKeyTransitionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int stableDetections,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> tracker = new(stableDetections);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current = StageScreenDetector.Detect(frame);
            bool hasChangeMode = StageScreenDetector.PostMatchChangeModeAction(frame) is not null;
            GameModeHandoffCommand command = SelectGameModeHandoffCommand(current.State, hasChangeMode);
            string? candidate = command is GameModeHandoffCommand.Complete or GameModeHandoffCommand.ChangeGamemode
                ? command.ToString()
                : null;
            if (tracker.Update(candidate) is string stable)
            {
                return Enum.Parse<GameModeHandoffCommand>(stable);
            }
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<StageScreenMatch> WaitForStateAsync(
        RobloxWindow window,
        StageScreenState expected,
        TimeSpan timeout,
        IDetectorPack detector,
        int stableDetections,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StageScreenMatch last = new(StageScreenState.None, 0);
        StableStateTracker<string> expectedTracker = new(stableDetections);
        StableStateTracker<string> recoveryTracker = new(stableDetections);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            last = StageScreenDetector.Detect(frame);
            string? candidate = last.State == expected ? expected.ToString() : null;
            if (expectedTracker.Update(candidate) is not null) return last;
            string? recovery = detector.RecoveryState(frame);
            if (recoveryTracker.Update(IsRootRecovery(recovery) ? recovery : null) is string stableRecovery)
            {
                throw new StageRecoveryException(stableRecovery);
            }
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException($"Timed out waiting for {expected}. Last state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task<bool> TryWaitForStateAsync(
        RobloxWindow window,
        StageScreenState expected,
        TimeSpan timeout,
        IDetectorPack detector,
        int stableDetections,
        CancellationToken cancellationToken)
    {
        try
        {
            await WaitForStateAsync(window, expected, timeout, detector, stableDetections, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task EnsureClientSizeAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        Focus(window);
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width != width || bounds.Height != height)
        {
            await _automation.ResizeClientAsync(window, width, height, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        ClientBounds actual = _automation.GetClientBounds(window);
        if (actual.Width != width || actual.Height != height)
        {
            throw new InvalidOperationException($"Roblox did not accept the required {width} by {height} client size.");
        }
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
            throw new InvalidOperationException("Roblox no longer matches the detector pack client size.");
        }
        return _automation.CaptureClient(window);
    }

    private ImageFrame? TryCaptureClient(RobloxWindow window, IDetectorPack detector)
    {
        try
        {
            return CaptureClient(window, detector);
        }
        catch
        {
            // Recovery can proceed even if its optional diagnostic image is unavailable.
            return null;
        }
    }

    private static void ValidateCompatibility(StageRuntimeModels models, DetectorPackManifest detector)
    {
        int width = models.Camera.Manifest.ClientWidth;
        int height = models.Camera.Manifest.ClientHeight;
        if (width != detector.ClientWidth || height != detector.ClientHeight)
        {
            throw new InvalidDataException("The camera model and detector pack use different Roblox client sizes.");
        }
        foreach (PlacementModel? placement in new[] { models.PrestartPlacement, models.DelayedPlacement })
        {
            if (placement is not null && (placement.ClientWidth != width || placement.ClientHeight != height))
            {
                throw new InvalidDataException("A selected placement model uses a different Roblox client size.");
            }
        }
    }

    private async Task TryNotifyAsync(
        string webhookUrl,
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        StageRunOutcome outcome,
        ImageFrame frame,
        TimeSpan runtime,
        int victories,
        int defeats,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;
        try
        {
            await _discord.SendAsync(new DiscordNotification
            {
                WebhookUrl = webhookUrl,
                Event = outcome == StageRunOutcome.Victory ? "victory" : "defeat",
                Runtime = runtime,
                Victories = victories,
                Defeats = defeats,
                MapNumber = 0,
                Difficulty = 0,
                MacroName = $"{Label(mode)} Macro",
                Route = RouteLabel(mode, story, raid),
                Detail = $"{RouteLabel(mode, story, raid)} ended in {outcome}.",
                AttachmentPrefix = mode.ToString().ToLowerInvariant(),
                Screenshot = frame,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            log($"Discord result notification failed: {error.Message}", MacroEventLevel.Warning, "discord", null);
        }
    }

    private async Task TryNotifyRecoveryAsync(
        string webhookUrl,
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        string detail,
        ImageFrame? frame,
        TimeSpan runtime,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;
        try
        {
            await _discord.SendAsync(new DiscordNotification
            {
                WebhookUrl = webhookUrl,
                Event = "recovery",
                Runtime = runtime,
                Victories = 0,
                Defeats = 0,
                MapNumber = 0,
                Difficulty = 0,
                MacroName = $"{Label(mode)} Macro",
                Route = RouteLabel(mode, story, raid),
                Detail = detail,
                AttachmentPrefix = mode.ToString().ToLowerInvariant(),
                Screenshot = frame,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            log($"Discord recovery notification failed: {error.Message}", MacroEventLevel.Warning, "discord", null);
        }
    }

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
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

    internal static void RequirePrestartForTeamLoad(StageScreenMatch current)
    {
        if (current.State != StageScreenState.Prestart)
        {
            throw new InvalidOperationException(
                $"Team loading requires a confirmed prestart screen. Current state: {current.State} ({current.Confidence:P0}).");
        }
    }

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
