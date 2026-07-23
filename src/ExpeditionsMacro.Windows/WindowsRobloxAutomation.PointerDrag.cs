using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public async Task DragClientAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        ClientBounds bounds = GetClientBounds(window);
        ValidateClientPoint(bounds, startX, startY);
        ValidateClientPoint(bounds, endX, endY);

        MoveCursorWithRegisteredMotion(
            bounds.X + startX,
            bounds.Y + startY,
            startX < bounds.Width - 1 ? 1 : -1,
            "Windows could not move the cursor to the Roblox drag handle.");
        await Task.Delay(ClickPositionSettleMilliseconds, cancellationToken).ConfigureAwait(false);

        NativeMethods.mouse_event(NativeMethods.MouseeventfLeftDown, 0, 0, 0, 0);
        EmitTrace(new WindowsAutomationTrace(
            DateTimeOffset.UtcNow,
            "mouse",
            "left_down",
            X: startX,
            Y: startY,
            Flags: NativeMethods.MouseeventfLeftDown));
        int currentX = startX;
        int currentY = startY;
        try
        {
            await Task.Delay(ClickHoldMilliseconds, cancellationToken).ConfigureAwait(false);
            int distance = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
            int steps = Math.Max(1, (int)Math.Ceiling(distance / 8d));
            for (int step = 1; step <= steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int x = startX + (int)Math.Round((endX - startX) * (step / (double)steps));
                int y = startY + (int)Math.Round((endY - startY) * (step / (double)steps));
                MoveCursorWithRegisteredMotion(
                    bounds.X + x,
                    bounds.Y + y,
                    x < bounds.Width - 1 ? 1 : -1,
                    "Windows could not drag the Roblox control.");
                currentX = x;
                currentY = y;
                if (step < steps)
                {
                    await Task.Delay(12, cancellationToken).ConfigureAwait(false);
                }
            }
            await Task.Delay(ClickHoldMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.mouse_event(NativeMethods.MouseeventfLeftUp, 0, 0, 0, 0);
            EmitTrace(new WindowsAutomationTrace(
                DateTimeOffset.UtcNow,
                "mouse",
                "left_up",
                X: currentX,
                Y: currentY,
                Flags: NativeMethods.MouseeventfLeftUp));
        }

        await ParkCursorWithAcknowledgedMotionAsync(bounds, cancellationToken).ConfigureAwait(false);
        await Task.Delay(HoverRenderSettleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateClientPoint(ClientBounds bounds, int x, int y)
    {
        if (x < 0 || x >= bounds.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Drag point falls outside the Roblox client.");
        }
        if (y < 0 || y >= bounds.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Drag point falls outside the Roblox client.");
        }
    }
}
