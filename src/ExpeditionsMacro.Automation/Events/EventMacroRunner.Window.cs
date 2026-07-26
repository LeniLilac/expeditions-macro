using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private async Task EnsureClientSizeAsync(
        RobloxWindow window,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        Focus(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != width ||
            bounds.Height != height)
        {
            await _automation.ResizeClientAsync(
                window,
                width,
                height,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                250,
                cancellationToken).ConfigureAwait(false);
        }
        ClientBounds actual =
            _automation.GetClientBounds(window);
        if (actual.Width != width ||
            actual.Height != height)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox did not accept the required {width} by {height} client size.");
        }
    }

    private async Task ClickAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.ClickClientAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame CaptureClient(
        RobloxWindow window,
        IDetectorPack detector)
    {
        Focus(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                detector.Manifest.ClientWidth ||
            bounds.Height !=
                detector.Manifest.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox no longer matches the detector pack client size.");
        }
        return _automation.CaptureClient(window);
    }

    private ImageFrame? TryCaptureClient(
        RobloxWindow window,
        IDetectorPack detector)
    {
        try
        {
            return CaptureClient(window, detector);
        }
        catch
        {
            return null;
        }
    }

    private void Focus(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
