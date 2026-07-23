using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class WindowsRobloxAutomationTests
{
    [Fact]
    public void ZoomOutInputPolicy_UsesRobloxLetterOThenWheelFallback()
    {
        (char primaryKey, bool primaryIsExtended, bool useWheelFallback) = WindowsRobloxAutomation.ZoomOutInputPolicy;

        Assert.Equal('O', primaryKey);
        Assert.False(primaryIsExtended);
        Assert.True(useWheelFallback);
    }

    [Theory]
    [InlineData(0xA0, 0x2A, false)]
    [InlineData(0xA1, 0x36, false)]
    [InlineData(0xA2, 0x1D, false)]
    [InlineData(0xA3, 0x1D, true)]
    [InlineData(0x41, 0x1E, false)]
    public void ShiftLockKeys_MapToPhysicalScanCodes(int virtualKey, int expectedScanCode, bool expectedExtended)
    {
        KeyboardInputDescriptor key = KeyboardInputDescriptor.FromShiftLockVirtualKey(virtualKey);

        Assert.Equal(virtualKey, key.VirtualKey);
        Assert.Equal(expectedScanCode, key.ScanCode);
        Assert.Equal(expectedExtended, key.Extended);
    }

    [Theory]
    [InlineData("RobloxPlayerBeta")]
    [InlineData("Windows10Universal")]
    [InlineData("Roblox")]
    public void SupportedRobloxProcessNames_AcceptKnownClients(string processName)
    {
        Assert.True(WindowsRobloxAutomation.IsSupportedRobloxProcessName(processName));
    }

    [Theory]
    [InlineData("Notepad")]
    [InlineData("RobloxCrashHandler")]
    [InlineData("RobloxStudioBeta")]
    public void SupportedRobloxProcessNames_RejectTitleOnlyAndNonPlayerProcesses(string processName)
    {
        Assert.False(WindowsRobloxAutomation.IsSupportedRobloxProcessName(processName));
    }

    [Fact]
    public void SelectBestWindow_PrefersExactRobloxTitleAndPlayerProcess()
    {
        RobloxWindow[] candidates =
        [
            new RobloxWindow((nint)1, "Anime Expeditions - Roblox", 101, "RobloxPlayerBeta"),
            new RobloxWindow((nint)2, "Roblox", 202, "Windows10Universal"),
            new RobloxWindow((nint)3, "Roblox", 303, "RobloxPlayerBeta"),
        ];

        RobloxWindow? selected = WindowsRobloxAutomation.SelectBestWindow(candidates, "Roblox");

        Assert.NotNull(selected);
        Assert.Equal((nint)3, selected.Value.Handle);
        Assert.Equal("RobloxPlayerBeta.exe, PID 303", selected.Value.ProcessDescription);
    }

    [Fact]
    public void ForcedWindowStyle_RemovesMinimumTrackingFrameAndKeepsOtherFlags()
    {
        const long visible = 0x10000000L;
        const long caption = 0x00C00000L;
        const long thickFrame = 0x00040000L;
        const long minimizeBox = 0x00020000L;
        const long maximizeBox = 0x00010000L;
        const long systemMenu = 0x00080000L;
        const long popup = 0x80000000L;
        long forced = WindowsRobloxAutomation.BuildForcedWindowStyle(
            visible | caption | thickFrame | minimizeBox | maximizeBox | systemMenu);

        Assert.NotEqual(0, forced & visible);
        Assert.NotEqual(0, forced & popup);
        Assert.Equal(0, forced & caption);
        Assert.Equal(0, forced & thickFrame);
        Assert.Equal(0, forced & minimizeBox);
        Assert.Equal(0, forced & maximizeBox);
        Assert.Equal(0, forced & systemMenu);
    }

    [Fact]
    public void ForcedExtendedWindowStyle_RemovesDecorativeEdges()
    {
        const long unrelatedFlag = 0x00000008L;
        const long decorativeEdges = 0x00000001L | 0x00000100L | 0x00000200L | 0x00020000L;

        long forced = WindowsRobloxAutomation.BuildForcedExtendedWindowStyle(unrelatedFlag | decorativeEdges);

        Assert.NotEqual(0, forced & unrelatedFlag);
        Assert.Equal(0, forced & decorativeEdges);
    }

    [Fact]
    public void WindowCapture_MapsClientPixelsInsideTheExtendedFrame()
    {
        ScreenRegion crop = WindowsGraphicsCapture.ResolveClientCrop(
            surfaceWidth: 824,
            surfaceHeight: 650,
            client: new ClientBounds(108, 131, 808, 611),
            windowBounds: new WindowBounds(100, 100, 824, 650),
            extendedFrameBounds: new WindowBounds(100, 100, 824, 650));

        Assert.Equal(new ScreenRegion(8, 31, 808, 611), crop);
    }

    [Fact]
    public void WindowCapture_AcceptsAClientOnlySurface()
    {
        ScreenRegion crop = WindowsGraphicsCapture.ResolveClientCrop(
            surfaceWidth: 808,
            surfaceHeight: 611,
            client: new ClientBounds(320, 240, 808, 611),
            windowBounds: new WindowBounds(312, 209, 824, 650),
            extendedFrameBounds: new WindowBounds(312, 209, 824, 650));

        Assert.Equal(new ScreenRegion(0, 0, 808, 611), crop);
    }

    [Fact]
    public void WindowCapture_ConvertsLinearScRgbToSrgbAndUsesTheClientCrop()
    {
        byte[] pixels = new byte[2 * 8];
        WriteRgbaHalf(pixels, 0, 0f, 0f, 0f, 1f);
        WriteRgbaHalf(pixels, 8, 0.5f, 0.25f, 0f, 1f);

        var frame = WindowsGraphicsCapture.ConvertScRgbRgba16ToRgb(
            pixels,
            surfaceWidth: 2,
            surfaceHeight: 1,
            new ScreenRegion(1, 0, 1, 1));

        Assert.Equal(new byte[] { 188, 137, 0 }, frame.Pixels);
    }

    [Fact]
    public void WindowCapture_CompressesHdrHighlightsInsteadOfClippingTheCapturePipeline()
    {
        byte[] pixels = new byte[8];
        WriteRgbaHalf(pixels, 0, 2f, 2f, 2f, 1f);

        var frame = WindowsGraphicsCapture.ConvertScRgbRgba16ToRgb(
            pixels,
            surfaceWidth: 1,
            surfaceHeight: 1,
            new ScreenRegion(0, 0, 1, 1));

        Assert.InRange(frame.Pixels[0], 230, 250);
        Assert.Equal(frame.Pixels[0], frame.Pixels[1]);
        Assert.Equal(frame.Pixels[0], frame.Pixels[2]);
    }

    [Fact]
    public async Task WindowCapture_FrameArrivalNotificationCrossesThreadsWithoutFrameAccess()
    {
        using var gate = new CaptureFrameArrivalGate();
        long targetGeneration = gate.Generation + 1;

        Task<bool> wait = Task.Run(() => gate.WaitForGeneration(targetGeneration, 1000));
        await Task.Run(gate.Notify);

        Assert.True(await wait);
        Assert.Equal(targetGeneration, gate.Generation);
    }

    [Fact]
    public void WindowCapture_FrameArrivalNotificationTimesOutWithoutAFrame()
    {
        using var gate = new CaptureFrameArrivalGate();

        Assert.False(gate.WaitForGeneration(gate.Generation + 1, 10));
    }

    [Fact]
    public void WindowCapture_FreshBarrierDoesNotAcceptBacklogGeneration()
    {
        using var gate = new CaptureFrameArrivalGate();
        gate.Notify();
        long targetGeneration = gate.Generation + 1;

        Assert.False(gate.WaitForGeneration(targetGeneration, 0));

        gate.Notify();

        Assert.True(gate.WaitForGeneration(targetGeneration, 0));
    }

    [Fact]
    public void WindowCapture_DrainsQueuedFramesAndKeepsOnlyTheNewest()
    {
        var first = new DisposableCaptureFrame();
        var second = new DisposableCaptureFrame();
        var newest = new DisposableCaptureFrame();
        var queued = new Queue<DisposableCaptureFrame>([first, second, newest]);

        using DisposableCaptureFrame? captured = CaptureFrameQueue.TakeLatest(
            () => queued.TryDequeue(out DisposableCaptureFrame? frame) ? frame : null);

        Assert.Same(newest, captured);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
        Assert.False(newest.Disposed);
        Assert.Empty(queued);
    }

    [Fact]
    public void WindowCapture_DiscardsEveryQueuedFrameBeforeFreshnessBarrier()
    {
        var first = new DisposableCaptureFrame();
        var newest = new DisposableCaptureFrame();
        var queued = new Queue<DisposableCaptureFrame>([first, newest]);

        CaptureFrameQueue.DiscardAll(
            () => queued.TryDequeue(out DisposableCaptureFrame? frame) ? frame : null);

        Assert.True(first.Disposed);
        Assert.True(newest.Disposed);
        Assert.Empty(queued);
    }

    [Fact]
    public void WindowCapture_TransientFreshFrameTimeoutRecreatesSessionOnce()
    {
        int captures = 0;
        int recreates = 0;

        int result = WindowsGraphicsCapture.CaptureWithSessionRecovery(
            () =>
            {
                captures++;
                if (captures == 1)
                {
                    throw new TimeoutException(
                        "No post-barrier frame arrived during teleport.");
                }

                return 42;
            },
            _ => recreates++);

        Assert.Equal(42, result);
        Assert.Equal(2, captures);
        Assert.Equal(1, recreates);
    }

    [Fact]
    public void WindowCapture_RepeatedFreshFrameTimeoutRemainsBounded()
    {
        int captures = 0;
        int recreates = 0;

        TimeoutException error = Assert.Throws<TimeoutException>(() =>
            WindowsGraphicsCapture.CaptureWithSessionRecovery<int>(
                () =>
                {
                    captures++;
                    throw new TimeoutException(
                        "The replacement session also produced no frame.");
                },
                _ => recreates++));

        Assert.Contains(
            "replacement session",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, captures);
        Assert.Equal(1, recreates);
    }

    [Fact]
    public void WindowCapture_SurfaceRecoveryRereadsGeometryUntilCaptureStabilizes()
    {
        int captures = 0;
        List<int> retries = [];

        int result = WindowsRobloxAutomation.CaptureWithSurfaceRecovery(
            () =>
            {
                captures++;
                if (captures < 3)
                {
                    throw new CaptureSurfaceChangedException(824, 650, 808, 611);
                }
                return 42;
            },
            (attempt, _) => retries.Add(attempt),
            maximumAttempts: 5);

        Assert.Equal(42, result);
        Assert.Equal(3, captures);
        Assert.Equal([1, 2], retries);
    }

    [Fact]
    public void WindowCapture_SurfaceRecoveryReturnsFriendlyErrorAfterBoundedAttempts()
    {
        int captures = 0;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            WindowsRobloxAutomation.CaptureWithSurfaceRecovery<int>(
                () =>
                {
                    captures++;
                    throw new CaptureSurfaceChangedException(824, 650, 808, 611);
                },
                maximumAttempts: 3));

        Assert.Equal(3, captures);
        Assert.Contains("could not stabilize", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<CaptureSurfaceChangedException>(error.InnerException);
    }

    private static void WriteRgbaHalf(byte[] target, int offset, float red, float green, float blue, float alpha)
    {
        WriteHalf(target, offset, red);
        WriteHalf(target, offset + 2, green);
        WriteHalf(target, offset + 4, blue);
        WriteHalf(target, offset + 6, alpha);
    }

    private static void WriteHalf(byte[] target, int offset, float value)
    {
        ushort bits = BitConverter.HalfToUInt16Bits((Half)value);
        target[offset] = (byte)bits;
        target[offset + 1] = (byte)(bits >> 8);
    }

    private sealed class DisposableCaptureFrame : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
