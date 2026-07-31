using ExpeditionsMacro.Automation.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class BountyRerollEvidenceTrackerTests
{
    [Fact]
    public void OneHundredConsecutiveOrdinaryRerolls_ProveGoldUnavailable()
    {
        BountyRerollEvidenceTracker tracker = new();

        for (int attempt = 1;
             attempt <
             BountyRerollEvidenceTracker
                 .OrdinaryRerollLimit;
             attempt++)
        {
            Assert.False(
                tracker.ObserveOrdinaryReroll());
        }

        Assert.True(
            tracker.ObserveOrdinaryReroll());
    }

    [Fact]
    public void ConfirmedMythic_ResetsTheOrdinaryRerollSequence()
    {
        BountyRerollEvidenceTracker tracker = new();
        for (int attempt = 1;
             attempt <
             BountyRerollEvidenceTracker
                 .OrdinaryRerollLimit;
             attempt++)
        {
            Assert.False(
                tracker.ObserveOrdinaryReroll());
        }

        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        Assert.False(
            tracker.ObserveOrdinaryReroll());
    }

    [Fact]
    public void FourUnchangedConfirmedRerolls_ProveGoldUnavailable()
    {
        BountyRerollEvidenceTracker tracker = new();
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        tracker.MarkMythicRerolled(5);

        for (int attempt = 1;
             attempt <
             BountyRerollEvidenceTracker
                 .UnchangedMythicLimit;
             attempt++)
        {
            Assert.False(
                tracker.ObserveConfirmedMythic(5));
            tracker.MarkMythicRerolled(5);
        }

        Assert.True(
            tracker.ObserveConfirmedMythic(5));
    }

    [Fact]
    public void AnOrdinaryRerollBreaksTheUnchangedMythicSequence()
    {
        BountyRerollEvidenceTracker tracker = new();
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        tracker.MarkMythicRerolled(5);
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        tracker.MarkMythicRerolled(5);

        Assert.False(
            tracker.ObserveOrdinaryReroll());
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
    }

    [Fact]
    public void AChangedMythic_RestartsTheUnchangedSequence()
    {
        BountyRerollEvidenceTracker tracker = new();
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        tracker.MarkMythicRerolled(5);
        Assert.False(
            tracker.ObserveConfirmedMythic(5));
        tracker.MarkMythicRerolled(5);

        Assert.False(
            tracker.ObserveConfirmedMythic(6));
        tracker.MarkMythicRerolled(6);
        Assert.False(
            tracker.ObserveConfirmedMythic(6));
    }
}
