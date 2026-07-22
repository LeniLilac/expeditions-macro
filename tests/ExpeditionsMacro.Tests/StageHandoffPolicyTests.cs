using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageHandoffPolicyTests
{
    [Theory]
    [InlineData(StageScreenState.GameModeSelector, false, "Complete")]
    [InlineData(StageScreenState.Victory, false, "CloseTerminal")]
    [InlineData(StageScreenState.Defeat, false, "CloseTerminal")]
    [InlineData(StageScreenState.PostMatchPreview, true, "ChangeGamemode")]
    [InlineData(StageScreenState.PostMatchPreview, false, "Wait")]
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
        StageMacroRunner.GameModeHandoffCommand actual = StageMacroRunner.SelectGameModeHandoffCommand(
            state,
            hasStageChangeModeAction);

        Assert.Equal(expected, actual.ToString());
    }
}
