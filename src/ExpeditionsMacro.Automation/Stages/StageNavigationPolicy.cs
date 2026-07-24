using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

internal enum GameModeHandoffCommand
{
    Complete,
    ChangeGamemode,
    Back,
    PressPlayKey,
    Wait,
}

internal static class StageNavigationPolicy
{
    public static GameModeHandoffCommand SelectGameModeHandoffCommand(
        StageScreenState state,
        bool hasStageChangeModeAction,
        bool recoveryTransitionPending = false) =>
        recoveryTransitionPending && state != StageScreenState.GameModeSelector
            ? GameModeHandoffCommand.Wait
            : state switch
        {
            StageScreenState.GameModeSelector => GameModeHandoffCommand.Complete,
            StageScreenState.Victory or StageScreenState.Defeat => GameModeHandoffCommand.PressPlayKey,
            StageScreenState.PostMatchPreview when hasStageChangeModeAction => GameModeHandoffCommand.ChangeGamemode,
            StageScreenState.PostMatchPreview => GameModeHandoffCommand.PressPlayKey,
            StageScreenState.StorySelector or StageScreenState.RaidSelector or StageScreenState.PreviewReady => GameModeHandoffCommand.Back,
            _ => GameModeHandoffCommand.PressPlayKey,
        };

    public static bool MatchesExpectedState(
        StageScreenState expected,
        StageScreenState actual,
        bool hasPreviewStartAction) => expected == StageScreenState.PreviewReady
            // GB-011: lobby and retained post-match parties have different exit
            // controls, but either is launch-ready only while Start is visible.
            ? (actual is StageScreenState.PreviewReady or StageScreenState.PostMatchPreview) && hasPreviewStartAction
            : actual == expected;

    public static void RequirePrestartForTeamLoad(StageScreenMatch current)
    {
        if (current.State != StageScreenState.Prestart)
        {
            throw new InvalidOperationException(
                $"Team loading requires a confirmed prestart screen. Current state: {current.State} ({current.Confidence:P0}).");
        }
    }
}
