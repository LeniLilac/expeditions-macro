using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
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
}
