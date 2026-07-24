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
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90);
        StableStateTracker<ChallengeScreenState> navigationTracker =
            new(preset.StableDetections);
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, preset.StableDetections));
        string? lastRecovery = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState? stableNavigation =
                navigationTracker.Update(match.State);
            (int X, int Y)? stableAction =
                actionTracker.Update(
                    IsChallengeNavigationAction(match.State)
                        ? match.State
                        : ChallengeScreenState.None,
                    MatchAction(match));
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
            if (match.State == ChallengeScreenState.GameModeSelector &&
                stableNavigation ==
                    ChallengeScreenState.GameModeSelector)
            {
                report("Navigation", 0, "Opening Challenges from the game-mode selector.", "game_mode_selector", match.Confidence);
                await ClickAsync(window, match.ActionX!.Value, match.ActionY!.Value, cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (match.State ==
                    ChallengeScreenState.PostMatchPreview &&
                stableAction is not null)
            {
                await ClickAsync(
                    window,
                    stableAction.Value.X,
                    stableAction.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(850, cancellationToken).ConfigureAwait(false);
                navigationTracker.Reset();
                actionTracker.Reset();
                continue;
            }
            if (match.State is ChallengeScreenState.Victory or ChallengeScreenState.Defeat)
            {
                await OpenPlayMenuAsync(window, preset, detector, playMenuKey, log, report, cancellationToken).ConfigureAwait(false);
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
                if (recovery == "lobby")
                {
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(detector.RecoveryState(candidate), "lobby", StringComparison.OrdinalIgnoreCase),
                        candidate => ChallengeScreenDetector.Detect(candidate).State == ChallengeScreenState.GameModeSelector,
                        (key, token) => _automation.TapLetterKeyAsync(window, key, token),
                        async (timeout, token) => await TryWaitForScreenAsync(
                            window,
                            preset,
                            detector,
                            ChallengeScreenState.GameModeSelector,
                            timeout,
                            report,
                            token).ConfigureAwait(false) is not null,
                        attempt => report("Navigation", 0, $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", recovery, null),
                        attempt => log($"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).", MacroEventLevel.Warning, recovery, null),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    (int x, int y) = detector.ActionFor(recovery, frame);
                    await ClickAsync(window, x, y, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(recovery == "disconnect" ? 5000 : 2200, cancellationToken).ConfigureAwait(false);
                }
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

            DateTimeOffset deadline = DateTimeOffset.UtcNow + verificationTimeout;
            StableStateTracker<ChallengeScreenState> tracker = new(stableDetections);
            ChallengeScreenMatch? last = null;
            while (DateTimeOffset.UtcNow < deadline)
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
                await Task.Delay(pollMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            attemptMissed?.Invoke(attempt, last);
        }

        throw new InvalidOperationException(
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
        return observation ?? throw new InvalidOperationException("Timed out waiting for the Challenge selector.");
    }

    private async Task<ChallengeSelectorObservation?> TryWaitForChallengeSelectorAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<ChallengeScreenState> tracker = new(preset.StableDetections);
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, preset.StableDetections));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(frame);
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

    private async Task ReturnFromPrestartAfterAlignmentFailureAsync(
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
        (int X, int Y)? changeMode =
            ChallengeScreenDetector.ActionFor(
                ChallengeScreenState.PostMatchPreview,
                party);
        if (changeMode is null)
        {
            throw new InvalidOperationException(
                "Change Gamemode could not be located after leaving the unstarted Challenge.");
        }
        await ClickAsync(
            window,
            changeMode.Value.X,
            changeMode.Value.Y,
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
            throw new InvalidOperationException(
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
            (timeout, token) => TryWaitForScreenAsync(
                window,
                preset,
                detector,
                ChallengeScreenState.PostMatchPreview,
                timeout,
                report,
                token),
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
