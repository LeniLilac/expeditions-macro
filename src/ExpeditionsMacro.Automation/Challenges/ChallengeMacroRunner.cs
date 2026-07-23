using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner : IGameModeWorkflow
{
    internal static readonly TimeSpan InitialPrestartTimeout = TimeSpan.FromSeconds(35);
    internal static readonly TimeSpan TeleportingPrestartTimeout = TimeSpan.FromMinutes(3);
    internal const int SchedulerHandoffMaximumAttempts = 3;

    private readonly IRobloxAutomation _automation;
    private readonly CameraAlignmentEngine _camera;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;

    public ChallengeMacroRunner(
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

    public string ModeId => "challenges";

    public async Task RunAsync(
        ChallengePreset preset,
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> mapModels,
        IDetectorPack detector,
        string webhookUrl,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ChallengeRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default,
        Func<Exception, CancellationToken, Task>? recoverableFailure = null,
        int? maximumCompletedRuns = null,
        bool returnWhenUnavailable = false,
        char? unitMenuKey = null)
    {
        if (maximumCompletedRuns is < 1) throw new ArgumentOutOfRangeException(nameof(maximumCompletedRuns));
        preset.ValidateReady();
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        if (!detector.SupportsChallengeMaps)
        {
            throw new InvalidDataException(DetectorPackCapabilities.ChallengeMapsUnavailableMessage(detector.Manifest));
        }
        ValidateRuntimeModels(preset, mapModels, detector.Manifest);
        ValidateTeamKey(preset.Maps.Any(profile => profile.TeamSlot > 0), unitMenuKey);
        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch runtime = Stopwatch.StartNew();
        ChallengeRotationState rotation = new();
        ChallengeType? currentType = null;
        ChallengeMapId? currentMap = null;
        DateTimeOffset? waitingUntil = null;
        int completed = 0;
        int victories = 0;
        int defeats = 0;
        int retries = 0;
        int recoveries = 0;

        void Write(string message, MacroEventLevel level = MacroEventLevel.Information, string? state = null, double? confidence = null) =>
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, level, message, state, confidence));
        void Report(string phase, int percent, string message, string? state = null, double? confidence = null) =>
            progress?.Report(new MacroProgress(phase, percent, message, state, confidence));
        void PublishSummary() => summaryChanged?.Invoke(new ChallengeRunSummary(
            startedAt,
            runtime.Elapsed,
            completed,
            victories,
            defeats,
            retries,
            recoveries,
            currentType,
            currentMap,
            waitingUntil,
            rotation.DailyLimitUntilUtc is not null));
        DiscordRunReporter reporter = new(_discord, webhookUrl, "Challenge Macro", "challenge", Write);

        Write($"Using Roblox window '{window.Title}' ({window.ProcessDescription}).");
        PublishSummary();
        try
        {
            Focus(window);
            await EnsureClientSizeAsync(window, detector.Manifest.ClientWidth, detector.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
            await EnsureChallengeListAsync(window, preset, detector, playMenuKey, Write, Report, () => { recoveries++; PublishSummary(); }, cancellationToken).ConfigureAwait(false);
            string enabledTypes = string.Join(", ", preset.EnabledTypes.Select(Label));
            reporter.Queue(
                "started",
                $"Monitoring {enabledTypes} Challenges on the global 30-minute rotation.",
                TryCaptureClient(window, detector),
                runtime.Elapsed,
                victories,
                defeats,
                new DiscordRunTarget(0, 0, "Regular Challenge rotation"));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool newEpoch = rotation.Advance(DateTimeOffset.Now);
                if (newEpoch) Write($"Challenge reset epoch is now {rotation.Epoch:HH:mm zzz}. Attempts were cleared.");

                if (rotation.DailyLimitUntilUtc is DateTimeOffset dailyUntil && DateTimeOffset.UtcNow < dailyUntil)
                {
                    waitingUntil = dailyUntil;
                    PublishSummary();
                    await WaitUntilAsync(window, dailyUntil, dailyLimit: true, Write, Report, cancellationToken).ConfigureAwait(false);
                    waitingUntil = null;
                    PublishSummary();
                    continue;
                }

                bool ranChallenge = false;
                bool sawAvailable = false;
                int cooldownCount = 0;
                foreach (ChallengeType type in Enum.GetValues<ChallengeType>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await EnsureChallengeListAsync(window, preset, detector, playMenuKey, Write, Report, () => { recoveries++; PublishSummary(); }, cancellationToken).ConfigureAwait(false);
                    ChallengeSelectorObservation selector = await WaitForChallengeSelectorAsync(window, preset, detector, TimeSpan.FromSeconds(12), Report, cancellationToken).ConfigureAwait(false);
                    if (selector.Match.State == ChallengeScreenState.ChallengeListUnavailable)
                    {
                        cooldownCount = Enum.GetValues<ChallengeType>().Length;
                        Write(
                            "Regular Challenges are unavailable for this 30-minute window.",
                            MacroEventLevel.Information,
                            "challenge_rotation_cooldown",
                            selector.Match.Confidence);
                        break;
                    }
                    ChallengeMapId map = await RecognizeMapAsync(window, preset, detector, type, Write, cancellationToken).ConfigureAwait(false);
                    currentType = type;
                    currentMap = map;
                    PublishSummary();
                    Report("Challenge selection", 10, $"Checking {Label(type)} on {Label(map)}.", "challenge_list", null);

                    ChallengeScreenMatch detail = await OpenChallengeTypeAsync(window, preset, detector, type, Report, cancellationToken).ConfigureAwait(false);
                    if (detail.State == ChallengeScreenState.ChallengeCooldown)
                    {
                        cooldownCount++;
                        Write($"{Label(type)} is on cooldown.", MacroEventLevel.Information, "challenge_cooldown", detail.Confidence);
                        await ClickAsync(window, detail.ActionX!.Value, detail.ActionY!.Value, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    sawAvailable = true;
                    rotation.ObserveAvailability();
                    if (!preset.EnabledTypes.Contains(type) || rotation.Attempted.Contains(type))
                    {
                        string reason = preset.EnabledTypes.Contains(type) ? "already attempted in this reset" : "disabled in this preset";
                        Write($"Skipping available {Label(type)} because it is {reason}.");
                        await ClickAsync(window, 308, 437, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    ChallengeMapRuntimeModels models = mapModels[map];
                    ChallengeTerminal terminal;
                    try
                    {
                        terminal = await RunSelectedChallengeAsync(
                            window,
                            preset,
                            type,
                            map,
                            models,
                            detector,
                            reporter,
                            playMenuKey,
                            unitMenuKey,
                            runtime,
                            victories,
                            defeats,
                            value => { retries += value; PublishSummary(); },
                            Write,
                            Report,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (CameraWorldNotRenderedException world)
                        when (preset.AutoRecover)
                    {
                        recoveries++;
                        PublishSummary();
                        string retryDetail =
                            $"{Label(type)} on {Label(map)} loaded without world geometry. Returning through Play and retrying the same Challenge without placement.";
                        Write(
                            retryDetail,
                            MacroEventLevel.Warning,
                            "camera_world_missing",
                            world.BestConfidence);
                        Report(
                            "Recovery",
                            0,
                            retryDetail,
                            "camera_world_missing",
                            world.BestConfidence);
                        await ReturnFromPrestartAfterAlignmentFailureAsync(
                            window,
                            preset,
                            detector,
                            playMenuKey,
                            Report,
                            cancellationToken).ConfigureAwait(false);
                        ranChallenge = true;
                        break;
                    }
                    catch (CameraAlignmentException alignment)
                    {
                        string skipDetail = $"Skipping {Label(type)} on {Label(map)} for this reset because camera alignment exhausted {alignment.Attempts} attempts (best {alignment.BestConfidence:P0}). No units were placed and the match was not started.";
                        Write(skipDetail, MacroEventLevel.Warning, "camera_alignment_skipped", alignment.BestConfidence);
                        Report("Task skipped", 100, skipDetail, "camera_alignment_skipped", alignment.BestConfidence);
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
                        reporter.Queue(
                            "skipped",
                            skipDetail,
                            TryCaptureClient(window, detector),
                            runtime.Elapsed,
                            victories,
                            defeats,
                            new DiscordRunTarget((int)map, 0, ChallengeRoute(type, map)));
                        await ReturnFromPrestartAfterAlignmentFailureAsync(window, preset, detector, playMenuKey, Report, cancellationToken).ConfigureAwait(false);
                        rotation.MarkAttempted(type);
                        PublishSummary();
                        continue;
                    }
                    catch (ChallengeRecoveryException recovery)
                    {
                        if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(recovery.State)} was recognized, but automatic recovery is disabled.", recovery);
                        recoveries++;
                        PublishSummary();
                        string recoveryDetail = $"Challenge match was interrupted by {Label(recovery.State)}. Returning through automatic recovery.";
                        Write(recoveryDetail, MacroEventLevel.Warning, recovery.State, null);
                        reporter.Queue(
                            "recovery",
                            recoveryDetail,
                            TryCaptureClient(window, detector),
                            runtime.Elapsed,
                            victories,
                            defeats,
                            new DiscordRunTarget((int)map, 0, ChallengeRoute(type, map)));
                        await EnsureChallengeListAsync(window, preset, detector, playMenuKey, Write, Report, () => { }, cancellationToken).ConfigureAwait(false);
                        ranChallenge = true;
                        break;
                    }
                    victories += terminal.Victories;
                    defeats += terminal.Defeats;
                    completed++;
                    rotation.MarkAttempted(type);
                    PublishSummary();
                    if (maximumCompletedRuns is int maximum && completed >= maximum)
                    {
                        Write($"Completed {completed} scheduled Challenge match(es). Returning control to the task scheduler.", MacroEventLevel.Success);
                        return;
                    }
                    ranChallenge = true;
                    break;
                }

                currentType = null;
                currentMap = null;
                PublishSummary();
                if (ranChallenge) continue;

                DateTimeOffset waitUntil;
                string waitDetail;
                bool dailyLimit = cooldownCount == Enum.GetValues<ChallengeType>().Length && rotation.ObserveAllCooldown(DateTimeOffset.Now);
                if (dailyLimit)
                {
                    waitUntil = rotation.DailyLimitUntilUtc!.Value;
                    waitDetail = $"All three Challenges remained on cooldown across a full global reset. Daily limits are treated as reached until {waitUntil:HH:mm} UTC.";
                    Write(waitDetail, MacroEventLevel.Warning, "daily_limit", null);
                }
                else
                {
                    waitUntil = ChallengeRunPolicy.NextGlobalReset(DateTimeOffset.Now).ToUniversalTime();
                    waitDetail = sawAvailable
                        ? "Every enabled Challenge was already attempted in this reset."
                        : "Every regular Challenge is on cooldown.";
                    waitDetail = DescribeUnavailable(waitDetail, waitUntil, returnWhenUnavailable);
                    Write(waitDetail);
                }

                waitingUntil = waitUntil;
                PublishSummary();
                reporter.Queue(
                    "waiting",
                    waitDetail,
                    screenshot: null,
                    runtime.Elapsed,
                    victories,
                    defeats,
                    new DiscordRunTarget(0, 0, "Regular Challenge rotation"));
                if (returnWhenUnavailable)
                {
                    Write($"Challenge rotation is unavailable until {waitUntil:HH:mm} UTC. Preparing shared navigation for the next scheduled task.");
                    await PrepareSchedulerHandoffAsync(window, preset, detector, Write, Report, cancellationToken).ConfigureAwait(false);
                    Write("Challenge handoff is ready. Returning control to the task scheduler.", MacroEventLevel.Success, "game_mode_selector", null);
                    return;
                }
                await WaitUntilAsync(window, waitUntil, dailyLimit, Write, Report, cancellationToken).ConfigureAwait(false);
                waitingUntil = null;
                PublishSummary();
            }
        }
        finally
        {
            await reporter.FlushAsync().ConfigureAwait(false);
        }
    }

    private async Task<ChallengeTerminal> RunSelectedChallengeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeType type,
        ChallengeMapId map,
        ChallengeMapRuntimeModels models,
        IDetectorPack detector,
        DiscordRunReporter reporter,
        char playMenuKey,
        char? unitMenuKey,
        Stopwatch runtime,
        int priorVictories,
        int priorDefeats,
        Action<int> retriesChanged,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        int victories = 0;
        int defeats = 0;
        int retry = 0;
        ChallengeMapProfile profile = preset.Maps.Single(value => value.Map == map);
        ImageFrame available = CaptureClient(window, detector);
        (int X, int Y)? stage = ChallengeScreenDetector.ActionFor(ChallengeScreenState.ChallengeAvailable, available);
        if (stage is null) throw new InvalidOperationException("The Challenge Select Stage button disappeared before it could be clicked.");
        await ClickAsync(window, stage.Value.X, stage.Value.Y, cancellationToken).ConfigureAwait(false);
        ImageFrame preview = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.PreviewReady, TimeSpan.FromSeconds(15), report, cancellationToken).ConfigureAwait(false);
        (int X, int Y)? startPreview = ChallengeScreenDetector.ActionFor(ChallengeScreenState.PreviewReady, preview);
        if (startPreview is null) throw new InvalidOperationException("The Challenge preview Start button could not be located.");
        await ClickAsync(window, startPreview.Value.X, startPreview.Value.Y, cancellationToken).ConfigureAwait(false);
        bool attemptNotified = false;
        bool teamLoaded = profile.TeamSlot == 0;
        while (true)
        {
            ImageFrame prestart = await WaitForPrestartAfterPreviewAsync(window, preset, detector, report, cancellationToken).ConfigureAwait(false);
            if (!teamLoaded)
            {
                await _teams.SelectAsync(window, profile.TeamSlot, unitMenuKey!.Value, progress: null, cancellationToken).ConfigureAwait(false);
                teamLoaded = true;
                prestart = await WaitForScreenAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.Prestart,
                    TimeSpan.FromSeconds(10),
                    report,
                    cancellationToken).ConfigureAwait(false);
            }
            report("Camera preparation", 20, $"Preparing {Label(map)} for {Label(type)}.", "prestart", null);
            await PrepareCameraAsync(window, preset, models.Camera, report, log, cancellationToken).ConfigureAwait(false);
            ChallengePlacementPartition? prestartPlacements = null;
            if (models.PrestartPlacement is not null)
            {
                ScreenRegion dialogOcclusion = ChallengeScreenDetector.PrestartOcclusion(prestart)
                    ?? throw new InvalidOperationException("The Challenge Start Game dialog could not be measured before placement.");
                prestartPlacements = ChallengeRunPolicy.PartitionPrestartPlacements(models.PrestartPlacement.Steps, dialogOcclusion);
                report("Placement", 45, "Placing units outside the Start Game dialog.", null, null);
                if (prestartPlacements.BeforeStart.Count > 0)
                {
                    await PlaceAsync(window, preset, models.PrestartPlacement, prestartPlacements.BeforeStart, log, cancellationToken).ConfigureAwait(false);
                }
                if (prestartPlacements.AfterStart.Count > 0)
                {
                    log(
                        $"Deferred {prestartPlacements.AfterStart.Count} before-start placement(s) hidden by the Start Game dialog.",
                        MacroEventLevel.Information,
                        null,
                        null);
                }
            }

            (int X, int Y)? start = await LocateActionAfterParkingAsync(
                token => _automation.ParkCursorAsync(window, token),
                () => CaptureClient(window, detector),
                frame => ChallengeScreenDetector.ActionFor(ChallengeScreenState.Prestart, frame),
                retryMilliseconds: 100,
                maximumAttempts: 3,
                cancellationToken).ConfigureAwait(false);
            if (start is null) throw new InvalidOperationException("The Challenge Start Game button disappeared before it could be clicked.");
            Stopwatch matchRuntime = Stopwatch.StartNew();
            await ClickAsync(window, start.Value.X, start.Value.Y, cancellationToken).ConfigureAwait(false);
            if (!attemptNotified)
            {
                reporter.Queue(
                    "attempt",
                    $"Starting the {Label(type)} Challenge on {Label(map)}.",
                    prestart,
                    runtime.Elapsed,
                    priorVictories,
                    priorDefeats,
                    new DiscordRunTarget((int)map, 0, ChallengeRoute(type, map)));
                attemptNotified = true;
            }
            if (prestartPlacements is { AfterStart.Count: > 0 } && models.PrestartPlacement is not null)
            {
                await Task.Delay(550, cancellationToken).ConfigureAwait(false);
                report("Placement", 50, $"Placing {prestartPlacements.AfterStart.Count} unit(s) that were covered by the Start Game dialog.", null, null);
                await PlaceAsync(window, preset, models.PrestartPlacement, prestartPlacements.AfterStart, log, cancellationToken).ConfigureAwait(false);
            }
            await Task.Delay(2200, cancellationToken).ConfigureAwait(false);
            MatchTerminal terminal = await MonitorMatchAsync(window, preset, profile, models, detector, log, report, cancellationToken).ConfigureAwait(false);
            if (terminal.State == ChallengeScreenState.Victory)
            {
                victories++;
                string detail = $"{Label(type)} on {Label(map)} ended in Victory.";
                log(detail, MacroEventLevel.Success, "victory", terminal.Confidence);
                reporter.Queue(
                    "victory",
                    detail,
                    terminal.Frame,
                    runtime.Elapsed,
                    priorVictories + victories,
                    priorDefeats + defeats,
                    new DiscordRunTarget((int)map, 0, ChallengeRoute(type, map)),
                    matchRuntime: matchRuntime.Elapsed);
                await ReturnFromTerminalAsync(window, preset, detector, playMenuKey, report, cancellationToken).ConfigureAwait(false);
                return new ChallengeTerminal(victories, defeats);
            }

            defeats++;
            bool willRetry = ChallengeRunPolicy.TerminalContinuation(
                victory: false,
                retry,
                preset.DefeatRetries) == ChallengeTerminalContinuation.RepeatStage;
            string defeatDetail = willRetry
                ? $"{Label(type)} on {Label(map)} ended in Defeat. Retry {retry + 1} of {preset.DefeatRetries} will start."
                : $"{Label(type)} on {Label(map)} ended in Defeat. The retry limit was reached.";
            log(defeatDetail, MacroEventLevel.Warning, "defeat", terminal.Confidence);
            reporter.Queue(
                "defeat",
                defeatDetail,
                terminal.Frame,
                runtime.Elapsed,
                priorVictories + victories,
                priorDefeats + defeats,
                new DiscordRunTarget((int)map, 0, ChallengeRoute(type, map)),
                matchRuntime: matchRuntime.Elapsed);
            if (willRetry)
            {
                retry++;
                retriesChanged(1);
                report("Retry", 0, $"Retrying after defeat ({retry}/{preset.DefeatRetries}).", "defeat", terminal.Confidence);
                (int x, int y) = ChallengeScreenDetector.DefeatRetryAction(terminal.Frame);
                await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                await Task.Delay(3500, cancellationToken).ConfigureAwait(false);
                continue;
            }

            log("Defeat retry limit reached. This Challenge will not be attempted again until the next global reset.", MacroEventLevel.Information, null, null);
            await ReturnFromTerminalAsync(window, preset, detector, playMenuKey, report, cancellationToken).ConfigureAwait(false);
            return new ChallengeTerminal(victories, defeats);
        }
    }

    private async Task<MatchTerminal> MonitorMatchAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeMapProfile profile,
        ChallengeMapRuntimeModels models,
        IDetectorPack detector,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        bool delayedPlaced = models.DelayedPlacement is null;
        StableStateTracker<ChallengeScreenState> terminalTracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(Math.Max(2, preset.StableDetections));
        StableStateTracker<string> rewardTracker = new(preset.StableDetections);
        InactivityKeepAlive keepAlive = new();

        report("Gameplay", 55, delayedPlaced ? "Match active. Watching for Victory or Defeat." : "Match active. Waiting for delayed placement.", null, null);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!delayedPlaced && ChallengeRunPolicy.IsDelayedPlacementDue(profile, elapsed.Elapsed))
            {
                report("Placement", 65, $"Running delayed placements after {elapsed.Elapsed.TotalSeconds:F0} seconds.", null, null);
                await PlaceAsync(window, preset, models.DelayedPlacement!, log, cancellationToken).ConfigureAwait(false);
                delayedPlaced = true;
            }

            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState candidate = match.State is ChallengeScreenState.Victory or ChallengeScreenState.Defeat
                ? match.State
                : ChallengeScreenState.None;
            ChallengeScreenState? stable = terminalTracker.Update(candidate);
            if (stable is ChallengeScreenState.Victory or ChallengeScreenState.Defeat)
            {
                return new MatchTerminal(stable.Value, match.Confidence, frame.Clone());
            }

            string? recovery = detector.RecoveryState(frame);
            if (recoveryTracker.Update(recovery ?? string.Empty) is string stableRecovery && !string.IsNullOrEmpty(stableRecovery))
            {
                throw new ChallengeRecoveryException(stableRecovery);
            }

            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? detectorState = detector.Classify(scores);
            if (rewardTracker.Update(detectorState == "reward" ? detectorState : string.Empty) == "reward")
            {
                (int x, int y) = detector.ActionFor("reward", frame);
                report("Reward", 70, "Selecting the first available upgrade.", "reward", scores.GetValueOrDefault("reward"));
                await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                await Task.Delay(4300, cancellationToken).ConfigureAwait(false);
                rewardTracker.Reset();
            }
            await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReturnFromTerminalAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        ImageFrame party = await OpenPlayMenuAsync(
            window,
            preset,
            detector,
            playMenuKey,
            log: null,
            report,
            cancellationToken).ConfigureAwait(false);
        (int X, int Y)? changeMode = ChallengeScreenDetector.ActionFor(ChallengeScreenState.PostMatchPreview, party);
        if (changeMode is null) throw new InvalidOperationException("Change Gamemode could not be located after the match.");
        await ClickAsync(window, changeMode.Value.X, changeMode.Value.Y, cancellationToken).ConfigureAwait(false);
        ImageFrame modes = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.GameModeSelector, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
        (int X, int Y)? challenge = ChallengeScreenDetector.ActionFor(ChallengeScreenState.GameModeSelector, modes);
        await ClickAsync(window, challenge!.Value.X, challenge.Value.Y, cancellationToken).ConfigureAwait(false);
        await WaitForChallengeSelectorAsync(window, preset, detector, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
    }

    private async Task PrepareSchedulerHandoffAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        double confidence = await CloseChallengeSelectorForHandoffAsync(
            async token => (await WaitForChallengeSelectorAsync(
                window,
                preset,
                detector,
                TimeSpan.FromSeconds(8),
                report,
                token).ConfigureAwait(false)).Match,
            (x, y, token) => ClickAsync(window, x, y, token),
            async token =>
            {
                ImageFrame? gameModes = await TryWaitForScreenAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.GameModeSelector,
                    TimeSpan.FromSeconds(5),
                    report,
                    token).ConfigureAwait(false);
                return gameModes is null
                    ? null
                    : ChallengeScreenDetector.ScoreStates(gameModes)[ChallengeScreenState.GameModeSelector];
            },
            (attempt, selector) => report(
                "Handoff",
                0,
                attempt == 1
                    ? "Closing the Challenge selector before handing off navigation."
                    : $"Challenge selector is still open; retrying its close button ({attempt}/{SchedulerHandoffMaximumAttempts}).",
                selector.State.ToString(),
                selector.Confidence),
            (attempt, selector) => log(
                $"Challenge selector did not close (attempt {attempt}/{SchedulerHandoffMaximumAttempts}).",
                MacroEventLevel.Warning,
                selector.State.ToString(),
                selector.Confidence),
            cancellationToken).ConfigureAwait(false);

        log(
            "Challenge selector closed. Shared game-mode navigation is ready for the next workflow.",
            MacroEventLevel.Success,
            "game_mode_selector",
            confidence);
    }

    internal static async Task<double> CloseChallengeSelectorForHandoffAsync(
        Func<CancellationToken, Task<ChallengeScreenMatch>> observeSelector,
        Func<int, int, CancellationToken, Task> clickClose,
        Func<CancellationToken, Task<double?>> waitForGameModeSelector,
        Action<int, ChallengeScreenMatch>? closeAttemptStarted,
        Action<int, ChallengeScreenMatch>? closeAttemptMissed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observeSelector);
        ArgumentNullException.ThrowIfNull(clickClose);
        ArgumentNullException.ThrowIfNull(waitForGameModeSelector);

        for (int attempt = 1; attempt <= SchedulerHandoffMaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChallengeScreenMatch selector = await observeSelector(cancellationToken).ConfigureAwait(false);
            if (selector.State is not (ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable))
            {
                throw new InvalidOperationException($"Cannot hand off from the unexpected Challenge state {selector.State}.");
            }
            if (selector.ActionX is not int closeX || selector.ActionY is not int closeY)
            {
                throw new InvalidOperationException("The Challenge selector close button could not be located.");
            }

            closeAttemptStarted?.Invoke(attempt, selector);
            await clickClose(closeX, closeY, cancellationToken).ConfigureAwait(false);
            double? confidence = await waitForGameModeSelector(cancellationToken).ConfigureAwait(false);
            if (confidence is not null) return confidence.Value;
            closeAttemptMissed?.Invoke(attempt, selector);
        }

        throw new InvalidOperationException(
            $"The Challenge selector remained open after {SchedulerHandoffMaximumAttempts} focused close attempts, so control was not returned to the task scheduler.");
    }

    private async Task<ChallengeScreenMatch> OpenChallengeTypeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeType type,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            (int x, int y) = ChallengeScreenDetector.ActionForType(type);
            await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            StableStateTracker<ChallengeScreenState> tracker = new(preset.StableDetections);
            while (DateTimeOffset.UtcNow < deadline)
            {
                ImageFrame frame = CaptureClient(window, detector);
                ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
                ChallengeScreenState candidate = match.State is ChallengeScreenState.ChallengeAvailable or ChallengeScreenState.ChallengeCooldown
                    ? match.State
                    : ChallengeScreenState.None;
                ChallengeScreenState? stable = tracker.Update(candidate);
                if (stable is ChallengeScreenState.ChallengeAvailable or ChallengeScreenState.ChallengeCooldown)
                {
                    report("Challenge selection", 15, stable == ChallengeScreenState.ChallengeAvailable ? "Select Stage is available." : "Challenge is on cooldown.", stable.ToString(), match.Confidence);
                    return match with { State = stable.Value };
                }
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            report("Challenge selection", 10, $"Selector click did not open Challenge {type} (attempt {attempt}/3).", null, null);
        }
        throw new InvalidOperationException($"Challenge {Label(type)} could not be opened from the fixed selector row.");
    }

    private async Task<ChallengeMapId> RecognizeMapAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeType type,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        int retryMilliseconds = Math.Clamp(preset.PollMilliseconds / 2, 180, 350);
        int maximumAttempts = Math.Max(2, (int)Math.Ceiling(6000d / retryMilliseconds));
        ChallengeMapId? map = await RecognizeMapAfterParkingAsync(
            token => _automation.ParkCursorAsync(window, token),
            () => CaptureClient(window, detector),
            frame => detector.ChallengeMapForType(frame, type),
            retryMilliseconds,
            maximumAttempts,
            cancellationToken).ConfigureAwait(false);
        if (map is not null)
        {
            log($"{Label(type)} map recognized as {Label(map.Value)}.", MacroEventLevel.Success, "challenge_map", null);
            return map.Value;
        }
        throw new InvalidOperationException($"The map thumbnail for {Label(type)} could not be recognized. Add this selector capture to the Challenge detector dataset before running automation.");
    }

    internal static async Task<ChallengeMapId?> RecognizeMapAfterParkingAsync(
        Func<CancellationToken, Task> parkCursor,
        Func<ImageFrame> capture,
        Func<ImageFrame, ChallengeMapId?> recognize,
        int retryMilliseconds,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parkCursor);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(recognize);
        if (retryMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryMilliseconds));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        await parkCursor(cancellationToken).ConfigureAwait(false);
        await Task.Delay(retryMilliseconds, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChallengeMapId? map = recognize(capture());
            if (map is not null) return map;
            if (attempt + 1 < maximumAttempts)
            {
                await Task.Delay(retryMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        return null;
    }

    internal static async Task<(int X, int Y)?> LocateActionAfterParkingAsync(
        Func<CancellationToken, Task> parkCursor,
        Func<ImageFrame> capture,
        Func<ImageFrame, (int X, int Y)?> locate,
        int retryMilliseconds,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parkCursor);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(locate);
        if (retryMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryMilliseconds));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await parkCursor(cancellationToken).ConfigureAwait(false);
            (int X, int Y)? action = locate(capture());
            if (action is not null) return action;
            if (attempt + 1 < maximumAttempts)
            {
                await Task.Delay(retryMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        return null;
    }

    private async Task<ImageFrame> WaitForScreenAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeScreenState desired,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        ImageFrame? frame = await TryWaitForScreenAsync(
            window,
            preset,
            detector,
            desired,
            timeout,
            report,
            cancellationToken).ConfigureAwait(false);
        return frame ?? throw new InvalidOperationException($"Timed out waiting for {Label(desired)}.");
    }

    private async Task<ImageFrame> WaitForPrestartAfterPreviewAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset deadline = startedAt + InitialPrestartTimeout;
        StableStateTracker<ChallengeScreenState> prestartTracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(preset.StableDetections);
        bool teleportingSeen = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            DateTimeOffset extendedDeadline = ExtendPrestartDeadline(startedAt, deadline, match.State);
            if (extendedDeadline > deadline)
            {
                deadline = extendedDeadline;
                teleportingSeen = true;
                report(
                    "Teleporting",
                    0,
                    "Roblox is still teleporting. Waiting up to three minutes for the Challenge prestart screen.",
                    "teleporting",
                    match.Confidence);
            }

            ChallengeScreenState? stable = prestartTracker.Update(
                match.State == ChallengeScreenState.Prestart
                    ? ChallengeScreenState.Prestart
                    : ChallengeScreenState.None);
            if (stable == ChallengeScreenState.Prestart) return frame;

            string? recovery = detector.RecoveryState(frame);
            string recoveryCandidate = recovery is "afk" or "disconnect" or "lobby" ? recovery : string.Empty;
            if (recoveryTracker.Update(recoveryCandidate) is string stableRecovery && !string.IsNullOrEmpty(stableRecovery))
            {
                throw new ChallengeRecoveryException(stableRecovery);
            }

            if (match.State is not (ChallengeScreenState.None or ChallengeScreenState.Teleporting))
            {
                report("Waiting", 0, $"Detected {Label(match.State)}.", match.State.ToString(), match.Confidence);
            }
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            teleportingSeen
                ? "Roblox remained on the Teleporting screen for three minutes and did not reach the Challenge prestart screen. The server or connection did not finish loading the stage."
                : "Timed out waiting for Prestart.");
    }

    internal static DateTimeOffset ExtendPrestartDeadline(
        DateTimeOffset startedAt,
        DateTimeOffset currentDeadline,
        ChallengeScreenState observedState)
    {
        if (observedState != ChallengeScreenState.Teleporting) return currentDeadline;
        DateTimeOffset teleportDeadline = startedAt + TeleportingPrestartTimeout;
        return teleportDeadline > currentDeadline ? teleportDeadline : currentDeadline;
    }

    private async Task<ImageFrame?> TryWaitForScreenAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeScreenState desired,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<ChallengeScreenState> tracker = new(preset.StableDetections);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState? stable = tracker.Update(match.State == desired ? desired : ChallengeScreenState.None);
            if (stable == desired) return frame;
            if (match.State != ChallengeScreenState.None) report("Waiting", 0, $"Detected {Label(match.State)}.", match.State.ToString(), match.Confidence);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task PrepareCameraAsync(
        RobloxWindow window,
        ChallengePreset preset,
        CameraModel model,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        double score = await _camera.PrepareAndAlignAsync(
            model,
            window,
            preset.ZoomTicks,
            preset.PitchDragPixels,
            progress: new Progress<MacroProgress>(value => report(value.Phase, value.Percent, value.Message, value.DetectedState, value.Confidence)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        log($"Camera alignment finished at {score:P0} confidence.", MacroEventLevel.Success, null, score);
    }

    private Task PlaceAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel model,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        PlaceAsync(window, preset, model, model.Steps, log, cancellationToken);

    private Task PlaceAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            model,
            steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            preset.UnitKeyHoldMilliseconds,
            preset.UnitSelectDelayMilliseconds,
            stepSent: null,
            status: message => log(message, MacroEventLevel.Information, null, null),
            cancellationToken);

    private async Task EnsureClientSizeAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        ClientBounds current = _automation.GetClientBounds(window);
        if (current.Width != width || current.Height != height)
        {
            await _automation.ResizeClientAsync(window, width, height, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        ClientBounds actual = _automation.GetClientBounds(window);
        if (actual.Width != width || actual.Height != height)
        {
            throw new InvalidOperationException($"Roblox did not accept the required {width} by {height} client size (actual: {actual.Width} by {actual.Height}).");
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
            // Recovery must continue even if the diagnostic screenshot cannot be captured.
            return null;
        }
    }

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
    }

    private static void ValidateRuntimeModels(
        ChallengePreset preset,
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> mapModels,
        DetectorPackManifest detector)
    {
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            if (!mapModels.TryGetValue(profile.Map, out ChallengeMapRuntimeModels? models)) throw new InvalidDataException($"Models for {Label(profile.Map)} were not loaded.");
            if (models.Camera.Manifest.ClientWidth != detector.ClientWidth || models.Camera.Manifest.ClientHeight != detector.ClientHeight) throw new InvalidDataException($"The {Label(profile.Map)} camera model uses a different Roblox client size.");
            foreach (PlacementModel placement in new[] { models.PrestartPlacement, models.DelayedPlacement }.Where(model => model is not null).Cast<PlacementModel>())
            {
                if (placement.ClientWidth != detector.ClientWidth || placement.ClientHeight != detector.ClientHeight) throw new InvalidDataException($"A {Label(profile.Map)} placement model uses a different Roblox client size.");
            }
        }
    }

    private static string FormatRemaining(TimeSpan remaining) => remaining.TotalHours >= 1
        ? $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m"
        : $"{Math.Max(0, (int)remaining.TotalMinutes)}m {Math.Max(0, remaining.Seconds):00}s";

    private static string ChallengeRoute(ChallengeType type, ChallengeMapId map) => $"{Label(type)} · {Label(map)}";

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

    private static string Label(ChallengeMapId map) => map switch
    {
        ChallengeMapId.SchoolGrounds => "School Grounds",
        ChallengeMapId.FlowerForest => "Flower Forest",
        ChallengeMapId.RoseKingdom => "Rose Kingdom",
        ChallengeMapId.FairyKingForest => "Fairy King Forest",
        ChallengeMapId.KingsTomb => "King's Tomb",
        _ => throw new ArgumentOutOfRangeException(nameof(map)),
    };

    private static string Label(ChallengeType type) => type switch
    {
        ChallengeType.Trait => "Trait",
        ChallengeType.Stat => "Stat",
        ChallengeType.Sprite => "Sprite",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static string Label(object value) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToString()!.Replace('_', ' '));

    private sealed record MatchTerminal(ChallengeScreenState State, double Confidence, ImageFrame Frame);

    private sealed record ChallengeTerminal(int Victories, int Defeats);

    private sealed class ChallengeRecoveryException : Exception
    {
        public ChallengeRecoveryException(string state) : base($"Challenge recovery screen recognized: {state}.")
        {
            State = state;
        }

        public string State { get; }
    }
}
