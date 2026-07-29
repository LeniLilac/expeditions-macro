using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Automation.Refuel;

internal sealed class ResourceRefuelScreenWaiter
{
    private static readonly TimeSpan PollDelay =
        TimeSpan.FromMilliseconds(200);
    private const int StableDetections = 2;

    private readonly IRobloxAutomation _automation;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;

    internal ResourceRefuelScreenWaiter(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset> utcNow)
    {
        _automation = automation;
        _delay = delay;
        _utcNow = utcNow;
    }

    internal async Task EnsureCanonicalClientAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        FocusOrThrow(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != 808 || bounds.Height != 611)
        {
            await _automation.ResizeClientAsync(
                window,
                808,
                611,
                cancellationToken).ConfigureAwait(false);
            await _delay(
                TimeSpan.FromMilliseconds(250),
                cancellationToken).ConfigureAwait(false);
        }
        RequireFocus(window);
    }

    internal async Task WaitForLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await WaitForStableAsync(
            window,
            frame =>
                string.Equals(
                    detector.RecoveryState(frame),
                    "lobby",
                    StringComparison.OrdinalIgnoreCase) &&
                AreasScreenDetector.Detect(frame).State ==
                    AreasScreenState.None,
            timeout,
            "Roblox did not reach a stable lobby with Areas closed before resource refuel.",
            cancellationToken).ConfigureAwait(false);
    }

    internal Task<bool> TryWaitForLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        WaitForStableAsync(
            window,
            frame =>
                string.Equals(
                    detector.RecoveryState(frame),
                    "lobby",
                    StringComparison.OrdinalIgnoreCase) &&
                AreasScreenDetector.Detect(frame).State ==
                    AreasScreenState.None,
            timeout,
            failureMessage: null,
            cancellationToken);

    internal async Task<ChallengeScreenMatch>
        WaitForPlaySurfaceAsync(
        RobloxWindow window,
        Func<ChallengeScreenState, bool> accept,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ChallengeScreenMatch? matched = null;
        await WaitForStableAsync(
            window,
            frame =>
            {
                ChallengeScreenMatch candidate =
                    ChallengeScreenDetector.Detect(frame);
                if (!accept(candidate.State))
                {
                    return false;
                }
                matched = candidate;
                return true;
            },
            timeout,
            "The shared Play interface did not reach a state that can return to the Lobby.",
            cancellationToken).ConfigureAwait(false);
        return matched!;
    }

    internal async Task<AreasScreenMatch> WaitForAreasAsync(
        RobloxWindow window,
        Func<AreasScreenState, bool> accept,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        AreasScreenMatch? matched = null;
        await WaitForStableAsync(
            window,
            frame =>
            {
                AreasScreenMatch candidate =
                    AreasScreenDetector.Detect(frame);
                if (!accept(candidate.State)) return false;
                matched = candidate;
                return true;
            },
            timeout,
            "The Areas interface did not reach its expected state.",
            cancellationToken).ConfigureAwait(false);
        return matched!;
    }

    internal async Task<ResourceStationScreenMatch>
        WaitForStationAsync(
            RobloxWindow window,
            Func<ResourceStationScreenState, bool> accept,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        ResourceStationScreenMatch? matched = null;
        await WaitForStableAsync(
            window,
            frame =>
            {
                ResourceStationScreenMatch candidate =
                    ResourceStationScreenDetector.Detect(frame);
                if (!accept(candidate.State)) return false;
                matched = candidate;
                return true;
            },
            timeout,
            "The resource station did not reach its expected state.",
            cancellationToken).ConfigureAwait(false);
        return matched!;
    }

    internal async Task<ResourceStationScreenMatch?>
        TryWaitForStationAsync(
        RobloxWindow window,
        ResourceStationScreenState state,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ResourceStationScreenMatch? matched = null;
        bool found = await WaitForStableAsync(
            window,
            frame =>
            {
                ResourceStationScreenMatch candidate =
                    ResourceStationScreenDetector.Detect(frame);
                if (candidate.State != state) return false;
                matched = candidate;
                return true;
            },
            timeout,
            failureMessage: null,
            cancellationToken).ConfigureAwait(false);
        return found ? matched : null;
    }

    internal async Task<ImageFrame?> WaitForPlayAsync(
        RobloxWindow window,
        ImageFrame? initialFrame,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ImageFrame? matched = null;
        bool found = await WaitForStableAsync(
            window,
            frame =>
            {
                ChallengeScreenState state =
                    ChallengeScreenDetector.Detect(frame).State;
                if (state is not (
                    ChallengeScreenState.GameModeSelector or
                    ChallengeScreenState.PostMatchPreview))
                {
                    return false;
                }
                matched = frame;
                return true;
            },
            timeout,
            failureMessage: null,
            cancellationToken: cancellationToken,
            initialFrame: initialFrame).ConfigureAwait(false);
        return found ? matched : null;
    }

    internal ImageFrame Capture(RobloxWindow window)
    {
        RequireFocus(window);
        return _automation.CaptureClient(window);
    }

    internal async Task ClickAsync(
        RobloxWindow window,
        AreasScreenMatch match,
        CancellationToken cancellationToken)
    {
        if (match.ActionX is not int x ||
            match.ActionY is not int y)
        {
            throw new InvalidOperationException(
                $"{match.State} did not expose a verified action.");
        }
        await ClickAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task ClickAsync(
        RobloxWindow window,
        ResourceStationScreenMatch match,
        CancellationToken cancellationToken)
    {
        if (match.ActionX is not int x ||
            match.ActionY is not int y)
        {
            throw new InvalidOperationException(
                $"{match.State} did not expose a verified action.");
        }
        await ClickAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    internal Task ClickConfirmAsync(
        RobloxWindow window,
        ResourceStationScreenMatch match,
        CancellationToken cancellationToken)
    {
        if (match.ConfirmActionX is not int x ||
            match.ConfirmActionY is not int y)
        {
            throw new InvalidOperationException(
                $"{match.State} did not expose a verified confirm action.");
        }
        return ClickAsync(
            window,
            x,
            y,
            cancellationToken);
    }

    internal Task ClickDismissAsync(
        RobloxWindow window,
        ResourceStationScreenMatch match,
        CancellationToken cancellationToken)
    {
        if (match.DismissActionX is not int x ||
            match.DismissActionY is not int y)
        {
            throw new InvalidOperationException(
                $"{match.State} did not expose a verified dismiss action.");
        }
        return ClickAsync(
            window,
            x,
            y,
            cancellationToken);
    }

    internal async Task ClickAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        RequireFocus(window);
        await _automation.ClickClientAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    internal void RequireFocus(RobloxWindow window)
    {
        FocusOrThrow(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != 808 || bounds.Height != 611)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox changed to {bounds.Width} by " +
                $"{bounds.Height} during resource refuel.");
        }
    }

    private async Task<bool> WaitForStableAsync(
        RobloxWindow window,
        Func<ImageFrame, bool> accept,
        TimeSpan timeout,
        string? failureMessage,
        CancellationToken cancellationToken,
        ImageFrame? initialFrame = null)
    {
        int stable = 0;
        ObservationWaitBudget budget = new(
            timeout,
            StableDetections,
            _utcNow);
        if (initialFrame is not null)
        {
            stable = accept(initialFrame) ? 1 : 0;
            budget.MarkObserved();
        }
        while (budget.ShouldObserve(
                   confirmationPending: stable == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = Capture(window);
            stable = accept(frame) ? stable + 1 : 0;
            budget.MarkObserved();
            if (stable >= StableDetections) return true;
            await _delay(
                PollDelay,
                cancellationToken).ConfigureAwait(false);
        }

        if (failureMessage is not null)
        {
            throw new TimeoutException(failureMessage);
        }
        return false;
    }

    private void FocusOrThrow(RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox during resource refuel.");
        }
    }
}
