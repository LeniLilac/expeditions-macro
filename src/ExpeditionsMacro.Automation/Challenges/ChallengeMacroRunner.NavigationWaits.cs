using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
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
            (int x, int y) =
                ChallengeScreenDetector.ActionForType(type);
            await ClickAsync(
                window,
                x,
                y,
                cancellationToken).ConfigureAwait(false);
            DateTimeOffset deadline =
                DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            StableStateTracker<ChallengeScreenState> stateTracker =
                new(preset.StableDetections);
            StableNavigationActionTracker<ChallengeScreenState>
                actionTracker =
                    new(Math.Max(2, preset.StableDetections));
            while (DateTimeOffset.UtcNow < deadline)
            {
                ImageFrame frame = CaptureClient(window, detector);
                ChallengeScreenMatch match =
                    ChallengeScreenDetector.Detect(frame);
                ChallengeScreenState candidate =
                    match.State is
                        ChallengeScreenState.ChallengeAvailable or
                        ChallengeScreenState.ChallengeCooldown
                        ? match.State
                        : ChallengeScreenState.None;
                ChallengeScreenState? stableState =
                    stateTracker.Update(candidate);
                (int X, int Y)? stableAction =
                    actionTracker.Update(
                        candidate ==
                            ChallengeScreenState.ChallengeAvailable
                            ? candidate
                            : ChallengeScreenState.None,
                        MatchAction(match));
                bool ready =
                    stableState ==
                        ChallengeScreenState.ChallengeCooldown ||
                    stableState ==
                        ChallengeScreenState.ChallengeAvailable &&
                    stableAction is not null;
                if (ready)
                {
                    ChallengeScreenState stable =
                        stableState!.Value;
                    report(
                        "Challenge selection",
                        15,
                        stable ==
                            ChallengeScreenState.ChallengeAvailable
                            ? "Select Stage is available."
                            : "Challenge is on cooldown.",
                        stable.ToString(),
                        match.Confidence);
                    return match with
                    {
                        State = stable,
                        ActionX = stableAction?.X ??
                            match.ActionX,
                        ActionY = stableAction?.Y ??
                            match.ActionY,
                    };
                }
                await Task.Delay(
                    preset.PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
            report(
                "Challenge selection",
                10,
                $"Selector click did not open Challenge {type} (attempt {attempt}/3).",
                null,
                null);
        }
        throw new InvalidOperationException(
            $"Challenge {Label(type)} could not be opened from the fixed selector row.");
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
        StableStateTracker<ChallengeScreenState> stateTracker =
            new(preset.StableDetections);
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, preset.StableDetections));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match =
                ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState candidate =
                match.State == desired
                    ? desired
                    : ChallengeScreenState.None;
            ChallengeScreenState? stable =
                stateTracker.Update(candidate);
            (int X, int Y)? stableAction =
                actionTracker.Update(
                    RequiresStableChallengeAction(desired)
                        ? candidate
                        : ChallengeScreenState.None,
                    MatchAction(match));
            if (RequiresStableChallengeAction(desired)
                ? stableAction is not null
                : stable == desired)
            {
                return frame;
            }
            if (match.State != ChallengeScreenState.None)
            {
                report(
                    "Waiting",
                    0,
                    $"Detected {Label(match.State)}.",
                    match.State.ToString(),
                    match.Confidence);
            }
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private static bool IsChallengeNavigationAction(
        ChallengeScreenState state) =>
        state is
            ChallengeScreenState.PostMatchPreview or
            ChallengeScreenState.PreviewReady;

    private static bool RequiresStableChallengeAction(
        ChallengeScreenState state) =>
        state is
            ChallengeScreenState.ChallengeAvailable or
            ChallengeScreenState.PreviewReady or
            ChallengeScreenState.PostMatchPreview or
            ChallengeScreenState.Prestart;

    private static (int X, int Y)? MatchAction(
        ChallengeScreenMatch match) =>
        match.ActionX is int x && match.ActionY is int y
            ? (x, y)
            : null;
}
