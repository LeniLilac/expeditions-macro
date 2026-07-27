using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

internal static class ManualInputMatchPlayback
{
    public static async Task<ManualInputRecording?> ResolveAsync(
        ManualInputRouteService? service,
        PlacementModel? placement,
        CancellationToken cancellationToken)
    {
        if (placement is null ||
            !ManualInputRouteService.IsConfigured(
                placement))
        {
            return null;
        }
        if (service is null)
        {
            throw new InvalidOperationException(
                "Manual input playback is unavailable.");
        }
        return await service.ResolveAsync(
                placement,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<Stopwatch> PlayAsync(
        ManualInputRouteService service,
        RobloxWindow window,
        ManualInputRecording recording,
        IProgress<MacroProgress>? progress,
        Action? matchStarting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(recording);
        Stopwatch runtime = new();
        await service.PlayAsync(
                window,
                recording,
                progress,
                () =>
                {
                    runtime.Start();
                    matchStarting?.Invoke();
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (!runtime.IsRunning)
        {
            throw new InvalidOperationException(
                "Manual playback ended without starting the match clock.");
        }
        return runtime;
    }
}
