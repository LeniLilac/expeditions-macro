using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<MacroPlan>
        SavePlanInternalAsync()
    {
        MacroPlan plan = BuildPlan();
        _currentPlanId = plan.Id;
        await _planAutoSave.SaveNowAsync(
            plan,
            _persistedPlanId);
        return plan;
    }

    private MacroPlan BuildPlan()
    {
        ReindexRows();
        string name = PlanNameText.Text.Trim();
        MacroTaskDefinition[] tasks =
            TaskRows
                .Select(row => row.Definition)
                .ToArray();
        IReadOnlyList<MacroPlanLoopDefinition> loops =
            LoopEditor.ReadDefinitions(tasks);
        MacroPlan plan = new()
        {
            Id = ModelId.FromName(name),
            Name = name,
            Tasks = tasks,
            Progress = TaskRows
                .Select(row => row.Progress)
                .ToArray(),
            Loops = loops,
            LoopStates =
                LoopEditor.ProgressFor(loops),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        plan.Validate();
        return plan;
    }

    private async void PlanCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loading ||
            PlanCombo.SelectedItem is not
                MacroPlan plan)
        {
            return;
        }
        _changingPlan = true;
        try
        {
            try
            {
                await _planAutoSave.FlushAsync();
            }
            catch (Exception error)
            {
                RestoreCurrentPlanSelection();
                ShowPlanBlocksStatus(
                    $"Could not save: {error.Message}");
                return;
            }

            ApplyPlan(plan);
            try
            {
                await _services.UpdateSettingsAsync(
                    settings => settings with
                    {
                        SelectedMacroPlanId =
                            plan.Id,
                    });
            }
            catch (Exception error)
            {
                ShowPlanBlocksStatus(
                    $"Plan opened, but its selection could not be saved: {error.Message}");
            }
        }
        finally
        {
            _changingPlan = false;
        }
    }

    private void RestoreCurrentPlanSelection()
    {
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            PlanCombo.SelectedItem =
                _plans.FirstOrDefault(plan =>
                    string.Equals(
                        plan.Id,
                        _currentPlanId,
                        StringComparison
                            .OrdinalIgnoreCase));
        }
        finally
        {
            _loading = previousLoading;
        }
    }

    private async void NewPlan_Click(
        object sender,
        RoutedEventArgs e)
    {
        _changingPlan = true;
        try
        {
            await _planAutoSave.FlushAsync();
            PlanCombo.SelectedItem = null;
            ApplyNewPlan();
            ShowPlanBlocksStatus(
                "Add a task to create the new plan.");
        }
        catch (Exception error)
        {
            ShowPlanBlocksStatus(
                $"Could not save: {error.Message}");
        }
        finally
        {
            _changingPlan = false;
        }
    }

    private void ApplyNewPlan()
    {
        WithoutPlanAutoSave(() =>
        {
            PlanNameText.Text = "Daily rotation";
            TaskRows.Clear();
            LoopEditor.SetTasks(TaskRows);
            EmptyTasksText.Visibility =
                Visibility.Visible;
            LoopEditor.Apply([], []);
            ResetTaskEditor();
            ApplyTotals();
        });
        ApplyPlanIdentity(id: null);
    }

    private void ApplyPlan(MacroPlan plan)
    {
        WithoutPlanAutoSave(() =>
        {
            PlanNameText.Text = plan.Name;
            TaskRows.Clear();
            foreach (MacroTaskDefinition definition in
                     plan.Tasks.OrderBy(
                         task => task.Priority))
            {
                TaskRows.Add(new MacroTaskRow
                {
                    Definition = definition,
                    Progress =
                        plan.ProgressFor(
                            definition.Id),
                });
            }
            ReindexRows();
            LoopEditor.Apply(
                plan.EffectiveLoops(),
                plan.EffectiveLoopStates());
            ResetTaskEditor();
            ApplyTotals();
        });
        ApplyPlanIdentity(plan.Id);
    }

    private void ApplyPlanProgress(
        MacroPlan plan)
    {
        LoopEditor.UpdateProgress(
            plan.EffectiveLoopStates());
        Dictionary<string, MacroTaskProgress>
            progress = plan.Progress.ToDictionary(
                value => value.TaskId,
                StringComparer.OrdinalIgnoreCase);
        for (int index = 0;
             index < TaskRows.Count;
             index++)
        {
            MacroTaskRow row = TaskRows[index];
            TaskRows[index] = new MacroTaskRow
            {
                Definition = row.Definition,
                Progress =
                    progress.GetValueOrDefault(
                        row.Definition.Id) ??
                    row.Progress,
            };
        }
        ApplyTotals();
    }

    private async void ResetProgress_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TaskRows.Count == 0) return;
        MessageBoxResult answer = MessageBox.Show(
            Window.GetWindow(this),
            "Reset victories, defeats, runtime, cooldowns, and completion for every task in this plan?",
            "Reset plan progress",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            MacroPlan reset =
                BuildPlan().ResetProgress();
            _currentPlanId = reset.Id;
            await _planAutoSave
                .SaveNowAsync(
                    reset,
                    _persistedPlanId);
            ApplyPlan(reset);
            PhaseText.Text =
                "Plan progress reset.";
            ShowPlanBlocksStatus("Saved.");
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
        }
    }

    private async Task RefreshPlansAsync()
    {
        string? selected =
            (PlanCombo.SelectedItem as MacroPlan)?.Id;
        _plans.Clear();
        foreach (MacroPlan plan in
                 await _services.MacroPlans
                     .ListAsync())
        {
            _plans.Add(plan);
        }
        PlanCombo.SelectedItem =
            _plans.FirstOrDefault(
                value => value.Id == selected);
    }

    private void ApplyTotals()
    {
        VictoriesText.Text = TaskRows
            .Sum(row => row.Progress.Victories)
            .ToString(
                CultureInfo.InvariantCulture);
        DefeatsText.Text = TaskRows
            .Sum(row => row.Progress.Defeats)
            .ToString(
                CultureInfo.InvariantCulture);
    }
}
