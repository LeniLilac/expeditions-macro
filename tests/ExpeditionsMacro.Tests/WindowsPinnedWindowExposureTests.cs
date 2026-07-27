using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class WindowsPinnedWindowExposureTests
{
    private static readonly WindowBounds Dashboard =
        new(100, 100, 1200, 800);

    [Theory]
    [InlineData(101)]
    [InlineData(202)]
    public void DashboardOrPinnedRobloxForeground_KeepsPin(
        int foreground)
    {
        Assert.True(
            IsExposed(
                foreground,
                new WindowBounds(
                    100,
                    100,
                    1200,
                    800)));
    }

    [Theory]
    [InlineData(1920, 80)]
    [InlineData(-1600, 40)]
    public void FocusedAppOnAnotherMonitor_KeepsPin(
        int x,
        int y)
    {
        Assert.True(
            IsExposed(
                foreground: 303,
                new WindowBounds(
                    x,
                    y,
                    1200,
                    900)));
    }

    [Fact]
    public void FocusedAppWithoutOverlapOnSameMonitor_KeepsPin()
    {
        Assert.True(
            IsExposed(
                foreground: 303,
                new WindowBounds(
                    1320,
                    100,
                    500,
                    600)));
    }

    [Fact]
    public void FocusedAppTouchingDashboardEdgeWithoutOverlap_KeepsPin()
    {
        Assert.True(
            IsExposed(
                foreground: 303,
                new WindowBounds(
                    1300,
                    100,
                    500,
                    600)));
    }

    [Theory]
    [MemberData(nameof(CoveringBounds))]
    public void FocusedAppWithPartialOrFullOverlap_SuspendsPin(
        WindowBounds foregroundBounds)
    {
        Assert.False(
            IsExposed(
                foreground: 303,
                foregroundBounds));
    }

    [Fact]
    public void DashboardOwnedModal_SuspendsEvenWithoutOverlap()
    {
        Assert.False(
            IsExposed(
                foreground: 303,
                new WindowBounds(
                    1920,
                    80,
                    600,
                    400),
                foregroundOwnedByOwner: true));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void HiddenOrMinimizedForeground_DoesNotOccludeDashboard(
        bool visible,
        bool minimized)
    {
        Assert.True(
            IsExposed(
                foreground: 303,
                Dashboard,
                foregroundVisible: visible,
                foregroundMinimized: minimized));
    }

    [Fact]
    public void UnreadableForeignBounds_DoNotCauseBlindSuspension()
    {
        Assert.True(
            IsExposed(
                foreground: 303,
                Dashboard,
                boundsAvailable: false));
    }

    public static TheoryData<WindowBounds> CoveringBounds =>
        new()
        {
            new WindowBounds(
                1299,
                400,
                500,
                300),
            new WindowBounds(
                300,
                250,
                600,
                400),
            new WindowBounds(
                50,
                50,
                1400,
                1000),
        };

    private static bool IsExposed(
        int foreground,
        WindowBounds foregroundBounds,
        bool foregroundVisible = true,
        bool foregroundMinimized = false,
        bool foregroundOwnedByOwner = false,
        bool boundsAvailable = true) =>
        WindowsPinnedWindowExposure
            .IsDashboardExposed(
                new PinnedWindowExposureObservation(
                    Owner: (nint)101,
                    Source: (nint)202,
                    Foreground: (nint)foreground,
                    ForegroundVisible:
                        foregroundVisible,
                    ForegroundMinimized:
                        foregroundMinimized,
                    ForegroundOwnedByOwner:
                        foregroundOwnedByOwner,
                    BoundsAvailable:
                        boundsAvailable,
                    DashboardBounds:
                        Dashboard,
                    ForegroundBounds:
                        foregroundBounds));
}
