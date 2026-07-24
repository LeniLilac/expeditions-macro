using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private Task ClickActionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        CancellationToken cancellationToken) =>
        ClickActionAsync(
            window,
            detector,
            state,
            clientImage: null,
            cancellationToken);

    private async Task ClickActionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        ImageFrame? clientImage,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
        StableNavigationActionTracker<string> tracker = new();
        ImageFrame? current = clientImage;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current ??= CaptureClient(window, detector);
            (int X, int Y) action =
                detector.ActionFor(state, current);
            (int X, int Y)? stable =
                tracker.Update(state, action);
            if (stable is not null)
            {
                Focus(window);
                await _automation.ClickClientAsync(
                    window,
                    stable.Value.X,
                    stable.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            current = null;
            await Task.Delay(
                150,
                cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"The {state} action did not settle before it could be clicked.");
    }
}
