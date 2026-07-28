using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class SelectedUnitPanelPlayback(
    IRobloxAutomation automation)
{
    private const int PollMilliseconds = 100;
    private const int VisibleTimeoutMilliseconds = 800;
    private const int RequiredStableFrames = 2;
    private const int DismissAttempts = 8;
    private const int DismissSamples = 4;
    private const int IdleCursorInsetPixels = 24;

    public async Task<bool> WaitForVisibleAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        int samples = Math.Max(
            RequiredStableFrames,
            1 +
            VisibleTimeoutMilliseconds /
            PollMilliseconds);
        for (int sample = 0;
             sample < samples ||
             stable > 0;
             sample++)
        {
            EnsureFocus(window);
            ImageFrame frame =
                automation.CaptureClient(window);
            SelectedUnitPanelMatch match =
                SelectedUnitPanelDetector.Detect(frame);
            stable = match.Visible ? stable + 1 : 0;
            if (stable >= RequiredStableFrames)
            {
                return true;
            }
            if (sample + 1 < samples ||
                stable > 0)
            {
                await Task.Delay(
                    PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        return false;
    }

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

    private async Task<bool> WaitForHiddenAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        for (int sample = 0;
             sample < DismissSamples ||
             stable > 0;
             sample++)
        {
            EnsureFocus(window);
            stable = IsVisible(window)
                ? 0
                : stable + 1;
            if (stable >= RequiredStableFrames)
            {
                return true;
            }
            if (sample + 1 < DismissSamples ||
                stable > 0)
            {
                await Task.Delay(
                    PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        return false;
    }

    private bool IsVisible(
        RobloxWindow window)
    {
        ImageFrame frame =
            automation.CaptureClient(window);
        return SelectedUnitPanelDetector
            .Detect(frame)
            .PanelVisible;
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
