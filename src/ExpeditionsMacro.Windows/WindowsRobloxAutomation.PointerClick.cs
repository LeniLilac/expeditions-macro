using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public Task MoveCursorToClientCenterAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        MoveCursorWithRegisteredMotion(
            bounds.X + bounds.Width / 2,
            bounds.Y + bounds.Height / 2,
            1,
            "Windows could not move the cursor to Roblox.");
        return Task.CompletedTask;
    }

    public async Task ParkCursorAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox.");
        }
        await ParkCursorWithAcknowledgedMotionAsync(
                GetClientBounds(window),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ClickClientAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        ClickClientCoreAsync(
            window,
            x,
            y,
            parkCursorAfterClick: true,
            cancellationToken);

    public Task ClickClientRetainingCursorAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        ClickClientCoreAsync(
            window,
            x,
            y,
            parkCursorAfterClick: false,
            cancellationToken);

    private async Task ClickClientCoreAsync(
        RobloxWindow window,
        int x,
        int y,
        bool parkCursorAfterClick,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        if (x < 0 ||
            y < 0 ||
            x >= bounds.Width ||
            y >= bounds.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Click falls outside the Roblox client.");
        }

        int clickNudge = x < bounds.Width - 1 ? 1 : -1;
        MoveCursorWithRegisteredMotion(
            bounds.X + x,
            bounds.Y + y,
            clickNudge,
            "Windows could not move the cursor to the Roblox coordinate.");
        // Low-frame-rate clients can render the new button before their input loop
        // acknowledges the registered cursor move. Give Roblox two typical frames
        // before pressing so the click is hit-tested at the visible target.
        await Task.Delay(
                ClickPositionSettleMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        NativeMethods.mouse_event(
            NativeMethods.MouseeventfLeftDown,
            0,
            0,
            0,
            0);
        EmitTrace(
            new WindowsAutomationTrace(
                DateTimeOffset.UtcNow,
                "mouse",
                "left_down",
                X: x,
                Y: y,
                Flags: NativeMethods.MouseeventfLeftDown));
        try
        {
            await Task.Delay(
                    ClickHoldMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.mouse_event(
                NativeMethods.MouseeventfLeftUp,
                0,
                0,
                0,
                0);
            EmitTrace(
                new WindowsAutomationTrace(
                    DateTimeOffset.UtcNow,
                    "mouse",
                    "left_up",
                    X: x,
                    Y: y,
                    Flags: NativeMethods.MouseeventfLeftUp));
        }

        if (parkCursorAfterClick)
        {
            // SetCursorPos alone can move the Windows pointer without making Roblox
            // process a mouse-motion event. Keep the pointer safely inside the client
            // and send spaced motion pulses so Roblox clears the hover.
            await ParkCursorWithAcknowledgedMotionAsync(
                    bounds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await Task.Delay(
                HoverRenderSettleMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
