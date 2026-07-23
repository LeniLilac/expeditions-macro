using System.ComponentModel;
using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Windows;

public sealed class WindowsRobloxProcessController : IRobloxProcessController
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(4);

    public async Task CloseAsync(
        RobloxWindow? window,
        CancellationToken cancellationToken)
    {
        if (window is null) return;
        RobloxWindow target = window.Value;
        if (target.ProcessId <= 0 ||
            !WindowsRobloxAutomation.IsSupportedRobloxProcessName(target.ProcessName))
        {
            throw new InvalidOperationException(
                "The visible window was not verified as a supported Roblox player process.");
        }

        using Process? process = TryGetVerifiedProcess(target);
        if (process is null) return;
        if (process.HasExited) return;

        bool closeRequested = process.CloseMainWindow();
        if (closeRequested &&
            await WaitForExitAsync(process, GracefulCloseTimeout, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task LaunchAsync(
        Uri launchUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launchUri);
        cancellationToken.ThrowIfCancellationRequested();
        if (!launchUri.IsAbsoluteUri ||
            !launchUri.Scheme.Equals("roblox", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only the registered Roblox URI protocol can be launched.",
                nameof(launchUri));
        }

        ProcessStartInfo start = new()
        {
            FileName = launchUri.AbsoluteUri,
            UseShellExecute = true,
        };
        try
        {
            _ = Process.Start(start);
        }
        catch (Exception error) when (
            error is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Windows could not launch Roblox. Reinstall Roblox or restore its roblox:// protocol registration.",
                error);
        }

        return Task.CompletedTask;
    }

    private static Process? TryGetVerifiedProcess(RobloxWindow window)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(window.ProcessId);
        }
        catch (Exception error) when (
            error is ArgumentException or InvalidOperationException)
        {
            _ = error;
            return null;
        }

        try
        {
            if (!WindowsRobloxAutomation.IsSupportedRobloxProcessName(process.ProcessName) ||
                !process.ProcessName.Equals(window.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The process identity changed before restart recovery could close Roblox.");
            }
            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }
}
