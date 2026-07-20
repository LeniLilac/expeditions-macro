using System.Diagnostics;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed class ChallengeMacroRunner : IGameModeWorkflow
{
    private readonly IRobloxAutomation _automation;
    private readonly CameraAlignmentEngine _camera;
    private readonly PlacementService _placements;
    private readonly IDiscordNotifier _discord;
    private readonly object _notificationGate = new();
    private readonly HashSet<Task> _pendingNotifications = [];

    public ChallengeMacroRunner(
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

    public string ModeId => "challenges";

    public async Task RunAsync(
        ChallengePreset preset,
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> mapModels,
        IDetectorPack detector,
        string webhookUrl,
        Func<DateTimeOffset, CancellationToken, Task>? idleWorkflow = null,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        Action<ChallengeRunSummary>? summaryChanged = null,
        CancellationToken cancellationToken = default)
    {
        preset.ValidateReady();
        ValidateRuntimeModels(preset, mapModels, detector.Manifest);
        RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        WindowBounds original = _automation.GetWindowBounds(window);
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

        Write($"Using Roblox window '{window.Title}'.");
        PublishSummary();
        try
        {
            Focus(window);
            await EnsureClientSizeAsync(window, detector.Manifest.ClientWidth, detector.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
            await EnsureChallengeListAsync(window, preset, detector, Write, Report, () => { recoveries++; PublishSummary(); }, cancellationToken).ConfigureAwait(false);
            string enabledTypes = string.Join(", ", preset.EnabledTypes.Select(Label));
            QueueNotification(
                webhookUrl,
                "started",
                $"Monitoring {enabledTypes} Challenges on the global 30-minute rotation.",
                screenshot: null,
                runtime.Elapsed,
                victories,
                defeats,
                mapNumber: 0,
                route: "Regular Challenge rotation",
                Write);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool newEpoch = rotation.Advance(DateTimeOffset.Now);
                if (newEpoch) Write($"Challenge reset epoch is now {rotation.Epoch:HH:mm zzz}. Attempts were cleared.");

                if (rotation.DailyLimitUntilUtc is DateTimeOffset dailyUntil && DateTimeOffset.UtcNow < dailyUntil)
                {
                    waitingUntil = dailyUntil;
                    PublishSummary();
                    await WaitUntilAsync(window, preset, detector, dailyUntil, dailyLimit: true, idleWorkflow, Write, Report, cancellationToken).ConfigureAwait(false);
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
                    await EnsureChallengeListAsync(window, preset, detector, Write, Report, () => { recoveries++; PublishSummary(); }, cancellationToken).ConfigureAwait(false);
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
                    ImageFrame listFrame = selector.Frame;
                    ChallengeMapId map = await RecognizeMapAsync(window, preset, detector, type, listFrame, Write, cancellationToken).ConfigureAwait(false);
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
                            webhookUrl,
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
                        QueueNotification(
                            webhookUrl,
                            "recovery",
                            recoveryDetail,
                            TryCaptureClient(window, detector),
                            runtime.Elapsed,
                            victories,
                            defeats,
                            (int)map,
                            ChallengeRoute(type, map),
                            Write);
                        await EnsureChallengeListAsync(window, preset, detector, Write, Report, () => { }, cancellationToken).ConfigureAwait(false);
                        ranChallenge = true;
                        break;
                    }
                    victories += terminal.Victories;
                    defeats += terminal.Defeats;
                    completed++;
                    rotation.MarkAttempted(type);
                    PublishSummary();
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
                    waitDetail = $"{waitDetail} Waiting for the next global reset at {waitUntil:HH:mm} UTC.";
                    Write(waitDetail);
                }

                waitingUntil = waitUntil;
                PublishSummary();
                QueueNotification(
                    webhookUrl,
                    "waiting",
                    waitDetail,
                    screenshot: null,
                    runtime.Elapsed,
                    victories,
                    defeats,
                    mapNumber: 0,
                    route: "Regular Challenge rotation",
                    Write);
                await WaitUntilAsync(window, preset, detector, waitUntil, dailyLimit, idleWorkflow, Write, Report, cancellationToken).ConfigureAwait(false);
                waitingUntil = null;
                PublishSummary();
            }
        }
        finally
        {
            try
            {
                _automation.RestoreWindowBounds(window, original);
                Write("Restored the original Roblox window bounds.");
            }
            catch (Exception error)
            {
                Write($"Could not restore the original Roblox window bounds: {error.Message}", MacroEventLevel.Warning);
            }
            await FlushNotificationsAsync(Write).ConfigureAwait(false);
        }
    }

    private async Task<ChallengeTerminal> RunSelectedChallengeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeType type,
        ChallengeMapId map,
        ChallengeMapRuntimeModels models,
        IDetectorPack detector,
        string webhookUrl,
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
        QueueNotification(
            webhookUrl,
            "attempt",
            $"Starting the {Label(type)} Challenge on {Label(map)}.",
            screenshot: null,
            runtime.Elapsed,
            priorVictories,
            priorDefeats,
            (int)map,
            ChallengeRoute(type, map),
            log);

        while (true)
        {
            ImageFrame prestart = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.Prestart, TimeSpan.FromSeconds(35), report, cancellationToken).ConfigureAwait(false);
            report("Camera preparation", 20, $"Preparing {Label(map)} for {Label(type)}.", "prestart", null);
            await PrepareCameraAsync(window, preset, models.Camera, report, log, cancellationToken).ConfigureAwait(false);
            if (models.PrestartPlacement is not null)
            {
                report("Placement", 45, "Placing the before-start units.", null, null);
                await PlaceAsync(window, preset, models.PrestartPlacement, log, cancellationToken).ConfigureAwait(false);
            }

            prestart = CaptureClient(window, detector);
            (int X, int Y)? start = ChallengeScreenDetector.ActionFor(ChallengeScreenState.Prestart, prestart);
            if (start is null) throw new InvalidOperationException("The Challenge Start Game button disappeared before it could be clicked.");
            await ClickAsync(window, start.Value.X, start.Value.Y, cancellationToken).ConfigureAwait(false);
            await Task.Delay(2200, cancellationToken).ConfigureAwait(false);
            MatchTerminal terminal = await MonitorMatchAsync(window, preset, profile, models, detector, log, report, cancellationToken).ConfigureAwait(false);
            if (terminal.State == ChallengeScreenState.Victory)
            {
                victories++;
                string detail = $"{Label(type)} on {Label(map)} ended in Victory.";
                log(detail, MacroEventLevel.Success, "victory", terminal.Confidence);
                QueueNotification(
                    webhookUrl,
                    "victory",
                    detail,
                    terminal.Frame,
                    runtime.Elapsed,
                    priorVictories + victories,
                    priorDefeats + defeats,
                    (int)map,
                    ChallengeRoute(type, map),
                    log);
                await CloseTerminalAndReturnAsync(window, preset, detector, terminal.Frame, report, cancellationToken).ConfigureAwait(false);
                return new ChallengeTerminal(victories, defeats);
            }

            defeats++;
            bool willRetry = retry < preset.DefeatRetries;
            string defeatDetail = willRetry
                ? $"{Label(type)} on {Label(map)} ended in Defeat. Retry {retry + 1} of {preset.DefeatRetries} will start."
                : $"{Label(type)} on {Label(map)} ended in Defeat. The retry limit was reached.";
            log(defeatDetail, MacroEventLevel.Warning, "defeat", terminal.Confidence);
            QueueNotification(
                webhookUrl,
                "defeat",
                defeatDetail,
                terminal.Frame,
                runtime.Elapsed,
                priorVictories + victories,
                priorDefeats + defeats,
                (int)map,
                ChallengeRoute(type, map),
                log);
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
            await CloseTerminalAndReturnAsync(window, preset, detector, terminal.Frame, report, cancellationToken).ConfigureAwait(false);
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
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CloseTerminalAndReturnAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ImageFrame terminal,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        (int closeX, int closeY) = ChallengeScreenDetector.TerminalCloseAction(terminal);
        await ClickAsync(window, closeX, closeY, cancellationToken).ConfigureAwait(false);
        await Task.Delay(900, cancellationToken).ConfigureAwait(false);
        (int playX, int playY) = ChallengeScreenDetector.OpenPlayAction();
        report("Return", 85, "Opening Play from the match HUD.", null, null);
        await ClickAsync(window, playX, playY, cancellationToken).ConfigureAwait(false);
        ImageFrame party = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.PostMatchPreview, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
        (int X, int Y)? changeMode = ChallengeScreenDetector.ActionFor(ChallengeScreenState.PostMatchPreview, party);
        if (changeMode is null) throw new InvalidOperationException("Change Gamemode could not be located after the match.");
        await ClickAsync(window, changeMode.Value.X, changeMode.Value.Y, cancellationToken).ConfigureAwait(false);
        ImageFrame modes = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.GameModeSelector, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
        (int X, int Y)? challenge = ChallengeScreenDetector.ActionFor(ChallengeScreenState.GameModeSelector, modes);
        await ClickAsync(window, challenge!.Value.X, challenge.Value.Y, cancellationToken).ConfigureAwait(false);
        await WaitForChallengeSelectorAsync(window, preset, detector, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitUntilAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        DateTimeOffset untilUtc,
        bool dailyLimit,
        Func<DateTimeOffset, CancellationToken, Task>? idleWorkflow,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        if (preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions && idleWorkflow is not null)
        {
            report("Waiting", 0, dailyLimit ? "Daily Challenge limits reached. Running Expeditions until midnight UTC." : "Challenges complete. Running Expeditions until the next reset.", null, null);
            await PrepareExpeditionsIdleHandoffAsync(window, preset, detector, log, report, cancellationToken).ConfigureAwait(false);
            await idleWorkflow(untilUtc, cancellationToken).ConfigureAwait(false);
            await ReturnFromIdleModeAsync(window, preset, detector, report, cancellationToken).ConfigureAwait(false);
            return;
        }

        while (DateTimeOffset.UtcNow < untilUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = untilUtc - DateTimeOffset.UtcNow;
            string message = dailyLimit
                ? $"Daily limit reached. Checking again after midnight UTC in {FormatRemaining(remaining)}."
                : $"Waiting for the next Challenge reset in {FormatRemaining(remaining)}.";
            report("Waiting", 0, message, dailyLimit ? "daily_limit" : "cooldown", null);
            await Task.Delay(remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        log("Challenge wait finished. Rechecking the selector.", MacroEventLevel.Information, null, null);
    }

    private async Task PrepareExpeditionsIdleHandoffAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ChallengeSelectorObservation selector = await WaitForChallengeSelectorAsync(
                window,
                preset,
                detector,
                TimeSpan.FromSeconds(8),
                report,
                cancellationToken).ConfigureAwait(false);
            if (selector.Match.ActionX is not int closeX || selector.Match.ActionY is not int closeY)
            {
                throw new InvalidOperationException("The Challenge selector close button could not be located.");
            }

            report(
                "Handoff",
                0,
                attempt == 1
                    ? "Closing the Challenge selector before starting Expeditions."
                    : $"Challenge selector is still open; retrying its close button ({attempt}/3).",
                selector.Match.State.ToString(),
                selector.Match.Confidence);
            await ClickAsync(window, closeX, closeY, cancellationToken).ConfigureAwait(false);
            ImageFrame? gameModes = await TryWaitForScreenAsync(
                window,
                preset,
                detector,
                ChallengeScreenState.GameModeSelector,
                TimeSpan.FromSeconds(5),
                report,
                cancellationToken).ConfigureAwait(false);
            if (gameModes is not null)
            {
                log(
                    "Challenge selector closed. Handing navigation to the Expeditions recovery flow.",
                    MacroEventLevel.Success,
                    "game_mode_selector",
                    ChallengeScreenDetector.ScoreStates(gameModes)[ChallengeScreenState.GameModeSelector]);
                return;
            }

            log(
                $"Challenge selector did not close (attempt {attempt}/3).",
                MacroEventLevel.Warning,
                selector.Match.State.ToString(),
                selector.Match.Confidence);
        }

        throw new InvalidOperationException(
            "The Challenge selector remained open after three focused close attempts, so Expeditions was not started.");
    }

    private async Task ReturnFromIdleModeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        report("Return", 0, "Returning from Expeditions to the Challenge selector.", null, null);
        (int playX, int playY) = ChallengeScreenDetector.OpenPlayAction();
        await ClickAsync(window, playX, playY, cancellationToken).ConfigureAwait(false);
        ImageFrame party = await WaitForScreenAsync(window, preset, detector, ChallengeScreenState.PostMatchPreview, TimeSpan.FromSeconds(12), report, cancellationToken).ConfigureAwait(false);
        (int X, int Y)? change = ChallengeScreenDetector.ActionFor(ChallengeScreenState.PostMatchPreview, party);
        if (change is null) throw new InvalidOperationException("Change Gamemode could not be located after the idle Expeditions run.");
        await ClickAsync(window, change.Value.X, change.Value.Y, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureChallengeListAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        Action recovered,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90);
        string? lastRecovery = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            if (match.State is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable) return;
            if (match.State is ChallengeScreenState.ChallengeAvailable or ChallengeScreenState.ChallengeCooldown)
            {
                await ClickAsync(window, 308, 437, cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (match.State == ChallengeScreenState.GameModeSelector)
            {
                report("Navigation", 0, "Opening Challenges from the game-mode selector.", "game_mode_selector", match.Confidence);
                await ClickAsync(window, match.ActionX!.Value, match.ActionY!.Value, cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (match.State == ChallengeScreenState.PostMatchPreview)
            {
                await ClickAsync(window, match.ActionX!.Value, match.ActionY!.Value, cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (match.State is ChallengeScreenState.Victory or ChallengeScreenState.Defeat)
            {
                (int x, int y) = ChallengeScreenDetector.TerminalCloseAction(frame);
                await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string? recovery = detector.RecoveryState(frame);
            if (recovery is "afk" or "disconnect" or "lobby")
            {
                if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(recovery)} was recognized, but automatic recovery is disabled.");
                if (!string.Equals(lastRecovery, recovery, StringComparison.OrdinalIgnoreCase))
                {
                    recovered();
                    lastRecovery = recovery;
                    log($"Automatic Challenge recovery started from {Label(recovery)}.", MacroEventLevel.Warning, recovery, null);
                }
                (int x, int y) = detector.ActionFor(recovery, frame);
                await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                await Task.Delay(recovery == "disconnect" ? 5000 : 2200, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (recovery == "play")
            {
                // The shared Play detector identifies the game-mode selector. The
                // Expeditions action attached to that detector is intentionally not
                // used here; Challenge has its own fixed tile.
                await ClickAsync(window, 480, 205, cancellationToken).ConfigureAwait(false);
                await Task.Delay(900, cancellationToken).ConfigureAwait(false);
                continue;
            }

            report("Navigation", 0, "Waiting for a Challenge navigation screen.", null, null);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Challenge navigation did not reach the selector within 90 seconds.");
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
        ImageFrame initial,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ImageFrame frame = initial;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(6);
        int retryMilliseconds = Math.Clamp(preset.PollMilliseconds / 2, 180, 350);
        while (true)
        {
            ChallengeMapId? map = detector.ChallengeMapForType(frame, type);
            if (map is not null)
            {
                log($"{Label(type)} map recognized as {Label(map.Value)}.", MacroEventLevel.Success, "challenge_map", null);
                return map.Value;
            }
            if (DateTimeOffset.UtcNow >= deadline) break;
            await Task.Delay(retryMilliseconds, cancellationToken).ConfigureAwait(false);
            frame = CaptureClient(window, detector);
        }
        throw new InvalidOperationException($"The map thumbnail for {Label(type)} could not be recognized. Add this selector capture to the Challenge detector dataset before running automation.");
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

    private async Task<ChallengeSelectorObservation> WaitForChallengeSelectorAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
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
            ChallengeScreenState candidate = match.State is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable
                ? match.State
                : ChallengeScreenState.None;
            ChallengeScreenState? stable = tracker.Update(candidate);
            if (stable is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable)
            {
                return new ChallengeSelectorObservation(frame, match with { State = stable.Value });
            }
            if (match.State != ChallengeScreenState.None) report("Waiting", 0, $"Detected {Label(match.State)}.", match.State.ToString(), match.Confidence);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Timed out waiting for the Challenge selector.");
    }

    private async Task PrepareCameraAsync(
        RobloxWindow window,
        ChallengePreset preset,
        CameraModel model,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.MoveCursorToClientCenterAsync(window, cancellationToken).ConfigureAwait(false);
        await _automation.ZoomOutFullyAsync(window, preset.ZoomTicks, cancellationToken).ConfigureAwait(false);
        await _automation.TapLeftControlAsync(window, cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            await _automation.DragCameraAsync(window, 0, preset.PitchDragPixels, 90, cancellationToken).ConfigureAwait(false);
            await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            double score = await _camera.AlignAsync(
                model,
                window,
                restoreWindow: false,
                manageShiftLock: false,
                progress: new Progress<MacroProgress>(value => report(value.Phase, value.Percent, value.Message, value.DetectedState, value.Confidence)),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            log($"Camera alignment finished at {score:P0} confidence.", MacroEventLevel.Success, null, score);
        }
        finally
        {
            try
            {
                Focus(window);
                await _automation.TapLeftControlAsync(window, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // The outer window restoration still proceeds.
            }
        }
    }

    private Task PlaceAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel model,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            model,
            model.Steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            preset.UnitKeyHoldMilliseconds,
            preset.UnitSelectDelayMilliseconds,
            stepSent: null,
            status: message => log(message, MacroEventLevel.Information, null, null),
            restoreWindow: false,
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

    private void QueueNotification(
        string webhookUrl,
        string eventName,
        string detail,
        ImageFrame? screenshot,
        TimeSpan runtime,
        int victories,
        int defeats,
        int mapNumber,
        string route,
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
            MapNumber = mapNumber,
            Difficulty = 0,
            Detail = detail,
            MacroName = "Challenge Macro",
            Route = route,
            AttachmentPrefix = "challenge",
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

    private sealed record ChallengeSelectorObservation(ImageFrame Frame, ChallengeScreenMatch Match);

    private sealed class ChallengeRecoveryException : Exception
    {
        public ChallengeRecoveryException(string state) : base($"Challenge recovery screen recognized: {state}.")
        {
            State = state;
        }

        public string State { get; }
    }
}
