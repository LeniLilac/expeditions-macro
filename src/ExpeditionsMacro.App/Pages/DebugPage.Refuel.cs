using System.Globalization;
using System.IO;
using System.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Automation.Refuel;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private void LoadRefuelRouteSettings()
    {
        ResourceRefuelDebugSettings settings =
            _services.Settings.ResourceRefuelDebug;
        GoldForward1Text.Text =
            Number(settings.GoldForward1Milliseconds);
        GoldLeftText.Text =
            Number(settings.GoldLeftMilliseconds);
        GoldForward2Text.Text =
            Number(settings.GoldForward2Milliseconds);
        DrillForward1Text.Text =
            Number(settings.DrillForward1Milliseconds);
        DrillLeft1Text.Text =
            Number(settings.DrillLeft1Milliseconds);
        DrillForward2Text.Text =
            Number(settings.DrillForward2Milliseconds);
        DrillLeft2Text.Text =
            Number(settings.DrillLeft2Milliseconds);
        RefuelRetriesText.Text = Number(settings.RetryCount);
    }

    private async void SaveRefuelRoute_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await SaveRefuelRouteAsync();
            DebugStateText.Text =
                "Experimental refuel route saved.";
        }
        catch (Exception error)
        {
            DebugStateText.Text = error.Message;
        }
    }

    private async void RunRefuel_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            ResourceRefuelDebugSettings settings =
                await SaveRefuelRouteAsync();
            ResourceRefuelRequest request =
                CreateRefuelRequest(settings);
            DeepDebugOperationContext context = new()
            {
                DebugTool = "resource-refuel",
                DebugStepMode =
                    SelectedStepMode().ToString(),
                OperationSettings = new
                {
                    Start = request.Start.ToString(),
                    Targets = request.Targets.ToString(),
                    Route = request.Settings,
                    AreasMenuKey =
                        request.AreasMenuKey.ToString(),
                    PlayMenuKey =
                        request.PlayMenuKey.ToString(),
                    PrivateServerLinkConfigured =
                        request.RestartTarget is not null,
                    EndState = "Play interface open",
                },
            };
            await RunDebugOperationAsync(
                $"Debug {RefuelLabel(request.Targets)} refuel",
                "resource-refuel",
                context,
                token => ExecuteRefuelAsync(
                    request,
                    token));
        }
        catch (Exception error)
        {
            DebugStateText.Text = error.Message;
        }
    }

    private async Task ExecuteRefuelAsync(
        ResourceRefuelRequest request,
        CancellationToken cancellationToken)
    {
        IDetectorPack detector =
            await LoadDetectorAsync(cancellationToken)
                .ConfigureAwait(false);
        await _services.ResourceRefuel.RunAsync(
            request,
            detector,
            CreateProgressReporter(),
            entry =>
            {
                _services.Log.Info(entry.Message);
                _services.DebugCheckpoints.RecordStatus(
                    entry.State ?? "resource_refuel",
                    entry.Message,
                    entry.State,
                    entry.Confidence);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private ResourceRefuelRequest CreateRefuelRequest(
        ResourceRefuelDebugSettings settings)
    {
        if (RefuelStartCombo.SelectedItem is not
                DebugOption<ResourceRefuelStart> start ||
            RefuelTargetCombo.SelectedItem is not
                DebugOption<ResourceRefuelTarget> target)
        {
            throw new InvalidOperationException(
                "Choose a refuel start state and station.");
        }

        char areasMenuKey =
            AppSettings.ParseAreasMenuKey(
                _services.Settings.AreasMenuKey,
                _services.Hotkey.VirtualKey,
                _services.Settings.PlayMenuKey,
                _services.Settings.UnitMenuKey);
        char playMenuKey =
            AppSettings.ParsePlayMenuKey(
                _services.Settings.PlayMenuKey,
                _services.Hotkey.VirtualKey);
        RobloxPrivateServerLaunchTarget? restartTarget =
            start.Value ==
            ResourceRefuelStart.RestartPrivateServer
                ? ReadPrivateServerTarget()
                : null;
        return new ResourceRefuelRequest
        {
            Start = start.Value,
            Targets = target.Value,
            Settings = settings,
            AreasMenuKey = areasMenuKey,
            PlayMenuKey = playMenuKey,
            RestartTarget = restartTarget,
        };
    }

    private RobloxPrivateServerLaunchTarget
        ReadPrivateServerTarget()
    {
        string protectedLink =
            _services.Settings.EncryptedPrivateServerLink;
        if (string.IsNullOrWhiteSpace(protectedLink))
        {
            throw new InvalidOperationException(
                "Save a private-server link on the Macro page before using the restart start state.");
        }

        string link;
        try
        {
            link = _services.SecretProtector.Unprotect(
                protectedLink);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "The saved private-server link could not be read. Enter it again on the Macro page.",
                error);
        }
        return RobloxPrivateServerLaunchTarget.Parse(link);
    }

    private async Task<ResourceRefuelDebugSettings>
        SaveRefuelRouteAsync()
    {
        ResourceRefuelDebugSettings settings = new()
        {
            GoldForward1Milliseconds =
                ParseMilliseconds(
                    GoldForward1Text.Text,
                    "Gold Mine W1"),
            GoldLeftMilliseconds =
                ParseMilliseconds(
                    GoldLeftText.Text,
                    "Gold Mine A"),
            GoldForward2Milliseconds =
                ParseMilliseconds(
                    GoldForward2Text.Text,
                    "Gold Mine W2"),
            DrillForward1Milliseconds =
                ParseMilliseconds(
                    DrillForward1Text.Text,
                    "Resource Drill W1"),
            DrillLeft1Milliseconds =
                ParseMilliseconds(
                    DrillLeft1Text.Text,
                    "Resource Drill A1"),
            DrillForward2Milliseconds =
                ParseMilliseconds(
                    DrillForward2Text.Text,
                    "Resource Drill W2"),
            DrillLeft2Milliseconds =
                ParseMilliseconds(
                    DrillLeft2Text.Text,
                    "Resource Drill A2"),
            RetryCount = ParseRetries(
                RefuelRetriesText.Text),
        };
        settings.Validate();
        await _services.UpdateSettingsAsync(value =>
            value with { ResourceRefuelDebug = settings });
        return settings;
    }

    private static int ParseMilliseconds(
        string value,
        string label)
    {
        if (!int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed))
        {
            throw new InvalidDataException(
                $"{label} must be a whole number of milliseconds.");
        }
        return parsed;
    }

    private static int ParseRetries(string value)
    {
        if (!int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed))
        {
            throw new InvalidDataException(
                "Retries must be a whole number.");
        }
        return parsed;
    }

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string RefuelLabel(
        ResourceRefuelTarget target) =>
        target switch
        {
            ResourceRefuelTarget.GoldMine => "Gold Mine",
            ResourceRefuelTarget.ResourceDrill =>
                "Resource Drill",
            ResourceRefuelTarget.Both =>
                "Gold Mine and Resource Drill",
            _ => "resource",
        };
}
