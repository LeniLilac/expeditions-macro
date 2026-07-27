using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Refuel;
using ExpeditionsMacro.Vision.Settings;
using ExpeditionsMacro.Vision.Stages;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Automation.Settings;

public sealed partial class MacroStartupPreflightService
{
    private async Task WaitForLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            timeout,
            StableFrames,
            _utcNow);
        int stable = 0;
        while (budget.ShouldObserve(
                   confirmationPending:
                       stable > 0 &&
                       stable < StableFrames))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateWindow(window);
            ImageFrame frame =
                _automation.CaptureClient(window);
            stable = IsCleanLobby(detector, frame)
                ? stable + 1
                : 0;
            if (stable >= StableFrames)
            {
                return;
            }
            budget.MarkObserved();
            await _delay(
                    PollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "Start the macro from the fully loaded Anime Expeditions lobby with Play, Areas, Units, and Settings closed.");
    }

    private static bool IsCleanLobby(
        IDetectorPack detector,
        ImageFrame frame)
    {
        if (!string.Equals(
                detector.RecoveryState(frame),
                "lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        GameSettingsPanelMatch settings =
            GameSettingsScreenDetector.DetectPanel(frame);
        return !settings.Visible &&
            settings.CloseX == 0 &&
            StageScreenDetector.Detect(frame).State ==
                StageScreenState.None &&
            TeamScreenDetector.Detect(frame).State ==
                TeamScreenState.None &&
            AreasScreenDetector.Detect(frame).State ==
                AreasScreenState.None;
    }
}
