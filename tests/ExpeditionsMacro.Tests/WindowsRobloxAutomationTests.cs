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
}
