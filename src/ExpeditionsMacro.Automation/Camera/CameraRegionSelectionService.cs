using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Camera;

public sealed record CameraRegionSelection(ScreenRegion Region, ImageFrame Preview);

public sealed class CameraRegionSelectionService
{
    private readonly IRobloxAutomation _automation;

    public CameraRegionSelectionService(IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public async Task<CameraRegionSelection?> SelectAsync(
        Func<ClientBounds, ScreenRegion?> selectScreenRegion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectScreenRegion);

        RobloxWindow window = _automation.FindWindow() ??
            throw new InvalidOperationException("No visible Roblox window was found.");
        if (!_automation.Focus(window))
        {
            throw new InvalidOperationException($"Found '{window.Title}', but Windows could not focus it. Restore Roblox and try again.");
        }

            await _automation.ResizeClientAsync(
                window,
                RobloxClientProfile.Width,
                RobloxClientProfile.Height,
                cancellationToken);
            await Task.Delay(250, cancellationToken);

            ClientBounds client = _automation.GetClientBounds(window);
            if (client.Width != RobloxClientProfile.Width || client.Height != RobloxClientProfile.Height)
            {
                throw new InvalidOperationException(
                    $"Roblox did not accept the standard {RobloxClientProfile.Width} × {RobloxClientProfile.Height} client size.");
            }

            ScreenRegion? selectedScreenRegion = selectScreenRegion(client);
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedScreenRegion is not { } screenRegion) return null;

            ScreenRegion relativeRegion = new(
                screenRegion.X - client.X,
                screenRegion.Y - client.Y,
                screenRegion.Width,
                screenRegion.Height);
            if (!relativeRegion.FitsWithin(client.Width, client.Height))
            {
                throw new InvalidOperationException("The comparison region must be completely inside the Roblox client area.");
            }

            ImageFrame preview = _automation.CaptureScreen(client.ToScreen(relativeRegion));
            return new CameraRegionSelection(relativeRegion, preview);
    }
}
