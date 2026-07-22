using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageHandoffPolicyTests
{
    [Theory]
    [InlineData(StageScreenState.PreviewReady, true, true)]
    [InlineData(StageScreenState.PostMatchPreview, true, true)]
    [InlineData(StageScreenState.PreviewReady, false, false)]
    [InlineData(StageScreenState.PostMatchPreview, false, false)]
    [InlineData(StageScreenState.GameModeSelector, true, false)]
    public void PreviewWait_AcceptsEitherPartyFamilyOnlyWithADetectedStartAction(
        StageScreenState actual,
        bool hasPreviewStartAction,
        bool expected)
    {
        Assert.Equal(
            expected,
            StageNavigationPolicy.MatchesExpectedState(
                StageScreenState.PreviewReady,
                actual,
                hasPreviewStartAction));
    }

    [Theory]
    [InlineData(StageScreenState.GameModeSelector, false, "Complete")]
    [InlineData(StageScreenState.Victory, false, "PressPlayKey")]
    [InlineData(StageScreenState.Defeat, false, "PressPlayKey")]
    [InlineData(StageScreenState.PostMatchPreview, true, "ChangeGamemode")]
    [InlineData(StageScreenState.PostMatchPreview, false, "PressPlayKey")]
    [InlineData(StageScreenState.PostMatchHud, false, "PressPlayKey")]
    [InlineData(StageScreenState.StorySelector, false, "Back")]
    [InlineData(StageScreenState.RaidSelector, false, "Back")]
    [InlineData(StageScreenState.PreviewReady, false, "Back")]
    [InlineData(StageScreenState.None, false, "PressPlayKey")]
    [InlineData(StageScreenState.Prestart, false, "PressPlayKey")]
    public void HandoffPolicy_UsesOnlyStateOwnedNavigation(
        StageScreenState state,
        bool hasStageChangeModeAction,
        string expected)
    {
        GameModeHandoffCommand actual = StageNavigationPolicy.SelectGameModeHandoffCommand(
            state,
            hasStageChangeModeAction);

        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void DifferentModeVictory_UsesTheFieldObservedPlayMenuSequence()
    {
        (StageScreenState State, bool HasChangeMode, string Expected)[] sequence =
        [
            (StageScreenState.Victory, false, "PressPlayKey"),
            (StageScreenState.PostMatchPreview, true, "ChangeGamemode"),
            (StageScreenState.GameModeSelector, false, "Complete"),
        ];

        foreach ((StageScreenState state, bool hasChangeMode, string expected) in sequence)
        {
            Assert.Equal(
                expected,
                StageNavigationPolicy.SelectGameModeHandoffCommand(state, hasChangeMode).ToString());
        }
    }
}
