using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(90);

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
        StableStateTracker<string> recoveryTracker =
            new(stableDetections);
        StableStateTracker<StageScreenState> navigationTracker =
            new(stableDetections);
        StableNavigationActionTracker<StageScreenState>
            changeModeTracker =
                new(Math.Max(2, stableDetections));
        int playMenuAttempts = 0;
        string? lastRecovery = null;
        bool recovered = false;
        bool recoveryTransitionPending = false;
        StageNavigationTransactionState transaction = new();
        ObservationWaitBudget budget = new(
            RecoveryTimeout,
            Math.Max(2, stableDetections));
        while (budget.ShouldObserve(
                   navigationTracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate ||
                   changeModeTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current = StageScreenDetector.Detect(frame);
            string? recovery = detector.RecoveryState(frame);
            budget.MarkObserved();
            StageScreenState selectorAwareState =
                StageNavigationPolicy.ResolveGameModeSelectorState(
                    current.State,
                    recovery);
            StageScreenState? stableNavigation =
                navigationTracker.Update(selectorAwareState);
            string? stableRecovery = recoveryTracker.Update(
                IsRootRecovery(recovery) ? recovery : null);
            if (stableNavigation == StageScreenState.GameModeSelector)
                return recovered;
            if (stableRecovery is null &&
                stableNavigation is not
                (null or StageScreenState.None))
            {
                transaction.ObserveVerifiedState(
                    stableNavigation.Value.ToString());
                recoveryTransitionPending = false;
            }

            if (stableRecovery is not null)
            {
                if (!autoRecover)
                {
                    throw new StageRecoveryException(stableRecovery);
                }
                recovered = true;
                if (!string.Equals(
                    lastRecovery,
                    stableRecovery,
                    StringComparison.OrdinalIgnoreCase))
                {
                    lastRecovery = stableRecovery;
                    log?.Invoke(
                        $"Automatic {Label(mode)} recovery started from {RecoveryLabel(stableRecovery)}.",
                        MacroEventLevel.Warning,
                        stableRecovery,
                        null);
                }

                if (stableRecovery == "lobby")
                {
                    _fastNoAlign.ObserveLobby(window);
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(
                            detector.RecoveryState(candidate),
                            "lobby",
                            StringComparison.OrdinalIgnoreCase),
                        candidate =>
                            StageScreenDetector.Detect(candidate).State ==
                            StageScreenState.GameModeSelector,
                        (key, token) =>
                            _automation.TapLetterKeyAsync(
                                window,
                                key,
                                token),
                        (
                            timeout,
                            initialOpenObservation,
                            token) => TryWaitForStateAsync(
                            window,
                            StageScreenState.GameModeSelector,
                            timeout,
                            detector,
                            stableDetections,
                            token,
                            initialOpenObservation),
                        attempt => report?.Invoke(
                            "Recovery",
                            0,
                            $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            stableRecovery,
                            null),
                        attempt => log?.Invoke(
                            $"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            MacroEventLevel.Warning,
                            stableRecovery,
                            null),
                        cancellationToken).ConfigureAwait(false);
                    return recovered;
                }

                string recoveryActionLabel =
                    stableRecovery == "disconnect"
                        ? "Reconnect"
                        : "Return to Lobby";
                StageNavigationActionIdentity recoveryAction =
                    new(stableRecovery, recoveryActionLabel);
                transaction.ObserveVerified(recoveryAction);
                int attempt = transaction.BeginAttempt(recoveryAction, cancellationToken);
                report?.Invoke(
                    "Recovery",
                    0,
                    stableRecovery == "disconnect"
                        ? $"Disconnected. Sending Reconnect ({attempt}/{StageNavigationTransactionState.MaximumAttemptsPerAction})."
                        : $"AFK Chamber recognized. Sending Return to Lobby ({attempt}/{StageNavigationTransactionState.MaximumAttemptsPerAction}).",
                    stableRecovery,
                    null);
                (int x, int y) = detector.ActionFor(
                    stableRecovery,
                    frame);
                await ClickAsync(
                    window,
                    x,
                    y,
                    cancellationToken).ConfigureAwait(false);
                recoveryTransitionPending = true;
                await Task.Delay(
                    stableRecovery == "disconnect" ? 5000 : 2200,
                    cancellationToken).ConfigureAwait(false);
                recoveryTracker.Reset();
                navigationTracker.Reset();
                changeModeTracker.Reset();
                continue;
            }

            (int X, int Y)? changeMode =
                StageScreenDetector.PostMatchChangeModeAction(frame);
            (int X, int Y)? stableChangeMode =
                changeModeTracker.Update(
                    current.State ==
                        StageScreenState.PostMatchPreview
                        ? current.State
                        : StageScreenState.None,
                    changeMode);
            StageNavigationActionIdentity? stableTransactionAction =
                StageNavigationTransactionState.ForVerifiedNavigation(
                    stableNavigation,
                    stableChangeMode is not null);
            if (stableTransactionAction is { } verifiedAction)
                transaction.ObserveVerified(verifiedAction);
            GameModeHandoffCommand command =
                StageNavigationPolicy.SelectGameModeHandoffCommand(
                    stableNavigation ?? StageScreenState.None,
                    stableChangeMode is not null,
                    recoveryTransitionPending,
                    selectorAwareState ==
                        StageScreenState.GameModeSelector ||
                    changeModeTracker.HasPendingCandidate ||
                    transaction.ConfirmationPending);
            switch (command)
            {
                case GameModeHandoffCommand.Complete:
                    return recovered;
                case GameModeHandoffCommand.ChangeGamemode:
                    int changeModeAttempt =
                        transaction.BeginAttempt(
                            stableTransactionAction!.Value, cancellationToken);
                    report?.Invoke(
                        "Handoff",
                        0,
                        $"Leaving the completed {Label(mode)} party through Change Gamemode ({changeModeAttempt}/{StageNavigationTransactionState.MaximumAttemptsPerAction}).",
                        "stage_change_gamemode",
                        current.Confidence);
                    await ClickAsync(
                        window,
                        stableChangeMode!.Value.X,
                        stableChangeMode.Value.Y,
                        cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    navigationTracker.Reset();
                    changeModeTracker.Reset();
                    if (await TryWaitForStateAsync(
                        window,
                        StageScreenState.GameModeSelector,
                        NavigationTimeout,
                        detector,
                        stableDetections,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return recovered;
                    }
                    continue;
                case GameModeHandoffCommand.Back:
                    int backAttempt =
                        transaction.BeginAttempt(
                            stableTransactionAction!.Value, cancellationToken);
                    report?.Invoke(
                        "Handoff",
                        0,
                        $"Leaving the nested {Label(mode)} interface through Back ({backAttempt}/{StageNavigationTransactionState.MaximumAttemptsPerAction}).",
                        "stage_back",
                        current.Confidence);
                    (int backX, int backY) =
                        StageScreenDetector.SelectorBackAction;
                    await ClickAsync(
                        window,
                        backX,
                        backY,
                        cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    navigationTracker.Reset();
                    recoveryTracker.Reset();
                    changeModeTracker.Reset();
                    if (await TryWaitForStateAsync(
                        window,
                        StageScreenState.GameModeSelector,
                        NavigationTimeout,
                        detector,
                        stableDetections,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return recovered;
                    }
                    continue;
                case GameModeHandoffCommand.PressPlayKey:
                    if (playMenuAttempts >=
                        LobbyPlayNavigator.MaximumAttempts)
                    {
                        throw new PlayMenuBindingException(
                            char.ToUpperInvariant(playMenuKey));
                    }
                    playMenuAttempts++;
                    report?.Invoke(
                        "Navigation",
                        0,
                        playMenuAttempts == 1
                            ? $"Opening the Play menu with {playMenuKey}."
                            : $"Retrying the {playMenuKey} Play-menu key ({playMenuAttempts}/{LobbyPlayNavigator.MaximumAttempts}).",
                        "play_menu_key",
                        null);
                    Focus(window);
                    await _automation.TapLetterKeyAsync(
                        window,
                        playMenuKey,
                        cancellationToken).ConfigureAwait(false);
                    GameModeHandoffCommand? transition =
                        await TryWaitForPlayKeyTransitionAsync(
                            window,
                            detector,
                            stableDetections,
                            TimeSpan.FromSeconds(4),
                            cancellationToken).ConfigureAwait(false);
                    if (transition == GameModeHandoffCommand.Complete)
                    {
                        return recovered;
                    }
                    if (transition ==
                        GameModeHandoffCommand.ChangeGamemode)
                    {
                        playMenuAttempts = 0;
                    }
                    continue;
                case GameModeHandoffCommand.Wait:
                    await Task.Delay(180, cancellationToken).ConfigureAwait(false);
                    continue;
                default:
                    throw new InvalidOperationException(
                        "The stage handoff policy returned an unknown command.");
            }
        }

        StageScreenMatch last = StageScreenDetector.Detect(
            CaptureClient(window, detector));
        throw new TimeoutException(
            $"Timed out opening the Play menu. Last detected state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task<GameModeHandoffCommand?>
        TryWaitForPlayKeyTransitionAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int stableDetections,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        StableStateTracker<string> tracker = new(stableDetections);
        StableNavigationActionTracker<StageScreenState>
            actionTracker =
                new(Math.Max(2, stableDetections));
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(2, stableDetections));
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current =
                StageScreenDetector.Detect(frame);
            StageScreenState selectorAwareState =
                StageNavigationPolicy.ResolveGameModeSelectorState(
                    current.State,
                    detector.RecoveryState(frame));
            bool hasChangeMode =
                StageScreenDetector.PostMatchChangeModeAction(frame)
                is not null;
            (int X, int Y)? stableChangeMode =
                actionTracker.Update(
                    current.State ==
                        StageScreenState.PostMatchPreview
                        ? current.State
                        : StageScreenState.None,
                    StageScreenDetector.PostMatchChangeModeAction(
                        frame));
            GameModeHandoffCommand command =
                StageNavigationPolicy.SelectGameModeHandoffCommand(
                    selectorAwareState,
                    hasChangeMode &&
                    stableChangeMode is not null);
            string? candidate = command is
                GameModeHandoffCommand.Complete or
                GameModeHandoffCommand.ChangeGamemode
                ? command.ToString()
                : null;
            if (tracker.Update(candidate) is string stable)
            {
                return Enum.Parse<GameModeHandoffCommand>(stable);
            }
            budget.MarkObserved();
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<StageScreenMatch> WaitForStateAsync(
        RobloxWindow window,
        StageScreenState expected,
        TimeSpan timeout,
        IDetectorPack detector,
        int stableDetections,
        CancellationToken cancellationToken,
        bool initialExpectedObservation = false)
    {
        StageScreenMatch last = new(StageScreenState.None, 0);
        StableStateTracker<string> expectedTracker =
            new(stableDetections);
        StableNavigationActionTracker<string> actionTracker =
            new(Math.Max(2, stableDetections));
        StableStateTracker<string> recoveryTracker =
            new(stableDetections);
        ObservationWaitBudget budget = new(
            timeout,
            RequiresStableAction(expected)
                ? Math.Max(2, stableDetections)
                : stableDetections);
        if (initialExpectedObservation &&
            !RequiresStableAction(expected))
        {
            _ = expectedTracker.Update(
                expected.ToString());
            budget.MarkObserved();
        }
        while (budget.ShouldObserve(
                   expectedTracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            last = StageScreenDetector.Detect(frame);
            string? recovery = detector.RecoveryState(frame);
            StageScreenState observedState =
                expected == StageScreenState.GameModeSelector
                    ? StageNavigationPolicy
                        .ResolveGameModeSelectorState(
                            last.State,
                            recovery)
                    : last.State;
            (int X, int Y)? action =
                ExpectedNavigationAction(expected, last, frame);
            string? candidate =
                StageNavigationPolicy.MatchesExpectedState(
                    expected,
                    observedState,
                    expected != StageScreenState.PreviewReady ||
                    action is not null)
                    ? expected.ToString()
                    : null;
            if (RequiresStableAction(expected))
            {
                (int X, int Y)? stableAction =
                    actionTracker.Update(candidate, action);
                if (stableAction is not null)
                {
                    return new StageScreenMatch(
                        expected,
                        last.Confidence,
                        stableAction.Value.X,
                        stableAction.Value.Y);
                }
            }
            else if (expectedTracker.Update(candidate) is not null)
            {
                return last;
            }

            if (recoveryTracker.Update(
                    IsRootRecovery(recovery) ? recovery : null)
                is string stableRecovery)
            {
                throw new StageRecoveryException(stableRecovery);
            }
            budget.MarkObserved();
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Timed out waiting for {expected}. Last state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task<bool> TryWaitForStateAsync(
        RobloxWindow window,
        StageScreenState expected,
        TimeSpan timeout,
        IDetectorPack detector,
        int stableDetections,
        CancellationToken cancellationToken,
        bool initialExpectedObservation = false)
    {
        try
        {
            await WaitForStateAsync(
                window,
                expected,
                timeout,
                detector,
                stableDetections,
                cancellationToken,
                initialExpectedObservation).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static bool RequiresStableAction(
        StageScreenState expected) =>
        expected is
            StageScreenState.StoryDetail or
            StageScreenState.RaidDetail or
            StageScreenState.PreviewReady or
            StageScreenState.Prestart;

    private static (int X, int Y)? ExpectedNavigationAction(
        StageScreenState expected,
        StageScreenMatch match,
        ImageFrame frame) =>
        expected == StageScreenState.PreviewReady
            ? StageScreenDetector.PreviewStartAction(frame)
            : match.ActionX is int x && match.ActionY is int y
                ? (x, y)
                : null;
}
