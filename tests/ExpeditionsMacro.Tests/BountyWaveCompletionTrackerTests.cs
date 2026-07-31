using ExpeditionsMacro.Automation.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class BountyWaveCompletionTrackerTests
{
    [Theory]
    [InlineData(15, 17)]
    [InlineData(30, 32)]
    [InlineData(45, 47)]
    [InlineData(60, 62)]
    public void ExactSafeExit_RequiresTwoStableObservations(
        int questWave,
        int safeExit)
    {
        BountyWaveCompletionTracker tracker =
            new(new StageWaveObjective
            {
                QuestWave = questWave,
            });

        Assert.False(tracker.Observe(safeExit));
        Assert.True(tracker.Observe(safeExit));
    }

    [Fact]
    public void HigherWaveFallback_RequiresThreeIncreasingReadings()
    {
        BountyWaveCompletionTracker tracker =
            new(new StageWaveObjective
            {
                QuestWave = 30,
            });

        Assert.False(tracker.Observe(33));
        Assert.False(tracker.Observe(33));
        Assert.False(tracker.Observe(34));
        Assert.True(tracker.Observe(35));
    }

    [Fact]
    public void IsolatedHighFalsePositive_DoesNotCompleteTheObjective()
    {
        BountyWaveCompletionTracker tracker =
            new(new StageWaveObjective
            {
                QuestWave = 60,
            });

        Assert.False(tracker.Observe(99));
        Assert.False(tracker.Observe(null));
        Assert.False(tracker.Observe(12));
        Assert.False(tracker.Observe(98));
    }

    [Fact]
    public void LowOrMissingReading_ResetsTheHighWaveFallbackSequence()
    {
        BountyWaveCompletionTracker tracker =
            new(new StageWaveObjective
            {
                QuestWave = 30,
            });

        Assert.False(tracker.Observe(33));
        Assert.False(tracker.Observe(34));
        Assert.False(tracker.Observe(null));
        Assert.False(tracker.Observe(35));
        Assert.False(tracker.Observe(36));
        Assert.True(tracker.Observe(37));
    }

    [Fact]
    public void RegressingHighReading_RestartsTheIncreasingSequence()
    {
        BountyWaveCompletionTracker tracker =
            new(new StageWaveObjective
            {
                QuestWave = 15,
            });

        Assert.False(tracker.Observe(18));
        Assert.False(tracker.Observe(20));
        Assert.False(tracker.Observe(19));
        Assert.False(tracker.Observe(20));
        Assert.True(tracker.Observe(21));
    }

    [Fact]
    public void QuestWavePolicy_UsesTwoExtraWavesForSafeExit()
    {
        StageWaveObjective objective = new()
        {
            QuestWave = 45,
        };

        objective.Validate();

        Assert.Equal(47, objective.SafeExitWave);
    }
}
