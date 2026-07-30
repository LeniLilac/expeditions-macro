using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class SelectedUnitPanelPlayback(
    IRobloxAutomation automation,
    Func<DateTimeOffset>? utcNow = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private const int PollMilliseconds = 100;
    private const int VisibleTimeoutMilliseconds = 800;
    private const int HiddenTimeoutMilliseconds =
        (DismissSamples - 1) *
        PollMilliseconds;
    private const int RequiredStableFrames = 2;
    private const int DismissAttempts = 8;
    private const int DismissSamples = 4;
    private const int IdleCursorInsetPixels = 24;
    private readonly Func<DateTimeOffset> _utcNow =
        utcNow ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay =
        delay ?? ((duration, token) =>
            Task.Delay(duration, token));

    public async Task<bool> WaitForVisibleAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        await WaitForStateAsync(
                window,
                static match => match.Visible,
                expectedVisible: true,
                TimeSpan.FromMilliseconds(
                    VisibleTimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> WaitForPanelVisibleAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        await WaitForStateAsync(
                window,
                static match => match.PanelVisible,
                expectedVisible: true,
                TimeSpan.FromMilliseconds(
                    VisibleTimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task DismissAsync(
        RobloxWindow window,
        int clientWidth,
        int clientHeight,
        CancellationToken cancellationToken)
    {
        int idleX = Math.Max(
            0,
            clientWidth -
            1 -
            IdleCursorInsetPixels);
        int idleY = Math.Max(
            0,
            clientHeight -
            1 -
            IdleCursorInsetPixels);
        await automation.ParkCursorAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        if (await WaitForHiddenAsync(
            window,
            cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        for (int attempt = 0;
             attempt < DismissAttempts;
             attempt++)
        {
            EnsureFocus(window);
            await automation.ClickClientAsync(
                window,
                idleX,
                idleY,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForHiddenAsync(
                window,
                cancellationToken).ConfigureAwait(false))
            {
                await automation.ParkCursorAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "The selected-unit panel remained open after " +
            $"{DismissAttempts} clicks at the safe idle point.");
    }

    public Task<bool> WaitForHiddenAfterActionAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        WaitForHiddenAsync(
            window,
            cancellationToken);

    private async Task<bool> WaitForHiddenAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        await WaitForStateAsync(
                window,
                static match => match.PanelVisible,
                expectedVisible: false,
                TimeSpan.FromMilliseconds(
                    HiddenTimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<bool> WaitForStateAsync(
        RobloxWindow window,
        Func<SelectedUnitPanelMatch, bool>
            isVisible,
        bool expectedVisible,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        int observations = 0;
        DateTimeOffset softDeadline =
            _utcNow() + timeout;
        ObservationWaitBudget budget = new(
            timeout,
            RequiredStableFrames,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending: stable > 0) &&
               (_utcNow() <= softDeadline ||
                observations <
                    RequiredStableFrames ||
                stable > 0))
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            EnsureFocus(window);
            bool observedVisible =
                IsVisible(window, isVisible);
            stable = observedVisible ==
                expectedVisible
                    ? stable + 1
                    : 0;
            observations++;
            budget.MarkObserved();
            if (stable >= RequiredStableFrames)
            {
                return true;
            }
            if (_utcNow() >= softDeadline &&
                observations >=
                    RequiredStableFrames &&
                stable == 0)
            {
                break;
            }
            if (!budget.ShouldObserve(
                    confirmationPending: stable > 0))
            {
                break;
            }
            await _delay(
                    TimeSpan.FromMilliseconds(
                        PollMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        return false;
    }

    private bool IsVisible(
        RobloxWindow window,
        Func<SelectedUnitPanelMatch, bool>
            isVisible)
    {
        ImageFrame frame =
            automation.CaptureClient(window);
        SelectedUnitPanelMatch match =
            SelectedUnitPanelDetector
                .Detect(frame);
        return isVisible(match);
    }

    private void EnsureFocus(
        RobloxWindow window)
    {
        if (!automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
