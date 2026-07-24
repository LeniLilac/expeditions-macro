using System.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private async void RunNavigation_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            DebugNavigationRequest request =
                CreateNavigationRequest();
            await RunDebugOperationAsync(
                "Debug Play navigation",
                "play-navigation",
                CreateNavigationContext(request),
                token => ExecuteNavigationAsync(
                    request,
                    token));
        }
        catch (Exception error)
        {
            DebugStateText.Text = error.Message;
        }
    }

    private async void RunTeam_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TeamCombo.SelectedItem is not DebugOption<int> team)
        {
            DebugStateText.Text = "Choose a saved team.";
            return;
        }
        DeepDebugOperationContext context = new()
        {
            DebugTool = "team-swap",
            DebugStepMode = SelectedStepMode().ToString(),
            OperationSettings = new
            {
                Team = team.Value,
                StartState = "Unit UI closed",
                EndState = "Unit UI closed",
            },
        };
        await RunDebugOperationAsync(
            $"Debug Team {team.Value} swap",
            "team-swap",
            context,
            token => ExecuteTeamSwapAsync(team.Value, token));
    }

    private async void InspectScreen_Click(
        object sender,
        RoutedEventArgs e)
    {
        DeepDebugOperationContext context = new()
        {
            DebugTool = "screen-inspection",
            DebugStepMode = DebugStepMode.Continuous.ToString(),
        };
        await RunDebugOperationAsync(
            "Debug screen inspection",
            "screen-inspection",
            context,
            InspectCurrentScreenAsync,
            forceMode: DebugStepMode.Continuous);
    }

    private async void NormalizeClient_Click(
        object sender,
        RoutedEventArgs e)
    {
        DeepDebugOperationContext context = new()
        {
            DebugTool = "standardize-client",
            DebugStepMode = SelectedStepMode().ToString(),
            OperationSettings = new
            {
                Width = RobloxClientProfile.Width,
                Height = RobloxClientProfile.Height,
            },
        };
        await RunDebugOperationAsync(
            "Debug Roblox standardization",
            "standardize-client",
            context,
            async token =>
            {
                var window = RequireRobloxWindow();
                RequireFocus(window);
                await _services.Automation.ResizeClientAsync(
                    window,
                    RobloxClientProfile.Width,
                    RobloxClientProfile.Height,
                    token).ConfigureAwait(false);
                RequireFocus(window);
                var bounds =
                    _services.Automation.GetClientBounds(window);
                if (bounds.Width != RobloxClientProfile.Width ||
                    bounds.Height != RobloxClientProfile.Height)
                {
                    throw new InvalidOperationException(
                        $"Roblox remained {bounds.Width} × {bounds.Height}.");
                }
                _services.DebugCheckpoints.RecordStatus(
                    "Roblox standardized",
                    $"Focused at {bounds.Width} × {bounds.Height}.");
            });
    }

    private async Task RunDebugOperationAsync(
        string description,
        string tool,
        DeepDebugOperationContext context,
        Func<CancellationToken, Task> operation,
        DebugStepMode? forceMode = null)
    {
        if (_services.Coordinator.IsBusy)
        {
            DebugStateText.Text =
                "Another workflow already owns Roblox input.";
            return;
        }
        _timeline.Clear();
        _followLive = true;
        ShowCheckpoint(index: -1);
        DebugStepMode mode =
            forceMode ?? SelectedStepMode();
        DeepDebugOperationContext completeContext =
            context with
            {
                DebugTool = tool,
                DebugStepMode = mode.ToString(),
            };
        await _services.Coordinator.RunNowAsync(
            description,
            async token =>
            {
                _services.DebugCheckpoints.Begin(mode, token);
                try
                {
                    _services.DebugCheckpoints.RecordStatus(
                        "Debug operation started",
                        description);
                    await Task.Run(
                        () => operation(token),
                        token).ConfigureAwait(false);
                    _services.DebugCheckpoints.RecordStatus(
                        "Debug operation completed",
                        $"{description} completed successfully.");
                }
                finally
                {
                    _services.DebugCheckpoints.Complete();
                }
            },
            completeContext);
    }

    private DebugNavigationRequest CreateNavigationRequest()
    {
        if (NavigationStartCombo.SelectedItem is not
                DebugOption<DebugNavigationStart> start ||
            NavigationModeCombo.SelectedItem is not
                DebugOption<DebugNavigationMode> mode ||
            NavigationPresetCombo.SelectedItem is not object preset)
        {
            throw new InvalidOperationException(
                "Choose a start state, mode, and preset.");
        }
        ChallengeType challengeType =
            ChallengeTypeCombo.SelectedItem is ChallengeType value
                ? value
                : ChallengeType.Trait;
        return new DebugNavigationRequest(
            start.Value,
            mode.Value,
            preset,
            challengeType);
    }

    private DeepDebugOperationContext CreateNavigationContext(
        DebugNavigationRequest request) =>
        request.Mode switch
        {
            DebugNavigationMode.Expedition =>
                new DeepDebugOperationContext
                {
                    ExpeditionPresetId =
                        ((ExpeditionPreset)request.Preset).Id,
                    OperationSettings =
                        NavigationSettings(request),
                },
            DebugNavigationMode.Challenge =>
                new DeepDebugOperationContext
                {
                    ChallengePresetId =
                        ((ChallengePreset)request.Preset).Id,
                    OperationSettings =
                        NavigationSettings(request),
                },
            DebugNavigationMode.Story =>
                new DeepDebugOperationContext
                {
                    StoryPresetId =
                        ((StoryPreset)request.Preset).Id,
                    OperationSettings =
                        NavigationSettings(request),
                },
            DebugNavigationMode.Raid =>
                new DeepDebugOperationContext
                {
                    RaidPresetId =
                        ((RaidPreset)request.Preset).Id,
                    OperationSettings =
                        NavigationSettings(request),
                },
            _ => throw new ArgumentOutOfRangeException(
                nameof(request)),
        };

    private static object NavigationSettings(
        DebugNavigationRequest request) => new
        {
            Start = request.Start.ToString(),
            Mode = request.Mode.ToString(),
            Preset = PresetName(request.Preset),
            ChallengeType = request.Mode ==
            DebugNavigationMode.Challenge
                ? request.ChallengeType.ToString()
                : null,
            EndState = "Prestart",
        };

    private DebugStepMode SelectedStepMode() =>
        StepModeCombo.SelectedItem is
            DebugOption<DebugStepMode> selected
            ? selected.Value
            : DebugStepMode.Continuous;

    private static string PresetName(object preset) =>
        preset switch
        {
            ExpeditionPreset value => value.Name,
            ChallengePreset value => value.Name,
            StoryPreset value => value.Name,
            RaidPreset value => value.Name,
            _ => preset.ToString() ?? "Unknown",
        };
}
