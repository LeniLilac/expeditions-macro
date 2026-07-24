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

    internal ResourceRefuelScreenWaiter(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _delay = delay;
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

    internal async Task<ImageFrame?> WaitForPlayAsync(
        RobloxWindow window,
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
            cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        int maximumPolls = Math.Max(
            1,
            (int)Math.Ceiling(
                timeout.TotalMilliseconds /
                PollDelay.TotalMilliseconds));
        int stable = 0;
        for (int poll = 0; poll < maximumPolls; poll++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = Capture(window);
            stable = accept(frame) ? stable + 1 : 0;
            if (stable >= StableDetections) return true;
            if (poll + 1 >= maximumPolls) break;
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
