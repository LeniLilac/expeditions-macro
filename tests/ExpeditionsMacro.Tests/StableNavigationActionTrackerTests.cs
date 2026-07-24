using ExpeditionsMacro.Automation.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class StableNavigationActionTrackerTests
{
    [Fact]
    public void MovingAction_DoesNotBecomeStable()
    {
        StableNavigationActionTracker<string> tracker = new(
            required: 2,
            coordinateTolerance: 3);

        Assert.Null(tracker.Update("preview", (300, 400)));
        Assert.Null(tracker.Update("preview", (300, 390)));
        Assert.Null(tracker.Update("preview", (300, 380)));
    }

    [Fact]
    public void SmallDetectorJitter_ReturnsTheCurrentAction()
    {
        StableNavigationActionTracker<string> tracker = new(
            required: 2,
            coordinateTolerance: 3);

        Assert.Null(tracker.Update("preview", (300, 400)));

        Assert.Equal((302, 398), tracker.Update("preview", (302, 398)));
    }

    [Fact]
    public void MissingAction_ResetsTheCandidate()
    {
        StableNavigationActionTracker<string> tracker = new(required: 2);

        Assert.Null(tracker.Update("preview", (300, 400)));
        Assert.Null(tracker.Update("preview", null));
        Assert.Null(tracker.Update("preview", (300, 400)));
        Assert.Equal((300, 400), tracker.Update("preview", (300, 400)));
    }

    [Fact]
    public void StateChange_ResetsTheCandidate()
    {
        StableNavigationActionTracker<string> tracker = new(required: 2);

        Assert.Null(tracker.Update("preview", (300, 400)));
        Assert.Null(tracker.Update("detail", (300, 400)));
        Assert.Equal((300, 400), tracker.Update("detail", (300, 400)));
    }
}
