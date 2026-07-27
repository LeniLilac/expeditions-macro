using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class WindowsWindowWorkAreaTests
{
    [Fact]
    public void ShortWorkArea_ShrinksExpandedWindowAndKeepsItReachable()
    {
        DesktopWorkAreaBounds fitted =
            WindowsWindowWorkArea
                .FitNormalBounds(
                    new DesktopWorkAreaBounds(
                        84,
                        32,
                        1200,
                        780),
                    new DesktopWorkAreaBounds(
                        0,
                        0,
                        1366,
                        728),
                    desiredWidth: 1660,
                    desiredHeight: 1040);

        Assert.Equal(
            new DesktopWorkAreaBounds(
                0,
                0,
                1366,
                728),
            fitted);
    }

    [Fact]
    public void OffsetWorkArea_ClampsAroundTheCurrentMonitor()
    {
        DesktopWorkAreaBounds workArea =
            new(
                -1872,
                -80,
                1872,
                1040);

        DesktopWorkAreaBounds fitted =
            WindowsWindowWorkArea
                .FitNormalBounds(
                    new DesktopWorkAreaBounds(
                        -1900,
                        -160,
                        1200,
                        780),
                    workArea,
                    desiredWidth: 1660,
                    desiredHeight: 1040);

        Assert.Equal(1660, fitted.Width);
        Assert.Equal(1040, fitted.Height);
        Assert.InRange(
            fitted.Left,
            workArea.Left,
            workArea.Right -
                fitted.Width);
        Assert.Equal(workArea.Top, fitted.Top);
        Assert.True(
            fitted.Right <= workArea.Right);
        Assert.True(
            fitted.Bottom <=
            workArea.Bottom);
    }

    [Fact]
    public void AlreadyLargeWindow_StillMovesInsideWorkArea()
    {
        DesktopWorkAreaBounds fitted =
            WindowsWindowWorkArea
                .FitNormalBounds(
                    new DesktopWorkAreaBounds(
                        2400,
                        900,
                        1660,
                        1040),
                    new DesktopWorkAreaBounds(
                        1920,
                        40,
                        1920,
                        1000),
                    desiredWidth: 1660,
                    desiredHeight: 1040);

        Assert.Equal(1660, fitted.Width);
        Assert.Equal(1000, fitted.Height);
        Assert.Equal(2180, fitted.Left);
        Assert.Equal(40, fitted.Top);
    }
}
