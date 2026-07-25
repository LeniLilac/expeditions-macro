using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class RaidDropDismissalTrackerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 25, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RaidAct.Act1)]
    public void UnsupportedRoute_NeverRequestsADismissal(RaidAct act)
    {
        RaidDropDismissalTracker tracker = new(Raid(act));

        Assert.False(tracker.Enabled);
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
    }

    [Theory]
    [InlineData(RaidAct.Act2)]
    [InlineData(RaidAct.Act3)]
    public void DropCheck_ArmsOnlyAfterAfterStartPlacementCompletes(
        RaidAct act)
    {
        RaidDropDismissalTracker tracker = new(Raid(act));

        Assert.True(tracker.Enabled);
        Assert.False(tracker.Observe(false, true, false, Start));
        Assert.False(tracker.Observe(false, true, false, Start));
        Assert.False(tracker.Observe(false, false, false, Start));
        Assert.False(tracker.Observe(false, false, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));

        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.True(tracker.Observe(true, false, false, Start));
    }

    [Fact]
    public void StableHudDisappearance_RequestsRateLimitedSafeClicks()
    {
        RaidDropDismissalTracker tracker = new(Raid(RaidAct.Act3));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.True(tracker.Observe(true, false, false, Start));
        Assert.False(tracker.Observe(
            true,
            false,
            false,
            Start + TimeSpan.FromMilliseconds(450)));
        Assert.False(tracker.Observe(
            true,
            false,
            false,
            Start + TimeSpan.FromMilliseconds(900)));
        Assert.False(tracker.Observe(
            true,
            false,
            false,
            Start + TimeSpan.FromMilliseconds(999)));
        Assert.True(tracker.Observe(
            true,
            false,
            false,
            Start + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void TerminalCandidate_SuppressesDismissal()
    {
        RaidDropDismissalTracker tracker = new(Raid(RaidAct.Act2));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, true, false, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.False(tracker.Observe(true, false, true, Start));
        Assert.False(tracker.Observe(true, false, false, Start));
        Assert.True(tracker.Observe(true, false, false, Start));
    }

    [Fact]
    public void SafeAction_UsesTheBottomRightRestingArea()
    {
        Assert.Equal(783, RaidDropDismissalTracker.ActionX);
        Assert.Equal(586, RaidDropDismissalTracker.ActionY);
    }

    private static RaidPreset Raid(RaidAct act) => new()
    {
        Id = "raid",
        Name = "Raid",
        Act = act,
    };
}
