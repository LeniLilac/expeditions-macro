using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private static readonly TimeSpan NavigationTimeout =
        TimeSpan.FromSeconds(12);

    private async Task NavigateToPrestartAsync(
        RobloxWindow window,
        EventPreset preset,
        IDetectorPack detector,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        await EnsureLobbyAsync(
            window,
            detector,
            playMenuKey,
            cancellationToken).ConfigureAwait(false);
        await ClickAsync(
            window,
            EventScreenDetector.LobbyEventAction.X,
            EventScreenDetector.LobbyEventAction.Y,
            cancellationToken).ConfigureAwait(false);
        EventScreenMatch home = await WaitForStateAsync(
            window,
            EventScreenState.EventHome,
            NavigationTimeout,
            detector,
            cancellationToken).ConfigureAwait(false);
        await ClickAsync(
            window,
            home.ActionX ??
                EventScreenDetector
                    .EventGameModeAction.X,
            home.ActionY ??
                EventScreenDetector
                    .EventGameModeAction.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            EventScreenState.ActSelector,
            NavigationTimeout,
            detector,
            cancellationToken).ConfigureAwait(false);

        if (EventScreenDetector
            .RequiresLaterActScroll(preset.Act))
        {
            (
                int startX,
                int startY,
                int endX,
                int endY) =
                EventScreenDetector.LaterActScroll;
            Focus(window);
            await _automation.DragClientAsync(
                window,
                startX,
                startY,
                endX,
                endY,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                350,
                cancellationToken).ConfigureAwait(false);
        }

        (int actX, int actY) =
            EventScreenDetector.ActAction(
                preset.Act);
        await ClickAsync(
            window,
            actX,
            actY,
            cancellationToken).ConfigureAwait(false);
        EventScreenMatch detail =
            await WaitForStateAsync(
                window,
                EventScreenState.ActDetail,
                NavigationTimeout,
                detector,
                cancellationToken).ConfigureAwait(false);
        await ClickAsync(
            window,
            detail.ActionX ??
                EventScreenDetector
                    .SelectStageAction.X,
            detail.ActionY ??
                EventScreenDetector
                    .SelectStageAction.Y,
            cancellationToken).ConfigureAwait(false);
        EventScreenMatch preview =
            await WaitForStateAsync(
                window,
                EventScreenState.PreviewReady,
                NavigationTimeout,
                detector,
                cancellationToken).ConfigureAwait(false);
        if (preview.ActionX is not int previewX ||
            preview.ActionY is not int previewY)
        {
            throw new RobloxUiUnavailableException(
                "The Event preview Start button could not be located.");
        }
        await ClickAsync(
            window,
            previewX,
            previewY,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            EventScreenState.Prestart,
            TimeSpan.FromSeconds(45),
            detector,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        ImageFrame current =
            CaptureClient(window, detector);
        EventScreenMatch state =
            EventScreenDetector.Detect(current);
        string? recovery =
            detector.RecoveryState(current);
        if (string.Equals(
                recovery,
                "lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            _fastNoAlign.ObserveLobby(window);
            return;
        }

        if (state.State ==
            EventScreenState.GameModeSelector)
        {
            await _automation.TapLetterKeyAsync(
                window,
                playMenuKey,
                cancellationToken).ConfigureAwait(false);
            await WaitForPlayClosedAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            current = CaptureClient(window, detector);
            recovery = detector.RecoveryState(current);
            if (string.Equals(
                    recovery,
                    "lobby",
                    StringComparison.OrdinalIgnoreCase))
            {
                _fastNoAlign.ObserveLobby(window);
                return;
            }
        }

        await _lobby.ReturnAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
        _fastNoAlign.ObserveLobby(window);
    }

    private async Task OpenPlayMenuAsync(
        RobloxWindow window,
        IDetectorPack detector,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ImageFrame current =
                CaptureClient(window, detector);
            if (EventScreenDetector.Detect(current).State ==
                EventScreenState.GameModeSelector)
            {
                return;
            }
            await _automation.TapLetterKeyAsync(
                window,
                playMenuKey,
                cancellationToken).ConfigureAwait(false);
            try
            {
                await WaitForStateAsync(
                    window,
                    EventScreenState.GameModeSelector,
                    TimeSpan.FromSeconds(4),
                    detector,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException)
                when (attempt < 3)
            {
            }
        }
        throw new PlayMenuBindingException(
            playMenuKey);
    }

    private async Task WaitForPlayClosedAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            DateTimeOffset.UtcNow +
            TimeSpan.FromSeconds(5);
        int stable = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            EventScreenState state =
                EventScreenDetector.Detect(
                    CaptureClient(window, detector))
                    .State;
            stable =
                state !=
                    EventScreenState.GameModeSelector
                    ? stable + 1
                    : 0;
            if (stable >= 2) return;
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            "The Play interface did not close before Event lobby navigation.");
    }

    private async Task<EventScreenMatch>
        WaitForStateAsync(
        RobloxWindow window,
        EventScreenState expected,
        TimeSpan timeout,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> tracker =
            new(2);
        StableNavigationActionTracker<string>
            actionTracker = new(required: 2);
        EventScreenMatch last = default;
        bool requiresAction =
            expected is
                EventScreenState.EventHome or
                EventScreenState.ActDetail or
                EventScreenState.PreviewReady or
                EventScreenState.Prestart;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            last = EventScreenDetector.Detect(
                CaptureClient(window, detector));
            if (requiresAction)
            {
                (int X, int Y)? action =
                    last.ActionX is int actionX &&
                    last.ActionY is int actionY
                        ? (actionX, actionY)
                        : null;
                if (actionTracker.Update(
                        last.State == expected
                            ? expected.ToString()
                            : null,
                        action) is not null)
                {
                    return last;
                }
            }
            else
            {
                string? candidate =
                    last.State == expected
                        ? expected.ToString()
                        : null;
                if (tracker.Update(candidate) is not null)
                {
                    return last;
                }
            }
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Event navigation did not reach {expected}. Last state: {last.State} ({last.Confidence:P0}).");
    }
}
