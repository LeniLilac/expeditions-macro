using ExpeditionsMacro.Windows;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Tests;

public sealed class WindowsMaximizedWorkAreaTests
{
    [Fact]
    public void BottomTaskbar_UsesWorkAreaHeight()
    {
        NativeMethods.MinMaxInfo sizing = new();

        WindowsMaximizedWorkArea.ApplyBounds(
            ref sizing,
            Rect(0, 0, 1920, 1080),
            Rect(0, 0, 1920, 1040));

        Assert.Equal(0, sizing.MaxPosition.X);
        Assert.Equal(0, sizing.MaxPosition.Y);
        Assert.Equal(1920, sizing.MaxSize.X);
        Assert.Equal(1040, sizing.MaxSize.Y);
    }

    [Fact]
    public void OffsetMonitorAndTaskbar_UseMonitorRelativePosition()
    {
        NativeMethods.MinMaxInfo sizing = new();

        WindowsMaximizedWorkArea.ApplyBounds(
            ref sizing,
            Rect(-1920, -120, 0, 960),
            Rect(-1868, -80, 0, 960));

        Assert.Equal(52, sizing.MaxPosition.X);
        Assert.Equal(40, sizing.MaxPosition.Y);
        Assert.Equal(1868, sizing.MaxSize.X);
        Assert.Equal(1040, sizing.MaxSize.Y);
    }

    [Theory]
    [InlineData(0, 40, 1920, 1080, 0, 40, 1920, 1040)]
    [InlineData(48, 0, 1920, 1080, 48, 0, 1872, 1080)]
    [InlineData(0, 0, 1872, 1080, 0, 0, 1872, 1080)]
    public void EveryTaskbarEdge_UsesTheAvailableRectangle(
        int workLeft,
        int workTop,
        int workRight,
        int workBottom,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        NativeMethods.MinMaxInfo sizing = new();

        WindowsMaximizedWorkArea.ApplyBounds(
            ref sizing,
            Rect(0, 0, 1920, 1080),
            Rect(
                workLeft,
                workTop,
                workRight,
                workBottom));

        Assert.Equal(expectedX, sizing.MaxPosition.X);
        Assert.Equal(expectedY, sizing.MaxPosition.Y);
        Assert.Equal(expectedWidth, sizing.MaxSize.X);
        Assert.Equal(expectedHeight, sizing.MaxSize.Y);
    }

    [Fact]
    public void MinimumTrackSize_IsPreservedAndEnforced()
    {
        NativeMethods.MinMaxInfo sizing = new()
        {
            MinTrackSize = new NativeMethods.Point
            {
                X = 1200,
                Y = 600,
            },
        };

        WindowsMaximizedWorkArea.ApplyBounds(
            ref sizing,
            Rect(0, 0, 1920, 1080),
            Rect(0, 0, 1920, 1040),
            minimumWidth: 960,
            minimumHeight: 720);

        Assert.Equal(1200, sizing.MinTrackSize.X);
        Assert.Equal(720, sizing.MinTrackSize.Y);
    }

    private static NativeMethods.Rect Rect(
        int left,
        int top,
        int right,
        int bottom) =>
        new()
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom,
        };
}
