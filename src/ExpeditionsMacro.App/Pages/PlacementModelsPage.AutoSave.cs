using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private readonly PlacementModelAutoSaveSession
        _placementAutoSave;
    private bool _suppressPlacementAutoSave;
    private bool _normalizingPlacementStepPhase;
    private bool _changingSetupSelection;
    private PlacementSetupNode? _activeSetupNode;

    private void InitializePlacementAutoSave()
    {
        _steps.CollectionChanged +=
            PlacementSteps_CollectionChanged;
        FastTeamCombo.SelectionChanged +=
            FastTeamCombo_SelectionChanged;
        _placementAutoSave.Saved +=
            PlacementAutoSave_Saved;
        _placementAutoSave.SaveFailed +=
            PlacementAutoSave_SaveFailed;
        Unloaded += PlacementModelsPage_Unloaded;
        Dispatcher.ShutdownStarted +=
            PlacementModelsPage_ShutdownStarted;
    }

    private void PlacementSteps_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PlacementStepRow row in
                     e.OldItems)
            {
                row.PropertyChanged -=
                    PlacementStep_PropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (PlacementStepRow row in
                     e.NewItems)
            {
                row.PropertyChanged +=
                    PlacementStep_PropertyChanged;
            }
        }

        UpdatePlacementMarkerLayout();
        UpdateFastPlacementCount();
        SchedulePlacementAutoSave();
    }

    private void PlacementStep_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName ==
            nameof(PlacementStepRow.MarkerLayout))
        {
            return;
        }
        if (e.PropertyName is
            nameof(PlacementStepRow.X) or
            nameof(PlacementStepRow.Y) or
            nameof(PlacementStepRow.UnitKey) or
            nameof(PlacementStepRow.Phase))
        {
            UpdatePlacementMarkerLayout();
        }
        if (_normalizingPlacementStepPhase)
        {
            return;
        }
        if (sender is PlacementStepRow row &&
            e.PropertyName ==
                nameof(PlacementStepRow.Phase) &&
            !NormalizeChangedStepPhase(row))
        {
            return;
        }
        SchedulePlacementAutoSave();
    }

    private void FastTeamCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        SchedulePlacementAutoSave();

    private void SchedulePlacementAutoSave()
    {
        if (_suppressPlacementAutoSave ||
            _changingSetupSelection ||
            _selectedSetupTarget is null)
        {
            return;
        }

        string modelId =
            PlacementSetupCatalog.IdFor(
                _selectedSetupTarget);
        if (_steps.Count == 0 &&
            string.IsNullOrWhiteSpace(
                _fastManualRecordingId))
        {
            _placementAutoSave.ScheduleDelete(
                modelId);
            FastStatusText.Text =
                "Clearing setup...";
            return;
        }

        try
        {
            PlacementModel model = BuildModel();
            _placementAutoSave.ScheduleSave(model);
            FastStatusText.Text = "Saving changes...";
        }
        catch (Exception error)
        {
            FastStatusText.Text =
                $"Changes are not saved: {error.Message}";
        }
    }

    private async Task<bool>
        FlushPlacementAutoSaveAsync()
    {
        bool succeeded =
            await _placementAutoSave.FlushAsync();
        if (!succeeded)
        {
            FastStatusText.Text =
                "Could not save this setup. Fix the storage error or make another change to retry.";
        }
        return succeeded;
    }

    private void RestoreActiveSetupSelection()
    {
        _changingSetupSelection = true;
        try
        {
            FastSetupList.SelectedItem =
                _activeSetupNode;
        }
        finally
        {
            _changingSetupSelection = false;
        }
    }

    private void ApplySelectedSetupNode(
        PlacementSetupNode node)
    {
        _activeSetupNode = node;
        ApplySetup(node.Row);
    }

    private void PlacementAutoSave_Saved(
        object? sender,
        PlacementModelAutoSaveEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            PlacementSetupRow? row =
                _setupRows.FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Route.ModelId,
                            e.ModelId,
                            StringComparison.OrdinalIgnoreCase));
            row?.UpdateModel(e.Model);

            if (!_placementAutoSave
                    .IsLatestVersion(e.Version))
            {
                return;
            }

            if (!IsCurrentSetup(e.ModelId))
            {
                return;
            }

            _selectedModel = e.Model;
            FastStatusText.Text =
                e.Model is null
                    ? "Setup cleared."
                    : "All changes saved.";
        });

    private void PlacementAutoSave_SaveFailed(
        object? sender,
        PlacementModelAutoSaveFailedEventArgs e)
    {
        _services.Log.Error(
            $"Placement setup '{e.ModelId}' could not be saved.",
            e.Error);
        Dispatcher.BeginInvoke(() =>
        {
            if (_placementAutoSave
                    .IsLatestVersion(e.Version) &&
                IsCurrentSetup(e.ModelId))
            {
                FastStatusText.Text =
                    $"Could not save changes: {e.Error.Message}";
            }
        });
    }

    private bool IsCurrentSetup(string modelId) =>
        _selectedSetupTarget is not null &&
        string.Equals(
            PlacementSetupCatalog.IdFor(
                _selectedSetupTarget),
            modelId,
            StringComparison.OrdinalIgnoreCase);

    private IDisposable SuspendPlacementAutoSave()
    {
        bool previous = _suppressPlacementAutoSave;
        _suppressPlacementAutoSave = true;
        return new AutoSaveSuspension(
            () => _suppressPlacementAutoSave =
                previous);
    }

    private void PlacementModelsPage_Unloaded(
        object sender,
        RoutedEventArgs e) =>
        FlushPlacementAutoSaveBlocking();

    private void PlacementModelsPage_ShutdownStarted(
        object? sender,
        EventArgs e) =>
        FlushPlacementAutoSaveBlocking();

    private void FlushPlacementAutoSaveBlocking()
    {
        try
        {
            _placementAutoSave.FlushAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception error)
        {
            _services.Log.Error(
                "Final placement setup autosave failed.",
                error);
        }
    }

    private sealed class AutoSaveSuspension(
        Action resume) : IDisposable
    {
        private Action? _resume = resume;

        public void Dispose() =>
            Interlocked.Exchange(
                ref _resume,
                null)?.Invoke();
    }
}
