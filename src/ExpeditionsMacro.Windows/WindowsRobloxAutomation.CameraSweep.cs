using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public async Task CaptureCameraYawSweepAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        TimeSpan maximumDuration,
        int maximumSamples,
        int sampleIntervalMilliseconds,
        Func<CameraYawSweepSample, bool> observe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observe);
        if (maximumDuration < TimeSpan.FromMilliseconds(500) ||
            maximumDuration > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }
        if (maximumSamples is < 12 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        }
        if (sampleIntervalMilliseconds is < 10 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIntervalMilliseconds));
        }
        if (!Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox for the camera sweep.");
        }

        ushort scanCode = direction switch
        {
            CameraYawDirection.Left => 0x4B,
            CameraYawDirection.Right => 0x4D,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
        Stopwatch timer = Stopwatch.StartNew();
        int sampleCount = 0;
        SendKeyboard(scanCode, keyUp: false);
        try
        {
            while (timer.Elapsed < maximumDuration &&
                   sampleCount < maximumSamples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImageFrame frame = CaptureClient(window);
                sampleCount++;
                if (!observe(new CameraYawSweepSample(timer.Elapsed, frame)))
                {
                    break;
                }

                TimeSpan nextSample =
                    TimeSpan.FromMilliseconds(sampleCount * sampleIntervalMilliseconds);
                TimeSpan delay = nextSample - timer.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            SendKeyboard(scanCode, keyUp: true);
        }
    }

    public async Task CaptureCameraFineYawSweepAsync(
        RobloxWindow window,
        int radiusPixels,
        int sampleStridePixels,
        Action<CameraFineYawSweepSample> observe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observe);
        if (radiusPixels is < 2 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusPixels));
        }
        if (sampleStridePixels is < 1 or > 25 ||
            radiusPixels % sampleStridePixels != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleStridePixels));
        }
        if (!Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox for the fine camera sweep.");
        }

        bool restoreCursor =
            NativeMethods.GetCursorPos(out NativeMethods.Point original);
        int currentOffset = 0;
        try
        {
            await MoveFineYawPixelsAsync(
                -radiusPixels,
                delta => currentOffset += delta,
                cancellationToken).ConfigureAwait(false);

            for (int offset = -radiusPixels;
                 offset <= radiusPixels;
                 offset += sampleStridePixels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (offset != currentOffset)
                {
                    int delta = offset - currentOffset;
                    await MoveFineYawPixelsAsync(
                        delta,
                        moved => currentOffset += moved,
                        cancellationToken).ConfigureAwait(false);
                }

                observe(new CameraFineYawSweepSample(
                    offset,
                    CaptureClient(window)));
            }
        }
        finally
        {
            if (currentOffset != 0)
            {
                await MoveFineYawPixelsAsync(
                    -currentOffset,
                    moved => currentOffset += moved,
                    CancellationToken.None).ConfigureAwait(false);
            }
            if (restoreCursor)
            {
                MoveCursorWithRegisteredMotion(
                    original.X,
                    original.Y,
                    1,
                    "Windows could not restore the cursor after the fine camera sweep.");
            }
        }
    }

    private async Task MoveFineYawPixelsAsync(
        int pixels,
        Action<int> moved,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moved);
        int direction = Math.Sign(pixels);
        for (int index = 0; index < Math.Abs(pixels); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendMouse(NativeMethods.MouseeventfRightDown);
            try
            {
                await Task.Delay(8, cancellationToken).ConfigureAwait(false);
                SendMouse(NativeMethods.MouseeventfMove, direction, 0);
                moved(direction);
                await Task.Delay(8, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                SendMouse(NativeMethods.MouseeventfRightUp);
            }
        }
    }

}
