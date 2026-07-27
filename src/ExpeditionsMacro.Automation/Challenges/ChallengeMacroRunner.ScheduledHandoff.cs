using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task ReturnFromTerminalAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        CancellationToken cancellationToken)
    {
        ImageFrame party =
            await OpenPlayMenuAsync(
                window,
                preset,
                detector,
                playMenuKey,
                log: null,
                report,
                cancellationToken)
            .ConfigureAwait(false);
        ChallengeScreenMatch changeMode =
            await RequireStableLiveActionAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.PostMatchPreview,
                    party,
                    "Change Gamemode could not be located after the match.",
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        await ClickAsync(
            window,
            changeMode.ActionX!.Value,
            changeMode.ActionY!.Value,
            cancellationToken).ConfigureAwait(false);
        ImageFrame modes =
            await WaitForScreenAsync(
                window,
                preset,
                detector,
                ChallengeScreenState
                    .GameModeSelector,
                TimeSpan.FromSeconds(12),
                report,
                cancellationToken)
            .ConfigureAwait(false);
        (int X, int Y)? challenge =
            ChallengeScreenDetector.ActionFor(
                ChallengeScreenState
                    .GameModeSelector,
                modes);
        if (challenge is null)
        {
            throw new RobloxUiUnavailableException(
                "Challenge could not be located in the game-mode selector.");
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

    private async Task RetryDefeatAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ImageFrame initialFrame,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        CancellationToken cancellationToken)
    {
        ChallengeScreenMatch retry =
            await RequireStableLiveActionAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.Defeat,
                    initialFrame,
                    "Repeat Stage could not be located after the Challenge defeat.",
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        await ClickAsync(
            window,
            retry.ActionX!.Value,
            retry.ActionY!.Value,
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(
            3500,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChallengeScreenMatch>
        RequireStableLiveActionAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeScreenState desired,
        ImageFrame initialFrame,
        string failureMessage,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        CancellationToken cancellationToken)
    {
        (ImageFrame Frame, ChallengeScreenMatch Match)? observation =
            await TryWaitForActionAsync(
                    window,
                    preset,
                    detector,
                    desired,
                    TimeSpan.FromSeconds(12),
                    report,
                    cancellationToken,
                    (
                        initialFrame,
                        ChallengeScreenDetector
                            .Detect(initialFrame)))
                .ConfigureAwait(false);
        return observation?.Match ??
            throw new RobloxUiUnavailableException(
                failureMessage);
    }

    private async Task PrepareSchedulerHandoffAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<
            string,
            MacroEventLevel,
            string?,
            double?> log,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        CancellationToken cancellationToken)
    {
        double confidence =
            await CloseChallengeSelectorForHandoffAsync(
                async token =>
                    (await WaitForChallengeSelectorAsync(
                        window,
                        preset,
                        detector,
                        TimeSpan.FromSeconds(8),
                        report,
                        token).ConfigureAwait(false))
                    .Match,
                (x, y, token) =>
                    ClickAsync(
                        window,
                        x,
                        y,
                        token),
                async token =>
                {
                    ImageFrame? gameModes =
                        await TryWaitForScreenAsync(
                            window,
                            preset,
                            detector,
                            ChallengeScreenState
                                .GameModeSelector,
                            TimeSpan.FromSeconds(5),
                            report,
                            token)
                        .ConfigureAwait(false);
                    return gameModes is null
                        ? null
                        : ChallengeScreenDetector
                            .ScoreStates(gameModes)[
                                ChallengeScreenState
                                    .GameModeSelector];
                },
                (attempt, selector) =>
                    report(
                        "Handoff",
                        0,
                        attempt == 1
                            ? "Closing the Challenge selector before handing off navigation."
                            : $"Challenge selector is still open; retrying its close button ({attempt}/{SchedulerHandoffMaximumAttempts}).",
                        selector.State.ToString(),
                        selector.Confidence),
                (attempt, selector) =>
                    log(
                        $"Challenge selector did not close (attempt {attempt}/{SchedulerHandoffMaximumAttempts}).",
                        MacroEventLevel.Warning,
                        selector.State.ToString(),
                        selector.Confidence),
                cancellationToken)
            .ConfigureAwait(false);

        log(
            "Challenge selector closed. Shared game-mode navigation is ready for the next workflow.",
            MacroEventLevel.Success,
            "game_mode_selector",
            confidence);
    }

    internal static async Task<double>
        CloseChallengeSelectorForHandoffAsync(
        Func<
            CancellationToken,
            Task<ChallengeScreenMatch>>
            observeSelector,
        Func<
            int,
            int,
            CancellationToken,
            Task> clickClose,
        Func<
            CancellationToken,
            Task<double?>>
            waitForGameModeSelector,
        Action<int, ChallengeScreenMatch>?
            closeAttemptStarted,
        Action<int, ChallengeScreenMatch>?
            closeAttemptMissed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            observeSelector);
        ArgumentNullException.ThrowIfNull(
            clickClose);
        ArgumentNullException.ThrowIfNull(
            waitForGameModeSelector);

        for (int attempt = 1;
             attempt <=
                SchedulerHandoffMaximumAttempts;
             attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ChallengeScreenMatch selector =
                await observeSelector(
                    cancellationToken)
                .ConfigureAwait(false);
            if (selector.State is not (
                    ChallengeScreenState
                        .ChallengeList or
                    ChallengeScreenState
                        .ChallengeListUnavailable))
            {
                throw new RobloxUiUnavailableException(
                    $"Cannot hand off from the unexpected Challenge state {selector.State}.");
            }
            if (selector.ActionX is not int closeX ||
                selector.ActionY is not int closeY)
            {
                throw new RobloxUiUnavailableException(
                    "The Challenge selector close button could not be located.");
            }

            closeAttemptStarted?.Invoke(
                attempt,
                selector);
            await clickClose(
                closeX,
                closeY,
                cancellationToken).ConfigureAwait(false);
            double? confidence =
                await waitForGameModeSelector(
                    cancellationToken)
                .ConfigureAwait(false);
            if (confidence is not null)
            {
                return confidence.Value;
            }
            closeAttemptMissed?.Invoke(
                attempt,
                selector);
        }

        throw new RobloxUiUnavailableException(
            $"The Challenge selector remained open after {SchedulerHandoffMaximumAttempts} focused close attempts, so control was not returned to the task scheduler.");
    }
}
