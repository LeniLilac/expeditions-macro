using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    internal const int SelectorBackMaximumAttempts = 3;

    private static readonly (int X, int Y) ChallengeDetailBackAction = (308, 437);

    private async Task EnsureChallengeListAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        Action recovered,
        CancellationToken cancellationToken)
    {
        StableStateTracker<ChallengeScreenState> navigationTracker =
            new(preset.StableDetections);
        ChallengeNavigationInputGate inputGate =
            new(preset.StableDetections);
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(90),
            Math.Max(2, preset.StableDetections));
        string? lastRecovery = null;
        bool navigationTransitionPending = false;
        while (budget.ShouldObserve(
                   navigationTracker.HasPendingCandidate ||
                   inputGate.HasPendingCandidate ||
                   navigationTransitionPending))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            string? recovery = detector.RecoveryState(frame);
            budget.MarkObserved();
            ChallengeScreenState? stableNavigation =
                navigationTracker.Update(match.State);
            ChallengeNavigationInputAttempt? inputAttempt =
                inputGate.Observe(
                    match,
                    recovery,
                    detector,
                    frame,
                    cancellationToken);
            if (stableNavigation is
                ChallengeScreenState.ChallengeList or
                ChallengeScreenState.ChallengeListUnavailable)
            {
                return;
            }
            if (stableNavigation is
                ChallengeScreenState.ChallengeAvailable or
                ChallengeScreenState.ChallengeCooldown)
            {
                await ReturnToChallengeSelectorWithVerificationAsync(
                    preset.StableDetections,
                    token => ClickAsync(
                        window,
                        ChallengeDetailBackAction.X,
                        ChallengeDetailBackAction.Y,
                        token),
                    () => ChallengeScreenDetector.Detect(CaptureClient(window, detector)),
                    preset.PollMilliseconds,
                    TimeSpan.FromSeconds(5),
                    SelectorBackMaximumAttempts,
                    attempt => report(
                        "Navigation",
                        0,
                        attempt == 1
                            ? "Returning from the open Challenge detail to the selector."
                            : $"The Challenge detail is still open; retrying Back ({attempt}/{SelectorBackMaximumAttempts}).",
                        match.State.ToString(),
                        match.Confidence),
                    (attempt, observed) => log(
                        $"Challenge Back did not reach the selector (attempt {attempt}/{SelectorBackMaximumAttempts}).",
                        MacroEventLevel.Warning,
                        observed?.State.ToString(),
                        observed?.Confidence),
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            if (inputAttempt?.Owner ==
                ChallengeNavigationInputOwner.GameModeSelector)
            {
                report("Navigation", 0, "Opening Challenges from the game-mode selector.", "game_mode_selector", match.Confidence);
                cancellationToken.ThrowIfCancellationRequested();
                await ClickAsync(
                    window,
                    inputAttempt.Value.X,
                    inputAttempt.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                navigationTracker.Reset();
                navigationTransitionPending = true;
                continue;
            }
            if (inputAttempt?.Owner ==
                ChallengeNavigationInputOwner.PostMatchPreview)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ClickAsync(
                    window,
                    inputAttempt.Value.X,
                    inputAttempt.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                navigationTracker.Reset();
                navigationTransitionPending = true;
                continue;
            }
            if (match.State is ChallengeScreenState.Victory or ChallengeScreenState.Defeat)
            {
                await OpenPlayMenuAsync(window, preset, detector, playMenuKey, log, report, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (recovery is "afk" or "disconnect" or "lobby")
            {
                if (!preset.AutoRecover) throw new InvalidOperationException($"{Label(recovery)} was recognized, but automatic recovery is disabled.");
                if (!string.Equals(lastRecovery, recovery, StringComparison.OrdinalIgnoreCase))
                {
                    recovered();
                    lastRecovery = recovery;
                    log($"Automatic Challenge recovery started from {Label(recovery)}.", MacroEventLevel.Warning, recovery, null);
                }
                if (recovery == "lobby")
                {
                    _fastNoAlign.ObserveLobby(window);
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(detector.RecoveryState(candidate), "lobby", StringComparison.OrdinalIgnoreCase),
                        candidate => ChallengeScreenDetector.Detect(candidate).State == ChallengeScreenState.GameModeSelector,
                        (key, token) => _automation.TapLetterKeyAsync(window, key, token),
                        async (
                            timeout,
                            initialOpenObservation,
                            token) => await TryWaitForScreenAsync(
                            window,
                            preset,
                            detector,
                            ChallengeScreenState.GameModeSelector,
                            timeout,
                            report,
                            token,
                            initialOpenObservation)
                            .ConfigureAwait(false) is not null,
                        attempt => report("Navigation", 0, $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", recovery, null),
                        attempt => log($"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", MacroEventLevel.Warning, recovery, null),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ChallengeNavigationInputOwner expectedOwner =
                        recovery == "disconnect"
                            ? ChallengeNavigationInputOwner
                                .DisconnectRecovery
                            : ChallengeNavigationInputOwner
                                .AfkRecovery;
                    if (inputAttempt?.Owner != expectedOwner)
                    {
                        report(
                            "Navigation",
                            0,
                            $"Confirming the {Label(recovery)} action before recovery input.",
                            recovery,
                            null);
                        await Task.Delay(
                            preset.PollMilliseconds,
                            cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await ClickAsync(
                        window,
                        inputAttempt.Value.X,
                        inputAttempt.Value.Y,
                        cancellationToken).ConfigureAwait(false);
                    await Task.Delay(recovery == "disconnect" ? 5000 : 2200, cancellationToken).ConfigureAwait(false);
                    navigationTracker.Reset();
                    navigationTransitionPending = true;
                }
                continue;
            }
            if (recovery == "play")
            {
                // The shared Play detector identifies the game-mode selector. The
                // Expeditions action attached to that detector is intentionally not
                // used here; Challenge has its own fixed tile.
                if (inputAttempt?.Owner !=
                    ChallengeNavigationInputOwner.GameModeSelector)
                {
                    report(
                        "Navigation",
                        0,
                        "Confirming the game-mode selector before opening Challenges.",
                        recovery,
                        null);
                    await Task.Delay(
                        preset.PollMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await ClickAsync(
                    window,
                    inputAttempt.Value.X,
                    inputAttempt.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(900, cancellationToken).ConfigureAwait(false);
                navigationTracker.Reset();
                navigationTransitionPending = true;
                continue;
            }

            report("Navigation", 0, "Waiting for a Challenge navigation screen.", null, null);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        if (inputGate.ExhaustedError is { } exhausted)
        {
            throw exhausted;
        }
        throw new TimeoutException(
            "Challenge navigation did not reach the selector within 90 seconds.");
    }

    internal static async Task<ChallengeScreenMatch> ReturnToChallengeSelectorWithVerificationAsync(
        int stableDetections,
        Func<CancellationToken, Task> clickBack,
        Func<ChallengeScreenMatch> observe,
        int pollMilliseconds,
        TimeSpan verificationTimeout,
        int maximumAttempts,
        Action<int>? attemptStarted,
        Action<int, ChallengeScreenMatch?>? attemptMissed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clickBack);
        ArgumentNullException.ThrowIfNull(observe);
        if (stableDetections < 1) throw new ArgumentOutOfRangeException(nameof(stableDetections));
        if (pollMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(pollMilliseconds));
        if (verificationTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(verificationTimeout));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attemptStarted?.Invoke(attempt);
            await clickBack(cancellationToken).ConfigureAwait(false);

            StableStateTracker<ChallengeScreenState> tracker = new(stableDetections);
            ObservationWaitBudget budget = new(
                verificationTimeout,
                stableDetections);
            ChallengeScreenMatch? last = null;
            while (budget.ShouldObserve(
                       tracker.HasPendingCandidate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                last = observe();
                ChallengeScreenState candidate = last.State is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable
                    ? last.State
                    : ChallengeScreenState.None;
                ChallengeScreenState? stable = tracker.Update(candidate);
                if (stable is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable)
                {
                    return last with { State = stable.Value };
                }
                budget.MarkObserved();
                await Task.Delay(pollMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            attemptMissed?.Invoke(attempt, last);
        }

        throw new RobloxUiUnavailableException(
            $"The Challenge detail remained open after {maximumAttempts} verified Back attempts.");
    }

    private async Task<ChallengeSelectorObservation> WaitForChallengeSelectorAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        ChallengeSelectorObservation? observation = await TryWaitForChallengeSelectorAsync(
            window,
            preset,
            detector,
            timeout,
            report,
            cancellationToken).ConfigureAwait(false);
        return observation ?? throw new TimeoutException("Timed out waiting for the Challenge selector.");
    }

    private async Task<ChallengeSelectorObservation?> TryWaitForChallengeSelectorAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        StableStateTracker<ChallengeScreenState> tracker = new(preset.StableDetections);
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, preset.StableDetections));
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(2, preset.StableDetections));
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            budget.MarkObserved();
            ChallengeScreenState candidate = match.State is ChallengeScreenState.ChallengeList or ChallengeScreenState.ChallengeListUnavailable
                ? match.State
                : ChallengeScreenState.None;
            ChallengeScreenState? stable = tracker.Update(candidate);
            (int X, int Y)? stableAction =
                actionTracker.Update(
                    candidate is
                        ChallengeScreenState.ChallengeList or
                        ChallengeScreenState.ChallengeListUnavailable
                        ? candidate
                        : ChallengeScreenState.None,
                    MatchAction(match));
            if ((stable is
                    ChallengeScreenState.ChallengeList or
                    ChallengeScreenState.ChallengeListUnavailable) &&
                stableAction is not null)
            {
                return new ChallengeSelectorObservation(
                    frame,
                    match with
                    {
                        State = stable.Value,
                        ActionX = stableAction.Value.X,
                        ActionY = stableAction.Value.Y,
                    });
            }
            if (match.State != ChallengeScreenState.None) report("Waiting", 0, $"Detected {Label(match.State)}.", match.State.ToString(), match.Confidence);
            await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task ReturnFromPrestartAfterPreparationFailureAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        report(
            "Recovery",
            0,
            "Leaving the unstarted match through Play and returning to the Challenge selector.",
            "camera_alignment_recovery",
            null);
        ImageFrame party = await OpenPlayMenuAsync(
            window,
            preset,
            detector,
            playMenuKey,
            log: null,
            report,
            cancellationToken).ConfigureAwait(false);
        ChallengeScreenMatch changeMode =
            await RequireStableLiveActionAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.PostMatchPreview,
                    party,
                    "Change Gamemode could not be located after leaving the unstarted Challenge.",
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        await ClickAsync(
            window,
            changeMode.ActionX!.Value,
            changeMode.ActionY!.Value,
            cancellationToken).ConfigureAwait(false);
        ImageFrame modes = await WaitForScreenAsync(
            window,
            preset,
            detector,
            ChallengeScreenState.GameModeSelector,
            TimeSpan.FromSeconds(12),
            report,
            cancellationToken).ConfigureAwait(false);
        (int X, int Y)? challenge =
            ChallengeScreenDetector.ActionFor(
                ChallengeScreenState.GameModeSelector,
                modes);
        if (challenge is null)
        {
            throw new RobloxUiUnavailableException(
                "Challenges could not be located after leaving the unstarted match.");
        }
        await ClickAsync(
            window,
            challenge.Value.X,
            challenge.Value.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForChallengeSelectorAsync(
            window,
            preset,
            detector,
            TimeSpan.FromSeconds(12),
            report,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<ImageFrame> OpenPlayMenuAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        Action<string, MacroEventLevel, string?, double?>? log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken) =>
        PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey,
            () => CaptureClient(window, detector),
            (key, token) =>
                _automation.TapLetterKeyAsync(window, key, token),
            (initialFrame, timeout, token) => TryWaitForScreenAsync(
                window,
                preset,
                detector,
                ChallengeScreenState.PostMatchPreview,
                timeout,
                report,
                token,
                initialFrame: initialFrame),
            attempt => report(
                "Return",
                85,
                attempt == 1
                    ? $"Opening the Play menu with {playMenuKey}."
                    : $"Retrying the {playMenuKey} Play-menu key ({attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                "play_menu_key",
                null),
            attempt => log?.Invoke(
                $"The {playMenuKey} Play-menu key did not open navigation (attempt {attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                MacroEventLevel.Warning,
                "play_menu_key",
                null),
            cancellationToken);

    private sealed record ChallengeSelectorObservation(ImageFrame Frame, ChallengeScreenMatch Match);
}
