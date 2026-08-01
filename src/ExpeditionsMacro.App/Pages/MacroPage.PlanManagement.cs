using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private ChallengeRotationProgress?
        _challengeRotationProgress;

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
            ChallengeRotation =
                tasks.Any(task => task.Kind is
                    MacroTaskKind.Challenge or
                    MacroTaskKind.Bounty)
                    ? _challengeRotationProgress
                    : null,
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

    private async void DeletePlan_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy ||
            string.IsNullOrWhiteSpace(
                _persistedPlanId))
        {
            return;
        }

        string displayName =
            PlanNameText.Text.Trim();
        if (MessageBox.Show(
                Window.GetWindow(this),
                $"Permanently delete the saved plan '{displayName}'?",
                "Delete plan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) !=
            MessageBoxResult.Yes)
        {
            return;
        }

        _changingPlan = true;
        bool deleted = false;
        try
        {
            await _planAutoSave.FlushAsync();
            string id = _persistedPlanId ??
                throw new InvalidOperationException(
                    "This plan has not been saved yet.");
            int previousIndex = Math.Max(
                0,
                _plans.Select(
                        (plan, index) =>
                            new { plan, index })
                    .FirstOrDefault(value =>
                        string.Equals(
                            value.plan.Id,
                            id,
                            StringComparison
                                .OrdinalIgnoreCase))
                    ?.index ?? 0);

            await _services.MacroPlans
                .DeleteAsync(id);
            deleted = true;

            bool previousLoading = _loading;
            _loading = true;
            MacroPlan? next;
            try
            {
                MacroPlan? removed =
                    _plans.FirstOrDefault(plan =>
                        string.Equals(
                            plan.Id,
                            id,
                            StringComparison
                                .OrdinalIgnoreCase));
                if (removed is not null)
                {
                    _plans.Remove(removed);
                }
                next = _plans.Count == 0
                    ? null
                    : _plans[Math.Min(
                        previousIndex,
                        _plans.Count - 1)];
                PlanCombo.SelectedItem = next;
                if (next is null)
                {
                    ApplyNewPlan();
                }
                else
                {
                    ApplyPlan(next);
                }
            }
            finally
            {
                _loading = previousLoading;
            }

            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    SelectedMacroPlanId =
                        next?.Id ?? string.Empty,
                });
            ShowPlanBlocksStatus(
                $"Deleted '{displayName}'.");
        }
        catch (Exception error)
        {
            ShowPlanBlocksStatus(
                deleted
                    ? $"Plan was deleted, but the next selection could not be saved: {error.Message}"
                    : $"Could not delete plan: {error.Message}");
        }
        finally
        {
            _changingPlan = false;
            DeletePlanButton.IsEnabled =
                !_services.Coordinator.IsBusy &&
                !string.IsNullOrWhiteSpace(
                    _persistedPlanId);
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
            _challengeRotationProgress = null;
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
            _challengeRotationProgress =
                plan.ChallengeRotation;
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
        _challengeRotationProgress =
            plan.ChallengeRotation;
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
