using System.Diagnostics;
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
    private readonly FastNoAlignPreparationSession _fastNoAlign;
    private readonly PlacementService _placements;
    private readonly TeamSelectionService _teams;
    private readonly IDiscordNotifier _discord;
    private readonly ManualInputRouteService? _manualInputs;
    public ChallengeMacroRunner(
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

    public string ModeId => "challenges";

    public async Task RunAsync(
        ChallengePreset preset,
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> mapModels,
        IDetectorPack detector,
        ChallengeRotationState rotation,
        string webhookUrl,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ChallengeRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default,
        int? maximumCompletedRuns = null,
        bool returnWhenUnavailable = false,
        char? unitMenuKey = null,
        MacroRunTotals? macroTotals = null,
        char cancelPlacementKey =
            AppSettings.DefaultCancelPlacementKeyChar)
    {
        ArgumentNullException.ThrowIfNull(rotation);
        if (maximumCompletedRuns is < 1) throw new ArgumentOutOfRangeException(nameof(maximumCompletedRuns));
        preset.Validate();
        CameraPreparationExecutionPolicy.ValidateForExecution(preset.CameraPreparationMode);
        preset.ValidateReady();
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        if (!detector.SupportsChallengeMaps)
        {
            throw new InvalidDataException(DetectorPackCapabilities.ChallengeMapsUnavailableMessage(detector.Manifest));
        }
        ValidateRuntimeModels(preset, mapModels, detector.Manifest);
        IReadOnlyDictionary<
            ChallengeMapId,
            ManualInputRecording> manualRecordings =
            await ResolveManualRecordingsAsync(
                    preset,
                    mapModels,
                    cancellationToken)
                .ConfigureAwait(false);
        ValidateTeamKey(preset.Maps.Any(profile => profile.TeamSlot > 0), unitMenuKey);
        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch runtime = Stopwatch.StartNew();
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
        DiscordRunReporter reporter = new(_discord, webhookUrl, "Challenge Macro", "challenge", Write, macroTotals);
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

                    (ImageFrame detailFrame, ChallengeScreenMatch detail) = await OpenChallengeTypeAsync(window, preset, detector, type, Report, cancellationToken).ConfigureAwait(false);
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
                            manualRecordings.GetValueOrDefault(
                                map),
                            (detailFrame, detail),
                            detector,
                            reporter,
                            playMenuKey,
                            unitMenuKey,
                            cancelPlacementKey,
                            runtime,
                            victories,
                            defeats,
                            value => { retries += value; PublishSummary(); },
                            Write,
                            Report,
                            cancellationToken).ConfigureAwait(false);
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
                        await CompleteScheduledChallengeRunAsync(window, preset, detector, completed, Write, Report, cancellationToken).ConfigureAwait(false);
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
        ManualInputRecording? manualRecording,
        (ImageFrame Frame, ChallengeScreenMatch Match) availableObservation,
        IDetectorPack detector,
        DiscordRunReporter reporter,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
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
        await ClickAvailableStageAsync(window, preset, detector, availableObservation, report, cancellationToken).ConfigureAwait(false);
        (_, ChallengeScreenMatch preview) = await WaitForPreviewStartAsync(window, preset, detector, report, cancellationToken).ConfigureAwait(false);
        await ClickAsync(window, preview.ActionX!.Value, preview.ActionY!.Value, cancellationToken).ConfigureAwait(false);
        bool attemptNotified = false;
        bool teamLoaded = profile.TeamSlot == 0;
        bool skipRepeatedPrestart = false;
        while (true)
        {
            bool skipThisPrestart =
                skipRepeatedPrestart;
            skipRepeatedPrestart = false;
            (ImageFrame prestart, teamLoaded) =
                await PrepareSelectedChallengeAttemptAsync(
                        window,
                        preset,
                        detector,
                        type,
                        map,
                        profile,
                        teamLoaded,
                        skipThisPrestart,
                        unitMenuKey,
                        report,
                        log,
                        cancellationToken)
                    .ConfigureAwait(false);
            (Stopwatch matchRuntime,
                IReadOnlyList<PlacementStep>
                    configuredAfterStart) =
                await BeginConfiguredMatchAsync(
                        window,
                        preset,
                        models,
                        manualRecording,
                        prestart,
                        detector,
                        () =>
                        {
                            if (attemptNotified)
                            {
                                return;
                            }
                            reporter.Queue(
                                "attempt",
                                $"Starting the {Label(type)} Challenge on {Label(map)}.",
                                prestart,
                                runtime.Elapsed,
                                priorVictories,
                                priorDefeats,
                                new DiscordRunTarget(
                                    (int)map,
                                    0,
                                    ChallengeRoute(
                                        type,
                                        map)));
                            attemptNotified = true;
                        },
                        report,
                        log,
                        cancelPlacementKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            MatchTerminal terminal = await MonitorMatchAsync(
                window,
                preset,
                models,
                configuredAfterStart,
                detector,
                matchRuntime,
                log,
                report,
                cancelPlacementKey,
                cancellationToken).ConfigureAwait(false);
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
                skipRepeatedPrestart = await RetryDefeatAsync(window, preset, detector, terminal.Frame, models.Placement, report, cancellationToken).ConfigureAwait(false);
                continue;
            }

            log("Defeat retry limit reached. This Challenge will not be attempted again until the next global reset.", MacroEventLevel.Information, null, null);
            await ReturnFromTerminalAsync(window, preset, detector, playMenuKey, report, cancellationToken).ConfigureAwait(false);
            return new ChallengeTerminal(victories, defeats);
        }
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
        ChallengeMapId? map = await RecognizeMapAfterParkingAsync(
            token => _automation.ParkCursorAsync(window, token),
            () => CaptureClient(window, detector),
            frame => detector.ChallengeMapForType(frame, type),
            retryMilliseconds,
            maximumAttempts: 3,
            cancellationToken,
            softTimeout: TimeSpan.FromSeconds(20))
            .ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        TimeSpan? softTimeout = null,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(parkCursor);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(recognize);
        if (retryMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryMilliseconds));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        delay ??= static (duration, token) =>
            Task.Delay(duration, token);
        TimeSpan timeout = softTimeout ??
            TimeSpan.FromMilliseconds(
                Math.Max(
                    1L,
                    (long)retryMilliseconds *
                    maximumAttempts));
        ObservationWaitBudget budget = new(
            timeout,
            maximumAttempts,
            utcNow);
        await parkCursor(cancellationToken).ConfigureAwait(false);
        await delay(
            TimeSpan.FromMilliseconds(retryMilliseconds),
            cancellationToken).ConfigureAwait(false);
        while (budget.ShouldObserve())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChallengeMapId? map = recognize(capture());
            if (map is not null) return map;
            budget.MarkObserved();
            await delay(
                TimeSpan.FromMilliseconds(retryMilliseconds),
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    internal static async Task<(int X, int Y)?> LocateActionAfterParkingAsync(
        Func<CancellationToken, Task> parkCursor,
        Func<ImageFrame> capture,
        Func<ImageFrame, (int X, int Y)?> locate,
        int retryMilliseconds,
        int maximumAttempts,
        CancellationToken cancellationToken,
        TimeSpan? softTimeout = null,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(parkCursor);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(locate);
        if (retryMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryMilliseconds));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        delay ??= static (duration, token) =>
            Task.Delay(duration, token);
        TimeSpan timeout = softTimeout ??
            TimeSpan.FromMilliseconds(
                Math.Max(
                    1L,
                    (long)retryMilliseconds *
                    maximumAttempts));
        ObservationWaitBudget budget = new(
            timeout,
            maximumAttempts,
            utcNow);
        for (int attempt = 0; attempt < maximumAttempts && budget.ShouldObserve(); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await parkCursor(cancellationToken).ConfigureAwait(false);
            (int X, int Y)? action = locate(capture());
            if (action is not null) return action;
            budget.MarkObserved();
            await delay(
                TimeSpan.FromMilliseconds(retryMilliseconds),
                cancellationToken).ConfigureAwait(false);
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
        return frame ?? throw new TimeoutException($"Timed out waiting for {Label(desired)}.");
    }

    private async Task<ImageFrame> WaitForPrestartAfterPreviewAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        StableStateTracker<ChallengeScreenState> prestartTracker = new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker = new(preset.StableDetections);
        ObservationWaitBudget budget = new(
            InitialPrestartTimeout,
            preset.StableDetections);
        bool teleportingSeen = false;
        ChallengeScreenState lastObservedState = ChallengeScreenState.None;
        while (budget.ShouldObserve(
                   prestartTracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            lastObservedState = match.State;
            budget.MarkObserved();
            if (!teleportingSeen &&
                match.State ==
                    ChallengeScreenState.Teleporting)
            {
                teleportingSeen = true;
                budget.ExtendSoftTimeout(
                    TeleportingPrestartTimeout);
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

        throw ChallengePrestartTimeoutPolicy.CreateException(
            teleportingSeen,
            lastObservedState);
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
            throw new RobloxSessionUnavailableException($"Roblox did not accept the required {width} by {height} client size (actual: {actual.Width} by {actual.Height}).");
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
            throw new RobloxSessionUnavailableException("Roblox no longer matches the detector pack client size.");
        }
        return _automation.CaptureClient(window);
    }

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new RobloxSessionUnavailableException("Windows could not focus Roblox.");
    }

    private static string FormatRemaining(TimeSpan remaining) => remaining.TotalHours >= 1
        ? $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m"
        : $"{Math.Max(0, (int)remaining.TotalMinutes)}m {Math.Max(0, remaining.Seconds):00}s";

    private static string ChallengeRoute(ChallengeType type, ChallengeMapId map) => $"{Label(type)} · {Label(map)}";

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
