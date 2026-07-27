using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private bool _updatingFastTarget;

    private void InitializeFastEditor()
    {
        TargetModeCombo.ItemsSource = TargetModes;
        TargetModeCombo.SelectedIndex = 0;
        UpdateTargetSelectors(
            PlacementTargetMode.Expedition);
    }

    private void FastUnitButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is RadioButton button &&
            int.TryParse(
                button.Tag?.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int unit))
        {
            _selectedFastUnit = unit;
        }
    }

    private void FastPhaseButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not RadioButton button ||
            !Enum.TryParse(
                button.Tag?.ToString(),
                ignoreCase: false,
                out PlacementPhase phase))
        {
            return;
        }
        _selectedFastPhase = phase;
    }

    private void ApplyDefaultFastTarget()
    {
        ApplyFastTarget(new PlacementTarget
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber =
                PlacementSetupCatalog
                    .SharedExpeditionMapNumber,
            ActNumber = 0,
        });
    }

    private void ApplyFastTarget(
        PlacementTarget target)
    {
        _selectedSetupTarget = target;
        _updatingFastTarget = true;
        try
        {
            TargetModeCombo.SelectedItem =
                TargetModes.First(
                    option => option.Value == target.Mode);
            UpdateTargetSelectors(target.Mode);
            TargetMapCombo.SelectedItem =
                MapChoices(target.Mode).First(
                    option =>
                        option.Value ==
                        target.MapNumber);
            switch (target.Mode)
            {
                case PlacementTargetMode.Story:
                    TargetVariantCombo.SelectedItem =
                        StoryVariants.First(
                            option =>
                                option.RunKind ==
                                target.StoryRunKind &&
                                option.ActNumber ==
                                target.ActNumber);
                    break;
                case PlacementTargetMode.Raid:
                    TargetVariantCombo.SelectedIndex =
                        target.ActNumber - 1;
                    break;
                case PlacementTargetMode.Event:
                    TargetVariantCombo.SelectedIndex =
                        target.ActNumber - 1;
                    break;
            }
        }
        finally
        {
            _updatingFastTarget = false;
        }
        FastRouteNameText.Text =
            PlacementSetupCatalog.NameFor(target);
        RefreshPlacementScreenshot();
    }

    private void TargetModeCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingFastTarget ||
            TargetModeCombo.SelectedItem is not
                PlacementEditorChoice<PlacementTargetMode>
                option)
        {
            return;
        }
        _updatingFastTarget = true;
        try
        {
            UpdateTargetSelectors(option.Value);
        }
        finally
        {
            _updatingFastTarget = false;
        }
        RefreshPlacementScreenshot();
    }

    private void TargetRoute_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_updatingFastTarget)
        {
            RefreshPlacementScreenshot();
        }
    }

    private void UpdateTargetSelectors(
        PlacementTargetMode mode)
    {
        TargetMapCombo.ItemsSource =
            MapChoices(mode);
        TargetMapCombo.SelectedIndex = 0;
        switch (mode)
        {
            case PlacementTargetMode.Story:
                TargetVariantPanel.Visibility =
                    Visibility.Visible;
                TargetVariantLabel.Text = "Run";
                TargetVariantCombo.ItemsSource =
                    StoryVariants;
                TargetVariantCombo.SelectedIndex = 0;
                break;
            case PlacementTargetMode.Raid:
                TargetVariantPanel.Visibility =
                    Visibility.Visible;
                TargetVariantLabel.Text = "Act";
                TargetVariantCombo.ItemsSource =
                    Enumerable.Range(1, 3)
                        .Select(act =>
                            new PlacementEditorChoice<int>(
                                act,
                                $"Act {act}"))
                        .ToArray();
                TargetVariantCombo.SelectedIndex = 0;
                break;
            case PlacementTargetMode.Event:
                TargetVariantPanel.Visibility =
                    Visibility.Visible;
                TargetVariantLabel.Text = "Act";
                TargetVariantCombo.ItemsSource =
                    Enumerable.Range(1, 4)
                        .Select(act =>
                            new PlacementEditorChoice<int>(
                                act,
                                $"Act {act}"))
                        .ToArray();
                TargetVariantCombo.SelectedIndex = 0;
                break;
            default:
                TargetVariantPanel.Visibility =
                    Visibility.Collapsed;
                TargetVariantCombo.ItemsSource = null;
                break;
        }
    }

    private void RefreshPlacementScreenshot()
    {
        try
        {
            PlacementTarget target =
                CurrentFastTarget();
            PlacementScreenshot.Source =
                PlacementScreenshotCatalog.Load(target);
            PlacementCanvas.IsEnabled =
                !_services.Coordinator.IsBusy;
            FastPositionButton.Visibility =
                PlacementRoutePositioningService
                    .IsAvailable(target)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            FastDetailText.Text =
                $"{TargetLabel(target)} · B before Start · A after Start";
        }
        catch (Exception error)
        {
            PlacementScreenshot.Source = null;
            PlacementCanvas.IsEnabled = false;
            FastPositionButton.Visibility =
                Visibility.Collapsed;
            FastDetailText.Text = error.Message;
        }
    }

    private PlacementTarget CurrentFastTarget()
    {
        if (_selectedSetupTarget is not null)
        {
            return _selectedSetupTarget;
        }
        if (TargetModeCombo.SelectedItem is not
                PlacementEditorChoice<PlacementTargetMode>
                mode ||
            TargetMapCombo.SelectedItem is not
                PlacementEditorChoice<int> map)
        {
            throw new InvalidOperationException(
                "Choose a placement mode and map.");
        }

        PlacementTarget target =
            mode.Value switch
            {
                PlacementTargetMode.Story =>
                    StoryTarget(map.Value),
                PlacementTargetMode.Raid =>
                    RaidTarget(map.Value),
                PlacementTargetMode.Event =>
                    EventTarget(map.Value),
                _ => new PlacementTarget
                {
                    Mode = mode.Value,
                    MapNumber = map.Value,
                    ActNumber = 0,
                },
            };
        target.Validate();
        return target;
    }

    private PlacementTarget StoryTarget(int map)
    {
        if (TargetVariantCombo.SelectedItem is not
            PlacementStoryVariant variant)
        {
            throw new InvalidOperationException(
                "Choose a Story run.");
        }
        return new PlacementTarget
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = map,
            StoryRunKind = variant.RunKind,
            ActNumber = variant.ActNumber,
        };
    }

    private PlacementTarget RaidTarget(int map)
    {
        if (TargetVariantCombo.SelectedItem is not
            PlacementEditorChoice<int> act)
        {
            throw new InvalidOperationException(
                "Choose a Raid act.");
        }
        return new PlacementTarget
        {
            Mode = PlacementTargetMode.Raid,
            MapNumber = map,
            ActNumber = act.Value,
        };
    }

    private PlacementTarget EventTarget(int mode)
    {
        if (TargetVariantCombo.SelectedItem is not
            PlacementEditorChoice<int> act)
        {
            throw new InvalidOperationException(
                "Choose an Event act.");
        }
        return new PlacementTarget
        {
            Mode = PlacementTargetMode.Event,
            MapNumber = mode,
            ActNumber = act.Value,
        };
    }

    private void PlacementCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (PlacementScreenshot.Source is null)
        {
            return;
        }
        Point point = e.GetPosition(PlacementCanvas);
        int x = Math.Clamp(
            (int)Math.Round(point.X),
            0,
            807);
        int y = Math.Clamp(
            (int)Math.Round(point.Y),
            0,
            610);
        if (_selectedFastPhase ==
                PlacementPhase.BeforeStart &&
            PlacementAuthoringRules
                .IsCoveredByStartDialog(x, y))
        {
            FastStatusText.Text =
                "That point is covered by the Start Game dialog. Move it outside the dialog or choose After Start.";
            e.Handled = true;
            return;
        }
        PlacementStepRow? nearby =
            _steps.FirstOrDefault(
                step =>
                    !PlacementAuthoringRules.AreSeparated(
                        x,
                        y,
                        step.X,
                        step.Y));
        if (nearby is not null)
        {
            FastStatusText.Text =
                $"Choose a point at least {PlacementAuthoringRules.MinimumPlacementSpacingPixels} pixels from the existing placement at ({nearby.X}, {nearby.Y}).";
            e.Handled = true;
            return;
        }
        PlacementStepRow row = new()
        {
            UnitKey = _selectedFastUnit,
            X = x,
            Y = y,
            Phase = _selectedFastPhase,
            DelayAfterMilliseconds =
                _fastPlacementIntervalMilliseconds,
            DelayAfterStartMilliseconds =
                _selectedFastPhase ==
                    PlacementPhase.AfterStart
                    ? _fastDefaultAfterStartDelayMilliseconds
                    : 0,
        };
        InsertStepInPhaseOrder(row);
        FastStepsList.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        FastStatusText.Text =
            $"Added Unit {_selectedFastUnit} at ({x}, {y}) {PhaseLabel(_selectedFastPhase)}.";
        e.Handled = true;
    }

    private void PlacementMarker_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                        PlacementStepRow row,
            })
        {
            FastStepsList.SelectedItem = row;
            FastStepsList.ScrollIntoView(row);
            e.Handled = true;
        }
    }

    private void PlacementMarker_MouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_services.Coordinator.IsBusy ||
            sender is not FrameworkElement
            {
                DataContext:
                    PlacementStepRow row,
            })
        {
            return;
        }
        _steps.Remove(row);
        FastStatusText.Text = "Placement removed.";
        e.Handled = true;
    }

    private void FastPrepare_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy) return;
        _services.Coordinator.Arm(
            "Fast no align",
            async token =>
            {
                Progress<MacroProgress> progress =
                    new(value =>
                    {
                        _services.DeepDebug.RecordProgress(value);
                        Dispatcher.BeginInvoke(() =>
                        {
                            FastStatusText.Text = value.Message;
                            FastOperationProgress.Value =
                                value.Percent;
                        });
                    });
                await _services.CameraPose
                    .PrepareWithoutYawAsync(
                        progress: progress,
                        cancellationToken: token);
            },
            new DeepDebugOperationContext
            {
                OperationSettings = new
                {
                    Action = "fast_no_align",
                    Target = CurrentFastTarget(),
                },
            });
        FastStatusText.Text =
            $"Fast no align armed. Focus Roblox and press {_services.Hotkey.DisplayName}.";
        UpdateBusyState();
    }

    private void UpdateFastPlacementCount()
    {
        int before = _steps.Count(
            step => step.Phase ==
                PlacementPhase.BeforeStart);
        int after = _steps.Count - before;
        FastPlacementCountText.Text =
            $"{before} before · {after} after";
    }

    private static string TargetLabel(
        PlacementTarget target) =>
        target.Mode switch
        {
            PlacementTargetMode.Expedition =>
                target.MapNumber ==
                    PlacementSetupCatalog
                        .SharedExpeditionMapNumber
                    ? "Expeditions"
                    : $"Expedition · {MapChoices(target.Mode).First(option => option.Value == target.MapNumber).Label}",
            PlacementTargetMode.Challenge =>
                $"Challenge · {MapChoices(target.Mode)[target.MapNumber - 1].Label}",
            PlacementTargetMode.Story =>
                $"Story · {MapChoices(target.Mode)[target.MapNumber - 1].Label} · {StoryVariantLabel(target)}",
            PlacementTargetMode.Raid =>
                $"Raid · Spirit City · Act {target.ActNumber}",
            PlacementTargetMode.Event =>
                target.ActNumber ==
                    (int)EventAct.Act1
                    ? $"Event · Villain Invasion · Act 1 · {SpawnRouteLabel(target.SpawnRoute)}"
                    : $"Event · Villain Invasion · Act {target.ActNumber}",
            _ => target.Mode.ToString(),
        };

    private static string SpawnRouteLabel(
        EventSpawnRoute route) =>
        route == EventSpawnRoute.Angle2
            ? "Angle 2"
            : "Angle 1";

    private static string PhaseLabel(
        PlacementPhase phase) =>
        phase == PlacementPhase.BeforeStart
            ? "before Start"
            : "after Start";
}
