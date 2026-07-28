using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Automation.Placement;

internal interface IQuickPlacementSelectionProof
{
    Task<bool> HasStableSelectionAsync(
        RobloxWindow window,
        int virtualKey,
        CancellationToken cancellationToken);
}

internal sealed class QuickPlacementSelectionProof :
    IQuickPlacementSelectionProof
{
    private const int RequiredStableFrames = 2;
    private const int MaximumSamples = 4;
    private const int PollMilliseconds = 100;

    private readonly IRobloxAutomation _automation;
    private readonly Func<ImageFrame, bool> _isVisible;

    public QuickPlacementSelectionProof(
        IRobloxAutomation automation)
        : this(
            automation,
            frame => QuickPlacementSelectionDetector
                .Detect(frame)
                .Visible)
    {
    }

    internal QuickPlacementSelectionProof(
        IRobloxAutomation automation,
        Func<ImageFrame, bool> isVisible)
    {
        _automation = automation;
        _isVisible = isVisible;
    }

    public Task<bool> HasStableSelectionAsync(
        RobloxWindow window,
        int virtualKey,
        CancellationToken cancellationToken) =>
        _automation.RunWithKeyHeldAsync(
            window,
            virtualKey,
            token => ObserveWhileHeldAsync(
                window,
                token),
            cancellationToken);

    private async Task<bool> ObserveWhileHeldAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        for (int sample = 0;
             sample < MaximumSamples ||
             stable > 0;
             sample++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RobloxWindow? foreground =
                _automation.ForegroundWindow();
            if (foreground is null ||
                !HasSameOwner(
                    window,
                    foreground.Value))
            {
                throw new RobloxSessionUnavailableException(
                    "Roblox lost focus while checking Quick Placement selection.");
            }

            ImageFrame frame =
                _automation.CaptureClient(window);
            stable = _isVisible(frame)
                ? stable + 1
                : 0;
            if (stable >= RequiredStableFrames)
            {
                return true;
            }

            if (sample + 1 < MaximumSamples ||
                stable > 0)
            {
                await Task.Delay(
                    PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private static bool HasSameOwner(
        RobloxWindow expected,
        RobloxWindow actual) =>
        actual.Handle == expected.Handle ||
        expected.ProcessId > 0 &&
        actual.ProcessId == expected.ProcessId;
}
