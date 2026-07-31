using System.Collections.Concurrent;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private readonly MacroPlanAutoSaveSession
        _planAutoSave;
    private readonly ConcurrentDictionary<
        string,
        string?> _replacedPlanIds =
            new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressPlanAutoSave;
    private bool _changingPlan;
    private bool _planAutoSaveDisposed;
    private string? _currentPlanId;
    private string? _persistedPlanId;

    private void InitializePlanAutoSave()
    {
        _planAutoSave.StatusChanged +=
            PlanAutoSave_StatusChanged;
        Unloaded += MacroPage_Unloaded;
        Dispatcher.ShutdownStarted +=
            MacroPage_ShutdownStarted;
    }

    private async Task PersistPlanAsync(
        MacroPlan plan,
        string? sourcePlanId,
        CancellationToken cancellationToken)
    {
        await _services.MacroPlans
            .SaveReplacingAsync(
                plan,
                sourcePlanId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(
                _currentPlanId,
                plan.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _persistedPlanId = plan.Id;
        if (!string.Equals(
                _services.Settings
                    .SelectedMacroPlanId,
                plan.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            await _services.UpdateSettingsAsync(
                    settings => settings with
                    {
                        SelectedMacroPlanId =
                            plan.Id,
                    })
                .ConfigureAwait(false);
        }
    }

    private void QueuePlanAutoSave()
    {
        if (_loading ||
            _suppressPlanAutoSave ||
            _changingPlan ||
            _services.Coordinator.IsBusy)
        {
            return;
        }
        if (TaskRows.Count == 0)
        {
            ShowPlanBlocksStatus(
                "Add at least one task to save this plan.");
            return;
        }
        try
        {
            MacroPlan plan = BuildPlan();
            _currentPlanId = plan.Id;
            _planAutoSave.Schedule(
                plan,
                _persistedPlanId);
        }
        catch (Exception error)
        {
            ShowPlanBlocksStatus(
                $"Could not save: {error.Message}");
        }
    }

    private void LoopEditor_ValueChanged(
        object? sender,
        EventArgs e)
    {
        ReindexRows();
        QueuePlanAutoSave();
    }

    private void PlanNameText_TextChanged(
        object sender,
        System.Windows.Controls
            .TextChangedEventArgs e) =>
        QueuePlanAutoSave();

    private void PlanAutoSave_StatusChanged(
        object? sender,
        MacroPlanAutoSaveStatusEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (e.State)
            {
                case MacroPlanAutoSaveState.Pending:
                case MacroPlanAutoSaveState.Saving:
                    ShowPlanBlocksStatus(
                        "Saving\u2026");
                    break;
                case MacroPlanAutoSaveState.Saved:
                    if (!string.IsNullOrWhiteSpace(
                            e.SourcePlanId) &&
                        !string.Equals(
                            e.SourcePlanId,
                            e.Plan.Id,
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        _replacedPlanIds[e.Plan.Id] =
                            e.SourcePlanId;
                    }
                    ShowPlanBlocksStatus("Saved.");
                    RefreshSavedPlanChoice(
                        e.Plan);
                    break;
                case MacroPlanAutoSaveState.Failed:
                    ShowPlanBlocksStatus(
                        $"Could not save: " +
                        $"{e.Error?.Message ?? "Unknown error."}");
                    break;
            }
        });
    }

    private void RefreshSavedPlanChoice(
        MacroPlan saved)
    {
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            if (_replacedPlanIds.TryRemove(
                    saved.Id,
                    out string? previousId) &&
                !string.IsNullOrWhiteSpace(
                    previousId))
            {
                MacroPlan? previous =
                    _plans.FirstOrDefault(plan =>
                        string.Equals(
                            plan.Id,
                            previousId,
                            StringComparison
                                .OrdinalIgnoreCase));
                if (previous is not null)
                {
                    _plans.Remove(previous);
                }
            }
            MacroPlan? existing =
                _plans.FirstOrDefault(plan =>
                    string.Equals(
                        plan.Id,
                        saved.Id,
                        StringComparison
                            .OrdinalIgnoreCase));
            if (existing is null)
            {
                _plans.Add(saved);
            }
            else
            {
                _plans[_plans.IndexOf(existing)] =
                    saved;
            }
            if (!_changingPlan &&
                string.Equals(
                    _currentPlanId,
                    saved.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                PlanCombo.SelectedItem = saved;
            }
        }
        finally
        {
            _loading = previousLoading;
        }
    }

    private void ApplyPlanIdentity(
        string? id)
    {
        _currentPlanId = id;
        _persistedPlanId = id;
        DeletePlanButton.IsEnabled =
            !_services.Coordinator.IsBusy &&
            !string.IsNullOrWhiteSpace(id);
    }

    private void WithoutPlanAutoSave(
        Action action)
    {
        bool previous = _suppressPlanAutoSave;
        _suppressPlanAutoSave = true;
        try
        {
            action();
        }
        finally
        {
            _suppressPlanAutoSave = previous;
        }
    }

    private void MacroPage_Unloaded(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        FlushPlanAutoSaveBlocking(
            dispose: false);

    private void MacroPage_ShutdownStarted(
        object? sender,
        EventArgs e) =>
        FlushPlanAutoSaveBlocking(
            dispose: true);

    private void FlushPlanAutoSaveBlocking(
        bool dispose)
    {
        if (_planAutoSaveDisposed)
        {
            return;
        }
        try
        {
            _planAutoSave.FlushAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception error)
        {
            _services.Log.Error(
                "Final Macro plan autosave failed.",
                error);
        }
        if (!dispose)
        {
            return;
        }
        _planAutoSave.StatusChanged -=
            PlanAutoSave_StatusChanged;
        try
        {
            _planAutoSave.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception error)
        {
            _services.Log.Error(
                "Macro plan autosave shutdown failed.",
                error);
        }
        finally
        {
            _planAutoSaveDisposed = true;
        }
    }
}
