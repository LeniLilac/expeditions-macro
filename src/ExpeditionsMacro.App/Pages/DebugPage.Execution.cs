using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Stages;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private async Task ExecuteNavigationAsync(
        DebugNavigationRequest request,
        CancellationToken cancellationToken)
    {
        IDetectorPack detector =
            await LoadDetectorAsync(cancellationToken)
                .ConfigureAwait(false);
        RobloxWindow window = RequireRobloxWindow();
        RequireFocus(window);
        await EnsureCanonicalClientAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        ImageFrame startFrame =
            _services.Automation.CaptureClient(window);
        ValidateNavigationStart(
            startFrame,
            detector,
            request.Start);
        char playMenuKey = AppSettings.ParsePlayMenuKey(
            _services.Settings.PlayMenuKey,
            _services.Settings.MacroHotkeyVirtualKey);
        Progress<MacroProgress> progress =
            CreateProgressReporter();

        switch (request.Mode)
        {
            case DebugNavigationMode.Expedition:
                await _services.Expeditions.DebugNavigateAsync(
                    (ExpeditionPreset)request.Preset,
                    detector,
                    playMenuKey,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DebugNavigationMode.Challenge:
                await _services.Challenges.DebugNavigateAsync(
                    (ChallengePreset)request.Preset,
                    request.ChallengeType,
                    detector,
                    playMenuKey,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DebugNavigationMode.Story:
                await _services.Stages.DebugNavigateStoryAsync(
                    (StoryPreset)request.Preset,
                    detector,
                    playMenuKey,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DebugNavigationMode.Raid:
                await _services.Stages.DebugNavigateRaidAsync(
                    (RaidPreset)request.Preset,
                    detector,
                    playMenuKey,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request));
        }
    }

    private async Task ExecuteTeamSwapAsync(
        int team,
        CancellationToken cancellationToken)
    {
        RobloxWindow window = RequireRobloxWindow();
        RequireFocus(window);
        await EnsureCanonicalClientAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        ImageFrame initial =
            _services.Automation.CaptureClient(window);
        TeamScreenMatch teamState =
            TeamScreenDetector.Detect(initial);
        if (teamState.State != TeamScreenState.None)
        {
            throw new InvalidOperationException(
                $"Close the Unit interface before this test. Current state: {teamState.State}.");
        }
        char unitMenuKey = AppSettings.ParseUnitMenuKey(
            _services.Settings.UnitMenuKey,
            _services.Settings.MacroHotkeyVirtualKey,
            _services.Settings.PlayMenuKey);
        await _services.Teams.SelectAsync(
            window,
            team,
            unitMenuKey,
            CreateProgressReporter(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task InspectCurrentScreenAsync(
        CancellationToken cancellationToken)
    {
        IDetectorPack detector =
            await LoadDetectorAsync(cancellationToken)
                .ConfigureAwait(false);
        RobloxWindow window = RequireRobloxWindow();
        RequireFocus(window);
        ClientBounds bounds =
            _services.Automation.GetClientBounds(window);
        if (bounds.Width != RobloxClientProfile.Width ||
            bounds.Height != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException(
                $"Roblox is {bounds.Width} × {bounds.Height}; standardize it before inspection.");
        }
        ImageFrame frame =
            _services.Automation.CaptureClient(window);
        StageScreenMatch stage =
            StageScreenDetector.Detect(frame);
        ChallengeScreenMatch challenge =
            ChallengeScreenDetector.Detect(frame);
        TeamScreenMatch team =
            TeamScreenDetector.Detect(frame);
        IReadOnlyDictionary<string, double> scores =
            detector.ScoreStates(frame);
        string packState =
            detector.Classify(scores) ?? "None";
        string recovery =
            detector.RecoveryState(frame) ?? "None";
        string detail =
            $"Stage: {stage.State} ({stage.Confidence:P0}) · " +
            $"Challenge: {challenge.State} ({challenge.Confidence:P0}) · " +
            $"Team: {team.State} ({team.Confidence:P0}) · " +
            $"Pack: {packState} · Recovery: {recovery}";
        _services.DebugCheckpoints.RecordStatus(
            "Current screen inspected",
            detail,
            packState,
            scores.GetValueOrDefault(packState));
    }

    private Progress<MacroProgress> CreateProgressReporter() =>
        new(value =>
        {
            _services.DebugCheckpoints.RecordStatus(
                value.Phase,
                value.Message,
                value.DetectedState,
                value.Confidence);
            Dispatcher.BeginInvoke(() =>
                DebugStateText.Text = value.Message);
        });

    private async Task<IDetectorPack> LoadDetectorAsync(
        CancellationToken cancellationToken)
    {
        IDetectorPack detector =
            await _services.DetectorPacks.LoadAsync(
                AnimeExpeditionsDetectorSpec.PackId,
                cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The Anime Expeditions detector pack is not installed.");
        return _services.TraceDetector(detector);
    }

    private async Task EnsureCanonicalClientAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        ClientBounds bounds =
            _services.Automation.GetClientBounds(window);
        if (bounds.Width == RobloxClientProfile.Width &&
            bounds.Height == RobloxClientProfile.Height)
        {
            return;
        }
        await _services.Automation.ResizeClientAsync(
            window,
            RobloxClientProfile.Width,
            RobloxClientProfile.Height,
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(250, cancellationToken)
            .ConfigureAwait(false);
    }

    private void ValidateNavigationStart(
        ImageFrame frame,
        IDetectorPack detector,
        DebugNavigationStart start)
    {
        string? recovery = detector.RecoveryState(frame);
        StageScreenState stage =
            StageScreenDetector.Detect(frame).State;
        ChallengeScreenState challenge =
            ChallengeScreenDetector.Detect(frame).State;
        bool valid = start switch
        {
            DebugNavigationStart.Lobby =>
                string.Equals(
                    recovery,
                    "lobby",
                    StringComparison.OrdinalIgnoreCase),
            DebugNavigationStart.PostMatch =>
                stage is
                    StageScreenState.Victory or
                    StageScreenState.Defeat or
                    StageScreenState.PostMatchPreview or
                    StageScreenState.PostMatchHud ||
                challenge is
                    ChallengeScreenState.Victory or
                    ChallengeScreenState.Defeat or
                    ChallengeScreenState.PostMatchPreview or
                    ChallengeScreenState.PostMatchHud,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                start == DebugNavigationStart.Lobby
                    ? "Start in the lobby with the Play interface closed."
                    : "Start on a supported post-match result or party screen with Play closed.");
        }
    }

    private RobloxWindow RequireRobloxWindow() =>
        _services.Automation.FindWindow() ??
        throw new RobloxSessionUnavailableException(
            "No visible Roblox window was found.");

    private void RequireFocus(RobloxWindow window)
    {
        if (!_services.Automation.Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox.");
        }
    }
}
