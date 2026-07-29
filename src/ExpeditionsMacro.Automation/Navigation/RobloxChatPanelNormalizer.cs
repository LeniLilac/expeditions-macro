using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Automation.Navigation;

public sealed class RobloxChatPanelNormalizer
{
    private const int StableFrames = 2;
    private const int MaximumClicks = 2;
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);
    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;

    public RobloxChatPanelNormalizer(
        IRobloxAutomation automation)
        : this(
            automation,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token))
    {
    }

    internal RobloxChatPanelNormalizer(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
    }

    public async Task<bool> EnsureClosedAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(12),
            StableFrames,
            _utcNow);
        RobloxChatButtonState candidate =
            RobloxChatButtonState.None;
        int stable = 0;
        int clicks = 0;
        bool changed = false;

        while (budget.ShouldObserve(
                   confirmationPending:
                       stable == 1 ||
                       changed))
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ValidateWindow(window);
            RobloxChatButtonMatch match =
                RobloxChatButtonDetector.Detect(
                    Capture(window));
            if (match.State ==
                RobloxChatButtonState.None)
            {
                candidate =
                    RobloxChatButtonState.None;
                stable = 0;
            }
            else if (match.State == candidate)
            {
                stable++;
            }
            else
            {
                candidate = match.State;
                stable = 1;
            }

            budget.MarkObserved();
            if (stable >= StableFrames &&
                candidate ==
                    RobloxChatButtonState.Closed)
            {
                return changed;
            }
            if (stable >= StableFrames &&
                candidate ==
                    RobloxChatButtonState.Open)
            {
                if (clicks >= MaximumClicks)
                {
                    break;
                }
                ValidateWindow(window);
                await _automation.ClickClientAsync(
                        window,
                        match.ActionX,
                        match.ActionY,
                        cancellationToken)
                    .ConfigureAwait(false);
                clicks++;
                changed = true;
                candidate =
                    RobloxChatButtonState.None;
                stable = 0;
            }

            await _delay(
                    PollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            changed
                ? "Roblox did not close its verified chat panel."
                : "Roblox did not expose a stable open or closed chat indicator.");
    }

    private ImageFrame Capture(
        RobloxWindow window) =>
        _automation.CaptureClient(window);

    private void ValidateWindow(
        RobloxWindow window)
    {
        RobloxWindow? current =
            _automation.FindWindow();
        if (current is null ||
            current.Value.Handle != window.Handle)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox closed or changed while checking chat.");
        }
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while checking chat.");
        }
        var bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != 808 ||
            bounds.Height != 611)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox changed size while checking chat.");
        }
    }
}
