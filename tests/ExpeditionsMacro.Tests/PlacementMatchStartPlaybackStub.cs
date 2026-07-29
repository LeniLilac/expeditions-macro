using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Tests;

internal sealed class PlacementMatchStartPlaybackStub :
    IPlacementMatchStartPlayback
{
    private readonly Action? _started;

    public PlacementMatchStartPlaybackStub(
        Action? started = null)
    {
        _started = started;
    }

    public Task StartAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _started?.Invoke();
        return Task.CompletedTask;
    }
}
